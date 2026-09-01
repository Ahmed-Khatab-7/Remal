using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Application.Features.Reports.Dtos;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Reports;

public interface IReportService
{
    Task<OverviewKpiDto> GetOverviewAsync(CancellationToken ct = default);
    Task<ReportsResponseDto> GetReportsAsync(int days = 30, CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;
    private const int LowStockThreshold = 10;

    public ReportService(IApplicationDbContext db) => _db = db;

    public async Task<OverviewKpiDto> GetOverviewAsync(CancellationToken ct = default)
    {
        // الإيراد = قيمة المنتجات بعد الخصم فقط. الشحن بيتحصّل ويتسلّم لشركة الشحن،
        // فدخوله في رقم المبيعات كان بيضخّمه ويشوّه متوسط قيمة الطلب وهامش الربح.
        var revenue = await _db.Orders.Where(o => o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)(o.Total - o.ShippingFee), ct) ?? 0;
        var shippingCollected = await _db.Orders.Where(o => o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.ShippingFee, ct) ?? 0;
        var deliveredCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Delivered, ct);
        var pending = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing, ct);
        var activeProducts = await _db.Products.CountAsync(p => p.Status == ProductStatus.Active, ct);
        var lowStock = await _db.Products
            .Include(p => p.Sizes)
            .CountAsync(p => p.Sizes.Sum(s => s.Stock) <= LowStockThreshold && p.Status != ProductStatus.Archived, ct);
        var customers = await _db.Customers.CountAsync(ct);
        var pendingReviews = await _db.Reviews.CountAsync(r => r.Status == ReviewStatus.Pending, ct);

        var startDate = DateTime.UtcNow.Date.AddDays(-13);
        var ordersInRange = await _db.Orders.AsNoTracking()
            .Where(o => o.PlacedAt >= startDate && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.PlacedAt, o.Total, o.ShippingFee }).ToListAsync(ct);
        var expensesInRange = await _db.Expenses.AsNoTracking()
            .Where(e => e.Date >= startDate)
            .Select(e => new { e.Date, e.Amount }).ToListAsync(ct);

        var daily = new List<DailyRevenueDto>();
        for (int i = 0; i < 14; i++)
        {
            var d = DateOnly.FromDateTime(startDate.AddDays(i));
            var rev = ordersInRange.Where(o => DateOnly.FromDateTime(o.PlacedAt) == d).Sum(o => o.Total - o.ShippingFee);
            var exp = expensesInRange.Where(e => DateOnly.FromDateTime(e.Date) == d).Sum(e => e.Amount);
            var cnt = ordersInRange.Count(o => DateOnly.FromDateTime(o.PlacedAt) == d);
            daily.Add(new DailyRevenueDto(d, rev, exp, cnt));
        }

        return new OverviewKpiDto
        {
            TotalRevenue = revenue,
            ShippingCollected = shippingCollected,
            DeliveredOrders = deliveredCount,
            PendingOrders = pending,
            ActiveProducts = activeProducts,
            LowStockCount = lowStock,
            CustomerCount = customers,
            PendingReviews = pendingReviews,
            RevenueLast14Days = daily,
        };
    }

    public async Task<ReportsResponseDto> GetReportsAsync(int days = 30, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.PlacedAt >= cutoff).ToListAsync(ct);
        var expenses = await _db.Expenses.AsNoTracking().Where(e => e.Date >= cutoff).ToListAsync(ct);

        var validOrders = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        // قيمة المنتجات بعد الخصم — بدون شحن (الشحن بند مستقل تحت).
        var revenue = validOrders.Sum(o => o.Total - o.ShippingFee);
        var shippingInRange = validOrders.Sum(o => o.ShippingFee);
        var expensesTotal = expenses.Sum(e => e.Amount);
        // متوسط قيمة الطلب لازم يكون على قيمة المنتجات — الشحن ثابت ومش بيعبّر عن
        // سلوك الشراء، ووجوده كان بيرفع المتوسط بشكل مضلّل في الطلبات الصغيرة.
        var avgOrder = validOrders.Count > 0 ? revenue / validOrders.Count : 0;

        var daily = new List<DailyRevenueDto>();
        for (int i = days - 1; i >= 0; i--)
        {
            var d = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-i));
            var rev = validOrders.Where(o => DateOnly.FromDateTime(o.PlacedAt) == d).Sum(o => o.Total - o.ShippingFee);
            var exp = expenses.Where(e => DateOnly.FromDateTime(e.Date) == d).Sum(e => e.Amount);
            var cnt = validOrders.Count(o => DateOnly.FromDateTime(o.PlacedAt) == d);
            daily.Add(new DailyRevenueDto(d, rev, exp, cnt));
        }

        var topProducts = validOrders.SelectMany(o => o.Items.Where(i => i.ProductId.HasValue)
            .Select(i => new { i.ProductId, i.ItemName, Qty = i.Quantity, Rev = i.UnitPrice * i.Quantity }))
            .GroupBy(x => x.ProductId!.Value)
            .Select(g => new TopProductDto(g.Key, g.First().ItemName, null, g.Sum(x => x.Qty), g.Sum(x => x.Rev)))
            .OrderByDescending(x => x.Revenue).Take(5).ToList();

        var topCustomers = validOrders.GroupBy(o => o.CustomerPhone)
            // قيمة العميل تتقاس بقيمة المنتجات اللي اشتراها — الشحن مش جزء من قيمته.
            .Select(g => new TopCustomerDto(g.First().CustomerName, g.Key, g.Count(), g.Sum(o => o.Total - o.ShippingFee)))
            .OrderByDescending(x => x.TotalSpent).Take(5).ToList();

        var statusBreakdown = orders.GroupBy(o => o.Status.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var paymentBreakdown = validOrders.GroupBy(o => o.PaymentMethod.ToString()).ToDictionary(g => g.Key, g => g.Count());

        return new ReportsResponseDto
        {
            RangeDays = days,
            RevenueInRange = revenue,
            ShippingInRange = shippingInRange,
            ExpensesInRange = expensesTotal,
            // الشحن المحصّل بيدخل الصافي لأن تكلفة المندوب متسجّلة في المصروفات —
            // من غيره الصافي بيطلع أقل من الحقيقة بقيمة الشحن كله.
            NetInRange = revenue + shippingInRange - expensesTotal,
            AverageOrderValue = avgOrder,
            Daily = daily,
            TopProducts = topProducts,
            TopCustomers = topCustomers,
            StatusBreakdown = statusBreakdown,
            PaymentBreakdown = paymentBreakdown,
        };
    }
}
