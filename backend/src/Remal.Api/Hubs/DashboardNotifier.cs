using Microsoft.AspNetCore.SignalR;
using Remal.Application.Common.Interfaces;

namespace Remal.Api.Hubs;

/// <summary>
/// Concrete IDashboardNotifier implementation. Lives in Api because it needs IHubContext<DashboardHub>.
/// </summary>
public class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<DashboardNotifier> _logger;

    public DashboardNotifier(IHubContext<DashboardHub> hub, ILogger<DashboardNotifier> logger)
    {
        _hub = hub; _logger = logger;
    }

    public Task NewOrderAsync(NewOrderNotification p, CancellationToken ct = default)        => Send("NewOrder", p, ct);
    public Task OrderUpdatedAsync(OrderUpdatedNotification p, CancellationToken ct = default) => Send("OrderUpdated", p, ct);
    public Task LowStockAsync(LowStockNotification p, CancellationToken ct = default)         => Send("LowStock", p, ct);
    public Task NewCustomerAsync(NewCustomerNotification p, CancellationToken ct = default)   => Send("NewCustomer", p, ct);
    public Task NewReviewAsync(NewReviewNotification p, CancellationToken ct = default)       => Send("NewReview", p, ct);
    public Task ExpenseAddedAsync(ExpenseAddedNotification p, CancellationToken ct = default) => Send("ExpenseAdded", p, ct);
    public Task AuditEntryAsync(AuditEntryNotification p, CancellationToken ct = default)     => Send("AuditEntry", p, ct);
    public Task SettlementChangedAsync(SettlementChangedNotification p, CancellationToken ct = default) => Send("SettlementChanged", p, ct);
    public Task WithdrawalChangedAsync(WithdrawalChangedNotification p, CancellationToken ct = default) => Send("WithdrawalChanged", p, ct);

    private async Task Send(string method, object payload, CancellationToken ct)
    {
        try
        {
            await _hub.Clients.Group(DashboardHub.GroupAdmins).SendAsync(method, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast failed: {Method}", method);
        }
    }
}
