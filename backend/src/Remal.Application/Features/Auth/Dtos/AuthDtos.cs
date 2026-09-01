namespace Remal.Application.Features.Auth.Dtos;

public record LoginDto(string Email, string Password);

/// <summary>Refresh via raw token (mobile clients). Web clients use HttpOnly cookie instead.</summary>
public record RefreshTokenDto(string? RefreshToken = null);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);

public record RegisterDto
{
    public string Email { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string? City { get; init; }
}

public record ConfirmEmailDto(string UserId, string Token);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Token, string NewPassword);
public record ResendConfirmationDto(string Email);
public record UpdateProfileDto(string FullName, string? Phone, string? City, DateTime? Birthday, string? Governorate = null, string? AddressLine = null);

/// <summary>Google Sign-In: client sends the ID token (credential) it got from Google Identity Services.</summary>
public record GoogleSignInDto(string Credential);

/// <summary>Saved shipping profile for fast checkout (returned by GET /api/auth/shipping-profile).</summary>
public record ShippingProfileDto(string? FirstName, string? LastName, string? Phone, string? Governorate, string? City, string? AddressLine);

public record ShippingProfileWriteDto
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Phone { get; init; }
    public string? Governorate { get; init; }
    public string? City { get; init; }
    public string? AddressLine { get; init; }
}

public record AuthResponseDto
{
    public string AccessToken { get; init; } = null!;
    public string? RefreshToken { get; init; }   // null when delivered via HttpOnly cookie
    public DateTime ExpiresAt { get; init; }
    public UserProfileDto User { get; init; } = null!;
}

public record UserProfileDto
{
    public string Id { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string? Phone { get; init; }
    public string? City { get; init; }
    public string? Governorate { get; init; }
    public string? AddressLine { get; init; }
    public DateTime? Birthday { get; init; }
    public string? AvatarInitials { get; init; }
    public string? AvatarUrl { get; init; }
    public bool EmailConfirmed { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
