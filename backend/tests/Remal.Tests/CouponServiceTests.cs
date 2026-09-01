using Remal.Application.Features.Coupons;
using Remal.Application.Features.Coupons.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Xunit;

namespace Remal.Tests;

/// <summary>يغطي §3: قواعد التحقق من الكوبونات (تعطيل، انتهاء، حدود الاستخدام، الحد الأدنى، التقريب، السقف).</summary>
public class CouponServiceTests
{
    private static CouponService Svc(ApplicationDbContextFactory f) => new(f.Ctx, new FakeAudit());

    private static (CouponService svc, ApplicationDbContextFactory f) Setup(Coupon c)
    {
        var f = new ApplicationDbContextFactory();
        f.Ctx.Coupons.Add(c);
        f.Ctx.SaveChanges();
        return (Svc(f), f);
    }

    [Fact]
    public async Task Inactive_coupon_is_rejected()
    {
        var (svc, _) = Setup(new Coupon { Code = "OFF10", Type = CouponType.Percent, Value = 10, MaxUses = 100, IsActive = false });
        var r = await svc.ValidateAsync(new CouponValidateDto("OFF10", 1000));
        Assert.False(r.Valid);
    }

    [Fact]
    public async Task Expired_coupon_is_rejected()
    {
        var (svc, _) = Setup(new Coupon { Code = "OLD", Type = CouponType.Percent, Value = 10, MaxUses = 100, IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        var r = await svc.ValidateAsync(new CouponValidateDto("OLD", 1000));
        Assert.False(r.Valid);
    }

    [Fact]
    public async Task Uses_equal_MaxUses_boundary_is_rejected()
    {
        var (svc, _) = Setup(new Coupon { Code = "MAX", Type = CouponType.Percent, Value = 10, MaxUses = 5, Uses = 5, IsActive = true });
        var r = await svc.ValidateAsync(new CouponValidateDto("MAX", 1000));
        Assert.False(r.Valid);
    }

    [Fact]
    public async Task OrderAmount_just_below_min_is_rejected()
    {
        var (svc, _) = Setup(new Coupon { Code = "MIN", Type = CouponType.Fixed, Value = 50, MinOrderAmount = 500, MaxUses = 100, IsActive = true });
        var r = await svc.ValidateAsync(new CouponValidateDto("MIN", 499));
        Assert.False(r.Valid);
    }

    [Fact]
    public async Task Percent_discount_rounds_to_two_decimals()
    {
        var (svc, _) = Setup(new Coupon { Code = "P15", Type = CouponType.Percent, Value = 15, MaxUses = 100, IsActive = true });
        var r = await svc.ValidateAsync(new CouponValidateDto("P15", 333)); // 15% = 49.95
        Assert.True(r.Valid);
        Assert.Equal(49.95m, r.DiscountAmount);
    }

    [Fact]
    public async Task Fixed_discount_is_capped_at_subtotal()
    {
        // كوبون 500 على طلب 200 → الخصم 200 مش 500
        var (svc, _) = Setup(new Coupon { Code = "BIG", Type = CouponType.Fixed, Value = 500, MaxUses = 100, IsActive = true });
        var r = await svc.ValidateAsync(new CouponValidateDto("BIG", 200));
        Assert.True(r.Valid);
        Assert.Equal(200m, r.DiscountAmount);
    }

    [Fact]
    public async Task Unknown_code_is_rejected()
    {
        var f = new ApplicationDbContextFactory();
        var r = await Svc(f).ValidateAsync(new CouponValidateDto("NOPE", 1000));
        Assert.False(r.Valid);
    }
}
