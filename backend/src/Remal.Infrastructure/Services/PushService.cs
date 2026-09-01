using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remal.Application.Common.Interfaces;
using Remal.Infrastructure.Persistence;
using WebPush;
using PushSubscriptionEntity = Remal.Domain.Entities.PushSubscription;

namespace Remal.Infrastructure.Services;

public class VapidOptions
{
    public string Subject { get; set; } = "mailto:hello@remal.eg";
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}

public class PushService : IPushService
{
    private readonly ApplicationDbContext _db;
    private readonly WebPushClient _client;
    private readonly VapidDetails _vapid;
    private readonly ILogger<PushService> _logger;

    public PushService(ApplicationDbContext db, IOptions<VapidOptions> opts, ILogger<PushService> logger)
    {
        _db = db;
        _logger = logger;
        var o = opts.Value;
        _vapid = new VapidDetails(o.Subject, o.PublicKey, o.PrivateKey);
        _client = new WebPushClient();
        VapidPublicKey = o.PublicKey;
    }

    public string VapidPublicKey { get; }

    public async Task SendToAllAsync(string title, string body, string? url = null, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { title, body, url });
        var subs = await _db.PushSubscriptions.AsNoTracking().ToListAsync(ct);
        if (subs.Count == 0) return;

        var deadIds = new List<Guid>();

        foreach (var s in subs)
        {
            try
            {
                var ps = new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                await _client.SendNotificationAsync(ps, payload, _vapid);
            }
            catch (WebPushException wex)
            {
                // 404 Not Found / 410 Gone → endpoint is dead; prune it.
                if (wex.StatusCode == HttpStatusCode.NotFound || wex.StatusCode == HttpStatusCode.Gone)
                {
                    deadIds.Add(s.Id);
                }
                else
                {
                    _logger.LogWarning(wex, "Web push send failed (status {Status}) for endpoint {Endpoint}", wex.StatusCode, s.Endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web push send threw for endpoint {Endpoint}", s.Endpoint);
            }
        }

        if (deadIds.Count > 0)
        {
            var dead = await _db.PushSubscriptions.Where(x => deadIds.Contains(x.Id)).ToListAsync(ct);
            _db.PushSubscriptions.RemoveRange(dead);
            await _db.SaveChangesAsync(ct);
        }
    }
}
