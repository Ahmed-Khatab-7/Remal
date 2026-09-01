using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;

namespace Remal.Tests;

/// <summary>مساعدات إنشاء DbContext في الذاكرة + بدائل no-op للخدمات الخارجية.</summary>
internal static class TestDb
{
    public static ApplicationDbContext New()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(opts);
    }
}

/// <summary>سياق قاعدة بيانات معزول لكل اختبار (InMemory).</summary>
internal sealed class ApplicationDbContextFactory
{
    public ApplicationDbContext Ctx { get; } = TestDb.New();
}

// بدائل no-op للاعتماديات الخارجية (لا تؤثر على منطق التسعير/المخزون/الكوبونات)
internal sealed class FakeAudit : IAuditService
{
    public Task LogAsync(AuditCategory category, string action, string description,
        string? entityName = null, string? entityId = null, object? before = null,
        object? after = null, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeNotifier : IDashboardNotifier
{
    public Task NewOrderAsync(NewOrderNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task OrderUpdatedAsync(OrderUpdatedNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task LowStockAsync(LowStockNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task NewCustomerAsync(NewCustomerNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task NewReviewAsync(NewReviewNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task ExpenseAddedAsync(ExpenseAddedNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task AuditEntryAsync(AuditEntryNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task SettlementChangedAsync(SettlementChangedNotification n, CancellationToken ct = default) => Task.CompletedTask;
    public Task WithdrawalChangedAsync(WithdrawalChangedNotification n, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakePush : IPushService
{
    public string VapidPublicKey => "test-key";
    public Task SendToAllAsync(string title, string body, string? url = null, CancellationToken ct = default) => Task.CompletedTask;
}
