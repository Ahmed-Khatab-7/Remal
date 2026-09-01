using Remal.Application.Features.Orders;
using Remal.Application.Features.Orders.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;
using Xunit;

namespace Remal.Tests;

/// <summary>يغطي §1 التسعير + §2 المخزون + §3 العروض التلقائية على OrderService.CreateAsync.</summary>
public class OrderServiceTests
{
    private static OrderService NewSvc(ApplicationDbContext ctx)
        => new(ctx, new FakeAudit(), new FakeNotifier(), new FakePush());

    private static (ApplicationDbContext ctx, Product p) SeedProduct(int stock50 = 10, decimal price50 = 1000)
    {
        var ctx = TestDb.New();
        var p = new Product { Name = "عطر تجريبي", NameEn = "Test", Status = ProductStatus.Active };
        p.Sizes.Add(new ProductSize { Volume = "50ML", Price = price50, Stock = stock50 });
        ctx.Products.Add(p);
        ctx.SaveChanges();
        return (ctx, p);
    }

    private static OrderCreateDto Dto(Guid productId, int qty = 1, string? coupon = null) => new()
    {
        CustomerName = "أحمد", CustomerPhone = "01000000000", CustomerAddress = "القاهرة",
        CouponCode = coupon,
        Items = new[] { new OrderItemWriteDto { ProductId = productId, Volume = "50ML", Quantity = qty } }
    };

    // ---------- §1 التسعير ----------

    [Fact]
    public async Task Total_equals_subtotal_plus_shipping_minus_discount()
    {
        var (ctx, p) = SeedProduct(price50: 1000);
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1));
        // 1000 subtotal، تحت حد الشحن المجاني 2000 → شحن 60، بدون خصم
        Assert.Equal(1000m, o.Subtotal);
        Assert.Equal(60m, o.ShippingFee);
        Assert.Equal(0m, o.DiscountAmount);
        Assert.Equal(1060m, o.Total);
    }

    [Fact]
    public async Task Free_shipping_uses_inclusive_threshold()
    {
        // بالظبط عند الحد 2000 → شحن مجاني (>=)
        var (ctx, p) = SeedProduct(price50: 2000);
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1));
        Assert.Equal(0m, o.ShippingFee);
    }

    [Fact]
    public async Task One_pound_below_threshold_still_charges_shipping()
    {
        var (ctx, p) = SeedProduct(price50: 1999);
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1));
        Assert.Equal(60m, o.ShippingFee);
    }

    [Fact]
    public async Task Discount_never_exceeds_subtotal_total_never_negative()
    {
        var (ctx, p) = SeedProduct(price50: 200);
        ctx.Coupons.Add(new Coupon { Code = "BIG", Type = CouponType.Fixed, Value = 500, MaxUses = 100, IsActive = true });
        ctx.SaveChanges();
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1, coupon: "BIG"));
        Assert.Equal(200m, o.DiscountAmount); // capped at subtotal
        Assert.True(o.Total >= 0);
        Assert.Equal(60m, o.Total); // 200 + 60 shipping - 200 discount
    }

    [Fact]
    public async Task Multi_line_total_matches_manual_sum()
    {
        var ctx = TestDb.New();
        var p1 = new Product { Name = "A", NameEn = "A", Status = ProductStatus.Active };
        p1.Sizes.Add(new ProductSize { Volume = "50ML", Price = 333.33m, Stock = 50 });
        var p2 = new Product { Name = "B", NameEn = "B", Status = ProductStatus.Active };
        p2.Sizes.Add(new ProductSize { Volume = "50ML", Price = 149.50m, Stock = 50 });
        ctx.Products.AddRange(p1, p2);
        ctx.SaveChanges();
        var dto = new OrderCreateDto
        {
            CustomerName = "x", CustomerPhone = "01000000001", CustomerAddress = "y",
            Items = new[]
            {
                new OrderItemWriteDto { ProductId = p1.Id, Volume = "50ML", Quantity = 3 }, // 999.99
                new OrderItemWriteDto { ProductId = p2.Id, Volume = "50ML", Quantity = 2 }, // 299.00
            }
        };
        var o = await NewSvc(ctx).CreateAsync(dto);
        Assert.Equal(1298.99m, o.Subtotal);
    }

    // ---------- §2 المخزون ----------

    [Fact]
    public async Task Insufficient_stock_is_rejected()
    {
        var (ctx, p) = SeedProduct(stock50: 1);
        await Assert.ThrowsAnyAsync<Exception>(() => NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 2)));
    }

    [Fact]
    public async Task Stock_decrements_and_sold_increments()
    {
        var (ctx, p) = SeedProduct(stock50: 10);
        await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 3));
        var size = ctx.ProductSizes.First(s => s.ProductId == p.Id && s.Volume == "50ML");
        Assert.Equal(7, size.Stock);
        Assert.Equal(3, ctx.Products.First(x => x.Id == p.Id).Sold);
    }

    [Fact]
    public async Task Selling_last_unit_flips_status_to_OutOfStock()
    {
        var (ctx, p) = SeedProduct(stock50: 1);
        await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1));
        Assert.Equal(ProductStatus.OutOfStock, ctx.Products.First(x => x.Id == p.Id).Status);
    }

    // ---------- §3 العروض التلقائية ----------

    [Fact]
    public async Task OrderPercentOver_applies_when_subtotal_over_minspend()
    {
        var (ctx, p) = SeedProduct(price50: 1000, stock50: 10);
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "خصم 10% فوق 800", Type = PromotionType.OrderPercentOver,
            MinSpend = 800, RewardPercentOff = 10, IsActive = true, Priority = 1
        });
        ctx.SaveChanges();
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1)); // subtotal 1000
        Assert.Equal(100m, o.DiscountAmount); // 10% of 1000
    }

    [Fact]
    public async Task BuyXGetYFree_adds_gift_line_capped_by_reward_stock()
    {
        var ctx = TestDb.New();
        var trigger = new Product { Name = "T", NameEn = "T", Status = ProductStatus.Active };
        trigger.Sizes.Add(new ProductSize { Volume = "50ML", Price = 500, Stock = 10 });
        var reward = new Product { Name = "G", NameEn = "G", Status = ProductStatus.Active };
        reward.Sizes.Add(new ProductSize { Volume = "30ML", Price = 300, Stock = 1 }); // only 1 in stock
        ctx.Products.AddRange(trigger, reward);
        ctx.SaveChanges();
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "اشترِ 2 خد هدية", Type = PromotionType.BuyXGetYFree,
            BuyQuantity = 2, RewardProductId = reward.Id, RewardVolume = "30ML",
            RewardQuantity = 1, IsActive = true, Priority = 1
        });
        ctx.SaveChanges();
        var dto = new OrderCreateDto
        {
            CustomerName = "x", CustomerPhone = "01000000002", CustomerAddress = "y",
            Items = new[] { new OrderItemWriteDto { ProductId = trigger.Id, Volume = "50ML", Quantity = 4 } }
        };
        var o = await NewSvc(ctx).CreateAsync(dto);
        // 4 / 2 = 2 sets × 1 = 2 gifts, but reward stock only 1 → capped at 1 free line
        var giftLine = o.Items.FirstOrDefault(i => i.UnitPrice == 0m);
        Assert.NotNull(giftLine);
        Assert.Equal(1, giftLine!.Quantity);
        Assert.Equal(0, ctx.ProductSizes.First(s => s.ProductId == reward.Id).Stock);
    }

    [Fact]
    public async Task Two_percent_promos_apply_highest_only_not_their_sum()
    {
        // D4 — عرضان بنسبة مئوية فعّالان في نفس الوقت: 10% و 25% على طلب 1000.
        // المتوقع: يُطبَّق الأعلى قيمةً فقط (250) وليس مجموعهما (350).
        var (ctx, p) = SeedProduct(price50: 1000, stock50: 10);
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "خصم 10% فوق 500", Type = PromotionType.OrderPercentOver,
            MinSpend = 500, RewardPercentOff = 10, IsActive = true, Priority = 5
        });
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "خصم 25% فوق 800", Type = PromotionType.OrderPercentOver,
            MinSpend = 800, RewardPercentOff = 25, IsActive = true, Priority = 1
        });
        ctx.SaveChanges();
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1)); // subtotal 1000
        Assert.Equal(250m, o.DiscountAmount); // الأعلى فقط (25% من 1000)، مش 350
    }

    [Fact]
    public async Task Highest_percent_promo_still_stacks_with_coupon()
    {
        // D4 — الكوبون يتراكم فوق العرض المختار: عرض 25% (250) + كوبون ثابت 100 = 350.
        var (ctx, p) = SeedProduct(price50: 1000, stock50: 10);
        ctx.Coupons.Add(new Coupon { Code = "SAVE100", Type = CouponType.Fixed, Value = 100, MaxUses = 100, IsActive = true });
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "خصم 10%", Type = PromotionType.OrderPercentOver,
            MinSpend = 500, RewardPercentOff = 10, IsActive = true, Priority = 5
        });
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "خصم 25%", Type = PromotionType.OrderPercentOver,
            MinSpend = 800, RewardPercentOff = 25, IsActive = true, Priority = 1
        });
        ctx.SaveChanges();
        var o = await NewSvc(ctx).CreateAsync(Dto(p.Id, qty: 1, coupon: "SAVE100"));
        Assert.Equal(350m, o.DiscountAmount); // 250 (أعلى عرض) + 100 (كوبون)
    }

    [Fact]
    public async Task FreeGiftOverAmount_does_not_trigger_below_minspend()
    {
        var ctx = TestDb.New();
        var trigger = new Product { Name = "T", NameEn = "T", Status = ProductStatus.Active };
        trigger.Sizes.Add(new ProductSize { Volume = "50ML", Price = 300, Stock = 10 });
        var reward = new Product { Name = "G", NameEn = "G", Status = ProductStatus.Active };
        reward.Sizes.Add(new ProductSize { Volume = "30ML", Price = 200, Stock = 10 });
        ctx.Products.AddRange(trigger, reward);
        ctx.SaveChanges();
        ctx.Promotions.Add(new Promotion
        {
            NameAr = "هدية فوق 1000", Type = PromotionType.FreeGiftOverAmount,
            MinSpend = 1000, RewardProductId = reward.Id, RewardVolume = "30ML",
            RewardQuantity = 1, IsActive = true, Priority = 1
        });
        ctx.SaveChanges();
        var dto = new OrderCreateDto
        {
            CustomerName = "x", CustomerPhone = "01000000003", CustomerAddress = "y",
            Items = new[] { new OrderItemWriteDto { ProductId = trigger.Id, Volume = "50ML", Quantity = 1 } } // 300 < 1000
        };
        var o = await NewSvc(ctx).CreateAsync(dto);
        Assert.DoesNotContain(o.Items, i => i.UnitPrice == 0m);
    }
}
