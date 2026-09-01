using Remal.Domain.Common;

namespace Remal.Domain.Entities;

public class ContactMessage : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string Message { get; set; } = null!;
    public bool Read { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public string? ReadByUserId { get; set; }
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
}
