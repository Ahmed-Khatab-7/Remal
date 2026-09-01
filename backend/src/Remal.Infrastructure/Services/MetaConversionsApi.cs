using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remal.Application.Common.Interfaces;
using Remal.Infrastructure.Persistence;

namespace Remal.Infrastructure.Services;

/// <summary>
/// Meta Conversions API — بيبعت أحداث التحويل من السيرفر مباشرة لـ Meta.
///
/// ليه ده مهم: بعد iOS 14 ومانعات الإعلانات، جزء كبير من أحداث البيكسل بتضيع قبل
/// ما توصل. الحدث اللي بيتبعت من السيرفر ما بيتأثرش بأي من ده، فبيرجّع التحويلات
/// الضايعة وبيرفع Event Match Quality — واللي بينزّل تكلفة التحويل في الحملات.
///
/// منع التكرار: البيكسل والسيرفر بيبعتوا **نفس الحدث بنفس event_id**، و Meta
/// بتدمجهم في حدث واحد. من غير المعرّف ده كل عملية شراء هتتحسب مرتين.
///
/// كل بيانات العميل بتتشفّر SHA-256 قبل ما تخرج من السيرفر — Meta بتطابق بالهاش
/// نفسه، فما بيوصلهاش بريد ولا موبايل واضح.
/// </summary>
public class MetaConversionsApi : IMetaConversionsApi
{
    // مفاتيح الإعدادات: قاعدة البيانات (من لوحة التحكم) لها الأولوية على appsettings
    private const string TokenSettingKey = "meta_capi_token";
    private const string PixelSettingKey = "meta_pixel_id";
    private const string GraphVersion = "v21.0";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MetaConversionsApi> _logger;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContext;

    public MetaConversionsApi(HttpClient http, IConfiguration config,
        IServiceScopeFactory scopes, ILogger<MetaConversionsApi> logger,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContext)
    {
        _http = http; _config = config; _scopes = scopes; _logger = logger; _httpContext = httpContext;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["Meta:PixelId"]);

    public async Task SendAsync(MetaEvent evt, CancellationToken ct = default)
    {
        try
        {
            var (pixelId, token) = await ResolveCredentialsAsync(ct);
            if (string.IsNullOrWhiteSpace(pixelId) || string.IsNullOrWhiteSpace(token))
            {
                _logger.LogDebug("Meta CAPI: مفيش Pixel ID أو Access Token — الحدث اتخطى.");
                return;
            }

            var payload = BuildPayload(evt);
            var url = $"https://graph.facebook.com/{GraphVersion}/{pixelId}/events?access_token={Uri.EscapeDataString(token)}";

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var res = await _http.PostAsync(url, content, ct);

            if (res.IsSuccessStatusCode)
            {
                _logger.LogInformation("Meta CAPI: اتبعت {Event} (order {Order}).", evt.EventName, evt.OrderId);
            }
            else
            {
                // بنسجّل الرد عشان أخطاء Meta بتوضّح السبب بالظبط (توكن منتهي، pixel غلط...)
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Meta CAPI: فشل {Status} — {Body}", (int)res.StatusCode, Truncate(body, 500));
            }
        }
        catch (Exception ex)
        {
            // التتبع ما ينفعش يكسر طلب حقيقي مهما حصل
            _logger.LogWarning(ex, "Meta CAPI: استثناء أثناء إرسال {Event}.", evt.EventName);
        }
    }

    /// <summary>القيمة من قاعدة البيانات (لوحة التحكم) وإلا من appsettings.</summary>
    private async Task<(string? PixelId, string? Token)> ResolveCredentialsAsync(CancellationToken ct)
    {
        string? dbPixel = null, dbToken = null;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.AppSettings.AsNoTracking()
                .Where(s => s.Key == TokenSettingKey || s.Key == PixelSettingKey)
                .ToListAsync(ct);
            dbToken = rows.FirstOrDefault(s => s.Key == TokenSettingKey)?.Value;
            dbPixel = rows.FirstOrDefault(s => s.Key == PixelSettingKey)?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Meta CAPI: تعذّرت قراءة الإعدادات من قاعدة البيانات — هنكمل بالـ appsettings.");
        }

        var pixelId = Pick(dbPixel, _config["Meta:PixelId"]);
        var token = Pick(dbToken, _config["Meta:CapiAccessToken"]);
        return (pixelId, token);
    }

    private object BuildPayload(MetaEvent evt)
    {
        var userData = new Dictionary<string, object>();

        void AddHashed(string key, string? value)
        {
            var h = Sha256(Normalize(value));
            if (h is not null) userData[key] = new[] { h };
        }

        AddHashed("em", evt.Email);
        AddHashed("ph", NormalizePhone(evt.Phone));
        AddHashed("ct", NormalizeCity(evt.City));
        AddHashed("external_id", evt.ExternalId);

        var (first, last) = SplitName(evt.FullName);
        AddHashed("fn", first);
        AddHashed("ln", last);
        AddHashed("country", "eg");

        // الكوكيز دي أقوى إشارات المطابقة عند Meta — بتيجي من المتصفح مع الطلب
        if (!string.IsNullOrWhiteSpace(evt.Fbp)) userData["fbp"] = evt.Fbp;
        if (!string.IsNullOrWhiteSpace(evt.Fbc)) userData["fbc"] = evt.Fbc;

        // IP والـ User-Agent بيتبعتوا **بدون تشفير** (Meta بتطلبهم كده بالظبط).
        // غيابهم أكبر سبب منفرد لانخفاض Event Match Quality، وصفحة Diagnostics
        // بتنبّه عليه صراحةً: "Server events missing client IP / user agent".
        var ctx = _httpContext.HttpContext;
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
        var ua = ctx?.Request?.Headers.UserAgent.ToString();
        if (!string.IsNullOrWhiteSpace(ip) && ip != "::1") userData["client_ip_address"] = ip;
        if (!string.IsNullOrWhiteSpace(ua)) userData["client_user_agent"] = ua;

        var customData = new Dictionary<string, object> { ["currency"] = evt.Currency };
        if (!string.IsNullOrWhiteSpace(evt.ContentName)) customData["content_name"] = evt.ContentName;
        if (evt.Value is not null) customData["value"] = decimal.Round(evt.Value.Value, 2);
        if (!string.IsNullOrWhiteSpace(evt.OrderId)) customData["order_id"] = evt.OrderId;
        if (evt.Contents is { Count: > 0 })
        {
            customData["content_type"] = "product";
            customData["contents"] = evt.Contents.Select(c => new
            {
                id = c.Id,
                quantity = c.Quantity,
                item_price = decimal.Round(c.ItemPrice, 2),
            }).ToArray();
        }

        var data = new Dictionary<string, object>
        {
            ["event_name"] = evt.EventName,
            ["event_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["action_source"] = "website",
            ["user_data"] = userData,
            ["custom_data"] = customData,
        };
        if (!string.IsNullOrWhiteSpace(evt.EventId)) data["event_id"] = evt.EventId;
        if (!string.IsNullOrWhiteSpace(evt.SourceUrl)) data["event_source_url"] = evt.SourceUrl;

        var payload = new Dictionary<string, object> { ["data"] = new[] { data } };

        // كود اختبار مؤقت — بيخلي الحدث يظهر في تبويب Test Events في Events Manager
        var testCode = _config["Meta:TestEventCode"];
        if (!string.IsNullOrWhiteSpace(testCode)) payload["test_event_code"] = testCode;

        return payload;
    }

    // ===== التطبيع والتشفير — Meta بتطلب lowercase ومن غير مسافات قبل الهاش =====

    private static string? Pick(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Normalize(string? v)
        => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToLowerInvariant();

    /// <summary>الموبايل المصري لصيغة دولية بدون + : 01114545419 → 201114545419.</summary>
    internal static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        if (digits.StartsWith("00")) digits = digits[2..];
        if (digits.StartsWith('0') && digits.Length == 11) digits = "20" + digits[1..];
        else if (digits.Length == 10 && digits.StartsWith('1')) digits = "20" + digits;
        return digits;
    }

    /// <summary>الواجهة بتبعت "المدينة — المحافظة"؛ Meta عايزة المدينة بس بدون رموز.</summary>
    internal static string? NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;
        var part = city.Split('—', '-', ',')[0];
        var cleaned = new string(part.Where(c => !char.IsPunctuation(c) && !char.IsSymbol(c)).ToArray());
        cleaned = cleaned.Replace(" ", "").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    internal static (string? First, string? Last) SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (null, null);
        if (parts.Length == 1) return (parts[0], null);
        return (parts[0], string.Join(' ', parts[1..]));
    }

    internal static string? Sha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
