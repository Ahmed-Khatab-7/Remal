using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Audit.Dtos;
using Remal.Domain.Entities;

namespace Remal.Application.Features.Audit;

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> GetAsync(AuditFilterDto filter, CancellationToken ct = default);
}

public class AuditQueryService : IAuditQueryService
{
    private readonly IApplicationDbContext _db;

    public AuditQueryService(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<AuditLogDto>> GetAsync(AuditFilterDto filter, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
            q = q.Where(a => EF.Functions.Like(a.Description, $"%{filter.Search}%"));
        if (!string.IsNullOrWhiteSpace(filter.UserId)) q = q.Where(a => a.UserId == filter.UserId);
        if (filter.Category.HasValue) q = q.Where(a => a.Category == filter.Category);
        if (filter.FromDate.HasValue) q = q.Where(a => a.Timestamp >= filter.FromDate);
        if (filter.ToDate.HasValue) q = q.Where(a => a.Timestamp <= filter.ToDate);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id, Timestamp = a.Timestamp, UserId = a.UserId, UserName = a.UserName,
                Category = a.Category, Action = a.Action, Description = a.Description,
                EntityName = a.EntityName, EntityId = a.EntityId, Before = a.Before, After = a.After,
                IpAddress = a.IpAddress,
            }).ToListAsync(ct);
        return PagedResult<AuditLogDto>.Create(items, total, filter.Page, filter.PageSize);
    }
}
