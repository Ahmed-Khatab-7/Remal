using Remal.Domain.Enums;

namespace Remal.Application.Features.Collections.Dtos;

public record CollectionItemDto(Guid Id, Guid ProductId, string ProductName, string? ProductNameEn, string? ProductImageUrl, int Order);

public record CollectionItemWriteDto(Guid ProductId, int Order);

public record CollectionListDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? NameEn { get; init; }
    public string? Description { get; init; }
    public string? DescriptionEn { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageUrl2 { get; init; }
    public string? ImageUrl3 { get; init; }
    public decimal OriginalPrice { get; init; }
    public decimal FinalPrice { get; init; }
    public decimal Savings => OriginalPrice - FinalPrice;
    public int Stock { get; init; }
    public string SampleVolume { get; init; } = "2ML";
    public CollectionStatus Status { get; init; }
    public IReadOnlyList<CollectionItemDto> Items { get; init; } = [];
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
    public string? DetailJson { get; init; }
}

public record CollectionWriteDto
{
    public string Name { get; init; } = null!;
    public string? NameEn { get; init; }
    public string? Description { get; init; }
    public string? DescriptionEn { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageUrl2 { get; init; }
    public string? ImageUrl3 { get; init; }
    public decimal OriginalPrice { get; init; }
    public decimal FinalPrice { get; init; }
    public int Stock { get; init; }
    public string SampleVolume { get; init; } = "2ML";
    public CollectionStatus Status { get; init; } = CollectionStatus.Active;
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
    public string? DetailJson { get; init; }
    public IReadOnlyList<CollectionItemWriteDto> Items { get; init; } = [];
}
