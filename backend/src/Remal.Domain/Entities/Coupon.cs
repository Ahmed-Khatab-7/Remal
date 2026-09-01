using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

public class Coupon : AuditableEntity
{
    public string Code { get; set; } = null!;
    public CouponType Type { get; set; } = CouponType.Percent;
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int MaxUses { get; set; }
    public int Uses { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt < DateTime.UtcNow;
    public bool IsUsable => IsActive && !IsExpired && Uses < MaxUses;
}
