using Remal.Domain.Common;
using Remal.Domain.Enums;
using Remal.Domain.Identity;

namespace Remal.Domain.Entities;

public class Expense : AuditableEntity
{
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The partner who paid for the expense out-of-pocket. NULL means the expense was paid
    /// directly from project revenue (e.g. admin used the project's cash on hand to buy
    /// materials) — in that case nobody "contributed" personally, so it doesn't affect
    /// per-partner balances, only net profit.
    /// </summary>
    public string? PaidById { get; set; }
    public ApplicationUser? PaidBy { get; set; }

    public decimal Amount { get; set; }
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public string Description { get; set; } = null!;
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
}

public class Settlement : AuditableEntity
{
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string FromUserId { get; set; } = null!;
    public string ToUserId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }

    public ApplicationUser FromUser { get; set; } = null!;
    public ApplicationUser ToUser { get; set; } = null!;
}

/// <summary>
/// A "drawing" — a partner withdrawing money from the project (e.g. for personal use).
/// This is NOT an expense; it doesn't enter the partner-paid expense pool. Instead, it
/// directly reduces only that partner's claim on the business (balance) and reduces
/// project cash on hand. Settlements remain partner-to-partner only.
/// </summary>
public class PartnerWithdrawal : AuditableEntity
{
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string PartnerId { get; set; } = null!;
    public ApplicationUser Partner { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
