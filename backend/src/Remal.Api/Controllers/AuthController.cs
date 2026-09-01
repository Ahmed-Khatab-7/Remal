using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Auth.Dtos;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
[EnableRateLimiting("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "remal_rt";
    private readonly IAuthService _auth;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _config;

    public AuthController(IAuthService auth, ICurrentUserService currentUser, IConfiguration config)
    {
        _auth = auth; _currentUser = currentUser; _config = config;
    }

    /// <summary>Register a new customer account. Logs an email-confirmation link.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Register(RegisterDto dto, CancellationToken ct)
    {
        var user = await _auth.RegisterAsync(dto, ct);
        return Ok(ApiResponse<UserProfileDto>.Ok(user, "تم إنشاء الحساب — تأكيد الإيميل اتبعت في الـ logs"));
    }

    /// <summary>Sign in. Returns access token in body; refresh token in HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(LoginDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var data = await _auth.LoginAsync(dto, ip, ua, ct);

        SetRefreshCookie(data.RefreshToken!);
        // Don't leak the refresh token in the body when delivered via cookie
        var safe = data with { RefreshToken = null };
        return Ok(ApiResponse<AuthResponseDto>.Ok(safe, "تم تسجيل الدخول"));
    }

    /// <summary>Refresh using the HttpOnly cookie (web) or body (mobile).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Refresh([FromBody] RefreshTokenDto? body, CancellationToken ct)
    {
        var raw = body?.RefreshToken ?? Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(raw)) return Unauthorized(ApiResponse.Fail("Refresh token مطلوب", "UNAUTHORIZED"));

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var data = await _auth.RefreshAsync(raw, ip, ua, ct);

        SetRefreshCookie(data.RefreshToken!);
        var safe = data with { RefreshToken = null };
        return Ok(ApiResponse<AuthResponseDto>.Ok(safe));
    }

    /// <summary>Revoke the current refresh token and clear the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Logout(CancellationToken ct)
    {
        var raw = Request.Cookies[RefreshCookieName];
        await _auth.LogoutAsync(raw, _currentUser.UserId, ct);
        ClearRefreshCookie();
        return Ok(ApiResponse.Ok("تم تسجيل الخروج"));
    }

    /// <summary>Confirm email via emailed link.</summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ConfirmEmail(ConfirmEmailDto dto, CancellationToken ct)
    {
        await _auth.ConfirmEmailAsync(dto.UserId, dto.Token, ct);
        return Ok(ApiResponse.Ok("تم تأكيد الإيميل"));
    }

    /// <summary>Re-send the email confirmation link.</summary>
    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ResendConfirmation(ResendConfirmationDto dto, CancellationToken ct)
    {
        await _auth.ResendConfirmationAsync(dto.Email, ct);
        return Ok(ApiResponse.Ok("لو الإيميل موجود هتلاقي رابط التأكيد"));
    }

    /// <summary>Request a password reset link.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ForgotPassword(ForgotPasswordDto dto, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(dto.Email, ct);
        return Ok(ApiResponse.Ok("لو الإيميل مسجّل، هيوصلك رابط استعادة"));
    }

    /// <summary>Check a reset link is still valid before showing the form (returns valid:true/false).</summary>
    [HttpGet("verify-reset-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> VerifyResetToken([FromQuery] string email, [FromQuery] string token, CancellationToken ct)
    {
        var valid = await _auth.VerifyResetTokenAsync(email ?? "", token ?? "", ct);
        return Ok(ApiResponse<object>.Ok(new { valid }));
    }

    /// <summary>Complete a password reset and revoke all sessions.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ResetPassword(ResetPasswordDto dto, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(dto, ct);
        return Ok(ApiResponse.Ok("تم تغيير كلمة السر"));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me(CancellationToken ct)
        => Ok(ApiResponse<UserProfileDto>.Ok(await _auth.GetMeAsync(_currentUser.UserId!, ct)));

    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(UpdateProfileDto dto, CancellationToken ct)
        => Ok(ApiResponse<UserProfileDto>.Ok(await _auth.UpdateProfileAsync(_currentUser.UserId!, dto, ct), "تم الحفظ"));

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ChangePassword(ChangePasswordDto dto, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(_currentUser.UserId!, dto, ct);
        return Ok(ApiResponse.Ok("تم تغيير كلمة السر"));
    }

    /// <summary>Get the saved shipping profile (autofills the checkout form for logged-in users).</summary>
    [HttpGet("shipping-profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ShippingProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShippingProfileDto>>> GetShippingProfile(CancellationToken ct)
        => Ok(ApiResponse<ShippingProfileDto>.Ok(await _auth.GetShippingProfileAsync(_currentUser.UserId!, ct)));

    /// <summary>Save the user's primary shipping address for fast future checkouts.</summary>
    [HttpPut("shipping-profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ShippingProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShippingProfileDto>>> SaveShippingProfile(ShippingProfileWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<ShippingProfileDto>.Ok(await _auth.SaveShippingProfileAsync(_currentUser.UserId!, dto, ct), "تم حفظ العنوان"));

    /// <summary>Google Sign-In: exchange a Google ID token for our own session.</summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Google(GoogleSignInDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var data = await _auth.GoogleSignInAsync(dto.Credential, ip, ua, ct);
        SetRefreshCookie(data.RefreshToken!);
        var safe = data with { RefreshToken = null };
        return Ok(ApiResponse<AuthResponseDto>.Ok(safe, "تم تسجيل الدخول"));
    }

    // ---- cookie helpers ----
    private void SetRefreshCookie(string token)
    {
        var days = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !HttpContext.Request.Host.Host.StartsWith("localhost") && !HttpContext.Request.Host.Host.StartsWith("127.0.0.1"),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            Path = "/api/auth",
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/api/auth",
        });
    }
}
