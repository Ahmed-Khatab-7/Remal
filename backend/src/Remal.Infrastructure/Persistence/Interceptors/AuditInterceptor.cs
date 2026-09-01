using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Captures CREATE / UPDATE / DELETE on tracked entities and writes an AuditLog row automatically.
/// Pairs with explicit IAuditService calls — both write to AuditLogs.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> SkipEntities = new(StringComparer.Ordinal)
    {
        nameof(AuditLog), "IdentityUserClaim`1", "IdentityUserToken`1", "IdentityUserLogin`1"
    };
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ICurrentUserService _currentUser;

    public AuditInterceptor(ICurrentUserService currentUser) => _currentUser = currentUser;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = ctx.ChangeTracker.Entries()
            .Where(e => !SkipEntities.Contains(e.Entity.GetType().Name)
                     && (e.State == Microsoft.EntityFrameworkCore.EntityState.Added
                      || e.State == Microsoft.EntityFrameworkCore.EntityState.Modified
                      || e.State == Microsoft.EntityFrameworkCore.EntityState.Deleted))
            .ToList();

        foreach (var entry in entries)
        {
            try
            {
                var entityName = entry.Entity.GetType().Name;
                var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

                var log = new AuditLog
                {
                    Timestamp = DateTime.UtcNow,
                    UserId = _currentUser.UserId,
                    UserName = _currentUser.UserName ?? "System",
                    Category = AuditCategory.System,
                    Action = $"{entry.State.ToString().ToUpperInvariant()}_{entityName.ToUpperInvariant()}",
                    EntityName = entityName,
                    EntityId = entityId,
                    Description = $"{entry.State} {entityName}",
                    IpAddress = _currentUser.IpAddress,
                    UserAgent = _currentUser.UserAgent,
                };

                if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
                {
                    log.Before = JsonSerializer.Serialize(GetOriginalValues(entry), JsonOpts);
                    log.After = JsonSerializer.Serialize(GetCurrentValues(entry), JsonOpts);
                }
                else if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                {
                    log.After = JsonSerializer.Serialize(GetCurrentValues(entry), JsonOpts);
                }
                else if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Deleted)
                {
                    log.Before = JsonSerializer.Serialize(GetOriginalValues(entry), JsonOpts);
                }

                ctx.Add(log);
            }
            catch
            {
                // never block a SaveChanges because of audit serialization issues
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static Dictionary<string, object?> GetCurrentValues(EntityEntry entry) =>
        entry.Properties.Where(p => !p.Metadata.IsShadowProperty())
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

    private static Dictionary<string, object?> GetOriginalValues(EntityEntry entry) =>
        entry.Properties.Where(p => !p.Metadata.IsShadowProperty())
            .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
}
