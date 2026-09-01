using Microsoft.AspNetCore.Identity;

namespace Remal.Domain.Identity;

/// <summary>
/// Identity user — partners (admins) and registered customers share the table; differentiated by role.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = null!;
    public string? AvatarInitials { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? Birthday { get; set; }
    public string? City { get; set; }
    /// <summary>Saved governorate for fast checkout (e.g. "القاهرة").</summary>
    public string? Governorate { get; set; }
    /// <summary>Saved detailed street address (line + building + apartment).</summary>
    public string? AddressLine { get; set; }

    // Navigation
    public ICollection<Entities.Expense> ExpensesPaid { get; set; } = new List<Entities.Expense>();
    public ICollection<Entities.RefreshToken> RefreshTokens { get; set; } = new List<Entities.RefreshToken>();
    public ICollection<Entities.WishlistItem> WishlistItems { get; set; } = new List<Entities.WishlistItem>();
    public ICollection<Entities.CartItem> CartItems { get; set; } = new List<Entities.CartItem>();
    public ICollection<Entities.AddressBookEntry> Addresses { get; set; } = new List<Entities.AddressBookEntry>();
    public Entities.LoyaltyAccount? LoyaltyAccount { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Partner = "Partner";
    public const string Customer = "Customer";
}
