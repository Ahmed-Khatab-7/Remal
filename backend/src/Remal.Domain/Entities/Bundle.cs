using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

public class Bundle : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = null!;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Tag { get; set; }
    public string? TagEn { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageUrl2 { get; set; }
    public string? ImageUrl3 { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public int Stock { get; set; }
    public BundleStatus Status { get; set; } = BundleStatus.Active;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>رمز التزامن (optimistic concurrency) — يمنع البيع الزائد للمخزون.</summary>
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<BundleItem> Items { get; set; } = new List<BundleItem>();

    public decimal Savings => Math.Max(0, OriginalPrice - FinalPrice);

    // Storefront card customization
    public string? BadgeArabic { get; set; }
    public string? BadgeEnglish { get; set; }
    public string? BadgeKind { get; set; }
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
    public string? TickerJson { get; set; }

    /// <summary>
    /// JSON لمحتوى صفحة التفاصيل القابل للتحرير من الداشبورد (سطر تعريفي + أكورديونات)،
    /// ثنائي اللغة: { taglineAr, taglineEn, whyAr, whyEn, boxAr, boxEn, benefitsAr, benefitsEn }.
    /// </summary>
    public string? DetailJson { get; set; }
}

public class BundleItem : BaseEntity
{
    public Guid BundleId { get; set; }
    public Guid ProductId { get; set; }
    public string Volume { get; set; } = "50ML";
    public int Order { get; set; }

    public Bundle Bundle { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
