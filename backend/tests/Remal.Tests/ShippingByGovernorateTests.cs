using Remal.Application.Features.Orders;
using Remal.Application.Features.Orders.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;
using Xunit;

namespace Remal.Tests;

/// <summary>
/// تكلفة الشحن حسب المحافظة (shipping_rates_json): مطابقة تامة، مطابقة داخل نص
/// "المدينة — المحافظة"، السقوط للسعر الافتراضي، تجاوز حد الشحن المجاني، وJSON تالف.
/// </summary>
public class ShippingByGovernorateTests
{
    private static OrderService NewSvc(ApplicationDbContext ctx)
        => new(ctx, new FakeAudit(), new FakeNotifier(), new FakePush());

    private const string Rates = """
        { "القاهرة": 60, "الجيزة": 70, "أسوان": 120, "بني سويف": 90 }
        """;

    private static (ApplicationDbContext ctx, Guid pid) Seed(string? ratesJson, decimal price = 500, decimal freeThreshold = 2000)
    {
        var ctx = TestDb.New();
        var p = new Product { Name = "عطر", NameEn = "P", Status = ProductStatus.Active };
        p.Sizes.Add(new ProductSize { Volume = "50ML", Price = price, Stock = 20 });
        ctx.Products.Add(p);
        ctx.AppSettings.Add(new AppSettingItem { Key = "shipping_fee", Value = "60", DataType = "decimal" });
        ctx.AppSettings.Add(new AppSettingItem { Key = "free_shipping_threshold", Value = freeThreshold.ToString(), DataType = "decimal" });
        if (ratesJson != null)
            ctx.AppSettings.Add(new AppSettingItem { Key = "shipping_rates_json", Value = ratesJson, DataType = "json" });
        ctx.SaveChanges();
        return (ctx, p.Id);
    }

    private static OrderCreateDto Dto(Guid pid, string city, int qty = 1) => new()
    {
        CustomerName = "أحمد", CustomerPhone = "01000000000", CustomerAddress = "شارع ١",
        City = city,
        Items = new[] { new OrderItemWriteDto { ProductId = pid, Volume = "50ML", Quantity = qty } }
    };

    [Theory]
    [InlineData("أسوان", 120)]        // مطابقة تامة
    [InlineData("الجيزة", 70)]
    [InlineData("القاهرة", 60)]
    public async Task Exact_governorate_match_uses_its_rate(string city, decimal expected)
    {
        var (ctx, pid) = Seed(Rates);
        var o = await NewSvc(ctx).CreateAsync(Dto(pid, city));
        Assert.Equal(expected, o.ShippingFee);
    }

    [Fact]
    public async Task City_plus_governorate_string_matches_governorate()
    {
        // الواجهة تبعت "المدينة — المحافظة" — لازم يلتقط المحافظة الصحيحة
        var (ctx, pid) = Seed(Rates);
        var o = await NewSvc(ctx).CreateAsync(Dto(pid, "كوم أمبو — أسوان"));
        Assert.Equal(120m, o.ShippingFee);
    }

    [Fact]
    public async Task Unlisted_governorate_falls_back_to_default_fee()
    {
        var (ctx, pid) = Seed(Rates);
        var o = await NewSvc(ctx).CreateAsync(Dto(pid, "المنصورة — الدقهلية"));
        Assert.Equal(60m, o.ShippingFee); // shipping_fee الافتراضي
    }

    [Fact]
    public async Task Free_shipping_threshold_still_wins_over_governorate_rate()
    {
        // طلب ٢٥٠٠ من أسوان (١٢٠) — يتجاوز حد الشحن المجاني فيصبح صفرًا
        var (ctx, pid) = Seed(Rates, price: 2500);
        var o = await NewSvc(ctx).CreateAsync(Dto(pid, "أسوان"));
        Assert.Equal(0m, o.ShippingFee);
        Assert.Equal(2500m, o.Total);
    }

    [Fact]
    public async Task Corrupt_or_missing_json_falls_back_without_throwing()
    {
        var (ctx1, pid1) = Seed("{ not json at all ");
        var o1 = await NewSvc(ctx1).CreateAsync(Dto(pid1, "أسوان"));
        Assert.Equal(60m, o1.ShippingFee);

        var (ctx2, pid2) = Seed(null); // الإعداد غير موجود إطلاقًا
        var o2 = await NewSvc(ctx2).CreateAsync(Dto(pid2, "أسوان"));
        Assert.Equal(60m, o2.ShippingFee);
    }

    [Fact]
    public async Task Empty_city_uses_default_fee()
    {
        var (ctx, pid) = Seed(Rates);
        var o = await NewSvc(ctx).CreateAsync(Dto(pid, ""));
        Assert.Equal(60m, o.ShippingFee);
    }

    [Fact]
    public async Task Total_reflects_governorate_shipping()
    {
        var (ctx, pid) = Seed(Rates, price: 500);
        var o = await NewSvc(ctx).CreateAsync(Dto(pid, "أسوان"));
        Assert.Equal(500m, o.Subtotal);
        Assert.Equal(120m, o.ShippingFee);
        Assert.Equal(620m, o.Total);
    }
}
