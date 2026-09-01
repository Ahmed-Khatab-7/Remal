using Remal.Domain.Common;
using Remal.Domain.Identity;

namespace Remal.Domain.Entities;

/// <summary>One row per (user, product) favorite.</summary>
public class WishlistItem : BaseEntity
{
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
