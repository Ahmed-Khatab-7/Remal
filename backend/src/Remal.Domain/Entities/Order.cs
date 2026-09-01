using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

public class Order : AuditableEntity
{
    /// <summary>Human-readable code like RML-284751</summary>
    public string Code { get; set; } = null!;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Snapshot of customer at order time
    public string CustomerName { get; set; } = null!;
    public string CustomerPhone { get; set; } = null!;
    public string CustomerAddress { get; set; } = null!;
    public string? CustomerEmail { get; set; }
    public string? City { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? PaymentReference { get; set; } // Paymob transaction id
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }
    public bool GiftWrap { get; set; }

    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PreparedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>لمنع منح نقاط الولاء أكثر من مرة لو تحوّل الطلب لـ Delivered أكثر من مرة (idempotency).</summary>
    public bool PointsAwarded { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? BundleId { get; set; }
    public Guid? CollectionId { get; set; }

    public string ItemName { get; set; } = null!;
    public string? Volume { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;

    public Order Order { get; set; } = null!;
    public Product? Product { get; set; }
    public Bundle? Bundle { get; set; }
    public Collection? Collection { get; set; }
}
