using Remal.Domain.Enums;

namespace Remal.Application.Features.Reviews.Dtos;

public record ReviewDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = null!;
    public string? ProductImageUrl { get; init; }
    public string CustomerName { get; init; } = null!;
    public int Rating { get; init; }
    public string? Text { get; init; }
    public ReviewStatus Status { get; init; }
    public bool IsVerifiedPurchase { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ReviewWriteDto
{
    public Guid ProductId { get; init; }
    public Guid? OrderId { get; init; }
    public string CustomerName { get; init; } = null!;
    public int Rating { get; init; }
    public string? Text { get; init; }
}

public record ReviewModerateDto(ReviewStatus Status, string? Note);
