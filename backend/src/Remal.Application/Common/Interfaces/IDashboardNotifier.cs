namespace Remal.Application.Common.Interfaces;

/// <summary>
/// Application-level abstraction so handlers don't depend on SignalR directly.
/// Infrastructure implements it via DashboardHub.
/// </summary>
public interface IDashboardNotifier
{
    Task NewOrderAsync(NewOrderNotification payload, CancellationToken ct = default);
    Task OrderUpdatedAsync(OrderUpdatedNotification payload, CancellationToken ct = default);
    Task LowStockAsync(LowStockNotification payload, CancellationToken ct = default);
    Task NewCustomerAsync(NewCustomerNotification payload, CancellationToken ct = default);
    Task NewReviewAsync(NewReviewNotification payload, CancellationToken ct = default);
    Task ExpenseAddedAsync(ExpenseAddedNotification payload, CancellationToken ct = default);
    Task AuditEntryAsync(AuditEntryNotification payload, CancellationToken ct = default);
    /// <summary>Fired on settlement create AND delete (treat as a generic "accounting changed").</summary>
    Task SettlementChangedAsync(SettlementChangedNotification payload, CancellationToken ct = default);
    /// <summary>Fired on partner-withdrawal create AND delete.</summary>
    Task WithdrawalChangedAsync(WithdrawalChangedNotification payload, CancellationToken ct = default);
}

public record NewOrderNotification(Guid OrderId, string Code, string CustomerName, decimal Total, int ItemCount, string PaymentMethod, DateTime CreatedAt);
public record OrderUpdatedNotification(Guid OrderId, string Code, string NewStatus, string? OldStatus, DateTime UpdatedAt);
public record LowStockNotification(Guid ProductId, string ProductName, string Volume, int RemainingQty);
public record NewCustomerNotification(string UserId, string Name, string Email, DateTime RegisteredAt);
public record NewReviewNotification(Guid ReviewId, string ProductName, string CustomerName, int Rating, DateTime CreatedAt);
public record ExpenseAddedNotification(Guid ExpenseId, string PaidByName, decimal Amount, string Category);
public record AuditEntryNotification(string Category, string Action, string Description, string? UserName, DateTime Timestamp);
public record SettlementChangedNotification(string Kind, Guid SettlementId, string FromName, string ToName, decimal Amount, DateTime At);
public record WithdrawalChangedNotification(string Kind, Guid WithdrawalId, string PartnerName, decimal Amount, DateTime At);
