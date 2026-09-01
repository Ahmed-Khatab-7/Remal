using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Accounting.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Domain.Identity;

namespace Remal.Application.Features.Accounting;

public interface IAccountingService
{
    Task<AccountingSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<PagedResult<ExpenseDto>> GetExpensesAsync(int page, int pageSize, string? search, string? userId, ExpenseCategory? category, CancellationToken ct = default);
    Task<ExpenseDto> CreateExpenseAsync(ExpenseWriteDto dto, CancellationToken ct = default);
    Task<ExpenseDto> UpdateExpenseAsync(Guid id, ExpenseWriteDto dto, CancellationToken ct = default);
    Task DeleteExpenseAsync(Guid id, CancellationToken ct = default);
    Task<List<SettlementDto>> GetSettlementsAsync(CancellationToken ct = default);
    Task<SettlementDto> CreateSettlementAsync(SettlementWriteDto dto, CancellationToken ct = default);
    Task DeleteSettlementAsync(Guid id, CancellationToken ct = default);
    Task<List<PartnerWithdrawalDto>> GetWithdrawalsAsync(CancellationToken ct = default);
    Task<PartnerWithdrawalDto> CreateWithdrawalAsync(PartnerWithdrawalWriteDto dto, CancellationToken ct = default);
    Task DeleteWithdrawalAsync(Guid id, CancellationToken ct = default);
    /// <summary>Hard-deletes ALL expenses, settlements and partner-withdrawals. Used for test resets.</summary>
    Task<(int expenses, int settlements, int withdrawals)> ResetAllAsync(CancellationToken ct = default);
}

public class AccountingService : IAccountingService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IDashboardNotifier _notifier;

    public AccountingService(IApplicationDbContext db, IAuditService audit, UserManager<ApplicationUser> users, IDashboardNotifier notifier)
    {
        _db = db; _audit = audit; _users = users; _notifier = notifier;
    }

    public async Task<AccountingSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        // === Resolve actual partners (exclude the brand-admin account) ===
        // admin@remal.eg represents the brand itself, not a 4th partner.
        var allPartners = await _users.GetUsersInRoleAsync(Roles.Partner);
        var partners = allPartners
            .Where(p => !string.Equals(p.Email, "admin@remal.eg", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (partners.Count == 0)
            partners = await _users.Users
                .Where(u => u.IsActive && u.Email != "admin@remal.eg")
                .ToListAsync(ct);

        var expenses = await _db.Expenses.AsNoTracking().ToListAsync(ct);
        var settlements = await _db.Settlements.AsNoTracking().ToListAsync(ct);
        var withdrawals = await _db.PartnerWithdrawals.AsNoTracking().ToListAsync(ct);
        var partnerIds = partners.Select(p => p.Id).ToHashSet();

        // === Split expenses by funding source ===
        // PartnerPaid: a partner paid out-of-pocket and is owed reimbursement / counted in the split.
        // ProjectPaid: paid directly from the project's cash on hand (no partner contributed).
        var partnerPaid = expenses.Where(e => e.PaidById != null && partnerIds.Contains(e.PaidById)).ToList();
        var projectPaid = expenses.Where(e => e.PaidById == null || !partnerIds.Contains(e.PaidById!)).ToList();
        var partnerPaidTotal = partnerPaid.Sum(e => e.Amount);
        var projectPaidTotal = projectPaid.Sum(e => e.Amount);
        var totalExpenses = partnerPaidTotal + projectPaidTotal;
        var totalWithdrawals = withdrawals.Sum(w => w.Amount);

        var partnerCount = Math.Max(1, partners.Count);
        // Each partner's fair share is only of the partner-paid expenses (project-paid doesn't enter personal balances).
        var sharePerPartner = partnerPaidTotal / partnerCount;

        var deliveredOrders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered)
            .Include(o => o.Items)
            .ToListAsync(ct);
        // ===== الإيراد = قيمة المنتجات فقط =====
        // الشحن مش إيراد مبيعات — هو مبلغ بنحصّله من العميل ونسلّمه لشركة الشحن.
        // لو دخل ضمن الإيراد بيضخّم رقم المبيعات ويشوّه هامش الربح (بيبان أعلى من
        // الحقيقة على المنتجات الرخيصة اللي شحنها نسبة كبيرة من قيمتها).
        // Total = Subtotal - Discount + ShippingFee، فـ (Total - ShippingFee) هي
        // قيمة المنتجات بعد الخصم — وده الرقم الصح للإيراد.
        var totalRevenue = deliveredOrders.Sum(o => o.Total - o.ShippingFee);
        var shippingCollected = deliveredOrders.Sum(o => o.ShippingFee);
        var deliveredOrderCount = deliveredOrders.Count;

        // ===== COGS (Cost of Goods Sold) =====
        // Sum per delivered order item: qty × (productCostOil + costAlcohol + costPackaging).
        // Items that aren't direct products (bundles/collections) are skipped — their COGS will be
        // captured naturally when individual product cost data is set up.
        var allProducts = await _db.Products.AsNoTracking().ToListAsync(ct);
        var productCostById = allProducts.ToDictionary(
            p => p.Id,
            p => (p.CostOil ?? 0m) + (p.CostAlcohol ?? 0m) + (p.CostPackaging ?? 0m));
        decimal totalCogs = 0m;
        foreach (var order in deliveredOrders)
        {
            foreach (var item in order.Items)
            {
                if (item.ProductId.HasValue && productCostById.TryGetValue(item.ProductId.Value, out var unitCost))
                {
                    totalCogs += unitCost * item.Quantity;
                }
            }
        }
        var grossProfit = totalRevenue - totalCogs;
        var grossMarginPct = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100m : 0m;

        // Net profit = Revenue + الشحن المحصّل - COGS - كل المصروفات التشغيلية.
        // ⚠️ الشحن المحصّل **لازم** يدخل هنا رغم إنه مش إيراد مبيعات: تكلفة المندوب
        // متسجّلة ضمن المصروفات، فلو شِلنا المحصّل وسِبنا التكلفة يبقى الربح أقل من
        // الحقيقة بقيمة الشحن كله. البندين بيقاصّوا بعض، والفرق بينهم (ربح أو خسارة
        // الشحن) هو الرقم الحقيقي اللي بيأثر على الربح.
        // Withdrawals are NOT business expenses — they're equity distributions to partners.
        var netProfit = totalRevenue + shippingCollected - totalCogs - totalExpenses;
        var profitPerPartner = netProfit / partnerCount;
        var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

        // Project cash on hand = revenue - project-paid expenses - withdrawals (cash physically leaves).
        // Note: COGS is an accounting concept — the cash for production was already spent when
        // partners paid for materials (i.e. already counted via expenses).
        // الكاش الفعلي بيشمل الشحن المحصّل — العميل دفعه فعلاً ودخل الخزنة.
        var projectCashOnHand = totalRevenue + shippingCollected - projectPaidTotal - totalWithdrawals;

        var partnerStats = partners.Select(p =>
        {
            var paidExpensesP = partnerPaid.Where(e => e.PaidById == p.Id).Sum(e => e.Amount);
            var settlementsPaid = settlements.Where(s => s.FromUserId == p.Id).Sum(s => s.Amount);
            var settlementsReceived = settlements.Where(s => s.ToUserId == p.Id).Sum(s => s.Amount);
            var withdrawalsP = withdrawals.Where(w => w.PartnerId == p.Id).Sum(w => w.Amount);
            // Net = what this partner contributed (expenses + settlement payments) MINUS
            //       what they took back out (settlement receipts + drawings).
            var net = paidExpensesP + settlementsPaid - settlementsReceived - withdrawalsP;
            // Balance vs the fair partner-paid share:
            //  positive = they overpaid → owed money;  negative = they owe.
            var balance = net - sharePerPartner;
            return new PartnerBalanceDto
            {
                UserId = p.Id, Name = p.FullName, AvatarInitials = p.AvatarInitials,
                AvatarUrl = p.AvatarUrl, Email = p.Email,
                PaidExpenses = paidExpensesP, SettlementsPaid = settlementsPaid,
                SettlementsReceived = settlementsReceived,
                Withdrawals = withdrawalsP,
                NetContribution = net, Balance = balance,
                ProfitShare = profitPerPartner,
            };
        }).ToList();

        var suggested = SuggestSettlements(partnerStats);

        var byCategory = expenses
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return new AccountingSummaryDto
        {
            TotalRevenue = totalRevenue,
            ShippingCollected = shippingCollected,
            TotalCOGS = totalCogs,
            GrossProfit = grossProfit,
            GrossMarginPercent = grossMarginPct,
            TotalExpenses = totalExpenses,
            PartnerPaidExpenses = partnerPaidTotal,
            ProjectPaidExpenses = projectPaidTotal,
            SharePerPartner = sharePerPartner,
            NetProfit = netProfit,
            ProfitPerPartner = profitPerPartner,
            ProfitMarginPercent = profitMargin,
            ProjectCashOnHand = projectCashOnHand,
            TotalWithdrawals = totalWithdrawals,
            PartnerCount = partnerCount,
            DeliveredOrderCount = deliveredOrderCount,
            Partners = partnerStats,
            SuggestedSettlements = suggested,
            ByCategory = byCategory,
        };
    }

    public async Task<List<PartnerWithdrawalDto>> GetWithdrawalsAsync(CancellationToken ct = default)
    {
        var list = await _db.PartnerWithdrawals.AsNoTracking()
            .Include(w => w.Partner)
            .OrderByDescending(w => w.Date).ToListAsync(ct);
        return list.Select(w => new PartnerWithdrawalDto
        {
            Id = w.Id, Date = w.Date, PartnerId = w.PartnerId,
            PartnerName = w.Partner?.FullName ?? "—",
            Amount = w.Amount, Note = w.Note,
        }).ToList();
    }

    public async Task<PartnerWithdrawalDto> CreateWithdrawalAsync(PartnerWithdrawalWriteDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0) throw new BadRequestException("المبلغ لازم يكون أكبر من صفر");
        var partner = await _users.FindByIdAsync(dto.PartnerId) ?? throw new NotFoundException("Partner", dto.PartnerId);

        var w = new PartnerWithdrawal
        {
            Date = dto.Date,
            PartnerId = dto.PartnerId,
            Amount = dto.Amount,
            Note = dto.Note,
        };
        _db.PartnerWithdrawals.Add(w);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Settlement, "PARTNER_WITHDRAWAL",
            $"سحب: {partner.FullName} سحب {w.Amount:N0} ج.م من المشروع",
            entityName: nameof(PartnerWithdrawal), entityId: w.Id.ToString(),
            after: new { w.PartnerId, w.Amount, w.Date, w.Note }, ct: ct);

        await _notifier.WithdrawalChangedAsync(new WithdrawalChangedNotification(
            "Created", w.Id, partner.FullName, w.Amount, DateTime.UtcNow), ct);

        return new PartnerWithdrawalDto
        {
            Id = w.Id, Date = w.Date, PartnerId = w.PartnerId,
            PartnerName = partner.FullName, Amount = w.Amount, Note = w.Note,
        };
    }

    public async Task DeleteWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        var w = await _db.PartnerWithdrawals.Include(x => x.Partner).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Withdrawal", id);
        var name = w.Partner?.FullName ?? "—";
        var amount = w.Amount;
        _db.PartnerWithdrawals.Remove(w);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Settlement, "DELETE_WITHDRAWAL",
            $"حذف سحب: {name} ({amount:N0} ج.م)", entityId: id.ToString(), ct: ct);
        await _notifier.WithdrawalChangedAsync(new WithdrawalChangedNotification(
            "Deleted", id, name, amount, DateTime.UtcNow), ct);
    }

    public async Task<(int expenses, int settlements, int withdrawals)> ResetAllAsync(CancellationToken ct = default)
    {
        // Hard-delete in one go via ExecuteDeleteAsync — bypasses the change tracker.
        var expCount = await _db.Expenses.CountAsync(ct);
        var stlCount = await _db.Settlements.CountAsync(ct);
        var wdrCount = await _db.PartnerWithdrawals.CountAsync(ct);
        await _db.Expenses.ExecuteDeleteAsync(ct);
        await _db.Settlements.ExecuteDeleteAsync(ct);
        await _db.PartnerWithdrawals.ExecuteDeleteAsync(ct);
        await _audit.LogAsync(AuditCategory.Settlement, "RESET_ACCOUNTING",
            $"تصفير الحسابات بالكامل: {expCount} مصروف، {stlCount} تسوية، {wdrCount} سحب",
            ct: ct);
        // Broadcast a generic refresh signal — reuse existing notifications so the dashboard refreshes
        await _notifier.SettlementChangedAsync(new SettlementChangedNotification(
            "Reset", Guid.Empty, "—", "—", 0m, DateTime.UtcNow), ct);
        return (expCount, stlCount, wdrCount);
    }

    /// <summary>
    /// Greedy algorithm: pair the most-overpaid (positive balance) with the most-underpaid.
    /// Minimizes number of transactions to balance the partners.
    /// </summary>
    private static List<SuggestedSettlementDto> SuggestSettlements(List<PartnerBalanceDto> partners)
    {
        var balances = partners.Select(p => new { p.UserId, p.Name, B = Math.Round(p.Balance, 2) }).ToList();
        var debtors = balances.Where(x => x.B < -0.5m)
            .Select(x => new MutableBalance { UserId = x.UserId, Name = x.Name, B = x.B })
            .OrderBy(x => x.B).ToList();
        var creditors = balances.Where(x => x.B > 0.5m)
            .Select(x => new MutableBalance { UserId = x.UserId, Name = x.Name, B = x.B })
            .OrderByDescending(x => x.B).ToList();

        var result = new List<SuggestedSettlementDto>();
        int i = 0, j = 0;
        while (i < debtors.Count && j < creditors.Count)
        {
            var owed = -debtors[i].B;
            var due = creditors[j].B;
            var pay = Math.Min(owed, due);
            if (pay > 0.5m)
            {
                result.Add(new SuggestedSettlementDto(
                    debtors[i].UserId, debtors[i].Name,
                    creditors[j].UserId, creditors[j].Name,
                    Math.Round(pay)));
            }
            debtors[i].B += pay;
            creditors[j].B -= pay;
            if (Math.Abs(debtors[i].B) < 0.5m) i++;
            if (Math.Abs(creditors[j].B) < 0.5m) j++;
        }
        return result;
    }

    private class MutableBalance
    {
        public string UserId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public decimal B { get; set; }
    }

    public async Task<PagedResult<ExpenseDto>> GetExpensesAsync(int page, int pageSize, string? search, string? userId, ExpenseCategory? category, CancellationToken ct = default)
    {
        var q = _db.Expenses.AsNoTracking().Include(e => e.PaidBy).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(e => EF.Functions.Like(e.Description, $"%{search}%"));
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.PaidById == userId);
        if (category.HasValue) q = q.Where(e => e.Category == category);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.Date).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => Map(e)).ToListAsync(ct);
        return PagedResult<ExpenseDto>.Create(items, total, page, pageSize);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(ExpenseWriteDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0) throw new BadRequestException("المبلغ لازم يكون أكبر من صفر");
        // PaidById is OPTIONAL — null means "paid from project revenue".
        string? paidById = string.IsNullOrWhiteSpace(dto.PaidById) ? null : dto.PaidById;
        string paidByLabel = "إدارة رمال (من الإيرادات)";
        if (paidById != null)
        {
            var user = await _users.FindByIdAsync(paidById) ?? throw new NotFoundException("Partner", paidById);
            paidByLabel = user.FullName;
        }

        var expense = new Expense
        {
            Date = dto.Date,
            PaidById = paidById,
            Amount = dto.Amount,
            Category = dto.Category,
            Description = dto.Description,
            ReceiptUrl = dto.ReceiptUrl,
            Notes = dto.Notes,
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Expense, "CREATE_EXPENSE",
            $"قيد مصروف: {expense.Description} ({expense.Amount:N0} ج.م) — دفع {paidByLabel}",
            entityName: nameof(Expense), entityId: expense.Id.ToString(),
            after: new { expense.Description, expense.Amount, expense.Category, expense.PaidById }, ct: ct);

        // Realtime: notify the dashboard of the new expense
        await _notifier.ExpenseAddedAsync(new ExpenseAddedNotification(
            expense.Id, paidByLabel, expense.Amount, expense.Category.ToString()), ct);

        return await GetExpense(expense.Id, ct);
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(Guid id, ExpenseWriteDto dto, CancellationToken ct = default)
    {
        var e = await _db.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Expense", id);
        var before = new { e.Description, e.Amount, e.Category, e.PaidById, e.Date };

        e.Date = dto.Date;
        e.PaidById = string.IsNullOrWhiteSpace(dto.PaidById) ? null : dto.PaidById;
        e.Amount = dto.Amount;
        e.Category = dto.Category;
        e.Description = dto.Description;
        e.ReceiptUrl = dto.ReceiptUrl;
        e.Notes = dto.Notes;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Expense, "UPDATE_EXPENSE",
            $"عدّل مصروف: {e.Description} ({e.Amount:N0} ج.م)",
            entityName: nameof(Expense), entityId: id.ToString(),
            before: before, after: new { e.Description, e.Amount, e.Category, e.PaidById, e.Date }, ct: ct);

        return await GetExpense(id, ct);
    }

    public async Task DeleteExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Expense", id);
        var amount = e.Amount;
        var description = e.Description;
        _db.Expenses.Remove(e);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Expense, "DELETE_EXPENSE",
            $"حذف مصروف: {description} ({amount:N0} ج.م)", entityId: id.ToString(), ct: ct);
        // Reuse ExpenseAdded as a generic "expense list changed" trigger on the dashboard
        await _notifier.ExpenseAddedAsync(new ExpenseAddedNotification(id, "—", -amount, "Deleted"), ct);
    }

    public async Task<List<SettlementDto>> GetSettlementsAsync(CancellationToken ct = default)
    {
        var list = await _db.Settlements.AsNoTracking()
            .Include(s => s.FromUser).Include(s => s.ToUser)
            .OrderByDescending(s => s.Date).ToListAsync(ct);
        return list.Select(s => MapSettlement(s)).ToList();
    }

    public async Task<SettlementDto> CreateSettlementAsync(SettlementWriteDto dto, CancellationToken ct = default)
    {
        if (dto.FromUserId == dto.ToUserId) throw new BadRequestException("الشريك لازم يحوّل لشريك تاني");
        if (dto.Amount <= 0) throw new BadRequestException("المبلغ لازم يكون أكبر من صفر");
        var from = await _users.FindByIdAsync(dto.FromUserId) ?? throw new NotFoundException("From user");
        var to = await _users.FindByIdAsync(dto.ToUserId) ?? throw new NotFoundException("To user");

        var s = new Settlement
        {
            Date = dto.Date,
            FromUserId = dto.FromUserId,
            ToUserId = dto.ToUserId,
            Amount = dto.Amount,
            Note = dto.Note,
        };
        _db.Settlements.Add(s);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Settlement, "CREATE_SETTLEMENT",
            $"تسوية: {from.FullName} → {to.FullName} ({s.Amount:N0} ج.م)",
            entityName: nameof(Settlement), entityId: s.Id.ToString(), ct: ct);

        await _notifier.SettlementChangedAsync(new SettlementChangedNotification(
            "Created", s.Id, from.FullName, to.FullName, s.Amount, DateTime.UtcNow), ct);

        return MapSettlement(s, from, to);
    }

    public async Task DeleteSettlementAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Settlements.Include(x => x.FromUser).Include(x => x.ToUser).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Settlement", id);
        var fromName = s.FromUser?.FullName ?? "—";
        var toName   = s.ToUser?.FullName   ?? "—";
        var amount   = s.Amount;
        _db.Settlements.Remove(s);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Settlement, "DELETE_SETTLEMENT",
            $"حذف تسوية بمبلغ {amount:N0} ج.م", entityId: id.ToString(), ct: ct);
        await _notifier.SettlementChangedAsync(new SettlementChangedNotification(
            "Deleted", id, fromName, toName, amount, DateTime.UtcNow), ct);
    }

    private async Task<ExpenseDto> GetExpense(Guid id, CancellationToken ct)
    {
        var e = await _db.Expenses.AsNoTracking().Include(e => e.PaidBy).FirstAsync(x => x.Id == id, ct);
        return Map(e);
    }

    private static ExpenseDto Map(Expense e) => new()
    {
        Id = e.Id, Date = e.Date,
        PaidById = e.PaidById,
        PaidByName = e.PaidBy?.FullName ?? (e.PaidById == null ? "إدارة رمال (من الإيرادات)" : "—"),
        IsProjectPaid = e.PaidById == null,
        Amount = e.Amount, Category = e.Category, Description = e.Description,
        ReceiptUrl = e.ReceiptUrl, Notes = e.Notes, CreatedAt = e.CreatedAt,
    };

    private static SettlementDto MapSettlement(Settlement s, ApplicationUser? from = null, ApplicationUser? to = null) => new()
    {
        Id = s.Id, Date = s.Date,
        FromUserId = s.FromUserId, FromUserName = (from ?? s.FromUser)?.FullName ?? "—",
        ToUserId = s.ToUserId, ToUserName = (to ?? s.ToUser)?.FullName ?? "—",
        Amount = s.Amount, Note = s.Note,
    };
}
