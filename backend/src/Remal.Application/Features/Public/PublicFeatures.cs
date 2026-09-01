using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;

namespace Remal.Application.Features.Public;

// =========== Newsletter ===========
public record SubscribeNewsletterCommand(string Email, string? Source) : IRequest;

public class SubscribeNewsletterValidator : AbstractValidator<SubscribeNewsletterCommand>
{
    public SubscribeNewsletterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public class SubscribeNewsletterHandler : IRequestHandler<SubscribeNewsletterCommand>
{
    private readonly IApplicationDbContext _db;
    public SubscribeNewsletterHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SubscribeNewsletterCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var existing = await _db.NewsletterSubscriptions.FirstOrDefaultAsync(s => s.Email == email, ct);
        if (existing is not null)
        {
            if (existing.UnsubscribedAt is not null)
            {
                existing.UnsubscribedAt = null;
                await _db.SaveChangesAsync(ct);
            }
            return;
        }
        _db.NewsletterSubscriptions.Add(new NewsletterSubscription { Email = email, Source = req.Source });
        await _db.SaveChangesAsync(ct);
    }
}

// =========== Contact ===========
public record ContactMessageDto(string Name, string Phone, string? Email, string Message);

public record SendContactMessageCommand(ContactMessageDto Dto) : IRequest;

public class SendContactValidator : AbstractValidator<SendContactMessageCommand>
{
    public SendContactValidator()
    {
        RuleFor(x => x.Dto.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Dto.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Dto.Message).NotEmpty().MaximumLength(2000);
    }
}

public class SendContactHandler : IRequestHandler<SendContactMessageCommand>
{
    private readonly IApplicationDbContext _db;
    public SendContactHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SendContactMessageCommand req, CancellationToken ct)
    {
        _db.ContactMessages.Add(new ContactMessage
        {
            Name = req.Dto.Name, Phone = req.Dto.Phone,
            Email = req.Dto.Email, Message = req.Dto.Message,
        });
        await _db.SaveChangesAsync(ct);
    }
}

// =========== Public Settings ===========
public record PublicSettingsDto(string AnnouncementAr, string AnnouncementEn,
    decimal ShippingFee, decimal FreeShippingThreshold, string CurrencyAr, string CurrencyEn,
    string SitePhone, string SiteEmail,
    string? HeroImageUrl = null, string? HeroTitleAr = null, string? HeroTitleEn = null,
    string? HeroSubtitleAr = null, string? HeroSubtitleEn = null,
    // Homepage builder (managed from the dashboard):
    // HeroSlidesJson   = JSON array of image URLs for the hero carousel
    // HomeMarqueeAr/En = text of the scrolling marquee strip
    // LinksPageJson    = JSON array of { titleAr, titleEn, url } for the /links (QR) page
    string? HeroSlidesJson = null,
    string? HomeMarqueeAr = null, string? HomeMarqueeEn = null,
    string? LinksPageJson = null,
    // نصوص صفحة /links ولغتها — تُدار بالكامل من لوحة التحكم بدون أي تعديل برمجي:
    // { lang, brandSubAr/En, taglineAr/En, footerAr/En, titleAr/En, metaDescAr/En }
    string? LinksPageMetaJson = null,
    // شريط الإعلانات العمودي: قائمة رسائل [{ar,en,url}] + مدة عرض كل رسالة بالثواني
    string? AnnouncementsJson = null, int AnnouncementInterval = 4,
    // تكلفة الشحن لكل محافظة: JSON { "القاهرة": 60, "أسوان": 100, ... }
    // أي محافظة غير مذكورة تأخذ shipping_fee الافتراضي.
    string? ShippingRatesJson = null,
    // قسم ترويجي في الرئيسية (صورة + عنوان + زر) يُدار بالكامل من الداشبورد:
    // { enabled, imageUrl, headlineAr/En, subAr/En, buttonTextAr/En, targetPage, targetId }
    string? PromoSectionJson = null,
    // أرقام تحويل الدفع (تظهر للعميل في صفحة الدفع)
    string? WalletNumber = null, string? InstaPayAddress = null,
    // إعدادات طباعة بوليصة الشحن — تُدار بالكامل من لوحة التحكم:
    // { paperSize, paperWidth, paperHeight, margin, fontSize, printerType,
    //   showLogo, showItems, showPrices, showNotes }
    string? PrintSettingsJson = null,
    // اللغة الافتراضية للواجهة: "ar" أو "en" — تُدار من لوحة التحكم.
    // تُطبَّق على الزائر الذي لم يختر لغة بنفسه (اختيار الزائر له الأولوية دائمًا).
    string DefaultLanguage = "ar");

public record GetPublicSettingsQuery() : IRequest<PublicSettingsDto>;

public class GetPublicSettingsHandler : IRequestHandler<GetPublicSettingsQuery, PublicSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    public GetPublicSettingsHandler(IApplicationDbContext db, ICacheService cache) { _db = db; _cache = cache; }

    public async Task<PublicSettingsDto> Handle(GetPublicSettingsQuery req, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(CacheKeys.PublicSettings, async (innerCt) =>
        {
            var all = await _db.AppSettings.AsNoTracking().ToListAsync(innerCt);
            string Val(string key, string fallback = "") => all.FirstOrDefault(s => s.Key == key)?.Value ?? fallback;
            decimal Dec(string key, decimal fallback) => decimal.TryParse(Val(key), out var v) ? v : fallback;

            return new PublicSettingsDto(
                Val("announcement", "شحن مجاني على جميع الطلبات فوق ٢٠٠٠ جنيه"),
                Val("announcement_en", "FREE SHIPPING OVER 2000 EGP"),
                Dec("shipping_fee", 60),
                Dec("free_shipping_threshold", 2000),
                Val("currency_ar", "ج.م"),
                Val("currency_en", "EGP"),
                Val("site_phone", "01114545419"),
                Val("site_email", "hello@remal.eg"),
                HeroImageUrl: string.IsNullOrWhiteSpace(Val("hero_image_url")) ? null : Val("hero_image_url"),
                HeroTitleAr: string.IsNullOrWhiteSpace(Val("hero_title_ar")) ? null : Val("hero_title_ar"),
                HeroTitleEn: string.IsNullOrWhiteSpace(Val("hero_title_en")) ? null : Val("hero_title_en"),
                HeroSubtitleAr: string.IsNullOrWhiteSpace(Val("hero_subtitle_ar")) ? null : Val("hero_subtitle_ar"),
                HeroSubtitleEn: string.IsNullOrWhiteSpace(Val("hero_subtitle_en")) ? null : Val("hero_subtitle_en"),
                HeroSlidesJson: string.IsNullOrWhiteSpace(Val("hero_slides_json")) ? null : Val("hero_slides_json"),
                HomeMarqueeAr: string.IsNullOrWhiteSpace(Val("home_marquee_ar")) ? null : Val("home_marquee_ar"),
                HomeMarqueeEn: string.IsNullOrWhiteSpace(Val("home_marquee_en")) ? null : Val("home_marquee_en"),
                LinksPageJson: string.IsNullOrWhiteSpace(Val("links_page_json")) ? null : Val("links_page_json"),
                LinksPageMetaJson: string.IsNullOrWhiteSpace(Val("links_page_meta_json")) ? null : Val("links_page_meta_json"),
                AnnouncementsJson: string.IsNullOrWhiteSpace(Val("announcements_json")) ? null : Val("announcements_json"),
                AnnouncementInterval: (int)Dec("announcement_interval", 4),
                ShippingRatesJson: string.IsNullOrWhiteSpace(Val("shipping_rates_json")) ? null : Val("shipping_rates_json"),
                PromoSectionJson: string.IsNullOrWhiteSpace(Val("promo_section_json")) ? null : Val("promo_section_json"),
                WalletNumber: string.IsNullOrWhiteSpace(Val("payment_wallet_number")) ? null : Val("payment_wallet_number"),
                InstaPayAddress: string.IsNullOrWhiteSpace(Val("payment_insta_address")) ? null : Val("payment_insta_address"),
                PrintSettingsJson: string.IsNullOrWhiteSpace(Val("print_settings_json")) ? null : Val("print_settings_json"),
                DefaultLanguage: string.Equals(Val("default_language"), "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar");
        }, TimeSpan.FromMinutes(15), ct);
    }
}

// =========== Featured Products ===========
public record FeaturedSectionDto(string Title, string Slug, List<Guid> ProductIds);
public record GetFeaturedHomepageQuery() : IRequest<List<FeaturedSectionDto>>;

public class GetFeaturedHomepageHandler : IRequestHandler<GetFeaturedHomepageQuery, List<FeaturedSectionDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    public GetFeaturedHomepageHandler(IApplicationDbContext db, ICacheService cache) { _db = db; _cache = cache; }

    public async Task<List<FeaturedSectionDto>> Handle(GetFeaturedHomepageQuery req, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(CacheKeys.FeaturedProducts, async (innerCt) =>
        {
            var bestsellers = await _db.Products.AsNoTracking()
                .Where(p => p.Status == Domain.Enums.ProductStatus.Active)
                .OrderByDescending(p => p.Sold).Take(6)
                .Select(p => p.Id).ToListAsync(innerCt);

            var newArrivals = await _db.Products.AsNoTracking()
                .Where(p => p.Status == Domain.Enums.ProductStatus.Active)
                .OrderByDescending(p => p.CreatedAt).Take(6)
                .Select(p => p.Id).ToListAsync(innerCt);

            return new List<FeaturedSectionDto>
            {
                new("اللي خطفوا القلوب", "cult-favorites", bestsellers),
                new("وصل حديثاً", "new-arrivals", newArrivals),
            };
        }, TimeSpan.FromMinutes(15), ct);
    }
}

// =========== Related Products ===========
public record GetRelatedProductsQuery(Guid ProductId, int Take = 4) : IRequest<List<Guid>>;

public class GetRelatedProductsHandler : IRequestHandler<GetRelatedProductsQuery, List<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    public GetRelatedProductsHandler(IApplicationDbContext db, ICacheService cache) { _db = db; _cache = cache; }

    public async Task<List<Guid>> Handle(GetRelatedProductsQuery req, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(CacheKeys.RelatedProducts(req.ProductId), async (innerCt) =>
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProductId, innerCt);
            if (product is null) return new List<Guid>();
            return await _db.Products.AsNoTracking()
                .Where(p => p.Id != req.ProductId && p.Category == product.Category && p.Status == Domain.Enums.ProductStatus.Active)
                .OrderByDescending(p => p.Sold).Take(req.Take)
                .Select(p => p.Id).ToListAsync(innerCt);
        }, TimeSpan.FromMinutes(30), ct);
    }
}
