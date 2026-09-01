using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;

namespace Remal.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public string? UserId => _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => _http.HttpContext?.User?.FindFirstValue("fullName") ?? _http.HttpContext?.User?.Identity?.Name;
    public string? Email => _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => _http.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    public IEnumerable<string> Roles => _http.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];
    public string? IpAddress => _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    public string? UserAgent => _http.HttpContext?.Request?.Headers?.UserAgent.ToString();
    public bool IsInRole(string role) => _http.HttpContext?.User?.IsInRole(role) ?? false;
}

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db; _currentUser = currentUser;
    }

    public async Task LogAsync(AuditCategory category, string action, string description,
        string? entityName = null, string? entityId = null,
        object? before = null, object? after = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName ?? "System",
            Category = category,
            Action = action,
            Description = description,
            EntityName = entityName,
            EntityId = entityId,
            Before = before == null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            After = after == null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
