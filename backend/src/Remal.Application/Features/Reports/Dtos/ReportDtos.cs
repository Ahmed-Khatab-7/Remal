namespace Remal.Application.Features.Reports.Dtos;

public record OverviewKpiDto
{
    /// <summary>قيمة المنتجات بعد الخصم — **بدون شحن**.</summary>
    public decimal TotalRevenue { get; init; }

    /// <summary>الشحن المحصّل من العملاء — بند مستقل، مش إيراد مبيعات.</summary>
    public decimal ShippingCollected { get; init; }
    public int DeliveredOrders { get; init; }
    public int PendingOrders { get; init; }
    public int ActiveProducts { get; init; }
    public int LowStockCount { get; init; }
    public int CustomerCount { get; init; }
    public int PendingReviews { get; init; }
    public IReadOnlyList<DailyRevenueDto> RevenueLast14Days { get; init; } = [];
}

public record DailyRevenueDto(DateOnly Date, decimal Revenue, decimal Expenses, int OrderCount);

public record TopProductDto(Guid ProductId, string Name, string? ImageUrl, int QuantitySold, decimal Revenue);

public record TopCustomerDto(string Name, string Phone, int OrderCount, decimal TotalSpent);

public record ReportsResponseDto
{
    public int RangeDays { get; init; }
    /// <summary>قيمة المنتجات بعد الخصم في الفترة — **بدون شحن**.</summary>
    public decimal RevenueInRange { get; init; }

    /// <summary>الشحن المحصّل في نفس الفترة — بند مستقل.</summary>
    public decimal ShippingInRange { get; init; }
    public decimal ExpensesInRange { get; init; }
    public decimal NetInRange { get; init; }
    public decimal AverageOrderValue { get; init; }
    public IReadOnlyList<DailyRevenueDto> Daily { get; init; } = [];
    public IReadOnlyList<TopProductDto> TopProducts { get; init; } = [];
    public IReadOnlyList<TopCustomerDto> TopCustomers { get; init; } = [];
    public IReadOnlyDictionary<string, int> StatusBreakdown { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> PaymentBreakdown { get; init; } = new Dictionary<string, int>();
}
