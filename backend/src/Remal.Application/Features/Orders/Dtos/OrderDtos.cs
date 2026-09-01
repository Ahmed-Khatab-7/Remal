using Remal.Domain.Enums;

namespace Remal.Application.Features.Orders.Dtos;

public record OrderItemDto(Guid Id, Guid? ProductId, Guid? BundleId, Guid? CollectionId, string ItemName, string? Volume, int Quantity, decimal UnitPrice, decimal LineTotal, string? ImageUrl);

public record OrderItemWriteDto
{
    public Guid? ProductId { get; init; }
    public Guid? BundleId { get; init; }
    public Guid? CollectionId { get; init; }
    public string? Volume { get; init; }
    public int Quantity { get; init; } = 1;
}

public record OrderListDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string CustomerName { get; init; } = null!;
    public string CustomerPhone { get; init; } = null!;
    public OrderStatus Status { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
    public decimal Total { get; init; }
    public int ItemCount { get; init; }
    public DateTime PlacedAt { get; init; }
}

public record OrderDetailDto : OrderListDto
{
    public string? CustomerEmail { get; init; }
    public string CustomerAddress { get; init; } = null!;
    public string? City { get; init; }
    public string? Notes { get; init; }
    public decimal Subtotal { get; init; }
    public decimal ShippingFee { get; init; }
    public decimal DiscountAmount { get; init; }
    public string? CouponCode { get; init; }
    public bool GiftWrap { get; init; }
    public string? PaymentReference { get; init; }
    public DateTime? PreparedAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
}

public record OrderCreateDto
{
    public string CustomerName { get; init; } = null!;
    public string CustomerPhone { get; init; } = null!;
    public string CustomerAddress { get; init; } = null!;
    public string? CustomerEmail { get; init; }
    public string? City { get; init; }
    public string? Notes { get; init; }
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.CashOnDelivery;
    public string? CouponCode { get; init; }
    public bool GiftWrap { get; init; }
    public IReadOnlyList<OrderItemWriteDto> Items { get; init; } = [];

    // ===== إشارات التتبع (اختيارية، بتيجي من المتصفح) =====
    // ما بتأثرش على الطلب نفسه إطلاقًا — بس بتخلي حدث الشراء اللي بيتبعت من السيرفر
    // لـ Meta يتدمج مع اللي البيكسل بعته بدل ما يتحسب مرتين، وبترفع دقة المطابقة.
    /// <summary>نفس المعرّف اللي البيكسل بعت بيه حدث Purchase (منع التكرار).</summary>
    public string? EventId { get; init; }
    /// <summary>كوكي _fbp من المتصفح.</summary>
    public string? Fbp { get; init; }
    /// <summary>كوكي _fbc (بيتولّد لما الزائر ييجي من إعلان بـ fbclid).</summary>
    public string? Fbc { get; init; }
    /// <summary>رابط الصفحة اللي اتعمل منها الطلب.</summary>
    public string? SourceUrl { get; init; }
}

public record OrderStatusUpdateDto(OrderStatus NewStatus, string? Note);

public record OrderTrackingDto(string Code, OrderStatus Status, DateTime PlacedAt, DateTime? PreparedAt, DateTime? ShippedAt, DateTime? DeliveredAt);

public record OrderFilterDto
{
    public string? Search { get; init; }
    public OrderStatus? Status { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
