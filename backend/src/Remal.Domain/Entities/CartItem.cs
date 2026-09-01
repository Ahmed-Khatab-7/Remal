using Remal.Domain.Common;
using Remal.Domain.Identity;

namespace Remal.Domain.Entities;

/// <summary>
/// Server-side persisted cart for authenticated customers (so cart syncs across devices).
/// Anonymous users keep cart in localStorage.
/// One CartItem = one (user, product+volume) OR (user, bundle) OR (user, collection).
/// </summary>
public class CartItem : BaseEntity
{
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid? BundleId { get; set; }
    public Bundle? Bundle { get; set; }

    public Guid? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    /// <summary>Required when ProductId is set: which size variant ("30ML", "50ML", "100ML").</summary>
    public string? Volume { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
