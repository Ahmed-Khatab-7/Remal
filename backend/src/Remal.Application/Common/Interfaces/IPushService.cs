namespace Remal.Application.Common.Interfaces;

/// <summary>
/// Web Push fan-out: send a notification to every stored PushSubscription.
/// Implementations swallow per-subscription failures and prune dead endpoints (404/410).
/// </summary>
public interface IPushService
{
    /// <summary>VAPID public key (base64url-encoded). The browser needs this to subscribe.</summary>
    string VapidPublicKey { get; }

    /// <summary>
    /// Send a push notification to every active subscription.
    /// </summary>
    /// <param name="title">Notification title (RTL Arabic OK).</param>
    /// <param name="body">Notification body text.</param>
    /// <param name="url">Click target — opened by the service worker on click.</param>
    Task SendToAllAsync(string title, string body, string? url = null, CancellationToken ct = default);
}
