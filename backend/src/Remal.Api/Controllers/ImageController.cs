using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Remal.Api.Controllers;

/// <summary>
/// وسيط تصغير الصور.
///
/// <para><b>ليه موجود:</b> صور المنتجات مرفوعة بمقاس 6000×6000. الملف على القرص
/// ٧٠٠ كيلوبايت بس، لكن المتصفح لازم **يفك ضغطها في الذاكرة** بمقاسها الأصلي:
/// 6000 × 6000 × 4 بايت = <b>١٣٧ ميجابايت للصورة الواحدة</b>. صفحة فيها ٨ منتجات
/// كانت بتحجز أكتر من <b>١ جيجابايت</b> من ذاكرة العرض — وسفاري على الآيفون بيقتل
/// التبويب عند مئات الميجات، فتظهر رسالة "A problem repeatedly occurred".</para>
///
/// <para>الوسيط ده بيجيب الصورة الأصلية مرة واحدة، يصغّرها للمقاس المطلوب فعلاً،
/// يحفظها على القرص، ويخدمها من الكاش بعد كده. صورة بعرض ٤٠٠ بكسل = ٠٫٦ ميجا في
/// الذاكرة بدل ١٣٧ — أقل بـ ٢٢٠ مرة.</para>
///
/// <para>وبيشتغل تلقائيًا على أي صورة تترفع مستقبلاً، فالمشكلة ما ترجعش تاني لما
/// صاحب المتجر يضيف منتجًا جديدًا بصورة كبيرة.</para>
/// </summary>
[ApiController]
public class ImageController : ControllerBase
{
    /// <summary>
    /// النطاقات المسموح بجلب الصور منها. القائمة دي **شرط أمني** — من غيرها يبقى
    /// الـ endpoint وسيلة SSRF: أي حد يقدر يخلي السيرفر يطلب أي عنوان داخلي.
    /// </summary>
    private static readonly string[] AllowedHosts =
    [
        "remal-perfume.runasp.net",
        "remalfragrances.com",
        "www.remalfragrances.com",
    ];

    /// <summary>العروض المسموح بها — قائمة مغلقة تمنع إغراق الكاش بمقاسات عشوائية.</summary>
    private static readonly int[] AllowedWidths = [200, 400, 800, 1200, 1600];

    /// <summary>
    /// جودة WebP. ٩٢ مختارة عن قصد: عند ٨٢ كانت تدرّجات الزجاج والظل في صور
    /// العطور بتبان فيها حِزَم (banding) واضحة على الشاشات الكبيرة. الفرق في
    /// الحجم ~٤٠٪ لكن الصور مكاشية للأبد، فبتتحمّل مرة واحدة بس.
    /// </summary>
    private const int Quality = 92;

    /// <summary>
    /// يتغيّر يدويًا مع أي تعديل في الجودة أو خوارزمية التصغير. مفتاح الكاش
    /// بيتبني منه، فتغييره بيولّد ملفات جديدة بدل ما الملفات القديمة تفضل
    /// تُخدَم بالإعدادات القديمة للأبد.
    /// </summary>
    private const string CacheVersion = "v2q92";

    private readonly IHttpClientFactory _http;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageController> _logger;

    public ImageController(IHttpClientFactory http, IWebHostEnvironment env, ILogger<ImageController> logger)
    {
        _http = http; _env = env; _logger = logger;
    }

    [HttpGet("/img")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get([FromQuery] string u, [FromQuery] int w, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(u)) return BadRequest("missing url");
        if (!Uri.TryCreate(u, UriKind.Absolute, out var src)) return BadRequest("bad url");
        if (src.Scheme != Uri.UriSchemeHttps && src.Scheme != Uri.UriSchemeHttp) return BadRequest("bad scheme");
        if (!AllowedHosts.Contains(src.Host, StringComparer.OrdinalIgnoreCase)) return BadRequest("host not allowed");

        var width = AllowedWidths.Contains(w) ? w : 400;

        var cacheDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "imgcache");
        Directory.CreateDirectory(cacheDir);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(src.AbsoluteUri + "|" + width + "|" + CacheVersion)))[..24].ToLowerInvariant();
        var cachePath = Path.Combine(cacheDir, key + ".webp");

        if (System.IO.File.Exists(cachePath))
            return PhysicalFile(cachePath, "image/webp");

        try
        {
            var client = _http.CreateClient("imgproxy");
            client.Timeout = TimeSpan.FromSeconds(30);
            using var res = await client.GetAsync(src, ct);
            if (!res.IsSuccessStatusCode) return StatusCode((int)res.StatusCode);

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var image = await Image.LoadAsync(stream, ct);
            var (origW, origH) = (image.Width, image.Height);

            // مش بنكبّر صورة أصلاً أصغر من المطلوب
            if (image.Width > width)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(width, 0),      // الارتفاع يتحسب تلقائيًا
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3,
                }));
            }

            // الكتابة لملف مؤقت ثم النقل — عشان طلبين متوازيين ما ينتجوش ملفًا نصّه ناقص
            var tmp = cachePath + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            await using (var outStream = System.IO.File.Create(tmp))
                await image.SaveAsync(outStream, new WebpEncoder { Quality = Quality }, ct);

            try { System.IO.File.Move(tmp, cachePath, overwrite: true); }
            catch { try { System.IO.File.Delete(tmp); } catch { } }

            _logger.LogInformation("ImageProxy: {Src} — الأصل {OrigW}×{OrigH} → {W}×{H} (توفير ذاكرة {SavedMB} ميجا)",
                src.AbsoluteUri, origW, origH, image.Width, image.Height,
                Math.Round((((long)origW * origH) - ((long)image.Width * image.Height)) * 4d / 1048576d, 1));

            return System.IO.File.Exists(cachePath)
                ? PhysicalFile(cachePath, "image/webp")
                : File(await System.IO.File.ReadAllBytesAsync(cachePath, ct), "image/webp");
        }
        catch (Exception ex)
        {
            // فشل التصغير ما ينفعش يخفي المنتج — بنحوّل للصورة الأصلية
            _logger.LogWarning(ex, "ImageProxy: فشل تصغير {Src} — هنحوّل للأصلية", src.AbsoluteUri);
            return Redirect(src.AbsoluteUri);
        }
    }
}
