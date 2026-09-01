using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Remal.Domain.Identity;

namespace Remal.Api.Hubs;

/// <summary>
/// Real-time dashboard hub. Only authenticated Admins/Partners may connect.
/// Clients subscribe to: NewOrder, OrderUpdated, LowStock, NewCustomer, NewReview, ExpenseAdded, AuditEntry.
/// </summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Partner)]
public class DashboardHub : Hub
{
    public const string Path = "/hubs/dashboard";
    public const string GroupAdmins = "admins";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupAdmins);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupAdmins);
        await base.OnDisconnectedAsync(exception);
    }
}
