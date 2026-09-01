using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;
using Xunit;

namespace Remal.Tests;

/// <summary>
/// تحديث الكتالوج: إضافة العطرين الجديدين، ملء بطاقة الخصائص، وإعادة بناء الباقات —
/// كله لازم يتنفذ مرة واحدة بس ومن غير ما يلمس أي حاجة الأدمن عدّلها.
/// </summary>
public class CatalogSeedTests
{
    // نفس أسماء وأسعار المتجر الحقيقي (حجم ٥٥ مل المخزّن كـ 50ML)
    private static readonly (string NameAr, string NameEn, decimal Price50)[] Live =
    [
        ("ارييچ", "Areej", 650m),
        ("جريتنيس", "Greatness", 750m),
        ("هيبة", "Hayba", 650m),
        ("ليل", "Lail", 750m),
        ("لهيب", "Lahib", 650m),
        ("برق", "Barq", 700m),
    ];

    private static ApplicationDbContext SeedLiveCatalog()
    {
        var ctx = TestDb.New();
        foreach (var (ar, en, price) in Live)
        {
            var p = new Product { Name = ar, NameEn = en, Status = ProductStatus.Active, ImageUrl = "https://img/" + en + ".webp" };
            p.Sizes.Add(new ProductSize { Volume = "30ML", Price = price - 50, Stock = 10 });
            p.Sizes.Add(new ProductSize { Volume = "50ML", Price = price, Stock = 10 });
            p.Sizes.Add(new ProductSize { Volume = "100ML", Price = price * 2, Stock = 5 });
            ctx.Products.Add(p);
        }
        ctx.Bundles.Add(new Bundle { Name = "باقة قديمة", OriginalPrice = 100, FinalPrice = 90, Stock = 1 });
        ctx.SaveChanges();
        return ctx;
    }

    private static Task Run(ApplicationDbContext ctx)
        => CatalogSeed.RunAsync(ctx, NullLogger.Instance);

    [Fact]
    public async Task Adds_both_new_fragrances_with_the_requested_pricing()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);

        var oud = await ctx.Products.Include(p => p.Sizes).FirstAsync(p => p.NameEn == "Oud Asel");
        var prestige = await ctx.Products.Include(p => p.Sizes).FirstAsync(p => p.NameEn == "Prestige");

        foreach (var p in new[] { oud, prestige })
        {
            var s50 = p.Sizes.Single(s => s.Volume == "50ML");
            Assert.Equal(750m, s50.Price);       // السعر بعد الخصم
            Assert.Equal(850m, s50.OldPrice);    // السعر الأساسي مشطوبًا
            Assert.Equal(ProductStatus.Active, p.Status);
            Assert.Equal(3, p.Sizes.Count);
        }
    }

    [Fact]
    public async Task Oud_Asel_is_a_creation_and_never_mentions_another_fragrance()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);
        var oud = await ctx.Products.FirstAsync(p => p.NameEn == "Oud Asel");

        // Creation خاصة بالدار: مفيش "مستوحى من" ولا أي ذكر للعطر اللي اتاخد منه الإلهام
        Assert.Null(oud.InspiredBy);
        Assert.Null(oud.InspiredByEn);
        var haystack = string.Join(" ", oud.Description, oud.DescriptionEn,
            oud.PerformanceAr, oud.PerformanceEn, oud.TickerJson, oud.BadgeArabic, oud.BadgeEnglish,
            oud.NotesTop, oud.NotesHeart, oud.NotesBase);
        foreach (var banned in new[] { "Noir", "نوار", "Majed", "ماجد", "محاكاة", "مستوحى", "Inspired" })
            Assert.DoesNotContain(banned, haystack, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Prestige_credits_its_inspiration()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);
        var p = await ctx.Products.FirstAsync(x => x.NameEn == "Prestige");
        Assert.Contains("Angels' Share Paradis", p.InspiredByEn);
        Assert.False(string.IsNullOrWhiteSpace(p.InspiredBy));
    }

    [Fact]
    public async Task Every_product_gets_a_full_note_pyramid_in_both_languages()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);

        foreach (var p in await ctx.Products.ToListAsync())
        {
            Assert.False(string.IsNullOrWhiteSpace(p.NotesTop), p.NameEn + ": المقدمة");
            Assert.False(string.IsNullOrWhiteSpace(p.NotesHeart), p.NameEn + ": القلب");
            Assert.False(string.IsNullOrWhiteSpace(p.NotesBase), p.NameEn + ": القاعدة");
            Assert.False(string.IsNullOrWhiteSpace(p.NotesTopEn), p.NameEn + ": top (EN)");
            Assert.False(string.IsNullOrWhiteSpace(p.NotesHeartEn), p.NameEn + ": heart (EN)");
            Assert.False(string.IsNullOrWhiteSpace(p.NotesBaseEn), p.NameEn + ": base (EN)");
            Assert.False(string.IsNullOrWhiteSpace(p.PerformanceAr), p.NameEn + ": الأداء");
            Assert.False(string.IsNullOrWhiteSpace(p.PerformanceEn), p.NameEn + ": performance (EN)");
        }
    }

    [Fact]
    public async Task Notes_match_the_original_fragrance_each_scent_is_built_on()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);
        var byName = await ctx.Products.ToDictionaryAsync(p => p.NameEn!);

        // برق مبني على Bvlgari Tygar: جريب فروت / زنجبيل / أمبروكسان — مفيش مانجو ولا نيرولي
        var barq = byName["Barq"];
        Assert.Contains("جريب فروت", barq.NotesTop);
        Assert.Contains("زنجبيل", barq.NotesHeart);
        Assert.Contains("أمبروكسان", barq.NotesBase);
        var barqCopy = string.Join(" ", barq.Description, barq.DescriptionEn, barq.TickerJson,
            barq.NotesTop, barq.NotesHeart, barq.NotesBase);
        Assert.DoesNotContain("مانجو", barqCopy);
        Assert.DoesNotContain("نيرولي", barqCopy);
        Assert.DoesNotContain("mango", barqCopy, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("أناناس", byName["Hayba"].NotesTop);       // Nishane Hacivat
        Assert.Contains("طحلب البلوط", byName["Hayba"].NotesBase);
        Assert.Contains("توت العليق", byName["Lail"].NotesTop);    // LV Ombre Nomade
        Assert.Contains("عود", byName["Lail"].NotesBase);
        Assert.Contains("أناناس", byName["Areej"].NotesTop);       // Xerjoff Accento
        Assert.Contains("كونياك", byName["Prestige"].NotesTop);    // Angels' Share Paradis
        Assert.Contains("فلفل وردي", byName["Lahib"].NotesTop);    // SWY Intensely
        Assert.Contains("قرفة", byName["Greatness"].NotesTop);     // Alexandria II
    }

    [Fact]
    public async Task Fills_the_product_that_had_no_description_at_all()
    {
        var ctx = SeedLiveCatalog();
        var areej = await ctx.Products.FirstAsync(p => p.NameEn == "Areej");
        areej.Description = null;                 // زي الإنتاج بالظبط
        await ctx.SaveChangesAsync();

        await Run(ctx);

        areej = await ctx.Products.FirstAsync(p => p.NameEn == "Areej");
        Assert.False(string.IsNullOrWhiteSpace(areej.Description));
        Assert.False(string.IsNullOrWhiteSpace(areej.TickerJson));
    }

    [Fact]
    public async Task Builds_four_bundles_and_archives_the_old_ones()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);

        var bundles = await ctx.Bundles.Include(b => b.Items).OrderBy(b => b.FinalPrice).ToListAsync();
        Assert.Equal(4, bundles.Count);
        Assert.All(bundles, b => Assert.Equal(BundleStatus.Active, b.Status));

        // الباقة القديمة اتأرشفت مش اتحذفت — الطلبات القديمة لازم تفضل مربوطة بيها
        var archived = await ctx.Bundles.IgnoreQueryFilters().Where(b => b.IsDeleted).ToListAsync();
        Assert.Single(archived);
        Assert.Equal("باقة قديمة", archived[0].Name);

        // كل باقة أرخص من مجموع عطورها، وبخصم متصاعد مع حجم الباقة
        decimal prevPct = 0;
        foreach (var b in bundles)
        {
            Assert.True(b.FinalPrice < b.OriginalPrice, b.Name + ": لازم يكون فيه توفير");
            Assert.NotEmpty(b.Items);
            Assert.All(b.Items, i => Assert.Equal("50ML", i.Volume));
            var pct = (b.OriginalPrice - b.FinalPrice) / b.OriginalPrice;
            Assert.True(pct >= prevPct, b.Name + ": نسبة الخصم لازم تكبر مع حجم الباقة");
            prevPct = pct;
        }

        // أكبر باقة = أعلى توفير
        var biggest = bundles.Last();
        Assert.Equal(5, biggest.Items.Count);
        Assert.Equal(3400m, biggest.OriginalPrice);
        Assert.Equal(2650m, biggest.FinalPrice);
    }

    [Fact]
    public async Task Bundle_original_price_matches_the_sum_of_its_products()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);

        var products = await ctx.Products.Include(p => p.Sizes).ToDictionaryAsync(p => p.Id);
        foreach (var b in await ctx.Bundles.Include(x => x.Items).ToListAsync())
        {
            var expected = b.Items.Sum(i => products[i.ProductId].Sizes.First(s => s.Volume == "50ML").Price);
            Assert.Equal(expected, b.OriginalPrice);
        }
    }

    [Fact]
    public async Task Running_twice_changes_nothing()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);
        var products = await ctx.Products.CountAsync();
        var bundles = await ctx.Bundles.CountAsync();

        await Run(ctx);   // إعادة تشغيل السيرفر

        Assert.Equal(products, await ctx.Products.CountAsync());
        Assert.Equal(bundles, await ctx.Bundles.CountAsync());
    }

    [Fact]
    public async Task Skips_a_product_the_admin_already_added_himself()
    {
        var ctx = SeedLiveCatalog();
        ctx.Products.Add(new Product { Name = "بريستيج", NameEn = "Prestige", Status = ProductStatus.Active });
        await ctx.SaveChangesAsync();

        await Run(ctx);

        Assert.Single(await ctx.Products.Where(p => p.NameEn == "Prestige").ToListAsync());
    }

    [Fact]
    public async Task Skips_a_bundle_whose_products_are_missing_instead_of_crashing()
    {
        var ctx = TestDb.New();   // كتالوج فاضي تمامًا
        await Run(ctx);

        Assert.Equal(2, await ctx.Products.CountAsync());   // العطران الجديدان بس
        Assert.Equal(0, await ctx.Bundles.CountAsync());    // مفيش باقة ممكن تتبني
    }

    [Fact]
    public async Task Never_touches_prices_stock_images_or_names()
    {
        var ctx = SeedLiveCatalog();
        var before = await ctx.Products.Include(p => p.Sizes).AsNoTracking()
            .Where(p => p.NameEn != "Oud Asel" && p.NameEn != "Prestige")
            .ToDictionaryAsync(p => p.NameEn!, p => new
            {
                p.Name, p.ImageUrl, p.Category, p.Status,
                Sizes = p.Sizes.OrderBy(s => s.Volume).Select(s => s.Volume + ":" + s.Price + ":" + s.Stock).ToList()
            });

        await Run(ctx);

        foreach (var (name, snapshot) in before)
        {
            var after = await ctx.Products.Include(p => p.Sizes).AsNoTracking().FirstAsync(p => p.NameEn == name);
            Assert.Equal(snapshot.Name, after.Name);
            Assert.Equal(snapshot.ImageUrl, after.ImageUrl);
            Assert.Equal(snapshot.Category, after.Category);
            Assert.Equal(snapshot.Status, after.Status);
            Assert.Equal(snapshot.Sizes,
                after.Sizes.OrderBy(s => s.Volume).Select(s => s.Volume + ":" + s.Price + ":" + s.Stock).ToList());
        }
    }

    [Fact]
    public async Task Second_run_does_not_rewrite_notes_the_admin_edited()
    {
        var ctx = SeedLiveCatalog();
        await Run(ctx);                                 // الملء الأول

        var barq = await ctx.Products.FirstAsync(p => p.NameEn == "Barq");
        barq.NotesTop = "نوتة كتبها الأدمن";
        await ctx.SaveChangesAsync();

        await Run(ctx);                                 // إعادة تشغيل السيرفر

        Assert.Equal("نوتة كتبها الأدمن", (await ctx.Products.FirstAsync(p => p.NameEn == "Barq")).NotesTop);
    }
}
