using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Enums;

namespace Remal.Api.Controllers;

/// <summary>
/// Dynamic sitemap.xml — built from the live catalog (products + gift collections)
/// so search engines always see the current inventory without manual updates.
/// The static wwwroot/sitemap.xml was removed; this controller now owns the route.
/// </summary>
[ApiController]
public class SitemapController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly IConfiguration _config;

    public SitemapController(IApplicationDbContext db, ICacheService cache, IConfiguration config)
    {
        _db = db; _cache = cache; _config = config;
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<ContentResult> Get(CancellationToken ct)
    {
        var xml = await _cache.GetOrCreateAsync("sitemap-xml", BuildAsync, TimeSpan.FromMinutes(30), ct);
        return Content(xml, "application/xml", Encoding.UTF8);
    }

    private async Task<string> BuildAsync(CancellationToken ct)
    {
        // Absolute URLs use the canonical host (same setting that drives the 301 redirect).
        var host = _config["CanonicalHost"];
        var baseUrl = string.IsNullOrWhiteSpace(host)
            ? $"{Request.Scheme}://{Request.Host}"
            : $"https://{host}";

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        // امتداد صور الخرائط — بيقول لجوجل صور إيه اللي تخص كل صفحة. من غيره جوجل
        // بيعتمد على اللي زحف عليه قبل كده، وده اللي خلاه يفضل يعرض صورًا قديمة
        // اتشالت من الموقع.
        XNamespace img = "http://www.google.com/schemas/sitemap-image/1.1";
        var urlset = new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "image", img));

        void Add(string path, DateTime? lastMod, string priority, string changefreq, string? imageUrl = null, string? imageTitle = null)
        {
            var url = new XElement(ns + "url",
                new XElement(ns + "loc", baseUrl + path),
                new XElement(ns + "changefreq", changefreq),
                new XElement(ns + "priority", priority));
            if (lastMod.HasValue)
                url.Add(new XElement(ns + "lastmod", lastMod.Value.ToString("yyyy-MM-dd")));
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                // نمرّر الصورة على وسيط التصغير — الأصل ٦٠٠٠×٦٠٠٠، وجوجل بيتعامل
                // بصعوبة مع الصور الضخمة وبيبطّئ زحفه عليها.
                var abs = imageUrl.StartsWith('/')
                    ? baseUrl + imageUrl
                    : $"{baseUrl}/img?w=1200&u={Uri.EscapeDataString(imageUrl)}";
                var el = new XElement(img + "image", new XElement(img + "loc", abs));
                if (!string.IsNullOrWhiteSpace(imageTitle))
                    el.Add(new XElement(img + "title", imageTitle));
                url.Add(el);
            }
            urlset.Add(url);
        }

        // ---- الصفحات الثابتة ----
        Add("/", null, "1.0", "daily");
        Add("/perfumes", null, "0.9", "daily");
        Add("/bundles", null, "0.8", "weekly");
        Add("/collections", null, "0.8", "weekly");
        Add("/about", null, "0.4", "monthly");
        Add("/contact", null, "0.4", "monthly");
        Add("/shipping", null, "0.3", "monthly");
        Add("/return", null, "0.3", "monthly");

        // ---- منتجات العطور (النشطة فقط) ----
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active || p.Status == ProductStatus.OutOfStock)
            .Select(p => new { p.Id, p.UpdatedAt, p.CreatedAt, p.ImageUrl, p.Name, p.NameEn })
            .ToListAsync(ct);
        foreach (var p in products)
            Add($"/product/{p.Id}", p.UpdatedAt ?? p.CreatedAt, "0.8", "weekly", p.ImageUrl, $"{p.Name} — {p.NameEn}");

        // ---- الباقات (لكل باقة صفحة تفاصيل خاصة بيها /bundle/{id}) ----
        var bundles = await _db.Bundles.AsNoTracking()
            .Where(b => b.Status == BundleStatus.Active)
            .Select(b => new { b.Id, b.UpdatedAt, b.CreatedAt, b.ImageUrl, b.Name, b.NameEn })
            .ToListAsync(ct);
        foreach (var b in bundles)
            Add($"/bundle/{b.Id}", b.UpdatedAt ?? b.CreatedAt, "0.7", "weekly", b.ImageUrl, $"{b.Name} — {b.NameEn}");

        // ---- مجموعات الاستكشاف ----
        var collections = await _db.Collections.AsNoTracking()
            .Where(c => c.Status == CollectionStatus.Active)
            .Select(c => new { c.Id, c.UpdatedAt, c.CreatedAt, c.ImageUrl, c.Name, c.NameEn })
            .ToListAsync(ct);
        foreach (var c in collections)
            Add($"/collection/{c.Id}", c.UpdatedAt ?? c.CreatedAt, "0.6", "weekly", c.ImageUrl, $"{c.Name} — {c.NameEn}");

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.None);
    }
}
