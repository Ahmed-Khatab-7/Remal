using Remal.Domain.Enums;

namespace Remal.Application.Features.Products.Dtos;

// OldPrice = السعر قبل الخصم (اختياري) — يُعرض مشطوبًا لو أكبر من Price
public record ProductSizeDto(Guid Id, string Volume, decimal Price, int Stock, decimal? OldPrice = null);

public record ProductSizeWriteDto(string Volume, decimal Price, int Stock, decimal? OldPrice = null);

public record ProductListDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string NameEn { get; init; } = null!;
    public string? InspiredBy { get; init; }
    public string? InspiredByEn { get; init; }
    public ProductCategory Category { get; init; }
    public ProductStatus Status { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageUrl2 { get; init; }
    public string? ImageUrl3 { get; init; }
    public int Sold { get; init; }
    public decimal Rating { get; init; }
    public int ReviewCount { get; init; }
    public int TotalStock { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public IReadOnlyList<ProductSizeDto> Sizes { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    // Per-unit cost components for profit reporting
    public decimal? CostOil { get; init; }
    public decimal? CostAlcohol { get; init; }
    public decimal? CostPackaging { get; init; }
    // Storefront card overrides
    public string? BadgeArabic { get; init; }
    public string? BadgeEnglish { get; init; }
    public string? BadgeKind { get; init; }
    public string? TickerLine1Ar { get; init; }
    public string? TickerLine1En { get; init; }
    public string? TickerLine2Ar { get; init; }
    public string? TickerLine2En { get; init; }
    public string? TickerLine3Ar { get; init; }
    public string? TickerLine3En { get; init; }
    public string? TickerLine4Ar { get; init; }
    public string? TickerLine4En { get; init; }
    public string? TickerLine5Ar { get; init; }
    public string? TickerLine5En { get; init; }
    public string? TickerLine6Ar { get; init; }
    public string? TickerLine6En { get; init; }
    public string? TickerJson { get; init; }
    // "الأداء والثبات" — نص خاص بكل عطر (اختياري؛ يسقط للنص الافتراضي في الواجهة عند غيابه)
    public string? PerformanceAr { get; init; }
    public string? PerformanceEn { get; init; }
}

public record ProductDetailDto : ProductListDto
{
    public string? NotesTop { get; init; }
    public string? NotesTopEn { get; init; }
    public string? NotesHeart { get; init; }
    public string? NotesHeartEn { get; init; }
    public string? NotesBase { get; init; }
    public string? NotesBaseEn { get; init; }
    public string? Description { get; init; }
    public string? DescriptionEn { get; init; }
}

public record ProductCreateDto
{
    public string Name { get; init; } = null!;
    public string NameEn { get; init; } = null!;
    public string? InspiredBy { get; init; }
    public string? InspiredByEn { get; init; }
    public ProductCategory Category { get; init; } = ProductCategory.Unisex;
    public ProductStatus Status { get; init; } = ProductStatus.Active;
    public string? ImageUrl { get; init; }
    public string? ImageUrl2 { get; init; }
    public string? ImageUrl3 { get; init; }
    public string? NotesTop { get; init; }
    public string? NotesTopEn { get; init; }
    public string? NotesHeart { get; init; }
    public string? NotesHeartEn { get; init; }
    public string? NotesBase { get; init; }
    public string? NotesBaseEn { get; init; }
    public string? Description { get; init; }
    public string? DescriptionEn { get; init; }
    public decimal? CostOil { get; init; }
    public decimal? CostAlcohol { get; init; }
    public decimal? CostPackaging { get; init; }
    public string? BadgeArabic { get; init; }
    public string? BadgeEnglish { get; init; }
    public string? BadgeKind { get; init; }
    public string? TickerLine1Ar { get; init; }
    public string? TickerLine1En { get; init; }
    public string? TickerLine2Ar { get; init; }
    public string? TickerLine2En { get; init; }
    public string? TickerLine3Ar { get; init; }
    public string? TickerLine3En { get; init; }
    public string? TickerLine4Ar { get; init; }
    public string? TickerLine4En { get; init; }
    public string? TickerLine5Ar { get; init; }
    public string? TickerLine5En { get; init; }
    public string? TickerLine6Ar { get; init; }
    public string? TickerLine6En { get; init; }
    public string? TickerJson { get; init; }
    public string? PerformanceAr { get; init; }
    public string? PerformanceEn { get; init; }
    public IReadOnlyList<ProductSizeWriteDto> Sizes { get; init; } = [];
}

public record ProductUpdateDto : ProductCreateDto;

public record ProductStockAdjustDto
{
    public string Volume { get; init; } = null!;
    public int NewStock { get; init; }
    public string? Reason { get; init; }
}

public record ProductStockBulkAdjustDto
{
    public IReadOnlyList<ProductStockAdjustDto> Adjustments { get; init; } = [];
    public string? Reason { get; init; }
}

public record ProductFilterDto
{
    public string? Search { get; init; }
    public ProductCategory? Category { get; init; }
    public ProductStatus? Status { get; init; }
    public bool? LowStockOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; } // "name", "price", "stock", "sold", "createdAt"
    public bool SortDesc { get; init; }
}
