using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using PushSubscriptionEntity = Remal.Domain.Entities.PushSubscription;

namespace Remal.Api.Controllers;

/// <summary>
/// Web Push subscription management for the dashboard (Admin/Partner only).
/// </summary>
[ApiController]
[Route("api/push")]
[Authorize(Roles = "Admin,Partner")]
public class PushController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IPushService _push;
    private readonly ICurrentUserService _currentUser;

    public PushController(IApplicationDbContext db, IPushService push, ICurrentUserService currentUser)
    {
        _db = db; _push = push; _currentUser = currentUser;
    }

    /// <summary>Returns the server's VAPID public key — the browser needs this to call PushManager.subscribe.</summary>
    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> VapidPublicKey()
        => Ok(ApiResponse<object>.Ok(new { publicKey = _push.VapidPublicKey }));

    public record SubscribeKeys(string P256dh, string Auth);
    public record SubscribeDto(string Endpoint, SubscribeKeys Keys);

    /// <summary>Upsert a browser push subscription for the logged-in admin/partner.</summary>
    [HttpPost("subscribe")]
    public async Task<ActionResult<ApiResponse>> Subscribe([FromBody] SubscribeDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Endpoint) || dto.Keys is null
            || string.IsNullOrWhiteSpace(dto.Keys.P256dh) || string.IsNullOrWhiteSpace(dto.Keys.Auth))
            return BadRequest(ApiResponse.Fail("بيانات الاشتراك ناقصة"));

        var userId = _currentUser.UserId!;
        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == dto.Endpoint, ct);
        if (existing != null)
        {
            existing.UserId = userId;
            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
            existing.UserAgent = Request.Headers.UserAgent.ToString();
            existing.LastSeenAt = DateTime.UtcNow;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscriptionEntity
            {
                UserId = userId,
                Endpoint = dto.Endpoint,
                P256dh = dto.Keys.P256dh,
                Auth = dto.Keys.Auth,
                UserAgent = Request.Headers.UserAgent.ToString(),
                LastSeenAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("اتسجل الاشتراك"));
    }

    /// <summary>Remove a subscription (used when the user disables notifications client-side).</summary>
    [HttpDelete("unsubscribe")]
    public async Task<ActionResult<ApiResponse>> Unsubscribe([FromQuery] string endpoint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return BadRequest(ApiResponse.Fail("endpoint مطلوب"));
        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == endpoint, ct);
        if (existing != null)
        {
            _db.PushSubscriptions.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
        return Ok(ApiResponse.Ok("تم إلغاء الاشتراك"));
    }

    /// <summary>Admin tool: send a test push to every active subscription.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<ApiResponse>> Test(CancellationToken ct)
    {
        await _push.SendToAllAsync("رمال — اختبار", "ده إشعار تجريبي للتأكد إن الإشعارات شغّالة 🤍", "/remal-dashboard.html", ct);
        return Ok(ApiResponse.Ok("اتبعتت إشعارات الاختبار"));
    }

    /// <summary>
    /// اختبار تليجرام. بيرجع سبب الفشل بوضوح عشان تعرف المشكلة في التوكن ولا في
    /// معرّف المحادثة — من غير ما تستنى طلب حقيقي عشان تكتشف إن الربط غلط.
    /// </summary>
    [HttpPost("telegram-test")]
    public async Task<ActionResult<ApiResponse>> TelegramTest(
        [FromServices] ITelegramNotifier telegram, CancellationToken ct)
    {
        if (!await telegram.IsConfiguredAsync(ct))
            return Ok(ApiResponse.Fail("تليجرام مش متضبط — احفظ توكن البوت ومعرّف المحادثة في الإعدادات الأول."));

        var sent = await telegram.SendAsync(
            "✅ <b>اختبار رمال</b>\nلو وصلتك الرسالة دي، إشعارات الطلبات الجديدة شغّالة.", ct);

        return sent
            ? Ok(ApiResponse.Ok("اتبعتت — شوف تليجرام."))
            : Ok(ApiResponse.Fail("الإرسال فشل. اتأكد إن التوكن صحيح وإنك بدأت محادثة مع البوت (اضغط Start)."));
    }
}
