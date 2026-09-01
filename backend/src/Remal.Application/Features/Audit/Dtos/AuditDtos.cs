using Remal.Domain.Enums;

namespace Remal.Application.Features.Audit.Dtos;

public record AuditLogDto
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public AuditCategory Category { get; init; }
    public string Action { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public string? Before { get; init; }
    public string? After { get; init; }
    public string? IpAddress { get; init; }
}

public record AuditFilterDto
{
    public string? Search { get; init; }
    public string? UserId { get; init; }
    public AuditCategory? Category { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
