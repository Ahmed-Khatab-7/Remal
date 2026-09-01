using Remal.Application.Common.Shipping;
using Remal.Application.Features.Orders;
using Remal.Application.Features.Orders.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;
using Xunit;

namespace Remal.Tests;

/// <summary>
/// نظام الشحن بالمحافظة + المدينة (shipping_rates_json v2): سعر المدينة يتفوق على سعر
/// المحافظة، المحافظة بدون مدن تستخدم سعرها، والصيغة القديمة لسه شغالة.
/// </summary>
public class ShippingByCityTests
{
    private const string RatesV2 = """
        { "v": 2, "govs": [
            { "ar": "بني سويف", "en": "Beni Suef", "price": 55, "cities": [
                { "ar": "بني سويف", "en": "Beni Suef City", "price": 40 },
                { "ar": "الفشن",   "en": "El Fashn",       "price": 20 },
                { "ar": "الواسطى", "en": "El Wasta",       "price": 35 },
                { "ar": "ببا",     "en": "Biba",           "price": 30 }
            ]},
            { "ar": "القاهرة", "en": "Cairo", "price": 60 },
            { "ar": "أسوان",   "en": "Aswan", "price": 120, "cities": [] }
        ]}
        """;

    // ===== الوحدة: ShippingRates.Resolve =====

    [Theory]
    [InlineData("الفشن — بني سويف", 20)]      // سعر المدينة
    [InlineData("الواسطى — بني سويف", 35)]
    [InlineData("ببا — بني سويف", 30)]
    [InlineData("بني سويف — بني سويف", 40)]   // اسم المدينة = اسم المحافظة → سعر المدينة يفوز
    public void City_price_wins_over_governorate_price(string city, decimal expected)
        => Assert.Equal(expected, ShippingRates.Resolve(RatesV2, city, 60m));

    [Theory]
    [InlineData("القاهرة", 60)]               // محافظة بدون مدن
    [InlineData("أسوان", 120)]                // مصفوفة مدن فاضية
    public void Governorate_without_cities_uses_its_own_price(string city, decimal expected)
        => Assert.Equal(expected, ShippingRates.Resolve(RatesV2, city, 999m));

    [Fact]
    public void Unknown_city_inside_known_governorate_falls_back_to_governorate_price()
        => Assert.Equal(55m, ShippingRates.Resolve(RatesV2, "سمسطا — بني سويف", 60m));

    [Fact]
    public void Unknown_governorate_falls_back_to_default_fee()
        => Assert.Equal(75m, ShippingRates.Resolve(RatesV2, "المنصورة — الدقهلية", 75m));

    [Fact]
    public void English_names_match_too()
    {
        Assert.Equal(20m, ShippingRates.Resolve(RatesV2, "El Fashn — Beni Suef", 60m));
        Assert.Equal(60m, ShippingRates.Resolve(RatesV2, "Cairo", 999m));
    }

    [Fact]
    public void Legacy_flat_format_still_works()
    {
        const string v1 = """{ "القاهرة": 60, "أسوان": 120 }""";
        Assert.Equal(120m, ShippingRates.Resolve(v1, "كوم أمبو — أسوان", 60m));
        Assert.Equal(60m, ShippingRates.Resolve(v1, "القاهرة", 999m));
        Assert.Equal(80m, ShippingRates.Resolve(v1, "الدقهلية", 80m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ broken json")]
    [InlineData("[1,2,3]")]
    public void Missing_or_corrupt_settings_fall_back_without_throwing(string? json)
        => Assert.Equal(70m, ShippingRates.Resolve(json, "الفشن — بني سويف", 70m));

    [Fact]
    public void Empty_city_uses_default_fee()
        => Assert.Equal(70m, ShippingRates.Resolve(RatesV2, "", 70m));

    [Fact]
    public void Negative_or_invalid_prices_are_ignored()
    {
        const string bad = """
            { "v": 2, "govs": [ { "ar": "الجيزة", "price": -5,
                "cities": [ { "ar": "الهرم", "price": "x" } ] } ] }
            """;
        // سعر المحافظة سالب → الافتراضي، وسعر المدينة غير رقمي → متجاهَل
        Assert.Equal(65m, ShippingRates.Resolve(bad, "الهرم — الجيزة", 65m));
    }

    // ===== التكامل: السعر النهائي بيتحسب في السيرفر =====

    private static (ApplicationDbContext ctx, Guid pid) Seed(decimal price = 500)
    {
        var ctx = TestDb.New();
        var p = new Product { Name = "عطر", NameEn = "P", Status = ProductStatus.Active };
        p.Sizes.Add(new ProductSize { Volume = "50ML", Price = price, Stock = 20 });
        ctx.Products.Add(p);
        ctx.AppSettings.Add(new AppSettingItem { Key = "shipping_fee", Value = "60", DataType = "decimal" });
        ctx.AppSettings.Add(new AppSettingItem { Key = "free_shipping_threshold", Value = "2000", DataType = "decimal" });
        ctx.AppSettings.Add(new AppSettingItem { Key = "shipping_rates_json", Value = RatesV2, DataType = "json" });
        ctx.SaveChanges();
        return (ctx, p.Id);
    }

    private static OrderCreateDto Dto(Guid pid, string city) => new()
    {
        CustomerName = "أحمد", CustomerPhone = "01000000000", CustomerAddress = "شارع ١",
        City = city,
        Items = new[] { new OrderItemWriteDto { ProductId = pid, Volume = "50ML", Quantity = 1 } }
    };

    [Fact]
    public async Task Order_total_uses_the_city_rate_not_the_governorate_rate()
    {
        var (ctx, pid) = Seed();
        var order = await new OrderService(ctx, new FakeAudit(), new FakeNotifier(), new FakePush())
            .CreateAsync(Dto(pid, "الفشن — بني سويف"));
        Assert.Equal(20m, order.ShippingFee);
        Assert.Equal(520m, order.Total);
    }

    [Fact]
    public async Task Free_shipping_threshold_still_beats_the_city_rate()
    {
        var (ctx, pid) = Seed(price: 2500);
        var order = await new OrderService(ctx, new FakeAudit(), new FakeNotifier(), new FakePush())
            .CreateAsync(Dto(pid, "الفشن — بني سويف"));
        Assert.Equal(0m, order.ShippingFee);
    }

    [Fact]
    public async Task Client_cannot_pick_a_cheaper_city_from_another_governorate()
    {
        // "الفشن" مدينة في بني سويف — لو العميل بعتها مع القاهرة، السعر يفضل سعر القاهرة
        var (ctx, pid) = Seed();
        var order = await new OrderService(ctx, new FakeAudit(), new FakeNotifier(), new FakePush())
            .CreateAsync(Dto(pid, "الفشن — القاهرة"));
        Assert.Equal(60m, order.ShippingFee);
    }
}
