using System.Text;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;

namespace Remal.Api.Middleware;

/// <summary>
/// يحقن وسوم المشاركة والفهرسة في <c>remal.html</c> حسب الصفحة المطلوبة.
///
/// <para><b>ليه موجود:</b> الموقع تطبيق صفحة واحدة (SPA) — الوسوم بتتحدّث بجافاسكريبت
/// بعد التحميل. لكن كراولر <b>واتساب وماسنجر وفيسبوك ما بيشغّلوش JavaScript إطلاقًا</b>؛
/// بيقروا الـ HTML الخام وبس. فكل رابط منتج كان بيطلع في المشاركة بنفس صورة وعنوان
/// الصفحة الرئيسية.</para>
///
/// <para><b>والأخطر من ده:</b> <c>&lt;link rel="canonical"&gt;</c> كان ثابتًا على
/// <c>/</c> في كل الصفحات — وده بيقول لجوجل حرفيًا إن كل صفحات المنتجات نسخة مكررة
/// من الرئيسية، فما بتتفهرسش. ده بيفسّر ليه جوجل بيعرض صورًا قديمة: هو أصلاً ما عندوش
/// صفحات منتجات مفهرسة يسحب منها صورًا حالية.</para>
///
/// <para><b>الأداء:</b> الملف ٧٥٠ كيلوبايت. بدل ما نعمل استبدال نصي عليه كله في كل
/// طلب، بنقسّمه <b>مرة واحدة</b> عند أول طلب لجزئين حوالين العلامتين، ونكتب
/// (المقدمة + الوسوم المولّدة + الباقي). المقدمة والباقي بايتات مكاشية ثابتة،
/// والمولّد بضع مئات البايت — يعني التكلفة لكل طلب شبه معدومة حتى مع ضغط الإعلانات.</para>
/// </summary>
public class SocialMetaMiddleware
{
    private const string StartMarker = "<!-- ===== SOCIAL-META:START =====";
    private const string EndMarker = "<!-- ===== SOCIAL-META:END ===== -->";
    private const string Origin = "https://remalfragrances.com";

    /// <summary>الصورة الافتراضية للمشاركة (الصفحات اللي مالهاش صورة كيان).</summary>
    private const string DefaultImage = Origin + "/og-image.png";

    private readonly RequestDelegate _next;
    private readonly ILogger<SocialMetaMiddleware> _logger;

    private static byte[]? _prefix;
    private static byte[]? _suffix;

    /// <summary>
    /// وقت آخر تعديل للملف وقت ما قسّمناه. **ضروري**: النشر السريع (‎-Mode html‎)
    /// بيرفع remal.html من غير ما يعيد تشغيل التطبيق، فلو كاشينا التقسيم للأبد
    /// كان هيفضل يخدم النسخة القديمة ويبان إن النشر مالوش أثر.
    /// </summary>
    private static DateTime _splitStamp;
    private static readonly SemaphoreSlim SplitLock = new(1, 1);

    public SocialMetaMiddleware(RequestDelegate next, ILogger<SocialMetaMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx, IWebHostEnvironment env, IServiceProvider sp)
    {
        var path = ctx.Request.Path.Value ?? "/";

        // بنتدخّل بس في تنقّلات HTML — مش في API ولا الأصول الثابتة.
        if (!IsHtmlNavigation(ctx, path))
        {
            await _next(ctx);
            return;
        }

        try
        {
            var meta = await BuildMetaAsync(path, sp, ctx.RequestAborted);
            if (meta is null) { await _next(ctx); return; }

            if (!await EnsureSplitAsync(env, ctx.RequestAborted)) { await _next(ctx); return; }

            var body = Encoding.UTF8.GetBytes(meta);

            // ═══ ETag ═══
            // من غيره الـ middleware دي بتاخد من السيرفر الثابت قدرته على الرد بـ 304
            // وما بتديش بديل: كل زائر راجع وكل زحفة كراولر كانت بتحمّل ٧٦٤ كيلوبايت
            // كاملة من تاني. الصفحة بتتغيّر لما يتغيّر ملف الـ HTML أو وسوم الصفحة،
            // فالبصمة بتتبني من الاتنين — أي تعديل بيبطّل الكاش تلقائيًا.
            var tag = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(
                Encoding.UTF8.GetBytes(_splitStamp.Ticks + "|" + meta)))[..16].ToLowerInvariant();
            var etag = $"\"{tag}\"";

            ctx.Response.Headers.ETag = etag;
            // الصفحات بتتغيّر مع الكتالوج — تحقق في كل مرة، لكن التحقق دلوقتي رخيص
            // (304 = بضع مئات بايت بدل ٧٦٤ كيلو).
            ctx.Response.Headers.CacheControl = "public, max-age=0, must-revalidate";

            if (string.Equals(ctx.Request.Headers.IfNoneMatch.ToString(), etag, StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength = _prefix!.Length + body.Length + _suffix!.Length;

            await ctx.Response.Body.WriteAsync(_prefix, ctx.RequestAborted);
            await ctx.Response.Body.WriteAsync(body, ctx.RequestAborted);
            await ctx.Response.Body.WriteAsync(_suffix, ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            // فشل الحقن **ما ينفعش** يمنع الصفحة — بنكمل بالنسخة الثابتة.
            _logger.LogWarning(ex, "SocialMeta: فشل حقن الوسوم لـ {Path} — هنخدم النسخة الثابتة", path);
            if (!ctx.Response.HasStarted) await _next(ctx);
        }
    }

    /// <summary>
    /// طلب تنقّل HTML = يقبل text/html ومش API ومالوش امتداد ملف.
    /// الكراولرز بتبعت Accept مختلف أحيانًا، فبنقبل <c>*/*</c> كمان.
    /// </summary>
    private static bool IsHtmlNavigation(HttpContext ctx, string path)
    {
        if (!HttpMethods.IsGet(ctx.Request.Method)) return false;
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.StartsWith("/img", StringComparison.OrdinalIgnoreCase)) return false;
        if (Path.HasExtension(path)) return false;          // .css/.js/.png/.webp…

        var accept = ctx.Request.Headers.Accept.ToString();
        return accept.Length == 0
            || accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("*/*", StringComparison.Ordinal);
    }

    /// <summary>يقسّم remal.html مرة واحدة حوالين العلامتين ويكاش الطرفين.</summary>
    private async Task<bool> EnsureSplitAsync(IWebHostEnvironment env, CancellationToken ct)
    {
        var file = Path.Combine(env.WebRootPath ?? "wwwroot", "remal.html");
        if (!File.Exists(file)) return false;

        // فحص وقت التعديل رخيص (بيقرا الميتاداتا بس مش الملف) — بيخلي أي نشر جديد
        // ينعكس فورًا من غير إعادة تشغيل، وبيمنع خدمة نسخة قديمة عالقة في الذاكرة.
        var stamp = File.GetLastWriteTimeUtc(file);
        if (_prefix is not null && _suffix is not null && _splitStamp == stamp) return true;

        await SplitLock.WaitAsync(ct);
        try
        {
            if (_prefix is not null && _suffix is not null && _splitStamp == stamp) return true;

            var html = await File.ReadAllTextAsync(file, ct);
            var start = html.IndexOf(StartMarker, StringComparison.Ordinal);
            var end = html.IndexOf(EndMarker, StringComparison.Ordinal);
            if (start < 0 || end < 0 || end <= start)
            {
                // العلامات اتشالت من الـ HTML — نسجّل ونرجّع الملف كما هو.
                _logger.LogError("SocialMeta: علامات SOCIAL-META مش موجودة في remal.html — الحقن متوقّف");
                return false;
            }

            _prefix = Encoding.UTF8.GetBytes(html[..start]);
            _suffix = Encoding.UTF8.GetBytes(html[(end + EndMarker.Length)..]);
            _splitStamp = stamp;
            return true;
        }
        finally { SplitLock.Release(); }
    }

    /// <summary>يبني كتلة الوسوم للمسار المطلوب، أو null لو المسار مالوش تخصيص.</summary>
    private static async Task<string?> BuildMetaAsync(string path, IServiceProvider sp, CancellationToken ct)
    {
        var trimmed = path.Trim('/');

        // ---- صفحات الكيانات: عنوان وصورة ووصف حقيقي من قاعدة البيانات ----
        var seg = trimmed.Split('/');
        if (seg.Length == 2 && Guid.TryParse(seg[1], out var id))
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            switch (seg[0].ToLowerInvariant())
            {
                case "product":
                {
                    var p = await db.Products.AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Name, x.NameEn, x.Description, x.ImageUrl })
                        .FirstOrDefaultAsync(ct);
                    if (p is null) break;
                    return Render(
                        title: $"{p.Name} — {p.NameEn} | Remal Fragrances",
                        description: Clean(p.Description) ?? $"عطر {p.Name} من رمال — عطور نيش فاخرة بخامات مختارة. شحن لكل محافظات مصر والدفع عند الاستلام.",
                        image: Proxy(p.ImageUrl, 1200),
                        url: $"{Origin}/product/{id}",
                        type: "product");
                }
                case "bundle":
                {
                    var b = await db.Bundles.AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Name, x.NameEn, x.Description, x.ImageUrl })
                        .FirstOrDefaultAsync(ct);
                    if (b is null) break;
                    return Render(
                        title: $"{b.Name} — {b.NameEn} | Remal Fragrances",
                        description: Clean(b.Description) ?? $"باقة {b.Name} من رمال — وفّر أكتر لما تجرّب أكتر.",
                        image: Proxy(b.ImageUrl, 1200),
                        url: $"{Origin}/bundle/{id}",
                        type: "product");
                }
                case "collection":
                {
                    var c = await db.Collections.AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Name, x.NameEn, x.Description, x.ImageUrl })
                        .FirstOrDefaultAsync(ct);
                    if (c is null) break;
                    return Render(
                        title: $"{c.Name} — {c.NameEn} | Remal Fragrances",
                        description: Clean(c.Description) ?? $"مجموعة {c.Name} من رمال — هدية جاهزة بتغليف فاخر.",
                        image: Proxy(c.ImageUrl, 1200),
                        url: $"{Origin}/collection/{id}",
                        type: "product");
                }
            }
            return null;
        }

        // ---- الصفحات الثابتة ----
        (string? t, string? d) = trimmed switch
        {
            "" => ("رمال — Remal Fragrances | عطور نيش فاخرة في مصر",
                   "عطور نيش فاخرة بخامات مختارة. عطور رجالية ونسائية ومجموعات اكتشاف. شحن لكل محافظات مصر والدفع عند الاستلام."),
            "perfumes" => ("كل العطور — Remal Fragrances",
                   "تصفّح مجموعة رمال الكاملة من العطور الرجالية والنسائية. خامات مختارة وثبات طويل، وشحن لكل محافظات مصر."),
            "bundles" => ("الباقات والعروض — Remal Fragrances",
                   "باقات رمال المختارة بأسعار أوفر — جرّب أكتر من عطر ووفّر أكتر."),
            "collections" => ("مجموعات الهدايا — Remal Fragrances",
                   "مجموعات هدايا جاهزة بتغليف فاخر من رمال — الاختيار الأسهل لما تحب تهدي."),
            "about" => ("عن رمال — Remal Fragrances", "حكاية رمال: عطور نيش مصرية بخامات فرنسية مختارة."),
            "contact" => ("تواصل معنا — Remal Fragrances", "عندك سؤال عن عطر أو طلب؟ فريق رمال جاهز يساعدك."),
            _ => (null, null),
        };
        if (t is null || d is null) return null;

        // الرئيسية لازم تفضل بشرطة مائلة في الآخر عشان تطابق الـ sitemap بالظبط —
        // اختلاف زي ده بين canonical والـ sitemap بيخلي جوجل يشوفهم رابطين مختلفين.
        var canonical = trimmed.Length == 0 ? Origin + "/" : $"{Origin}/{trimmed}";
        return Render(t, d, DefaultImage, canonical, "website");
    }

    /// <summary>
    /// صور المنتجات ٦٠٠٠×٦٠٠٠ — فيسبوك وواتساب بيرفضوا الصور الأكبر من ٨ ميجا
    /// وبيقصّوا وقت التحميل. بنمرّرها على وسيط التصغير بعرض ١٢٠٠ (المقاس اللي
    /// بيوصي بيه فيسبوك للمعاينة الكبيرة).
    /// </summary>
    private static string Proxy(string? url, int width)
    {
        if (string.IsNullOrWhiteSpace(url)) return DefaultImage;
        if (url.StartsWith('/')) return Origin + url;
        return $"{Origin}/img?w={width}&u={Uri.EscapeDataString(url)}";
    }

    /// <summary>يشيل الوسوم ويقصّ الوصف لطول مناسب للمعاينة.</summary>
    private static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var text = System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length == 0) return null;
        return text.Length <= 200 ? text : text[..197].TrimEnd() + "…";
    }

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Render(string title, string description, string image, string url, string type)
    {
        var t = Esc(title); var d = Esc(description); var i = Esc(image); var u = Esc(url);
        return $"""
        <link rel="canonical" href="{u}">
            <link rel="alternate" hreflang="ar" href="{u}">
            <link rel="alternate" hreflang="en" href="{u}{(u.Contains('?') ? "&" : "?")}lang=en">
            <link rel="alternate" hreflang="x-default" href="{u}">
            <link rel="icon" type="image/x-icon" href="/favicon.ico">
            <link rel="icon" type="image/png" sizes="192x192" href="/favicon-192.png">
            <link rel="icon" type="image/png" sizes="512x512" href="/favicon-512.png">
            <link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png">

            <meta name="description" content="{d}">
            <meta property="og:type" content="{type}">
            <meta property="og:site_name" content="Remal Fragrances — رمال">
            <meta property="og:title" content="{t}">
            <meta property="og:description" content="{d}">
            <meta property="og:url" content="{u}">
            <meta property="og:image" content="{i}">
            <meta property="og:image:secure_url" content="{i}">
            <meta property="og:image:type" content="image/webp">
            <meta property="og:image:width" content="1200">
            <meta property="og:image:height" content="1200">
            <meta property="og:image:alt" content="{t}">
            <meta property="og:locale" content="ar_EG">
            <meta property="og:locale:alternate" content="en_US">

            <meta name="twitter:card" content="summary_large_image">
            <meta name="twitter:title" content="{t}">
            <meta name="twitter:description" content="{d}">
            <meta name="twitter:image" content="{i}">
            <meta name="twitter:image:alt" content="{t}">
        """;
    }
}
