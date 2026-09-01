using Remal.Domain.Enums;

namespace Remal.Application.Features.Accounting.Dtos;

public record ExpenseDto
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    /// <summary>NULL means paid from project revenue (no partner contributed personally).</summary>
    public string? PaidById { get; init; }
    /// <summary>Resolved label: partner's name OR "إدارة رمال (من الإيرادات)" when project-paid.</summary>
    public string PaidByName { get; init; } = null!;
    /// <summary>True if expense was paid from project cash, not a partner's pocket.</summary>
    public bool IsProjectPaid { get; init; }
    public decimal Amount { get; init; }
    public ExpenseCategory Category { get; init; }
    public string Description { get; init; } = null!;
    public string? ReceiptUrl { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ExpenseWriteDto
{
    public DateTime Date { get; init; } = DateTime.UtcNow;
    /// <summary>NULL/empty = paid from project revenue. Otherwise the partner user-id.</summary>
    public string? PaidById { get; init; }
    public decimal Amount { get; init; }
    public ExpenseCategory Category { get; init; } = ExpenseCategory.Other;
    public string Description { get; init; } = null!;
    public string? ReceiptUrl { get; init; }
    public string? Notes { get; init; }
}

public record SettlementDto
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string FromUserId { get; init; } = null!;
    public string FromUserName { get; init; } = null!;
    public string ToUserId { get; init; } = null!;
    public string ToUserName { get; init; } = null!;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public record SettlementWriteDto
{
    public DateTime Date { get; init; } = DateTime.UtcNow;
    public string FromUserId { get; init; } = null!;
    public string ToUserId { get; init; } = null!;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public record PartnerBalanceDto
{
    public string UserId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? AvatarInitials { get; init; }
    public decimal PaidExpenses { get; init; }
    public decimal SettlementsPaid { get; init; }
    public decimal SettlementsReceived { get; init; }
    /// <summary>Total drawings this partner has withdrawn from the project (reduces their balance).</summary>
    public decimal Withdrawals { get; init; }
    public decimal NetContribution { get; init; }
    public decimal Balance { get; init; }   // positive: overpaid (owed to him); negative: owes
    /// <summary>This partner's share of net profit (after expenses + withdrawals).</summary>
    public decimal ProfitShare { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Email { get; init; }
}

public record PartnerWithdrawalDto
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string PartnerId { get; init; } = null!;
    public string PartnerName { get; init; } = null!;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public record PartnerWithdrawalWriteDto
{
    public DateTime Date { get; init; } = DateTime.UtcNow;
    public string PartnerId { get; init; } = null!;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public record SuggestedSettlementDto(string FromUserId, string FromName, string ToUserId, string ToName, decimal Amount);

public record AccountingSummaryDto
{
    // ===== REVENUE & COGS (Gross Profit layer) =====
    /// <summary>إيراد المبيعات من الطلبات المسلّمة — **قيمة المنتجات بعد الخصم فقط، بدون شحن**.</summary>
    public decimal TotalRevenue { get; init; }

    /// <summary>
    /// الشحن المحصّل من العملاء — مبلغ منفصل بيتسلّم لشركة الشحن، مش إيراد مبيعات.
    /// بيدخل في صافي الربح والكاش لأن تكلفة المندوب متسجّلة ضمن المصروفات.
    /// </summary>
    public decimal ShippingCollected { get; init; }
    /// <summary>Cost of Goods Sold — sum of (qty × per-unit production cost) for every delivered order item.</summary>
    public decimal TotalCOGS { get; init; }
    /// <summary>Revenue minus COGS.</summary>
    public decimal GrossProfit { get; init; }
    public decimal GrossMarginPercent { get; init; }

    // ===== OPERATING EXPENSES =====
    public decimal TotalExpenses { get; init; }
    public decimal PartnerPaidExpenses { get; init; }
    public decimal ProjectPaidExpenses { get; init; }
    /// <summary>Fair share of partner-paid expenses per partner (PartnerPaidExpenses / partnerCount).</summary>
    public decimal SharePerPartner { get; init; }

    // ===== NET PROFIT =====
    /// <summary>Net profit = Revenue - COGS - All operating expenses. Withdrawals are equity distributions, NOT expenses.</summary>
    public decimal NetProfit { get; init; }
    public decimal ProfitPerPartner { get; init; }
    public decimal ProfitMarginPercent { get; init; }

    // ===== CASH POSITION =====
    /// <summary>Cash physically in the project account: Revenue - project-paid expenses - withdrawals.</summary>
    public decimal ProjectCashOnHand { get; init; }
    public decimal TotalWithdrawals { get; init; }

    /// <summary>The actual number of partners the math is divided by (excludes brand-admin).</summary>
    public int PartnerCount { get; init; }
    /// <summary>Number of delivered orders that count toward revenue.</summary>
    public int DeliveredOrderCount { get; init; }

    public IReadOnlyList<PartnerBalanceDto> Partners { get; init; } = [];
    public IReadOnlyList<SuggestedSettlementDto> SuggestedSettlements { get; init; } = [];
    public IReadOnlyDictionary<ExpenseCategory, decimal> ByCategory { get; init; } = new Dictionary<ExpenseCategory, decimal>();
}
