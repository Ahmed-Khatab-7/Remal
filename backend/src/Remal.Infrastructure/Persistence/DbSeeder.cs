using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Domain.Identity;

namespace Remal.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // If migrations exist, run them. Otherwise fall back to EnsureCreated (dev convenience).
        try
        {
            var pending = (await ctx.Database.GetPendingMigrationsAsync()).ToList();
            var applied = (await ctx.Database.GetAppliedMigrationsAsync()).ToList();
            if (pending.Count > 0 || applied.Count > 0)
                await ctx.Database.MigrateAsync();
            else
                await ctx.Database.EnsureCreatedAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("No migrations", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        await SeedRolesAsync(roleMgr);
        await SeedUsersAsync(userMgr, config, logger);

        // البيانات التجريبية (منتجات/باقات/مجموعات/طلبات عيّنة) تُزرع مرة واحدة فقط
        // على قاعدة بيانات جديدة تماماً. لو فيه أي منتجات — حتى المؤرشفة/المخفية —
        // فهذا متجر حقيقي ببيانات صاحبه، ولا يجوز إعادة زرع أي محتوى تجريبي فيه.
        var isFreshCatalog = !await ctx.Products.IgnoreQueryFilters().AnyAsync();
        if (isFreshCatalog)
        {
            await SeedProductsAsync(ctx);
            await ctx.SaveChangesAsync();
            await SeedBundlesAsync(ctx);
            await ctx.SaveChangesAsync();
            await SeedCollectionsAsync(ctx);
            await ctx.SaveChangesAsync();
        }

        await SeedCouponsAsync(ctx);
        await SeedSettingsAsync(ctx);
        await ctx.SaveChangesAsync();

        // تحديثات الكتالوج على المتجر الحقيقي (عطور جديدة + إعادة بناء الباقات) —
        // كل خطوة بتتنفذ مرة واحدة بس، ومحروسة بمفتاح في AppSettings.
        await CatalogSeed.RunAsync(ctx, logger);

        if (isFreshCatalog)
        {
            await SeedSampleOrdersAndReviewsAsync(ctx);
            await ctx.SaveChangesAsync();
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleMgr)
    {
        foreach (var role in new[] { Roles.Admin, Roles.Partner, Roles.Customer })
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userMgr, IConfiguration config, ILogger logger)
    {
        // ⚠️ لا توجد كلمة سر افتراضية في الكود إطلاقًا.
        // كل حساب لازم يتحدد له كلمة سر من الإعدادات (appsettings.Production.json أو
        // متغيرات البيئة) بالشكل:  Seed:Passwords:<البريد>  — وإلا الحساب **لا يُنشأ**.
        // السبب: أي كلمة سر مكتوبة في الكود بتوصل لكل نسخة من المشروع وبتفضل صالحة
        // للأبد لو الأدمن ما غيّرهاش. الحسابات الموجودة بالفعل ما بتتأثرش نهائيًا.
        var fallbackPassword = config["Seed:DefaultPartnerPassword"];   // اختياري للتطوير المحلي فقط

        // حساب "إدارة رمال" العام (admin@remal.eg) اتشال: كل شريك له حسابه الشخصي بصلاحياته.
        var accounts = new[]
        {
            new { Email = "aby@remal.eg",   FullName = "عبدالرحمن ياسر", Avatar = "AY", Roles = new[] { Roles.Admin, Roles.Partner } },
            new { Email = "omr@remal.eg",   FullName = "عمر ماهر",        Avatar = "OM", Roles = new[] { Roles.Admin, Roles.Partner } },
            new { Email = "akh@remal.eg",   FullName = "أحمد خطاب",      Avatar = "AK", Roles = new[] { Roles.Admin, Roles.Partner } },
        };

        foreach (var p in accounts)
        {
            var existing = await userMgr.FindByEmailAsync(p.Email);
            if (existing != null) continue;   // حساب قائم — ما بنلمسش كلمة سره ولا صلاحياته

            // كلمة سر خاصة بكل حساب من الإعدادات؛ وإلا لا يُنشأ الحساب.
            var password = config[$"Seed:Passwords:{p.Email}"] ?? fallbackPassword;
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "Skipped seeding {Email}: no password configured. Set Seed:Passwords:{Email} " +
                    "(appsettings or environment variable) to create this account.", p.Email, p.Email);
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = p.Email, Email = p.Email, EmailConfirmed = true,
                FullName = p.FullName, AvatarInitials = p.Avatar, IsActive = true,
            };
            var result = await userMgr.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userMgr.AddToRolesAsync(user, p.Roles);
                logger.LogInformation("Seeded user: {Email}", p.Email);
            }
            else
                logger.LogError("Failed to seed user {Email}: {Errors}", p.Email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedProductsAsync(ApplicationDbContext ctx)
    {
        if (await ctx.Products.AnyAsync()) return;

        Product Mk(string name, string nameEn, string inspired, ProductCategory cat, string img,
            string nT, string nH, string nB,
            int s30, decimal p30, int s50, decimal p50, int s100, decimal p100)
            => new()
            {
                Name = name, NameEn = nameEn, InspiredBy = inspired, Category = cat,
                ImageUrl = img, NotesTop = nT, NotesHeart = nH, NotesBase = nB,
                Status = (s30 + s50 + s100) == 0 ? ProductStatus.OutOfStock : ProductStatus.Active,
                Sizes = new List<ProductSize>
                {
                    new() { Volume = "30ML",  Price = p30,  Stock = s30 },
                    new() { Volume = "50ML",  Price = p50,  Stock = s50 },
                    new() { Volume = "100ML", Price = p100, Stock = s100 },
                }
            };

        var products = new[]
        {
            // Unisex
            Mk("فريش سَمر", "Fresh Summer", "إيماجينيشن", ProductCategory.Unisex,
               "https://remal-perfume.runasp.net/freshSummer.webp",
               "حمضيات، برغموت، برتقال صقلي", "شاي أسود، زهر برتقال", "أمبروكسان، خشب الغاياك",
               8, 590, 14, 990, 6, 1590),

            Mk("ميستيك عود", "Mystic Oud", "أومبر نوماد", ProductCategory.Unisex,
               "https://remal-perfume.runasp.net/mysticOud.webp",
               "بخور", "عود، زعفران", "مسك، أمبر",
               5, 790, 8, 1200, 3, 1900),

            Mk("روستد موكا", "Roasted Mocha", "خمرة قهوة", ProductCategory.Unisex,
               "https://remal-perfume.runasp.net/roastedMocha.webp",
               "قهوة محمصة", "فانيليا", "تبغ، تونكا",
               4, 550, 7, 890, 4, 1450),

            // Men
            Mk("أمبر أديكشن", "Amber Addiction", "سترونجر ويز يو", ProductCategory.Men,
               "https://remal-perfume.runasp.net/amberAddiction.webp",
               "كاردامون", "كراميل، فانيليا", "فيتيفر، كستناء",
               3, 550, 5, 890, 2, 1450),

            Mk("سيتروس فلير", "Citrus Flare", "تايجر", ProductCategory.Men,
               "https://remal-perfume.runasp.net/citrusFlare.webp",
               "جريب فروت", "لافندر", "أمبروكسان، أخشاب",
               6, 590, 8, 990, 4, 1590),

            Mk("ليكويد جولد", "Liquid Gold", "لي مال إكسير", ProductCategory.Men,
               "https://remal-perfume.runasp.net/liquidGold.webp",
               "لافندر، نعناع", "تبغ، فانيليا", "عسل، أخشاب",
               1, 580, 2, 950, 1, 1550),

            // Women
            Mk("روز إمبيريال", "Rose Imperial", "روج 540", ProductCategory.Women,
               "https://remal-perfume.runasp.net/roseImperial.webp",
               "زعفران، يانسون", "ورد بلدي، ياسمين", "عنبر، خشب الأرز",
               6, 690, 9, 1150, 4, 1850),

            Mk("بلاش ڤيلڤت", "Blush Velvet", "ديليشيوس", ProductCategory.Women,
               "https://remal-perfume.runasp.net/blushVelvet.webp",
               "كمثرى، بيستاشيو", "بنفسج، إيريس", "كستناء، فانيليا",
               5, 650, 8, 1100, 3, 1750),

            Mk("جاسمين نوار", "Jasmine Noir", "بلاك أوبيوم", ProductCategory.Women,
               "https://remal-perfume.runasp.net/jasmineNoir.webp",
               "قهوة، كمثرى وردي", "ياسمين، زهرة البرتقال", "فانيليا، شعر مولد",
               4, 700, 7, 1200, 3, 1900),
        };
        ctx.Products.AddRange(products);
    }

    private static async Task SeedBundlesAsync(ApplicationDbContext ctx)
    {
        if (await ctx.Bundles.AnyAsync()) return;
        var products = await ctx.Products.ToListAsync();
        // الباقات التجريبية تستخدم المنتجات حتى الفهرس 8 — لازم 9 منتجات على الأقل
        if (products.Count < 9) return;

        ctx.Bundles.AddRange(
            new Bundle
            {
                Name = "باقة الصيف", NameEn = "The Summer Bundle",
                Description = "٣ روائح منعشة منتقاة لأيام الصيف الحارة.",
                Tag = "صيف", ImageUrl = "https://remal-perfume.runasp.net/freshSummer.webp",
                OriginalPrice = 2970, FinalPrice = 2400, Stock = 6, Status = BundleStatus.Active,
                Items = new List<BundleItem>
                {
                    new() { ProductId = products[0].Id, Volume = "50ML", Order = 0 },
                    new() { ProductId = products[4].Id, Volume = "50ML", Order = 1 },
                    new() { ProductId = products[2].Id, Volume = "50ML", Order = 2 },
                }
            },
            new Bundle
            {
                Name = "باقة الشتا", NameEn = "The Winter Bundle",
                Description = "دفا الكراميل والعود لسهرات الشتا الدافية.",
                Tag = "شتاء", ImageUrl = "https://remal-perfume.runasp.net/amberAddiction.webp",
                OriginalPrice = 3090, FinalPrice = 2500, Stock = 4, Status = BundleStatus.Active,
                Items = new List<BundleItem>
                {
                    new() { ProductId = products[3].Id, Volume = "50ML", Order = 0 },
                    new() { ProductId = products[1].Id, Volume = "50ML", Order = 1 },
                    new() { ProductId = products[2].Id, Volume = "50ML", Order = 2 },
                }
            },
            new Bundle
            {
                Name = "باقة الست المنفردة", NameEn = "Her Signature Bundle",
                Description = "٣ روائح حريمي راقية في باكدج واحدة.",
                Tag = "حريمي", ImageUrl = "https://remal-perfume.runasp.net/roseImperial.webp",
                OriginalPrice = 3450, FinalPrice = 2800, Stock = 5, Status = BundleStatus.Active,
                Items = new List<BundleItem>
                {
                    new() { ProductId = products[6].Id, Volume = "50ML", Order = 0 },
                    new() { ProductId = products[7].Id, Volume = "50ML", Order = 1 },
                    new() { ProductId = products[8].Id, Volume = "50ML", Order = 2 },
                }
            }
        );
    }

    private static async Task SeedCollectionsAsync(ApplicationDbContext ctx)
    {
        if (await ctx.Collections.AnyAsync()) return;
        var products = await ctx.Products.ToListAsync();
        if (products.Count == 0) return;

        ctx.Collections.Add(new Collection
        {
            Name = "مجموعة الاكتشاف الشاملة",
            NameEn = "The Ultimate Discovery Set",
            Description = "٩ عينات × ٥ مل — اكتشف كل عالمنا في بوكس واحد.",
            ImageUrl = "https://remal-perfume.runasp.net/disk1.webp",
            OriginalPrice = 450, FinalPrice = 275, Stock = 12,
            SampleVolume = "5ML", Status = CollectionStatus.Active,
            Items = products.Select((p, i) => new CollectionItem { ProductId = p.Id, Order = i }).ToList(),
        });
    }

    private static async Task SeedCouponsAsync(ApplicationDbContext ctx)
    {
        if (await ctx.Coupons.AnyAsync()) return;
        ctx.Coupons.AddRange(
            new Coupon { Code = "REMAL10",   Type = CouponType.Percent, Value = 10, MinOrderAmount = 0,    MaxUses = 100, ExpiresAt = DateTime.UtcNow.AddMonths(2), IsActive = true },
            new Coupon { Code = "WELCOME50", Type = CouponType.Fixed,   Value = 50, MinOrderAmount = 500,  MaxUses = 50,  ExpiresAt = DateTime.UtcNow.AddMonths(3), IsActive = true },
            new Coupon { Code = "SUMMER20",  Type = CouponType.Percent, Value = 20, MinOrderAmount = 1000, MaxUses = 50,  ExpiresAt = DateTime.UtcNow.AddDays(45),  IsActive = true }
        );
    }

    private static async Task SeedSettingsAsync(ApplicationDbContext ctx)
    {
        if (await ctx.AppSettings.AnyAsync()) return;
        ctx.AppSettings.AddRange(
            new AppSettingItem { Key = "shipping_fee",             Value = "60",   DataType = "decimal" },
            new AppSettingItem { Key = "free_shipping_threshold",  Value = "2000", DataType = "decimal" },
            // تكلفة الشحن لكل محافظة — أي محافظة غير مذكورة تأخذ shipping_fee الافتراضي
            new AppSettingItem { Key = "shipping_rates_json",      Value = "{}",   DataType = "json" },
            new AppSettingItem { Key = "low_stock_threshold",      Value = "10",   DataType = "int" },
            new AppSettingItem { Key = "currency_ar",              Value = "ج.م",  DataType = "string" },
            new AppSettingItem { Key = "currency_en",              Value = "EGP",  DataType = "string" },
            new AppSettingItem { Key = "announcement",             Value = "شحن مجاني على جميع الطلبات فوق ٢٠٠٠ جنيه", DataType = "string" },
            new AppSettingItem { Key = "announcement_en",          Value = "FREE SHIPPING OVER 2000 EGP", DataType = "string" },
            new AppSettingItem { Key = "site_phone",               Value = "01114545419", DataType = "string" },
            new AppSettingItem { Key = "site_email",               Value = "hello@remal.eg", DataType = "string" }
        );
    }

    private static async Task SeedSampleOrdersAndReviewsAsync(ApplicationDbContext ctx)
    {
        if (await ctx.Orders.AnyAsync()) return;

        var products = await ctx.Products.Include(p => p.Sizes).ToListAsync();
        // الطلبات التجريبية تستخدم المنتج الرابع ومقاس 50ML — تحقق دفاعي كامل
        if (products.Count < 4 || products.Take(4).Any(p => p.Sizes.All(s => s.Volume != "50ML"))) return;
        var p0 = products[0]; var p1 = products[1]; var p3 = products[3];
        var fifty = p0.Sizes.First(s => s.Volume == "50ML");
        var fiftyOud = p1.Sizes.First(s => s.Volume == "50ML");
        var fiftyAmber = p3.Sizes.First(s => s.Volume == "50ML");

        // 3 sample customers
        var customers = new[]
        {
            new Customer { Name = "أحمد فاروق", Phone = "01012345678", Email = "ahmed.f@example.com", City = "القاهرة", Address = "التجمع الخامس، شارع التسعين" },
            new Customer { Name = "منى السيد",  Phone = "01198765432", Email = "mona.s@example.com",  City = "الإسكندرية", Address = "سيدي بشر" },
            new Customer { Name = "سارة محمد",  Phone = "01044556677", Email = "sara.m@example.com",  City = "القاهرة", Address = "المعادي" },
        };
        ctx.Customers.AddRange(customers);
        await ctx.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var orders = new List<Order>
        {
            new()
            {
                Code = "RML-284751",
                CustomerId = customers[0].Id,
                CustomerName = customers[0].Name, CustomerPhone = customers[0].Phone, CustomerAddress = customers[0].Address!, City = customers[0].City, CustomerEmail = customers[0].Email,
                Status = OrderStatus.Delivered, PaymentMethod = PaymentMethod.CashOnDelivery, PaymentStatus = PaymentStatus.Paid,
                Subtotal = fifty.Price * 2 + fiftyOud.Price, ShippingFee = 0,
                Total = fifty.Price * 2 + fiftyOud.Price,
                PlacedAt = now.AddDays(-7), DeliveredAt = now.AddDays(-4),
                Items = new List<OrderItem>
                {
                    new() { ProductId = p0.Id, ItemName = p0.Name, Volume = "50ML", Quantity = 2, UnitPrice = fifty.Price },
                    new() { ProductId = p1.Id, ItemName = p1.Name, Volume = "50ML", Quantity = 1, UnitPrice = fiftyOud.Price },
                },
            },
            new()
            {
                Code = "RML-284123",
                CustomerId = customers[1].Id,
                CustomerName = customers[1].Name, CustomerPhone = customers[1].Phone, CustomerAddress = customers[1].Address!, City = customers[1].City, CustomerEmail = customers[1].Email,
                Status = OrderStatus.Shipping, PaymentMethod = PaymentMethod.InstaPay, PaymentStatus = PaymentStatus.Paid,
                Subtotal = fifty.Price, ShippingFee = 60, Total = fifty.Price + 60,
                PlacedAt = now.AddDays(-3), PreparedAt = now.AddDays(-2), ShippedAt = now.AddDays(-1),
                Items = new List<OrderItem>
                {
                    new() { ProductId = p0.Id, ItemName = p0.Name, Volume = "50ML", Quantity = 1, UnitPrice = fifty.Price },
                },
            },
            new()
            {
                Code = "RML-283456",
                CustomerId = customers[2].Id,
                CustomerName = customers[2].Name, CustomerPhone = customers[2].Phone, CustomerAddress = customers[2].Address!, City = customers[2].City, CustomerEmail = customers[2].Email,
                Status = OrderStatus.Preparing, PaymentMethod = PaymentMethod.Wallet, PaymentStatus = PaymentStatus.Paid,
                Subtotal = fiftyAmber.Price + fiftyOud.Price, ShippingFee = 60,
                Total = fiftyAmber.Price + fiftyOud.Price + 60,
                PlacedAt = now.AddDays(-1), PreparedAt = now.AddHours(-3),
                Items = new List<OrderItem>
                {
                    new() { ProductId = p3.Id, ItemName = p3.Name, Volume = "50ML", Quantity = 1, UnitPrice = fiftyAmber.Price },
                    new() { ProductId = p1.Id, ItemName = p1.Name, Volume = "50ML", Quantity = 1, UnitPrice = fiftyOud.Price },
                },
            },
        };
        ctx.Orders.AddRange(orders);

        // Customer totals
        foreach (var c in customers)
        {
            c.OrderCount = orders.Count(o => o.CustomerId == c.Id);
            c.TotalSpent = orders.Where(o => o.CustomerId == c.Id).Sum(o => o.Total);
        }

        // Sample reviews
        ctx.Reviews.AddRange(
            new Review { ProductId = p0.Id, CustomerName = customers[0].Name, Rating = 5,
                Text = "العطر تحفة، ثباته رهيب وفوحانه ملى المكتب كله.", Status = ReviewStatus.Approved, IsVerifiedPurchase = true },
            new Review { ProductId = p1.Id, CustomerName = customers[2].Name, Rating = 5,
                Text = "ميستيك عود فعلاً فخامة. أنصح بيه جداً للمناسبات.", Status = ReviewStatus.Approved, IsVerifiedPurchase = true },
            new Review { ProductId = p3.Id, CustomerName = customers[1].Name, Rating = 4,
                Text = "حلو لكن الثبات أقل من المتوقع شوية.", Status = ReviewStatus.Pending, IsVerifiedPurchase = true }
        );
    }
}
