using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Infrastructure.Persistence;

/// <summary>
/// تحديثات الكتالوج اللي لازم تتطبق على قاعدة البيانات الحقيقية (مش بيانات تجريبية).
/// كل خطوة محروسة بمفتاح في AppSettings عشان تتنفذ مرة واحدة بس مهما اتعمل restart،
/// وكلها idempotent — لو الأدمن عدّل أو حذف حاجة بعد كده، السيرفر مش هيرجّعها تاني.
/// </summary>
public static class CatalogSeed
{
    private const string ProductsFlag = "catalog_v3_products";
    private const string BundlesFlag = "catalog_v3_bundles";
    private const string NotesFlag = "catalog_v4_notes";
    private const string PlaceholderImage = "/product-placeholder.svg";

    public static async Task RunAsync(ApplicationDbContext ctx, ILogger logger)
    {
        await AddCreationsAsync(ctx, logger);
        await RebuildBundlesAsync(ctx, logger);
        await FillNotesAndPerformanceAsync(ctx, logger);
    }

    // ==================== 3) نوتات كل عطر وأداؤه ====================
    // بيملا الأكورديونين الموجودين في صفحة المنتج: «التركيبة السرية (النوتات)» و«الأداء والثبات».
    // هرم النوتات مأخوذ من التوصيف المنشور للعطر الأصلي اللي كل محاكاة مبنية عليه
    // (Fragrantica / Parfumo / موقع الدار)، ونص الأداء موصوف بأسلوب رمال.
    // بيتنفذ مرة واحدة بس؛ بعدها أي تعديل من لوحة التحكم بيفضل زي ما هو.
    private static async Task FillNotesAndPerformanceAsync(ApplicationDbContext ctx, ILogger logger)
    {
        if (await AlreadyDoneAsync(ctx, NotesFlag)) return;

        var data = new Dictionary<string, Notes>(StringComparer.OrdinalIgnoreCase)
        {
            // ارييچ — Xerjoff Accento (شيبر زهري فاكهي)
            ["Areej"] = new(
                "أناناس، صفير (هياسينث)", "Pineapple, Hyacinth",
                "إيريس، فلفل وردي، ياسمين", "Iris, Pink Pepper, Jasmine",
                "مسك، عنبر، فيتيفر، فانيليا، باتشولي", "Musk, Amber, Vetiver, Vanilla, Patchouli",
                "ثبات من ٨ إلى ١٠ ساعات على البشرة وأطول على الهدوم، وفوحان متوسط أنيق بيفضل قريب منك "
                + "بدل ما يملا المكان — عطر شخصي راقي مش صاخب. الأنسب للربيع والنهار والشغل.",
                "8 – 10 hours on skin and longer on fabric, with an elegant moderate sillage that stays close "
                + "rather than filling a room — refined and personal, never loud. Best in spring, daytime and at work."),

            // جريتنيس — Xerjoff Alexandria II (شرقي عودي فاخر)
            ["Greatness"] = new(
                "خشب الورد (باليساندر)، لافندر، تفاح، قرفة", "Rosewood, Lavender, Apple, Cinnamon",
                "ورد، خشب الأرز، زنبق الوادي", "Rose, Cedar, Lily of the Valley",
                "عود، خشب الصندل، عنبر، فانيليا، مسك", "Oud, Sandalwood, Amber, Vanilla, Musk",
                "من أقوى عطور الدار ثباتًا: ١٢ ساعة كاملة على البشرة وأثر يفضل على الهدوم أيام. الفوحان "
                + "بيتصاعد مع دفء الجسم فبيزيد جمالًا مع الوقت — رشتين كفاية تمامًا. الأنسب للشتاء والمساء والمناسبات.",
                "One of the strongest performers in the house: a full 12 hours on skin and a trail that lingers on "
                + "fabric for days. Projection blooms with body heat, growing richer as the hours pass — two sprays "
                + "are plenty. Best in winter, evenings and special occasions."),

            // هيبة — Nishane Hacivat (شيبر فاكهي خشبي)
            ["Hayba"] = new(
                "أناناس، جريب فروت، برغموت", "Pineapple, Grapefruit, Bergamot",
                "خشب الأرز، باتشولي، ياسمين", "Cedar, Patchouli, Jasmine",
                "طحلب البلوط، نوتات خشبية", "Oakmoss, Woody Notes",
                "ثبات من ٨ إلى ١٠ ساعات، وفوحان عالي بيتلاحظ من مسافة في أول ٣ ساعات ثم بيهدى لهالة "
                + "خشبية أنيقة — عطر إعجابات بامتياز. الأنسب للربيع والصيف والأجواء المعتدلة والشغل.",
                "8 – 10 hours with high projection noticed from a distance for the first 3 hours, then settling into "
                + "an elegant woody aura — a genuine compliment-getter. Best in spring, summer, mild weather and at work."),

            // ليل — Louis Vuitton Ombre Nomade (شرقي خشبي عودي)
            ["Lail"] = new(
                "توت العليق، زعفران", "Raspberry, Saffron",
                "ورد، جيرانيوم، بخور", "Rose, Geranium, Incense",
                "عود، خشب العنبر، بنزوين، بتولا", "Oud, Amberwood, Benzoin, Birch",
                "أثقل عطورنا حضورًا: ١٢ ساعة وأكتر على البشرة، والرائحة بتفضل على الهدوم أيام. فوحان "
                + "عريض بيملا المكان — رشة أو رشتين كفاية تمامًا. الأنسب للشتاء والمساء والمناسبات الكبيرة.",
                "Our heaviest performer: 12+ hours on skin and days on fabric. Broad projection that fills a room — "
                + "one or two sprays are more than enough. Best in winter, evenings and grand occasions."),

            // لهيب — Armani Stronger With You Intensely (عنبري حلو)
            ["Lahib"] = new(
                "فلفل وردي، عرعر، بنفسج", "Pink Pepper, Juniper, Violet",
                "كستناء وتوفي كراميلي، قرفة، لافندر، مريمية", "Chestnut & Toffee, Cinnamon, Lavender, Sage",
                "فانيليا، عنبر، حبة التونكا، جلد السويد", "Vanilla, Amber, Tonka Bean, Suede",
                "ثبات من ١٠ إلى ١٢ ساعة، وفوحان حلو دافئ بيسيب أثر واضح في المكان في أول ٤ ساعات. "
                + "الأنسب للشتاء والمساء والسهرات والمواعيد.",
                "10 – 12 hours with a warm, sweet projection that leaves a clear trail through the first 4 hours. "
                + "Best in winter, evenings, nights out and dates."),

            // برق — Bvlgari Le Gemme Tygar (حمضي عنبري)
            ["Barq"] = new(
                "جريب فروت", "Grapefruit",
                "زنجبيل، أمبريت (المسك النباتي)", "Ginger, Ambrette",
                "أمبروكسان، مسك، فيتيفر، باتشولي", "Ambroxan, Musk, Vetiver, Patchouli",
                "ثبات يتجاوز ١٠ ساعات على البشرة وأكتر على الهدوم، وفوحان قوي جدًا في أول ٣ ساعات بفضل "
                + "الأمبروكسان، ثم هالة قريبة أنيقة تفضل لآخر اليوم. الأنسب للصيف والنهار.",
                "Over 10 hours on skin and longer on fabric, with very strong ambroxan-driven projection for the first "
                + "3 hours settling into an elegant close aura for the rest of the day. Best in summer and daytime."),

            // عود أصيل — Creation خاصة بالدار
            ["Oud Asel"] = new(
                "توت أحمر، فلفل وردي", "Red Berries, Pink Pepper",
                "سوسن (إيريس)، ورد دمشقي", "Orris, Damask Rose",
                "جلد ناعم، عود، تبغ، عنبر، مسك أبيض", "Soft Leather, Oud, Tobacco, Amber, White Musk",
                "تركيز Extrait de Parfum بنسبة زيوت عالية — ثبات من ١٠ إلى ١٢ ساعة على البشرة وأكتر على "
                + "الهدوم، وفوحان قوي في أول ٤ ساعات بيهدى بعدها لهالة قريبة أنيقة. الأنسب للخريف والشتاء وسهرات المساء.",
                "A high-oil Extrait de Parfum — 10 to 12 hours on skin and longer on fabric, with strong projection "
                + "for the first 4 hours settling into an elegant close aura. Best in autumn, winter and evenings."),

            // بريستيج — Kilian Angels' Share Paradis
            ["Prestige"] = new(
                "كونياك، توت العليق، ليكيور", "Cognac, Raspberry, Liqueur",
                "ورد بلغاري، حبة التونكا، طحلب البلوط", "Bulgarian Rose, Tonka Bean, Oakmoss",
                "برالين، خشب البلوط، خشب الصندل", "Praline, Oak, Sandalwood",
                "تركيز Extrait de Parfum — فوحان انفجاري في أول ساعتين بيملا المكان، وثبات من ١٠ إلى ١٢ "
                + "ساعة على البشرة وأكتر على الهدوم. الأنسب للخريف والشتاء والسهرات.",
                "An Extrait de Parfum concentration — explosive projection that fills a room for the first two hours, "
                + "and 10 to 12 hours of longevity on skin, longer on fabric. Best in autumn, winter and evenings."),
        };

        var all = await ctx.Products.ToListAsync();
        var touched = 0;
        foreach (var p in all)
        {
            if (!data.TryGetValue(p.NameEn ?? "", out var n)) continue;
            p.NotesTop = n.TopAr;   p.NotesTopEn = n.TopEn;
            p.NotesHeart = n.HeartAr; p.NotesHeartEn = n.HeartEn;
            p.NotesBase = n.BaseAr; p.NotesBaseEn = n.BaseEn;
            p.PerformanceAr = n.PerfAr; p.PerformanceEn = n.PerfEn;
            touched++;
        }

        FixInaccurateCopy(all, logger);

        MarkDone(ctx, NotesFlag);
        await ctx.SaveChangesAsync();
        logger.LogInformation("CatalogSeed: اتملت النوتات والأداء لـ {Count} عطر.", touched);
    }

    /// <summary>
    /// تصحيحات نصية على بيانات موجودة بالفعل (مش إضافة أي حقل جديد):
    /// وصف «برق» وشريطه كانا بيذكروا مانجو ونيرولي — مش من نوتات العطر الأصلي أصلًا،
    /// و«ارييچ» كان بدون وصف ولا شريط خالص.
    /// </summary>
    private static void FixInaccurateCopy(IEnumerable<Product> products, ILogger logger)
    {
        foreach (var p in products)
        {
            if (string.Equals(p.NameEn, "Barq", StringComparison.OrdinalIgnoreCase))
            {
                p.Description = "برق هو قراءتنا لروح Bvlgari Le Gemme Tygar — افتتاحية جريب فروت نابضة بالحيوية، "
                    + "وقلب زنجبيل وأمبريت يديه دفء خفيف، وقاعدة أمبروكسان ومسك وفيتيفر تثبت على الملابس حتى اليوم "
                    + "التالي. انتعاش صيفي بجودة نيش حقيقية.";
                p.DescriptionEn = "Barq is our reading of Bvlgari Le Gemme Tygar — a vibrant grapefruit opening, a heart "
                    + "of ginger and ambrette for gentle warmth, and an ambroxan-musk-vetiver base that lasts on fabric "
                    + "into the next day. Summer radiance with true niche quality.";
                p.TickerJson = Ticker(
                    ("star", "مستوحى من Bvlgari Tygar", "Inspired by Bvlgari Tygar"),
                    ("fire", "الأعلى مبيعًا في رمال", "Remal best seller"),
                    ("check", "جريب فروت وزنجبيل منعش", "Zesty grapefruit & ginger"),
                    ("lab", "تركيز Extrait de Parfum", "Extrait de Parfum strength"),
                    ("fast", "ثبات يتجاوز ١٠ ساعات", "Lasts 10+ hours"),
                    ("shipping", "شحن سريع لكل المحافظات", "Fast nationwide delivery"));
                logger.LogInformation("CatalogSeed: اتصحح وصف «برق» (كان بيذكر مانجو ونيرولي مش من نوتاته).");
            }
            else if (string.Equals(p.NameEn, "Areej", StringComparison.OrdinalIgnoreCase)
                     && string.IsNullOrWhiteSpace(p.Description))
            {
                p.Description = "ارييچ مستوحى من Xerjoff Accento — أناناس لامع مع الصفير يفتح العطر بحيوية، ثم قلب "
                    + "من الإيريس البودري والياسمين والفلفل الوردي يديه رقيًّا هادئًا، وقاعدة باتشولي وفيتيفر ومسك "
                    + "تخليه أنيق وقريب. عطر نهاري راقٍ للجنسين.";
                p.DescriptionEn = "Areej is inspired by Xerjoff Accento — bright pineapple and hyacinth open with energy, "
                    + "a heart of powdery iris, jasmine and pink pepper brings quiet refinement, and a base of patchouli, "
                    + "vetiver and musk keeps it elegant and close. A polished daytime unisex signature.";
                p.TickerJson = Ticker(
                    ("star", "مستوحى من Xerjoff Accento", "Inspired by Xerjoff Accento"),
                    ("check", "أناناس وإيريس وياسمين", "Pineapple, iris & jasmine"),
                    ("lab", "تركيز Extrait de Parfum", "Extrait de Parfum strength"),
                    ("fire", "أناقة نهارية للجنسين", "Refined unisex daywear"),
                    ("fast", "ثبات من ٨ لـ ١٠ ساعات", "8 – 10 hours of longevity"),
                    ("shipping", "شحن سريع لكل المحافظات", "Fast nationwide delivery"));
                logger.LogInformation("CatalogSeed: اتملا وصف «ارييچ» (كان فاضي تمامًا).");
            }
        }
    }

    private sealed record Notes(
        string TopAr, string TopEn, string HeartAr, string HeartEn,
        string BaseAr, string BaseEn, string PerfAr, string PerfEn);

    private static async Task<bool> AlreadyDoneAsync(ApplicationDbContext ctx, string flag)
        => await ctx.AppSettings.AsNoTracking().AnyAsync(s => s.Key == flag && s.Value == "done");

    private static void MarkDone(ApplicationDbContext ctx, string flag)
        => ctx.AppSettings.Add(new AppSettingItem
        {
            Key = flag,
            Value = "done",
            DataType = "string",
            Description = "علامة داخلية — تمنع إعادة تنفيذ تحديث الكتالوج عند كل تشغيل"
        });

    // ==================== 1) العطران الجديدان ====================
    private static async Task AddCreationsAsync(ApplicationDbContext ctx, ILogger logger)
    {
        if (await AlreadyDoneAsync(ctx, ProductsFlag)) return;

        // عود أصيل — Creation خاصة برمال (مش محاكاة لأي عطر، فـ InspiredBy فاضية عن قصد)
        var oud = new Product
        {
            Name = "عود أصيل",
            NameEn = "Oud Asel",
            Category = ProductCategory.Unisex,
            Status = ProductStatus.Active,
            ImageUrl = PlaceholderImage,
            Description = "عود أصيل هو التوقيع الخالص لدار رمال — عطر لم يُبنَ على أثر أحد، بل على فكرة واحدة: "
                        + "أن يكون الجلد فاخرًا لا قاسيًا. يفتح بلمسة توت أحمر مخملية تكسر صرامة البداية، ثم ينساب "
                        + "إلى قلب من السوسن البودري والورد الدمشقي يمنحه رقيًّا هادئًا، قبل أن يستقر على قاعدة من "
                        + "الجلد الناعم والعود والتبغ والعنبر تترك أثرًا يُعرَف قبل صاحبه.",
            DescriptionEn = "Oud Asel is Remal's own signature — a fragrance built on a single idea rather than "
                        + "anyone else's trail: that leather can be luxurious without ever turning harsh. It opens on "
                        + "velvety red berries that soften the edge, slips into a powdery heart of orris and damask rose, "
                        + "then settles on supple leather, oud, tobacco and amber — a trail recognised before you are.",
            BadgeArabic = "Creation من رمال",
            BadgeEnglish = "A Remal Creation",
            BadgeKind = "new",
            TickerJson = Ticker(
                ("star", "Creation خاصة بدار رمال", "An exclusive Remal creation"),
                ("lab", "تركيز Extrait de Parfum", "Extrait de Parfum strength"),
                ("check", "جلد فاخر بلمسة توت وسوسن", "Luxurious leather with berries & orris"),
                ("fire", "أثر داكن يُعرَف قبل صاحبه", "A dark trail recognised before you are"),
                ("fast", "ثبات من ١٠ لـ ١٢ ساعة", "10 – 12 hours of longevity"),
                ("shipping", "شحن سريع لكل المحافظات", "Fast nationwide delivery")),
            Sizes = new List<ProductSize>
            {
                new() { Volume = "30ML",  Price = 700m,  Stock = 10 },
                new() { Volume = "50ML",  Price = 750m,  OldPrice = 850m, Stock = 15 },
                new() { Volume = "100ML", Price = 1750m, Stock = 6 },
            }
        };

        // بريستيج — محاكاة رمال لعطر Kilian Angels' Share Paradis
        var prestige = new Product
        {
            Name = "بريستيج",
            NameEn = "Prestige",
            InspiredBy = "كيليان أنجلز شير باراديه",
            InspiredByEn = "Kilian Angels' Share Paradis",
            Category = ProductCategory.Unisex,
            Status = ProductStatus.Active,
            ImageUrl = PlaceholderImage,
            Description = "بريستيج هو قراءتنا لـ Kilian Angels' Share Paradis — النسخة الأغنى والأكثر فخامة من "
                        + "الحكاية الأصلية. يفتح بدفقة كونياك وتوت عليق ناضج تعطيه لمعة فاكهية غير متوقعة، ثم يكشف "
                        + "قلبًا من الورد البلغاري والتونكا مسنودًا بطحلب البلوط، وينتهي على برالين وخشب بلوط وصندل "
                        + "يخلّي الأثر دافي وحلو من غير ما يتقل أبدًا.",
            DescriptionEn = "Prestige is our reading of Kilian Angels' Share Paradis — the richer, more opulent chapter "
                        + "of the original story. It opens on cognac and ripe raspberry for an unexpected fruity gleam, "
                        + "reveals a heart of Bulgarian rose and tonka framed by oakmoss, and closes on praline, oak and "
                        + "sandalwood: warm and sweet, yet never heavy.",
            BadgeArabic = "وصل حديثًا",
            BadgeEnglish = "New Arrival",
            BadgeKind = "new",
            TickerJson = Ticker(
                ("star", "مستوحى من Angels' Share Paradis", "Inspired by Angels' Share Paradis"),
                ("lab", "تركيز Extrait de Parfum", "Extrait de Parfum strength"),
                ("check", "كونياك وتوت وبرالين دافية", "Cognac, berries & warm praline"),
                ("fire", "فوحان انفجاري في أول ساعتين", "Explosive projection for two hours"),
                ("fast", "ثبات من ١٠ لـ ١٢ ساعة", "10 – 12 hours of longevity"),
                ("shipping", "شحن سريع لكل المحافظات", "Fast nationwide delivery")),
            Sizes = new List<ProductSize>
            {
                new() { Volume = "30ML",  Price = 700m,  Stock = 10 },
                new() { Volume = "50ML",  Price = 750m,  OldPrice = 850m, Stock = 15 },
                new() { Volume = "100ML", Price = 1750m, Stock = 6 },
            }
        };

        var added = 0;
        foreach (var p in new[] { oud, prestige })
        {
            // لو الأدمن أضافه بنفسه (بأي حالة، حتى مؤرشف أو محذوف) مش هنضيف نسخة تانية
            var exists = await ctx.Products.IgnoreQueryFilters()
                .AnyAsync(x => x.NameEn == p.NameEn || x.Name == p.Name);
            if (exists) continue;
            ctx.Products.Add(p);
            added++;
        }

        MarkDone(ctx, ProductsFlag);
        await ctx.SaveChangesAsync();
        logger.LogInformation("CatalogSeed: أُضيف {Count} عطر جديد (عود أصيل / بريستيج).", added);
    }

    // ==================== 2) إعادة بناء الباقات ====================
    private static async Task RebuildBundlesAsync(ApplicationDbContext ctx, ILogger logger)
    {
        if (await AlreadyDoneAsync(ctx, BundlesFlag)) return;

        var products = await ctx.Products.Include(p => p.Sizes).ToListAsync();
        Product? By(string nameEn) => products.FirstOrDefault(p =>
            string.Equals(p.NameEn, nameEn, StringComparison.OrdinalIgnoreCase));

        // كل باقة: (اسم عربي، اسم إنجليزي، تاج، وصف، عطور، السعر قبل/بعد، المخزون، تفاصيل الصفحة)
        var specs = new List<BundleSpec>
        {
            new("ثنائي البداية", "The Starter Duo", "الأفضل للبداية", "Best First Step",
                "أول خطوة في عالم رمال: عطران بشخصيتين متقابلتين — واحد منعش لنهارك، وواحد دافئ لليلك.",
                "Your first step into Remal: two opposite characters — one fresh for your day, one warm for your night.",
                new[] { "Areej", "Lahib" }, 1100m, 10,
                "عطران، شخصيتان، سعر واحد.", "Two scents. Two moods. One price.",
                "لو لسه بتتعرّف على الدار، دي أذكى بداية: بدل ما تختار عطر واحد وتفضل تفكر في التاني، تاخد الاتنين بسعر أقل من سعرهم منفصلين — واحد منعش للصبح والشغل، وواحد دافئ حلو للخروجات.",
                "If you're still getting to know the house, this is the smartest way in: instead of choosing one and wondering about the other, take both for less than their separate prices — one fresh for mornings and work, one warm and sweet for going out.",
                "العلبة فيها إيه؟", "What's in the box?",
                "٢ عطر ٥٥ مل بعبواتهم الأصلية + ٢ تيستر ٥ مل هدية من عطور تانية للدار، كله في تغليف رمال الرسمي.",
                "2 × 55 ml in their original bottles + 2 free 5 ml testers of other house scents, in official Remal packaging."),

            new("من الصبح للسهرة", "Day to Night", "الأكثر مبيعًا", "Best Seller",
                "ثلاثة عطور تغطي يومك بالكامل: انتعاش الصبح، وقار الشغل، ودفء السهرة.",
                "Three fragrances that cover your whole day: morning freshness, daytime poise, and evening warmth.",
                new[] { "Barq", "Hayba", "Lahib" }, 1650m, 10,
                "يوم كامل، ثلاث بصمات.", "One full day, three signatures.",
                "أغلب الناس بتحتاج أكتر من عطر واحد: حاجة خفيفة منعشة للصبح، حاجة محترمة تقيلة شوية للشغل والمشاوير، وحاجة دافئة حلوة لليل. الباقة دي بتحل الثلاثة مرة واحدة، وبتوفر لك أكتر من ٣٥٠ جنيه عن شرائهم منفصلين.",
                "Most people need more than one scent: something light for mornings, something composed for work and errands, and something warm for the night. This bundle solves all three at once and saves you over 350 EGP versus buying them separately.",
                "العلبة فيها إيه؟", "What's in the box?",
                "٣ عطور ٥٥ مل بعبواتهم الأصلية + ٢ تيستر ٥ مل هدية، كله في تغليف رمال الرسمي.",
                "3 × 55 ml in their original bottles + 2 free 5 ml testers, in official Remal packaging."),

            new("مجموعة الفخامة", "The Prestige Collection", "الأفخم", "Most Luxurious",
                "أغلى ما في الدار في علبة واحدة: التوقيعان الجديدان مع أيقونة العود والبخور.",
                "The house at its richest: our two newest signatures alongside our oud-and-incense icon.",
                new[] { "Oud Asel", "Prestige", "Lail" }, 1850m, 6,
                "ثلاثة عطور للمساء بلا منافس.", "Three unrivalled evening fragrances.",
                "دي الباقة اللي بتتشال للمناسبات: عود أصيل بجلده الفاخر، بريستيج بحلاوته الدافية، وليل بعوده وبخوره. ثلاثتهم من أعلى تركيز في الدار، وكلهم بيشتغلوا صح في الخريف والشتاء وسهرات المساء. توفير ٤٠٠ جنيه عن السعر المنفصل، وشحن مجاني.",
                "This is the occasion bundle: Oud Asel with its luxurious leather, Prestige with its warm sweetness, and Lail with its oud and incense. All three are our highest concentration, and all three come alive in autumn, winter and evenings. You save 400 EGP versus separate prices — with free shipping.",
                "العلبة فيها إيه؟", "What's in the box?",
                "٣ عطور ٥٥ مل بعبواتهم الأصلية + ٢ تيستر ٥ مل هدية، مع شحن مجاني وتغليف رمال الرسمي.",
                "3 × 55 ml in their original bottles + 2 free 5 ml testers, with free shipping and official Remal packaging."),

            new("خزانة رمال الكاملة", "The Complete Wardrobe", "أعلى توفير", "Biggest Saving",
                "خمسة عطور تغطي كل مناسبة وكل فصل — أكبر توفير في الدار.",
                "Five fragrances covering every season and every occasion — the biggest saving in the house.",
                new[] { "Areej", "Hayba", "Lahib", "Barq", "Lail" }, 2650m, 6,
                "خزانة عطور كاملة في خطوة واحدة.", "A complete fragrance wardrobe in one step.",
                "بدل ما تشتري عطر كل شهر، دي خزانة كاملة مرة واحدة: منعش، فاكهي، حلو دافئ، صيفي، وليلي عودي. توفير ٧٥٠ جنيه — أعلى نسبة خصم في الموقع (٢٢٪) — وشحن مجاني.",
                "Instead of buying one bottle a month, this is an entire wardrobe at once: fresh, fruity, sweet-warm, summery, and dark oud. You save 750 EGP — the biggest discount on the site (22%) — with free shipping.",
                "العلبة فيها إيه؟", "What's in the box?",
                "٥ عطور ٥٥ مل بعبواتهم الأصلية + ٢ تيستر ٥ مل هدية، مع شحن مجاني وتغليف رمال الرسمي.",
                "5 × 55 ml in their original bottles + 2 free 5 ml testers, with free shipping and official Remal packaging."),
        };

        // أرشفة الباقات القديمة (أرشفة مش حذف — الطلبات القديمة لازم تفضل مربوطة بيها)
        var old = await ctx.Bundles.ToListAsync();
        foreach (var b in old) { b.Status = BundleStatus.Archived; b.IsDeleted = true; b.DeletedAt = DateTime.UtcNow; }

        var created = 0;
        foreach (var spec in specs)
        {
            var items = spec.ProductsEn.Select(By).ToList();
            if (items.Any(p => p is null))
            {
                logger.LogWarning("CatalogSeed: الباقة «{Name}» اتخطّت — عطر ناقص من: {Items}",
                    spec.NameAr, string.Join(", ", spec.ProductsEn));
                continue;
            }

            var original = items.Sum(p => Price50(p!));
            var image = items.Select(p => p!.ImageUrl)
                             .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u) && u != PlaceholderImage)
                        ?? items[0]!.ImageUrl;

            ctx.Bundles.Add(new Bundle
            {
                Name = spec.NameAr,
                NameEn = spec.NameEn,
                Tag = spec.TagAr,
                TagEn = spec.TagEn,
                Description = spec.DescAr,
                DescriptionEn = spec.DescEn,
                ImageUrl = image,
                OriginalPrice = original,
                FinalPrice = spec.FinalPrice,
                Stock = spec.Stock,
                Status = BundleStatus.Active,
                BadgeArabic = spec.TagAr,
                BadgeEnglish = spec.TagEn,
                BadgeKind = "sale",
                TickerJson = Ticker(
                    ("star", spec.TagAr, spec.TagEn),
                    ("check", items.Count + " عطور ٥٥ مل", items.Count + " × 55 ml bottles"),
                    ("fire", "وفر " + (int)(original - spec.FinalPrice) + " جنيه", "Save " + (int)(original - spec.FinalPrice) + " EGP"),
                    ("lab", "تركيز Extrait de Parfum", "Extrait de Parfum strength"),
                    ("shipping", "شحن سريع لكل المحافظات", "Fast nationwide delivery")),
                DetailJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    taglineAr = spec.TaglineAr, taglineEn = spec.TaglineEn,
                    whyAr = spec.WhyAr, whyEn = spec.WhyEn,
                    boxTitleAr = spec.BoxTitleAr, boxTitleEn = spec.BoxTitleEn,
                    boxAr = spec.BoxAr, boxEn = spec.BoxEn
                }),
                Items = items.Select((p, i) => new BundleItem { ProductId = p!.Id, Volume = "50ML", Order = i }).ToList()
            });
            created++;
        }

        MarkDone(ctx, BundlesFlag);
        await ctx.SaveChangesAsync();
        logger.LogInformation("CatalogSeed: أُرشفت {Old} باقة قديمة وأُنشئت {New} باقة جديدة.", old.Count, created);
    }

    /// <summary>سعر حجم ٥٥ مل (المخزّن كـ 50ML) — وإلا أرخص حجم متاح.</summary>
    private static decimal Price50(Product p)
        => p.Sizes.FirstOrDefault(s => s.Volume == "50ML")?.Price
           ?? (p.Sizes.Count > 0 ? p.Sizes.Min(s => s.Price) : 0m);

    private static string Ticker(params (string Icon, string Ar, string En)[] items)
        => System.Text.Json.JsonSerializer.Serialize(
            items.Select(i => new { icon = i.Icon, ar = i.Ar, en = i.En }),
            new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

    private sealed record BundleSpec(
        string NameAr, string NameEn, string TagAr, string TagEn,
        string DescAr, string DescEn,
        string[] ProductsEn, decimal FinalPrice, int Stock,
        string TaglineAr, string TaglineEn,
        string WhyAr, string WhyEn,
        string BoxTitleAr, string BoxTitleEn,
        string BoxAr, string BoxEn);
}
