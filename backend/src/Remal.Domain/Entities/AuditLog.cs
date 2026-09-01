using Remal.Domain.Common;
using Remal.Domain.Enums;

namespace Remal.Domain.Entities;

public class AuditLog : BaseEntity
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public AuditCategory Category { get; set; }
    public string Action { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Before { get; set; }   // JSON snapshot
    public string? After { get; set; }    // JSON snapshot
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
