using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Orders.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Orders;

public interface IOrderService
{
    Task<PagedResult<OrderListDto>> GetListAsync(OrderFilterDto filter, CancellationToken ct = default);
    Task<OrderDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderDetailDto> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<OrderDetailDto> CreateAsync(OrderCreateDto dto, CancellationToken ct = default);
    Task<OrderDetailDto> UpdateStatusAsync(Guid id, OrderStatusUpdateDto dto, CancellationToken ct = default);
    Task<OrderTrackingDto> TrackAsync(string code, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IDashboardNotifier _notifier;
    private readonly IPushService _push;
    private readonly IMetaConversionsApi _meta;
    private readonly IEmailService? _email;
    private readonly ITelegramNotifier? _telegram;

    public OrderService(IApplicationDbContext db, IAuditService audit, IDashboardNotifier notifier,
        IPushService push, IMetaConversionsApi? meta = null, IEmailService? email = null,
        ITelegramNotifier? telegram = null)
    {
        _db = db;
        _audit = audit;
        _notifier = notifier;
        _push = push;
        // اختياريان عن قصد: الاختبارات بتبني الخدمة من غيرهم، ولا التتبع ولا البريد
        // جزء من منطق الطلب نفسه.
        _meta = meta ?? NullMetaConversionsApi.Instance;
        _email = email;
        _telegram = telegram;
    }

    public async Task<PagedResult<OrderListDto>> GetListAsync(OrderFilterDto filter, CancellationToken ct = default)
    {
        var q = _db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
            q = q.Where(o => EF.Functions.Like(o.Code, $"%{filter.Search}%")
                || EF.Functions.Like(o.CustomerName, $"%{filter.Search}%")
                || EF.Functions.Like(o.CustomerPhone, $"%{filter.Search}%"));
        if (filter.Status.HasValue) q = q.Where(o => o.Status == filter.Status);
        if (filter.PaymentMethod.HasValue) q = q.Where(o => o.PaymentMethod == filter.PaymentMethod);
        if (filter.FromDate.HasValue) q = q.Where(o => o.PlacedAt >= filter.FromDate);
        if (filter.ToDate.HasValue) q = q.Where(o => o.PlacedAt <= filter.ToDate);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.PlacedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(o => new OrderListDto
            {
                Id = o.Id, Code = o.Code, CustomerName = o.CustomerName, CustomerPhone = o.CustomerPhone,
                Status = o.Status, PaymentMethod = o.PaymentMethod, PaymentStatus = o.PaymentStatus,
                Total = o.Total, ItemCount = o.Items.Sum(i => i.Quantity), PlacedAt = o.PlacedAt,
            }).ToListAsync(ct);
        return PagedResult<OrderListDto>.Create(items, total, filter.Page, filter.PageSize);
    }

    public async Task<OrderDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order", id);
        return MapDetail(o);
    }

    public async Task<OrderDetailDto> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var o = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Code == code, ct)
            ?? throw new NotFoundException($"الطلب '{code}' غير موجود");
        return MapDetail(o);
    }

    public async Task<OrderDetailDto> CreateAsync(OrderCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Items.Count == 0) throw new BadRequestException("لازم تضيف منتجات للطلب");

        var order = new Order
        {
            Code = await GenerateCodeAsync(ct),
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            CustomerAddress = dto.CustomerAddress,
            CustomerEmail = dto.CustomerEmail,
            City = dto.City,
            Notes = dto.Notes,
            PaymentMethod = dto.PaymentMethod,
            GiftWrap = dto.GiftWrap,
            Status = OrderStatus.Pending,
        };

        decimal subtotal = 0;
        foreach (var line in dto.Items)
        {
            decimal unitPrice;
            string itemName;
            string? volume = line.Volume;

            if (line.ProductId.HasValue)
            {
                var product = await _db.Products
                    .Include(p => p.Sizes)
                    .FirstOrDefaultAsync(p => p.Id == line.ProductId, ct)
                    ?? throw new NotFoundException("Product", line.ProductId);

                var size = product.Sizes.FirstOrDefault(s => s.Volume == (line.Volume ?? "50ML"))
                    ?? product.Sizes.First();
                if (size.Stock < line.Quantity) throw new BadRequestException($"المخزون غير كافٍ لـ {product.Name} ({size.Volume})");

                size.Stock -= line.Quantity;
                product.Sold += line.Quantity;
                unitPrice = size.Price;
                itemName = product.Name;
                volume = size.Volume;
                if (product.Sizes.Sum(s => s.Stock) == 0 && product.Status == ProductStatus.Active)
                    product.Status = ProductStatus.OutOfStock;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ItemName = itemName,
                    Volume = volume,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                });
            }
            else if (line.BundleId.HasValue)
            {
                var bundle = await _db.Bundles.FirstOrDefaultAsync(b => b.Id == line.BundleId, ct)
                    ?? throw new NotFoundException("Bundle", line.BundleId);
                if (bundle.Stock < line.Quantity) throw new BadRequestException($"الباقة {bundle.Name} غير كافية في المخزون");
                bundle.Stock -= line.Quantity;
                unitPrice = bundle.FinalPrice;
                itemName = bundle.Name;
                order.Items.Add(new OrderItem
                {
                    BundleId = bundle.Id, ItemName = itemName, Volume = "Bundle",
                    Quantity = line.Quantity, UnitPrice = unitPrice,
                });
            }
            else if (line.CollectionId.HasValue)
            {
                var coll = await _db.Collections.FirstOrDefaultAsync(c => c.Id == line.CollectionId, ct)
                    ?? throw new NotFoundException("Collection", line.CollectionId);
                if (coll.Stock < line.Quantity) throw new BadRequestException($"المجموعة {coll.Name} غير كافية في المخزون");
                coll.Stock -= line.Quantity;
                unitPrice = coll.FinalPrice;
                itemName = coll.Name;
                order.Items.Add(new OrderItem
                {
                    CollectionId = coll.Id, ItemName = itemName, Volume = "Collection",
                    Quantity = line.Quantity, UnitPrice = unitPrice,
                });
            }
            else
            {
                throw new BadRequestException("يجب تحديد نوع العنصر (منتج/باقة/مجموعة)");
            }

            subtotal += unitPrice * line.Quantity;
        }

        // Coupon
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(dto.CouponCode))
        {
            var code = dto.CouponCode!.Trim().ToUpperInvariant();
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
            if (coupon != null && coupon.IsUsable && subtotal >= coupon.MinOrderAmount)
            {
                discount = coupon.Type == CouponType.Percent
                    ? Math.Round(subtotal * (coupon.Value / 100m), 2)
                    : Math.Min(coupon.Value, subtotal);
                coupon.Uses++;
                order.CouponCode = coupon.Code;
            }
        }

        // ===== Automatic promotions (no coupon code needed) =====
        // Evaluated against the order's product lines. Adds free gift lines (price 0) and/or
        // percentage discounts. Reward stock is decremented and capped by availability.
        var nowUtc = DateTime.UtcNow;
        var activePromos = await _db.Promotions
            .Where(p => p.IsActive && (p.ExpiresAt == null || p.ExpiresAt > nowUtc))
            .OrderByDescending(p => p.Priority).ToListAsync(ct);

        // Snapshot product lines BEFORE we append any gift lines (so a gift never re-triggers a promo).
        var productLines = order.Items.Where(i => i.ProductId.HasValue).ToList();
        int TriggerQty(Domain.Entities.Promotion promo)
        {
            if (promo.TriggerProductId.HasValue)
                return productLines.Where(i => i.ProductId == promo.TriggerProductId
                        && (promo.TriggerVolume == null || i.Volume == promo.TriggerVolume))
                    .Sum(i => i.Quantity);
            if (!string.IsNullOrEmpty(promo.TriggerVolume))
                return productLines.Where(i => i.Volume == promo.TriggerVolume).Sum(i => i.Quantity);
            return productLines.Sum(i => i.Quantity);
        }
        async Task AddGiftAsync(Domain.Entities.Promotion promo, int giftQty)
        {
            if (giftQty <= 0 || !promo.RewardProductId.HasValue) return;
            var rp = await _db.Products.Include(p => p.Sizes).FirstOrDefaultAsync(p => p.Id == promo.RewardProductId, ct);
            if (rp == null) return;
            var rvol = promo.RewardVolume ?? "30ML";
            var rsize = rp.Sizes.FirstOrDefault(s => s.Volume == rvol) ?? rp.Sizes.FirstOrDefault();
            if (rsize != null) giftQty = Math.Min(giftQty, rsize.Stock);
            if (giftQty <= 0) return;
            if (rsize != null) { rsize.Stock -= giftQty; rp.Sold += giftQty; }
            order.Items.Add(new OrderItem
            {
                ProductId = rp.Id,
                ItemName = rp.Name + " (هدية 🎁)",
                Volume = rsize?.Volume ?? rvol,
                Quantity = giftQty,
                UnitPrice = 0m,
            });
        }
        var appliedPromoNames = new List<string>();

        // ===== D4 — العروض بالنسبة المئوية لا تتراكم =====
        // لو أكثر من عرض نسبة مئوية فعّال في نفس الوقت (OrderPercentOver و/أو BuyXGetPercentOff)
        // نحسب قيمة الخصم الفعلية لكل واحد بعد تحقّق شروطه، ونطبّق العرض الأعلى قيمةً فقط
        // (مقارنة بالمبلغ الفعلي وليس بالنسبة). الكوبون يظل يتراكم فوق العرض المختار (discount محسوب فوق).
        decimal bestPercentDiscount = 0m;
        string? bestPercentName = null;
        foreach (var promo in activePromos)
        {
            decimal candidate = promo.Type switch
            {
                PromotionType.OrderPercentOver when subtotal >= promo.MinSpend && promo.RewardPercentOff > 0
                    => Math.Round(subtotal * (promo.RewardPercentOff / 100m), 2),
                PromotionType.BuyXGetPercentOff when promo.BuyQuantity > 0 && TriggerQty(promo) >= promo.BuyQuantity && promo.RewardPercentOff > 0
                    => Math.Round(subtotal * (promo.RewardPercentOff / 100m), 2),
                _ => 0m,
            };
            if (candidate > bestPercentDiscount)
            {
                bestPercentDiscount = candidate;
                bestPercentName = promo.NameAr;
            }
        }
        if (bestPercentDiscount > 0)
        {
            discount += bestPercentDiscount;
            if (bestPercentName != null) appliedPromoNames.Add(bestPercentName);
        }

        // عروض الهدايا المجانية تُطبَّق باستقلالية (تضيف سطور مجانية، لا تخصم نسبة) فلا تدخل في مقارنة الأعلى.
        foreach (var promo in activePromos)
        {
            switch (promo.Type)
            {
                case PromotionType.BuyXGetYFree:
                    if (promo.BuyQuantity > 0)
                    {
                        var sets = TriggerQty(promo) / promo.BuyQuantity;
                        if (sets > 0) { await AddGiftAsync(promo, sets * Math.Max(1, promo.RewardQuantity)); appliedPromoNames.Add(promo.NameAr); }
                    }
                    break;
                case PromotionType.FreeGiftOverAmount:
                    if (subtotal >= promo.MinSpend)
                    { await AddGiftAsync(promo, Math.Max(1, promo.RewardQuantity)); appliedPromoNames.Add(promo.NameAr); }
                    break;
            }
        }
        if (appliedPromoNames.Count > 0)
        {
            var note = "عروض مطبّقة: " + string.Join("، ", appliedPromoNames.Distinct());
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? note : (order.Notes + " | " + note);
        }

        // Never let discount exceed the subtotal.
        discount = Math.Min(discount, subtotal);

        // Shipping — read fee + free-shipping threshold from AppSettings (admin-configurable).
        var settings = await _db.AppSettings.AsNoTracking().ToListAsync(ct);
        decimal SettingDec(string key, decimal fallback)
            => decimal.TryParse(settings.FirstOrDefault(s => s.Key == key)?.Value, out var v) ? v : fallback;
        var shippingFee = SettingDec("shipping_fee", 60m);
        var freeThreshold = SettingDec("free_shipping_threshold", 2000m);
        // تكلفة الشحن حسب المحافظة/المدينة — المنطق كله في ShippingRates (يدعم الصيغة الجديدة
        // بالمحافظات والمدن، والصيغة القديمة المسطّحة). الواجهة تبعت City بصيغة
        // "المدينة — المحافظة" أو "المحافظة" وحدها لو مالهاش مدن.
        // ملاحظة: السعر النهائي بيتحسب هنا في السيرفر دائمًا — الواجهة تعرض تقديرًا فقط،
        // فلا يمكن لعميل التلاعب بقيمة الشحن من المتصفح.
        var ratesJson = settings.FirstOrDefault(s => s.Key == "shipping_rates_json")?.Value;
        shippingFee = Common.Shipping.ShippingRates.Resolve(ratesJson, dto.City, shippingFee);
        var shipping = (freeThreshold > 0 && subtotal >= freeThreshold) ? 0m : shippingFee;
        // ملاحظة: ميزة "تغليف الهدية" أُزيلت من الواجهة بالكامل، فلا نضيف أي رسوم عليها
        // (الحقل يبقى في الـ DTO للتوافق الخلفي لكن بدون أي أثر على السعر).

        order.Subtotal = subtotal;
        order.DiscountAmount = discount;
        order.ShippingFee = shipping;
        order.Total = subtotal + shipping - discount;

        // Upsert customer
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == dto.CustomerPhone, ct);
        if (customer == null)
        {
            customer = new Customer { Name = dto.CustomerName, Phone = dto.CustomerPhone, Email = dto.CustomerEmail, Address = dto.CustomerAddress, City = dto.City };
            _db.Customers.Add(customer);
        }
        customer.OrderCount++;
        customer.TotalSpent += order.Total;
        order.CustomerId = customer.Id;

        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // طلب متزامن آخر عدّل مخزون نفس القطعة بين القراءة والحفظ →
            // نرفض بدل البيع الزائد، والعميل يعيد المحاولة بالمخزون المحدّث.
            throw new BadRequestException("المخزون اتغيّر دلوقتي — حدّث السلة وحاول تاني");
        }

        await _audit.LogAsync(AuditCategory.Order, "CREATE_ORDER",
            $"طلب جديد {order.Code} ({order.CustomerName}) — {order.Total:N0} ج.م",
            entityName: nameof(Order), entityId: order.Id.ToString(),
            after: new { order.Code, order.Total, order.PaymentMethod, ItemCount = order.Items.Count }, ct: ct);

        // Realtime: notify the dashboard of the new order
        await _notifier.NewOrderAsync(new NewOrderNotification(
            order.Id, order.Code, order.CustomerName, order.Total,
            order.Items.Sum(i => i.Quantity), order.PaymentMethod.ToString(), DateTime.UtcNow), ct);

        // Web Push: fan out to every subscribed admin/partner browser (works when dashboard is closed)
        await _push.SendToAllAsync(
            "طلب جديد — رمال 🛍",
            $"طلب جديد من {order.CustomerName} بقيمة {order.Total:N0} ج.م",
            "/remal-dashboard.html",
            ct);

        // تليجرام: الطبقة الأضمن على الآيفون. Web Push فوق بيتطلب الداشبورد يكون
        // متثبّت كتطبيق على الشاشة الرئيسية، والاشتراك بيتلغى بصمت لو اتشال —
        // فتليجرام هو اللي بيضمن إن الطلب ما يفوتش.
        await SendTelegramNewOrderAsync(order, ct);

        // Realtime: any product size that dropped below 5 after this sale
        foreach (var line in dto.Items.Where(l => l.ProductId.HasValue))
        {
            var product = await _db.Products.AsNoTracking().Include(p => p.Sizes)
                .FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            var size = product?.Sizes.FirstOrDefault(s => s.Volume == (line.Volume ?? "50ML"));
            if (size != null && size.Stock < 5)
                await _notifier.LowStockAsync(new LowStockNotification(
                    product!.Id, product.Name, size.Volume, size.Stock), ct);
        }

        await SendOrderConfirmationEmailAsync(order, ct);
        await SendPurchaseToMetaAsync(order, dto, ct);

        return await GetByIdAsync(order.Id, ct);
    }

    /// <summary>
    /// إيميل تأكيد الطلب للعميل. البريد اختياري في صفحة الدفع، فلو العميل ما كتبوش
    /// بنتخطى بهدوء. وأي فشل في الإرسال (سيرفر بريد واقع مثلاً) **ما ينفعش** يفشّل
    /// الطلب نفسه — الطلب اتسجّل بالفعل والعميل شايف رقمه على الشاشة.
    /// </summary>
    private async Task SendOrderConfirmationEmailAsync(Order order, CancellationToken ct)
    {
        if (_email is null || string.IsNullOrWhiteSpace(order.CustomerEmail)) return;
        try
        {
            // الفاتورة كانت بتتبعت بالرقم والإجمالي بس — العميل بياخد رقم من غير
            // ما يعرف اشترى إيه، وده بيولّد رسايل "الطلب ده كان إيه؟" على الواتساب.
            var summary = new OrderEmailSummary(
                OrderCode: order.Code,
                Lines: order.Items
                    .Select(i => new OrderEmailLine(i.ItemName, i.Volume, i.Quantity, i.UnitPrice))
                    .ToList(),
                Subtotal: order.Subtotal,
                ShippingFee: order.ShippingFee,
                Discount: order.DiscountAmount,
                Total: order.Total,
                PaymentMethod: PaymentMethodLabel(order.PaymentMethod),
                ShippingAddress: order.CustomerAddress,
                City: order.City);

            await _email.SendOrderConfirmationAsync(
                order.CustomerEmail, order.CustomerName, summary, ct);
        }
        catch { /* الطلب أهم من الإيميل */ }
    }

    /// <summary>
    /// إشعار تليجرام بطلب جديد. كل الأخطاء مبتلعة — الطلب اتسجّل بالفعل والعميل
    /// شايف رقمه، فما ينفعش إشعار فاشل يرمي استثناء ويخلي العميل يفتكر إن الطلب
    /// ما اتمش.
    /// </summary>
    private async Task SendTelegramNewOrderAsync(Order order, CancellationToken ct)
    {
        if (_telegram is null) return;
        try
        {
            var lines = string.Join("\n", order.Items.Select(i =>
                $"• {Esc(i.ItemName)}"
                + (string.IsNullOrWhiteSpace(i.Volume) ? "" : $" — {Esc(i.Volume)}")
                + $" × {i.Quantity}"));

            var text =
                $"🛍 <b>طلب جديد</b>\n" +
                $"<code>{Esc(order.Code)}</code>\n\n" +
                $"{lines}\n\n" +
                $"👤 {Esc(order.CustomerName)}\n" +
                $"📱 <code>{Esc(order.CustomerPhone)}</code>\n" +
                $"📍 {Esc(order.City ?? "—")}\n" +
                $"💳 {Esc(PaymentMethodLabel(order.PaymentMethod))}\n\n" +
                $"المنتجات: <b>{order.Subtotal:N0}</b> ج.م\n" +
                (order.DiscountAmount > 0 ? $"الخصم: −{order.DiscountAmount:N0} ج.م\n" : "") +
                $"الشحن: {(order.ShippingFee <= 0 ? "مجاني" : $"{order.ShippingFee:N0} ج.م")}\n" +
                $"<b>الإجمالي: {order.Total:N0} ج.م</b>";

            await _telegram.SendAsync(text, ct);
        }
        catch { /* الإشعار ما ينفعش يكسر الطلب */ }
    }

    /// <summary>تهريب رموز HTML الثلاثة اللي تليجرام بيرفضها في parse_mode=HTML.</summary>
    private static string Esc(string? s) => (s ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>اسم طريقة الدفع بالعربي للفاتورة — الاسم البرمجي مش مفهوم للعميل.</summary>
    private static string PaymentMethodLabel(PaymentMethod m) => m switch
    {
        PaymentMethod.CashOnDelivery => "الدفع عند الاستلام",
        PaymentMethod.InstaPay => "إنستا باي",
        PaymentMethod.Wallet => "محفظة إلكترونية",
        _ => m.ToString(),
    };

    /// <summary>
    /// حدث Purchase من السيرفر لـ Meta (Conversions API). بيتبعت بنفس الـ event_id
    /// اللي البيكسل استخدمه في المتصفح، فـ Meta بتدمج النسختين في حدث واحد بدل ما
    /// تحسب الشراء مرتين. أي فشل هنا بيتبلع تمامًا — التتبع ما ينفعش يكسر طلب حقيقي.
    /// </summary>
    private async Task SendPurchaseToMetaAsync(Order order, OrderCreateDto dto, CancellationToken ct)
    {
        try
        {
            if (!_meta.IsConfigured) return;

            var contents = order.Items
                .Select(i => new MetaEventContent(
                    (i.ProductId ?? i.BundleId ?? i.CollectionId ?? Guid.Empty).ToString(),
                    i.Quantity, i.UnitPrice))
                .ToList();

            await _meta.SendAsync(new MetaEvent(
                EventName: "Purchase",
                EventId: dto.EventId,
                Value: order.Total,
                Currency: "EGP",
                OrderId: order.Code,
                Contents: contents,
                Email: order.CustomerEmail,
                Phone: order.CustomerPhone,
                FullName: order.CustomerName,
                City: order.City,
                SourceUrl: dto.SourceUrl,
                Fbp: dto.Fbp,
                Fbc: dto.Fbc,
                // معرّف ثابت للعميل عبر الطلبات — الموبايل هو المفتاح الوحيد المضمون
                // وجوده لكل طلب (فيه طلبات كتير بدون بريد ولا حساب).
                ExternalId: order.CustomerId?.ToString() ?? order.CustomerPhone,
                ContentName: order.Items.FirstOrDefault()?.ItemName), ct);
        }
        catch { /* التتبع ما بيعطّلش الطلب أبدًا */ }
    }

    public async Task<OrderDetailDto> UpdateStatusAsync(Guid id, OrderStatusUpdateDto dto, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Order", id);

        var oldStatus = order.Status;
        order.Status = dto.NewStatus;

        switch (dto.NewStatus)
        {
            case OrderStatus.Preparing: order.PreparedAt = DateTime.UtcNow; break;
            case OrderStatus.Shipping: order.ShippedAt = DateTime.UtcNow; break;
            case OrderStatus.Delivered: order.DeliveredAt = DateTime.UtcNow; break;
            case OrderStatus.Cancelled: order.CancelledAt = DateTime.UtcNow; break;
        }

        if (!string.IsNullOrWhiteSpace(dto.Note))
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? dto.Note : $"{order.Notes}\n— {dto.Note}";

        // ===== D1 — منح نقاط الولاء عند التسليم (ربط برقم الهاتف) =====
        // عند تحوّل الطلب لأول مرة إلى Delivered نبحث عن مستخدم مسجّل بنفس رقم هاتف الطلب.
        // لو لقيناه: نمنحه نقاطًا بمعدل نقطة واحدة لكل 10 ج.م من الـ Subtotal (Earn) — بنفس منطق AwardPointsCommand.
        // لو الطلب لعميل زائر بدون حساب مطابق → لا نقاط، ولا نعطّل تحديث الحالة (بدون خطأ).
        // idempotent: order.PointsAwarded يمنع منح النقاط مرتين لو رجعت الحالة لـ Delivered تاني.
        // ملاحظة: الربط بالهاتف يعني إن رقمًا مشتركًا بين أكثر من حساب سيمنح النقاط لأول حساب مطابق فقط.
        if (dto.NewStatus == OrderStatus.Delivered && !order.PointsAwarded)
        {
            var pts = (int)Math.Floor(order.Subtotal / 10m);
            if (pts > 0 && !string.IsNullOrWhiteSpace(order.CustomerPhone))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == order.CustomerPhone, ct);
                if (user != null)
                {
                    var acct = await _db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id, ct);
                    if (acct is null)
                    {
                        acct = new LoyaltyAccount { UserId = user.Id, Balance = 0, LifetimeEarned = 0, LifetimeSpent = 0 };
                        _db.LoyaltyAccounts.Add(acct);
                    }
                    acct.Balance += pts;
                    acct.LifetimeEarned += pts;
                    _db.PointsTransactions.Add(new PointsTransaction
                    {
                        LoyaltyAccount = acct,
                        Type = PointsTransactionType.Earn,
                        Points = pts,
                        Description = $"نقاط شراء — طلب {order.Code}",
                        OrderId = order.Id,
                    });
                    order.PointsAwarded = true;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Order, "UPDATE_ORDER_STATUS",
            $"الطلب {order.Code}: {oldStatus} ← {dto.NewStatus}",
            entityName: nameof(Order), entityId: order.Id.ToString(),
            before: oldStatus, after: dto.NewStatus, ct: ct);

        // Realtime: notify the dashboard of the status change
        await _notifier.OrderUpdatedAsync(new OrderUpdatedNotification(
            order.Id, order.Code, dto.NewStatus.ToString(), oldStatus.ToString(), DateTime.UtcNow), ct);

        return await GetByIdAsync(order.Id, ct);
    }

    public async Task<OrderTrackingDto> TrackAsync(string code, CancellationToken ct = default)
    {
        var o = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct)
            ?? throw new NotFoundException($"الطلب '{code}' غير موجود");
        return new OrderTrackingDto(o.Code, o.Status, o.PlacedAt, o.PreparedAt, o.ShippedAt, o.DeliveredAt);
    }

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        // RML-XXXXXX-LL  (6 أرقام + حرفين) → فضاء ~608 مليون احتمال.
        // الـ endpoint العام orders/by-code يكشف بيانات العميل، فرفعنا العشوائية
        // لمنع التخمين المتسلسل (brute-force) لأكواد طلبات الآخرين.
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // بدون I/O لتفادي الالتباس
        for (var i = 0; i < 6; i++)
        {
            var n = Random.Shared.Next(100000, 1000000);
            var suffix = new string(new[] { letters[Random.Shared.Next(letters.Length)], letters[Random.Shared.Next(letters.Length)] });
            var code = $"RML-{n}-{suffix}";
            if (!await _db.Orders.AnyAsync(o => o.Code == code, ct)) return code;
        }
        return $"RML-{DateTime.UtcNow.Ticks}";
    }

    private static OrderDetailDto MapDetail(Order o) => new()
    {
        Id = o.Id, Code = o.Code, CustomerName = o.CustomerName, CustomerPhone = o.CustomerPhone,
        CustomerEmail = o.CustomerEmail, CustomerAddress = o.CustomerAddress, City = o.City,
        Status = o.Status, PaymentMethod = o.PaymentMethod, PaymentStatus = o.PaymentStatus,
        PaymentReference = o.PaymentReference, Notes = o.Notes,
        Subtotal = o.Subtotal, ShippingFee = o.ShippingFee, DiscountAmount = o.DiscountAmount,
        Total = o.Total, CouponCode = o.CouponCode, GiftWrap = o.GiftWrap,
        PlacedAt = o.PlacedAt, PreparedAt = o.PreparedAt, ShippedAt = o.ShippedAt,
        DeliveredAt = o.DeliveredAt, CancelledAt = o.CancelledAt,
        ItemCount = o.Items.Sum(i => i.Quantity),
        Items = o.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.BundleId, i.CollectionId,
            i.ItemName, i.Volume, i.Quantity, i.UnitPrice, i.UnitPrice * i.Quantity, i.Product?.ImageUrl)).ToList(),
    };
}
