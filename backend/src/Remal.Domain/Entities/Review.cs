using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

public class Review : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int Rating { get; set; }
    public string? Text { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public bool IsVerifiedPurchase { get; set; }
    public string? ModeratedById { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? ModerationNote { get; set; }

    public Product Product { get; set; } = null!;
}
