using Remal.Domain.Common;

namespace Remal.Domain.Entities;

public class NewsletterSubscription : BaseEntity
{
    public string Email { get; set; } = null!;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnsubscribedAt { get; set; }
    public bool IsActive => UnsubscribedAt is null;
    public string? Source { get; set; } // 'footer', 'popup', etc.
}
