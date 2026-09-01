using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Features.Auth.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Domain.Identity;
using Remal.Infrastructure.Persistence;
using Remal.Infrastructure.Services;

namespace Remal.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly SignInManager<ApplicationUser> _signInMgr;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ApplicationDbContext _db;
    private readonly EmailOptions _emailOpts;
    private readonly ILogger<AuthService> _logger;
    private readonly IDashboardNotifier _notifier;

    private readonly IConfiguration _config;

    public AuthService(
        UserManager<ApplicationUser> userMgr,
        SignInManager<ApplicationUser> signInMgr,
        IJwtService jwt, IAuditService audit, IEmailService email,
        ApplicationDbContext db, IOptions<EmailOptions> emailOpts,
        ILogger<AuthService> logger, IDashboardNotifier notifier,
        IConfiguration config)
    {
        _userMgr = userMgr; _signInMgr = signInMgr; _jwt = jwt; _audit = audit; _email = email;
        _db = db; _emailOpts = emailOpts.Value; _logger = logger; _notifier = notifier;
        _config = config;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByEmailAsync(dto.Email)
            ?? throw new UnauthorizedException("الإيميل أو كلمة السر غلط");
        if (!user.IsActive) throw new UnauthorizedException("الحساب غير نشط");

        var ok = await _userMgr.CheckPasswordAsync(user, dto.Password);
        if (!ok) throw new UnauthorizedException("الإيميل أو كلمة السر غلط");

        var roles = await _userMgr.GetRolesAsync(user);
        return await IssueTokensAsync(user, roles, ip, userAgent, ct);
    }

    public async Task<AuthResponseDto> RefreshAsync(string rawRefreshToken, string? ip, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) throw new UnauthorizedException("Refresh token مطلوب");

        var hash = HashToken(rawRefreshToken);
        var token = await _db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) throw new UnauthorizedException("Token غير صالح");

        if (!token.IsActive)
        {
            // Compromise detection: if a revoked/expired token is reused, revoke ALL the user's tokens.
            if (token.IsRevoked)
            {
                var allActive = await _db.RefreshTokens.Where(t => t.UserId == token.UserId && t.RevokedAt == null).ToListAsync(ct);
                foreach (var t in allActive)
                {
                    t.RevokedAt = DateTime.UtcNow;
                    t.RevokedReason = "Compromise detected: revoked token reused";
                    t.RevokedByIp = ip;
                }
                await _db.SaveChangesAsync(ct);
                _logger.LogWarning("Refresh token reuse detected for user {UserId}", token.UserId);
            }
            throw new UnauthorizedException("Token غير صالح أو منتهي");
        }

        var roles = await _userMgr.GetRolesAsync(token.User);

        // Rotate: revoke old, issue new
        var newAccess = _jwt.GenerateAccessToken(token.User, roles);
        var newRefreshRaw = _jwt.GenerateRefreshToken();
        var newRefreshHash = HashToken(newRefreshRaw);

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ip;
        token.RevokedReason = "Rotated";
        token.ReplacedByTokenHash = newRefreshHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = newRefreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
            CreatedByIp = ip,
            UserAgent = userAgent,
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponseDto
        {
            AccessToken = newAccess,
            RefreshToken = newRefreshRaw,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
            User = await BuildProfile(token.User, roles),
        };
    }

    public async Task LogoutAsync(string? rawRefreshToken, string? userId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            var hash = HashToken(rawRefreshToken);
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (token is not null && token.RevokedAt is null)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedReason = "User logout";
                await _db.SaveChangesAsync(ct);
            }
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await _userMgr.FindByIdAsync(userId);
            if (user is not null)
                await _audit.LogAsync(AuditCategory.Auth, "LOGOUT", $"سجل {user.FullName} الخروج", ct: ct);
        }
    }

    public async Task<UserProfileDto> GetMeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByIdAsync(userId) ?? throw new NotFoundException("User", userId);
        var roles = await _userMgr.GetRolesAsync(user);
        return await BuildProfile(user, roles);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByIdAsync(userId) ?? throw new NotFoundException("User", userId);
        user.FullName = dto.FullName;
        user.PhoneNumber = dto.Phone;
        user.City = dto.City;
        user.Birthday = dto.Birthday;
        if (dto.Governorate != null) user.Governorate = dto.Governorate;
        if (dto.AddressLine != null) user.AddressLine = dto.AddressLine;
        var result = await _userMgr.UpdateAsync(user);
        if (!result.Succeeded) throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));
        var roles = await _userMgr.GetRolesAsync(user);
        return await BuildProfile(user, roles);
    }

    public async Task<ShippingProfileDto> GetShippingProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByIdAsync(userId) ?? throw new NotFoundException("User", userId);
        var parts = (user.FullName ?? "").Split(' ', 2);
        var first = parts.Length > 0 ? parts[0] : "";
        var last = parts.Length > 1 ? parts[1] : "";
        return new ShippingProfileDto(first, last, user.PhoneNumber, user.Governorate, user.City, user.AddressLine);
    }

    public async Task<ShippingProfileDto> SaveShippingProfileAsync(string userId, ShippingProfileWriteDto dto, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByIdAsync(userId) ?? throw new NotFoundException("User", userId);
        // Update only the address parts — don't overwrite name/phone if already set unless caller supplied new values
        if (!string.IsNullOrWhiteSpace(dto.FirstName) || !string.IsNullOrWhiteSpace(dto.LastName))
        {
            var full = ((dto.FirstName ?? "") + " " + (dto.LastName ?? "")).Trim();
            if (!string.IsNullOrWhiteSpace(full)) user.FullName = full;
        }
        if (!string.IsNullOrWhiteSpace(dto.Phone)) user.PhoneNumber = dto.Phone;
        if (dto.Governorate != null) user.Governorate = dto.Governorate;
        if (dto.City != null) user.City = dto.City;
        if (dto.AddressLine != null) user.AddressLine = dto.AddressLine;
        await _userMgr.UpdateAsync(user);
        return await GetShippingProfileAsync(userId, ct);
    }

    public async Task<AuthResponseDto> GoogleSignInAsync(string credential, string? ip, string? ua, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(credential)) throw new BadRequestException("Google credential مطلوب");

        // Verify the ID token by calling Google's tokeninfo endpoint — no extra package needed.
        // Returns claims as a JSON document on success, HTTP 4xx on bad token.
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var resp = await http.GetAsync("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(credential), ct);
        if (!resp.IsSuccessStatusCode) throw new BadRequestException("Google credential غير صالح");
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Audience check — make sure the token was issued for OUR client ID, not someone else's app.
        var expectedClientId = _config["Google:ClientId"];
        if (!string.IsNullOrWhiteSpace(expectedClientId))
        {
            var aud = root.TryGetProperty("aud", out var audEl) ? audEl.GetString() : null;
            if (!string.Equals(aud, expectedClientId, StringComparison.Ordinal))
                throw new BadRequestException("Google credential ليس مخصصاً لهذا الموقع");
        }
        // Issuer must be Google
        var iss = root.TryGetProperty("iss", out var issEl) ? issEl.GetString() : null;
        if (iss != "https://accounts.google.com" && iss != "accounts.google.com")
            throw new BadRequestException("Google credential من مُصدر غير معروف");

        if (!root.TryGetProperty("email", out var emEl) || string.IsNullOrWhiteSpace(emEl.GetString()))
            throw new BadRequestException("Google credential لا يحتوي على بريد إلكتروني");
        var email = emEl.GetString()!.Trim();
        var name = root.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? email) : email;
        var picture = root.TryGetProperty("picture", out var pEl) ? pEl.GetString() : null;
        var emailVerified = root.TryGetProperty("email_verified", out var evEl) && evEl.ValueKind == System.Text.Json.JsonValueKind.True;

        // Find existing user, or create a new one.
        var user = await _userMgr.FindByEmailAsync(email);
        var isNew = false;
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email, Email = email, FullName = name,
                EmailConfirmed = emailVerified,
                AvatarInitials = MakeInitials(name),
                AvatarUrl = picture,
                IsActive = true,
            };
            var create = await _userMgr.CreateAsync(user);
            if (!create.Succeeded) throw new BadRequestException(string.Join(", ", create.Errors.Select(e => e.Description)));
            await _userMgr.AddToRoleAsync(user, Roles.Customer);
            isNew = true;

            // نفس مكافأة التسجيل العادي: ١٠٠ نقطة ترحيبية لأول تسجيل عبر جوجل
            _db.LoyaltyAccounts.Add(new LoyaltyAccount
            {
                UserId = user.Id,
                Balance = 100, LifetimeEarned = 100,
                Transactions = new List<PointsTransaction>
                {
                    new() { Timestamp = DateTime.UtcNow, Type = PointsTransactionType.Welcome,
                            Points = 100, Description = "ترحيب: إنشاء حساب عبر جوجل" },
                },
            });
            // رسالة ترحيب + إشعار الداشبورد بعميل جديد (زي التسجيل العادي)
            await _email.SendWelcomeAsync(user.Email!, user.FullName, ct);
            await _notifier.NewCustomerAsync(new NewCustomerNotification(
                user.Id, user.FullName, user.Email!, DateTime.UtcNow), ct);
        }
        else
        {
            // Update avatar from Google if blank locally
            if (string.IsNullOrWhiteSpace(user.AvatarUrl) && !string.IsNullOrWhiteSpace(picture)) user.AvatarUrl = picture;
            if (!user.EmailConfirmed && emailVerified) user.EmailConfirmed = true;
            user.LastLoginAt = DateTime.UtcNow;
            await _userMgr.UpdateAsync(user);
        }

        var roles = await _userMgr.GetRolesAsync(user);
        var accessToken = _jwt.GenerateAccessToken(user, roles);
        var refreshRaw = _jwt.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = HashToken(refreshRaw),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
            CreatedByIp = ip,
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Auth, isNew ? "GOOGLE_REGISTER" : "GOOGLE_LOGIN",
            $"{(isNew ? "أنشأ" : "سجّل")} {user.FullName} الدخول عبر جوجل", ct: ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken, RefreshToken = refreshRaw,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
            User = await BuildProfile(user, roles),
        };
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByIdAsync(userId) ?? throw new NotFoundException("User", userId);
        var result = await _userMgr.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new Application.Common.Exceptions.ValidationException(errors!);
        }
        await _audit.LogAsync(AuditCategory.Auth, "PASSWORD_CHANGED", $"غيّر {user.FullName} كلمة السر", ct: ct);

        // Revoke all refresh tokens after password change
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) { t.RevokedAt = DateTime.UtcNow; t.RevokedReason = "Password changed"; }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserProfileDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (await _userMgr.FindByEmailAsync(dto.Email) is not null)
            throw new ConflictException("الإيميل ده مسجّل بالفعل");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            PhoneNumber = dto.Phone,
            FullName = dto.FullName,
            City = dto.City,
            AvatarInitials = MakeInitials(dto.FullName),
            EmailConfirmed = false,
            IsActive = true,
        };

        var result = await _userMgr.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new Application.Common.Exceptions.ValidationException(errors!);
        }

        await _userMgr.AddToRoleAsync(user, Roles.Customer);

        // Create loyalty account + welcome bonus
        var loyalty = new LoyaltyAccount
        {
            UserId = user.Id,
            Balance = 100, LifetimeEarned = 100,
            Transactions = new List<PointsTransaction>
            {
                new() { Timestamp = DateTime.UtcNow, Type = PointsTransactionType.Welcome,
                        Points = 100, Description = "ترحيب: إنشاء حساب" },
            },
        };
        _db.LoyaltyAccounts.Add(loyalty);
        await _db.SaveChangesAsync(ct);

        // Log confirmation link (per spec) — also log raw values for easy Swagger testing
        var token = await _userMgr.GenerateEmailConfirmationTokenAsync(user);
        var encoded = Uri.EscapeDataString(token);
        var url = $"{_emailOpts.FrontendBaseUrl}/confirm-email?uid={user.Id}&t={encoded}";
        await _email.SendEmailConfirmationAsync(user.Email!, user.FullName, url, ct);
        await _email.SendWelcomeAsync(user.Email!, user.FullName, ct);
        _logger.LogInformation("==== EMAIL CONFIRMATION FOR {Email} ====\nuserId: {UserId}\ntoken (RAW — paste into Swagger as-is):\n{Token}\n========================================",
            user.Email, user.Id, token);

        await _audit.LogAsync(AuditCategory.Auth, "REGISTER", $"حساب عميل جديد: {user.FullName} ({user.Email})", ct: ct);

        // Realtime: notify the dashboard of the new customer
        await _notifier.NewCustomerAsync(new NewCustomerNotification(
            user.Id, user.FullName, user.Email!, DateTime.UtcNow), ct);

        var roles = await _userMgr.GetRolesAsync(user);
        return await BuildProfile(user, roles);
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByIdAsync(userId) ?? throw new NotFoundException("User", userId);
        if (user.EmailConfirmed) return true;
        var result = await _userMgr.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) throw new BadRequestException("الرابط منتهي أو غير صالح");
        await _audit.LogAsync(AuditCategory.Auth, "EMAIL_CONFIRMED", $"تأكيد إيميل {user.Email}", ct: ct);
        return true;
    }

    public async Task ResendConfirmationAsync(string email, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed) return; // silent
        var token = await _userMgr.GenerateEmailConfirmationTokenAsync(user);
        var encoded = Uri.EscapeDataString(token);
        var url = $"{_emailOpts.FrontendBaseUrl}/confirm-email?uid={user.Id}&t={encoded}";
        await _email.SendEmailConfirmationAsync(user.Email!, user.FullName, url, ct);
        _logger.LogInformation("==== RESEND EMAIL CONFIRMATION FOR {Email} ====\nuserId: {UserId}\ntoken (RAW — paste into Swagger as-is):\n{Token}\n========================================",
            user.Email, user.Id, token);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByEmailAsync(email);
        if (user is null) return; // silent — don't leak account existence
        var token = await _userMgr.GeneratePasswordResetTokenAsync(user);
        var encoded = Uri.EscapeDataString(token);
        var url = $"{_emailOpts.FrontendBaseUrl}/reset-password?email={Uri.EscapeDataString(email)}&t={encoded}";
        await _email.SendPasswordResetAsync(user.Email!, user.FullName, url, ct);
        await _audit.LogAsync(AuditCategory.Auth, "PASSWORD_RESET_REQUESTED", $"طلب استعادة كلمة سر لـ {email}", ct: ct);
        _logger.LogInformation("==== PASSWORD RESET TOKEN FOR {Email} ====\nemail: {Email}\ntoken (RAW — paste into Swagger as-is):\n{Token}\n========================================",
            user.Email, user.Email, token);
    }

    public async Task<bool> VerifyResetTokenAsync(string email, string token, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByEmailAsync(email);
        if (user is null) return false;
        // نفس مزوّد التوكن المستخدم في التوليد؛ يرجع false لو التوكن مستخدم أو منتهي
        return await _userMgr.VerifyUserTokenAsync(
            user, _userMgr.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", token);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        var user = await _userMgr.FindByEmailAsync(dto.Email) ?? throw new NotFoundException("User", dto.Email);
        var result = await _userMgr.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new Application.Common.Exceptions.ValidationException(errors!);
        }
        // Revoke all refresh tokens
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) { t.RevokedAt = DateTime.UtcNow; t.RevokedReason = "Password reset"; }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Auth, "PASSWORD_RESET", $"تم استعادة كلمة سر {dto.Email}", ct: ct);
    }

    // ---------------- helpers ----------------

    private async Task<AuthResponseDto> IssueTokensAsync(ApplicationUser user, IList<string> roles, string? ip, string? userAgent, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user, roles);
        var refreshRaw = _jwt.GenerateRefreshToken();
        var refreshHash = HashToken(refreshRaw);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
            CreatedByIp = ip,
            UserAgent = userAgent,
        });
        user.LastLoginAt = DateTime.UtcNow;
        await _userMgr.UpdateAsync(user);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Auth, "LOGIN", $"سجل {user.FullName} الدخول", ct: ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshRaw,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
            User = await BuildProfile(user, roles),
        };
    }

    private Task<UserProfileDto> BuildProfile(ApplicationUser user, IList<string> roles) =>
        Task.FromResult(new UserProfileDto
        {
            Id = user.Id, Email = user.Email!, FullName = user.FullName,
            Phone = user.PhoneNumber, City = user.City,
            Governorate = user.Governorate, AddressLine = user.AddressLine,
            Birthday = user.Birthday,
            AvatarInitials = user.AvatarInitials, AvatarUrl = user.AvatarUrl,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToList(), CreatedAt = user.CreatedAt, LastLoginAt = user.LastLoginAt,
        });

    internal static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static string MakeInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "U";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return (parts[0][0].ToString() + parts[^1][0]).ToUpperInvariant();
    }
}
