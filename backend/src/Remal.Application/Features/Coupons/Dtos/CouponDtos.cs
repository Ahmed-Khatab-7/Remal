using Remal.Domain.Enums;

namespace Remal.Application.Features.Coupons.Dtos;

public record CouponDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public CouponType Type { get; init; }
    public decimal Value { get; init; }
    public decimal MinOrderAmount { get; init; }
    public int MaxUses { get; init; }
    public int Uses { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public bool IsExpired { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CouponWriteDto
{
    public string Code { get; init; } = null!;
    public CouponType Type { get; init; } = CouponType.Percent;
    public decimal Value { get; init; }
    public decimal MinOrderAmount { get; init; }
    public int MaxUses { get; init; } = 100;
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; } = true;
}

public record CouponValidateDto(string Code, decimal OrderAmount);

public record CouponValidationResult(bool Valid, decimal DiscountAmount, string? Reason);
