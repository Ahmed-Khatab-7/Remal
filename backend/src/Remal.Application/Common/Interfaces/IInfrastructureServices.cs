using Remal.Application.Features.Auth.Dtos;
using Remal.Domain.Enums;
using Remal.Domain.Identity;

namespace Remal.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsInRole(string role);
}

public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
    DateOnly Today { get; }
}

public interface IJwtService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    int AccessTokenExpiryMinutes { get; }
    int RefreshTokenExpiryDays { get; }
}

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ip, string? userAgent, CancellationToken ct = default);
    Task<AuthResponseDto> RefreshAsync(string rawRefreshToken, string? ip, string? userAgent, CancellationToken ct = default);
    Task LogoutAsync(string? rawRefreshToken, string? userId, CancellationToken ct = default);
    Task<UserProfileDto> GetMeAsync(string userId, CancellationToken ct = default);
    Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken ct = default);
    Task ChangePasswordAsync(string userId, ChangePasswordDto dto, CancellationToken ct = default);
    Task<UserProfileDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<bool> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);
    Task ResendConfirmationAsync(string email, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
    /// <summary>يتحقق من صلاحية رابط الاستعادة قبل عرض النموذج (بدون تغيير كلمة السر).</summary>
    Task<bool> VerifyResetTokenAsync(string email, string token, CancellationToken ct = default);
    Task<ShippingProfileDto> GetShippingProfileAsync(string userId, CancellationToken ct = default);
    Task<ShippingProfileDto> SaveShippingProfileAsync(string userId, ShippingProfileWriteDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> GoogleSignInAsync(string credential, string? ip, string? userAgent, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(AuditCategory category, string action, string description,
        string? entityName = null, string? entityId = null,
        object? before = null, object? after = null, CancellationToken ct = default);
}

public interface IPaymobService
{
    Task<PaymobPaymentSession> CreatePaymentSessionAsync(decimal amount, string orderCode, string customerName, string customerPhone, string customerEmail, CancellationToken ct = default);
    bool VerifyHmac(string hmac, IDictionary<string, string> payload);
}

public class PaymobPaymentSession
{
    public string PaymentToken { get; set; } = null!;
    public string IframeUrl { get; set; } = null!;
    public string PaymobOrderId { get; set; } = null!;
}

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}
