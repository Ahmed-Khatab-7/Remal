using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remal.Application.Common.Interfaces;
using Remal.Infrastructure.Persistence;

namespace Remal.Infrastructure.Services;

/// <summary>
/// إشعارات تليجرام عبر Bot API.
///
/// <para><b>الأسرار في قاعدة البيانات مش في الكود.</b> التوكن ومعرّف المحادثة
/// بيتحفظوا في جدول <c>AppSettings</c> ويتكتبوا من لوحة التحكم — نفس نمط
/// <c>meta_capi_token</c>. السبب: <c>deploy.ps1</c> **بيرفع** appsettings.json،
/// فحطّ التوكن هناك معناه إنه يتنشر مع كل نسخة ويدخل الريبو.</para>
///
/// <para>القراءة بتتم لكل رسالة (مش مكاشية) عشان تغيير التوكن من اللوحة يسري
/// فورًا بدون إعادة تشغيل. الاستعلام على مفتاحين بس، وبيحصل مرة لكل طلب جديد
/// — تكلفته مهملة.</para>
/// </summary>
public class TelegramNotifier : ITelegramNotifier
{
    private const string TokenKey = "telegram_bot_token";
    private const string ChatKey = "telegram_chat_id";

    private readonly IHttpClientFactory _http;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(IHttpClientFactory http, IServiceScopeFactory scopes, ILogger<TelegramNotifier> logger)
    {
        _http = http; _scopes = scopes; _logger = logger;
    }

    private async Task<(string? Token, string? ChatId)> ReadSettingsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.AppSettings.AsNoTracking()
                .Where(s => s.Key == TokenKey || s.Key == ChatKey)
                .ToListAsync(ct);
            return (rows.FirstOrDefault(s => s.Key == TokenKey)?.Value,
                    rows.FirstOrDefault(s => s.Key == ChatKey)?.Value);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Telegram: تعذّرت قراءة الإعدادات من قاعدة البيانات.");
            return (null, null);
        }
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var (token, chat) = await ReadSettingsAsync(ct);
        return !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(chat);
    }

    public async Task<bool> SendAsync(string text, CancellationToken ct = default)
    {
        var (token, chatId) = await ReadSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogDebug("Telegram: مش متضبط (توكن أو chat id ناقص) — الرسالة اتخطت.");
            return false;
        }

        try
        {
            var client = _http.CreateClient("telegram");
            client.Timeout = TimeSpan.FromSeconds(10);

            var payload = new
            {
                chat_id = chatId,
                text,
                parse_mode = "HTML",
                // معاينات الروابط بتملا الرسالة بصور مش مفيدة هنا
                disable_web_page_preview = true,
            };

            using var res = await client.PostAsJsonAsync(
                $"https://api.telegram.org/bot{token}/sendMessage", payload, ct);

            if (res.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram: الرسالة اتبعتت.");
                return true;
            }

            // الرد بيشرح السبب بدقة (chat not found / unauthorized / bot blocked)
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Telegram: فشل {Status} — {Body}",
                (int)res.StatusCode, body.Length > 300 ? body[..300] : body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram: استثناء أثناء الإرسال.");
            return false;
        }
    }
}
