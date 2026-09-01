using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Api.Controllers;

/// <summary>
/// خلاصات المنتجات (Product feeds) لـ Google Merchant Center و Meta Catalog.
///
/// بتتولّد من الكتالوج الحي، فأي تغيير في السعر أو المخزون بيوصلهم في أول جلب
/// من غير ما حد يرفع ملف. الاتنين بيقبلوا نفس صيغة RSS 2.0 بمساحة الأسماء g:.
///
/// قواعد مقصودة:
///  • كل حجم = صنف مستقل، والمنتج الأب بيربطهم بـ item_group_id — كده جوجل بتعرض
///    الأحجام كخيارات لنفس المنتج مش كمنتجات منفصلة.
///  • حجم 50ML بيتعرض "55 ML" — نفس اللي مكتوب في الموقع بالظبط. أي اختلاف بين
///    الفيد والصفحة بيتسبب في رفض المنتج من جوجل.
///  • أي صنف من غير صورة حقيقية بيتشال — جوجل بترفض الصور البديلة، ورفض كتير
///    ممكن يعلّق الحساب كله.
/// </summary>
[ApiController]
public class FeedController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly IConfiguration _config;

    public FeedController(IApplicationDbContext db, ICacheService cache, IConfiguration config)
    {
        _db = db; _cache = cache; _config = config;
    }

    [HttpGet("/feed/google.xml")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<ContentResult> Google(CancellationToken ct)
        => Content(await _cache.GetOrCreateAsync("feed-google",
            c => BuildAsync(forMeta: false, c), TimeSpan.FromMinutes(30), ct),
            "application/xml", Encoding.UTF8);

    [HttpGet("/feed/meta.xml")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<ContentResult> Meta(CancellationToken ct)
        => Content(await _cache.GetOrCreateAsync("feed-meta",
            c => BuildAsync(forMeta: true, c), TimeSpan.FromMinutes(30), ct),
            "application/xml", Encoding.UTF8);

    private static readonly XNamespace G = "http://base.google.com/ns/1.0";

    private async Task<string> BuildAsync(bool forMeta, CancellationToken ct)
    {
        var host = _config["CanonicalHost"];
        var baseUrl = string.IsNullOrWhiteSpace(host)
            ? $"{Request.Scheme}://{Request.Host}"
            : $"https://{host}";

        var channel = new XElement("channel",
            new XElement("title", "Remal Fragrances"),
            new XElement("link", baseUrl),
            new XElement("description", "عطور نيش فاخرة — صناعة مصرية بزيوت مستوردة"));

        foreach (var item in await BuildProductItemsAsync(baseUrl, forMeta, ct)) channel.Add(item);
        foreach (var item in await BuildBundleItemsAsync(baseUrl, forMeta, ct)) channel.Add(item);

        var rss = new XElement("rss", new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "g", G), channel);

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), rss);
        return doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.None);
    }

    // ===== العطور: صنف لكل حجم، مربوطين بـ item_group_id =====
    private async Task<List<XElement>> BuildProductItemsAsync(string baseUrl, bool forMeta, CancellationToken ct)
    {
        var products = await _db.Products.AsNoTracking().Include(p => p.Sizes)
            .Where(p => p.Status != ProductStatus.Archived)
            .ToListAsync(ct);

        var items = new List<XElement>();
        foreach (var p in products)
        {
            if (!HasRealImage(p.ImageUrl)) continue;   // من غير صورة حقيقية → متبعتش

            var description = FirstNonEmpty(
                p.Description,
                Join(" · ", p.NotesTop, p.NotesHeart, p.NotesBase),
                p.Name);

            foreach (var size in p.Sizes.OrderBy(s => s.Price))
            {
                var el = NewItem(
                    id: $"{p.Id}-{size.Volume}",
                    title: $"{p.Name} — {DisplayVolume(size.Volume)}",
                    description: description!,
                    link: $"{baseUrl}/product/{p.Id}",
                    imageUrl: Absolute(p.ImageUrl!, baseUrl),
                    price: size.OldPrice is > 0 && size.OldPrice > size.Price ? size.OldPrice.Value : size.Price,
                    salePrice: size.OldPrice is > 0 && size.OldPrice > size.Price ? size.Price : null,
                    inStock: size.Stock > 0,
                    quantity: size.Stock,
                    forMeta: forMeta);

                el.Add(new XElement(G + "item_group_id", p.Id.ToString()));
                el.Add(new XElement(G + "product_type", ProductType(p.Category)));
                el.Add(new XElement(G + "size", DisplayVolume(size.Volume)));
                if (!string.IsNullOrWhiteSpace(p.ImageUrl2))
                    el.Add(new XElement(G + "additional_image_link", Absolute(p.ImageUrl2, baseUrl)));

                items.Add(el);
            }
        }
        return items;
    }

    // ===== الباقات: صنف واحد لكل باقة =====
    private async Task<List<XElement>> BuildBundleItemsAsync(string baseUrl, bool forMeta, CancellationToken ct)
    {
        var bundles = await _db.Bundles.AsNoTracking()
            .Where(b => b.Status == BundleStatus.Active)
            .ToListAsync(ct);

        var items = new List<XElement>();
        foreach (var b in bundles)
        {
            if (!HasRealImage(b.ImageUrl)) continue;

            var el = NewItem(
                id: $"bundle-{b.Id}",
                title: b.Name,
                description: FirstNonEmpty(b.Description, b.Name)!,
                link: $"{baseUrl}/bundle/{b.Id}",
                imageUrl: Absolute(b.ImageUrl!, baseUrl),
                price: b.OriginalPrice > b.FinalPrice ? b.OriginalPrice : b.FinalPrice,
                salePrice: b.OriginalPrice > b.FinalPrice ? b.FinalPrice : null,
                inStock: b.Stock > 0,
                quantity: b.Stock,
                forMeta: forMeta);

            el.Add(new XElement(G + "product_type", "باقات"));
            el.Add(new XElement(G + "is_bundle", "yes"));
            items.Add(el);
        }
        return items;
    }

    private static XElement NewItem(string id, string title, string description, string link,
        string imageUrl, decimal price, decimal? salePrice, bool inStock, int quantity, bool forMeta)
    {
        var el = new XElement("item",
            new XElement(G + "id", id),
            new XElement("title", Trim(title, 150)),
            new XElement("description", Trim(Clean(description), 5000)),
            new XElement("link", link),
            new XElement(G + "image_link", imageUrl),
            new XElement(G + "availability", inStock ? "in_stock" : "out_of_stock"),
            new XElement(G + "condition", "new"),
            new XElement(G + "brand", "Remal Fragrances"),
            new XElement(G + "price", Money(price)),
            // مفيش باركود عالمي للعطور دي — لازم نقولها لجوجل صراحةً وإلا بترفض الصنف
            new XElement(G + "identifier_exists", "no"),
            new XElement(G + "google_product_category", "Health & Beauty > Personal Care > Cosmetics > Perfume & Cologne"));

        if (salePrice is not null) el.Add(new XElement(G + "sale_price", Money(salePrice.Value)));
        if (forMeta) el.Add(new XElement(G + "quantity_to_sell_on_facebook", Math.Max(0, quantity)));
        return el;
    }

    // ===== مساعدات =====

    /// <summary>حجم 50ML بيتعرض 55 ML — نفس اللي في الموقع بالظبط.</summary>
    private static string DisplayVolume(string volume)
        => string.Equals(volume, "50ML", StringComparison.OrdinalIgnoreCase)
            ? "55 ML"
            : volume.Replace("ML", " ML", StringComparison.OrdinalIgnoreCase).Trim();

    /// <summary>الصورة البديلة مش صورة منتج — جوجل بترفضها.</summary>
    private static bool HasRealImage(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && !url.Contains("product-placeholder", StringComparison.OrdinalIgnoreCase)
           && !url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    private static string Absolute(string url, string baseUrl)
        => url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : baseUrl + "/" + url.TrimStart('/');

    private static string Money(decimal v) => $"{decimal.Round(v, 2)} EGP";

    private static string ProductType(ProductCategory c) => c switch
    {
        ProductCategory.Men => "عطور > رجالي",
        ProductCategory.Women => "عطور > نسائي",
        _ => "عطور > للجنسين",
    };

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Join(string sep, params string?[] parts)
        => string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Clean(string s)
        => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", " ").Replace("  ", " ").Trim();

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
