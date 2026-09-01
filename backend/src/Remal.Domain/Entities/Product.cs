using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

public class Product : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string? InspiredBy { get; set; }
    public string? InspiredByEn { get; set; }
    public ProductCategory Category { get; set; } = ProductCategory.Unisex;
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public string? ImageUrl { get; set; }
    public string? ImageUrl2 { get; set; }
    public string? ImageUrl3 { get; set; }
    public string? NotesTop { get; set; }
    public string? NotesTopEn { get; set; }
    public string? NotesHeart { get; set; }
    public string? NotesHeartEn { get; set; }
    public string? NotesBase { get; set; }
    public string? NotesBaseEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    /// <summary>نص "الأداء والثبات" الخاص بهذا العطر (يختلف من عطر لآخر) — يظهر في أكورديون صفحة المنتج.</summary>
    public string? PerformanceAr { get; set; }
    public string? PerformanceEn { get; set; }
    public int Sold { get; set; }
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }

    // Per-unit cost components (used by the accounting page to compute profit margin).
    // All optional — products without cost data are excluded from profit reports.
    public decimal? CostOil { get; set; }
    public decimal? CostAlcohol { get; set; }
    public decimal? CostPackaging { get; set; }
    public decimal? TotalCost => (CostOil ?? 0) + (CostAlcohol ?? 0) + (CostPackaging ?? 0);

    // === Storefront card customization (overrides the random defaults) ===
    /// <summary>Optional badge label shown on the card (e.g. "خصم ١٥٪", "كمية محدودة"). NULL = use the auto rotating badge.</summary>
    public string? BadgeArabic { get; set; }
    public string? BadgeEnglish { get; set; }
    /// <summary>Badge color/style: "sale" (gold), "new" (sand), "limited" (orange), "bestseller" (red). Defaults to "sale".</summary>
    public string? BadgeKind { get; set; }
    /// <summary>Up to 6 marketing/description lines shown in the rotating ticker on the card. Both AR and EN. NULL = use default pool.</summary>
    public string? TickerLine1Ar { get; set; }
    public string? TickerLine1En { get; set; }
    public string? TickerLine2Ar { get; set; }
    public string? TickerLine2En { get; set; }
    public string? TickerLine3Ar { get; set; }
    public string? TickerLine3En { get; set; }
    public string? TickerLine4Ar { get; set; }
    public string? TickerLine4En { get; set; }
    public string? TickerLine5Ar { get; set; }
    public string? TickerLine5En { get; set; }
    public string? TickerLine6Ar { get; set; }
    public string? TickerLine6En { get; set; }
    /// <summary>Optional JSON array of ticker items: [{"icon":"fire","ar":"...","en":"..."}]. Takes priority over the Line fields when set.</summary>
    public string? TickerJson { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<ProductSize> Sizes { get; set; } = new List<ProductSize>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<BundleItem> BundleItems { get; set; } = new List<BundleItem>();
    public ICollection<CollectionItem> CollectionItems { get; set; } = new List<CollectionItem>();

    // Computed
    public int TotalStock => Sizes?.Sum(s => s.Stock) ?? 0;
}

public class ProductSize : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Volume { get; set; } = null!; // "30ML", "50ML", "100ML"
    public decimal Price { get; set; }
    /// <summary>
    /// السعر قبل الخصم (اختياري). لو أكبر من Price يُعرض مشطوبًا في الواجهة مع نسبة الخصم.
    /// Price هو السعر الفعلي المُحتسب دائمًا — ده للعرض فقط.
    /// </summary>
    public decimal? OldPrice { get; set; }
    public int Stock { get; set; }

    /// <summary>رمز التزامن (optimistic concurrency) — يمنع البيع الزائد عند طلبين متزامنين على آخر قطعة.</summary>
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[]? RowVersion { get; set; }

    public Product Product { get; set; } = null!;
}
