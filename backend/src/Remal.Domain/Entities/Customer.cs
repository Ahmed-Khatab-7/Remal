using Remal.Domain.Common;

namespace Remal.Domain.Entities;

public class Customer : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
