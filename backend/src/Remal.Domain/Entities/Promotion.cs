using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

/// <summary>
/// An automatic cart-level offer (no coupon code needed). Applied during order creation.
/// Examples:
///   - BuyXGetYFree: "اشترِ 2 × 50ML خد 30ML هدية" (BuyQuantity=2, TriggerVolume=50ML, RewardVolume=30ML)
///   - BuyXGetPercentOff: "اشترِ 3 عطور خد خصم 15%"
///   - FreeGiftOverAmount: "اطلب فوق 2000 ج.م خد عينة هدية"
///   - OrderPercentOver: "خصم 10% على الطلبات فوق 1500 ج.م"
/// </summary>
public class Promotion : AuditableEntity
{
    public string NameAr { get; set; } = null!;
    public string? NameEn { get; set; }
    public PromotionType Type { get; set; } = PromotionType.BuyXGetYFree;

    // ===== Trigger conditions =====
    /// <summary>NULL = applies to ANY product. Otherwise restrict trigger to this product.</summary>
    public Guid? TriggerProductId { get; set; }
    /// <summary>NULL = any size. Otherwise the buy must be of this volume (e.g. "50ML").</summary>
    public string? TriggerVolume { get; set; }
    /// <summary>Quantity that must be bought to trigger (for BuyX types).</summary>
    public int BuyQuantity { get; set; } = 2;
    /// <summary>Minimum order subtotal to trigger (for amount-based types).</summary>
    public decimal MinSpend { get; set; }

    // ===== Reward =====
    /// <summary>The product given free / discounted (for gift types). NULL allowed for order-level % off.</summary>
    public Guid? RewardProductId { get; set; }
    public string? RewardVolume { get; set; }
    public int RewardQuantity { get; set; } = 1;
    /// <summary>Percent off (for percent types), 0-100.</summary>
    public decimal RewardPercentOff { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public int Priority { get; set; } = 0; // higher applies first

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt < DateTime.UtcNow;
    public bool IsUsable => IsActive && !IsExpired;
}
