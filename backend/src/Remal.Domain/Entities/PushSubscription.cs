using Remal.Domain.Common;

namespace Remal.Domain.Entities;

/// <summary>
/// Stores a browser's Web Push subscription. One row per (User × device/browser).
/// The endpoint is unique — re-subscribing from the same browser updates the existing row.
/// </summary>
public class PushSubscription : BaseEntity
{
    /// <summary>Identity user id of the Admin/Partner the browser is logged into.</summary>
    public string UserId { get; set; } = null!;

    /// <summary>The push service endpoint URL (from PushSubscription.endpoint).</summary>
    public string Endpoint { get; set; } = null!;

    /// <summary>p256dh public key (base64url).</summary>
    public string P256dh { get; set; } = null!;

    /// <summary>Auth secret (base64url).</summary>
    public string Auth { get; set; } = null!;

    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
}
