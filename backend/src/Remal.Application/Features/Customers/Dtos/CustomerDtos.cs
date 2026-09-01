namespace Remal.Application.Features.Customers.Dtos;

public record CustomerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string? Email { get; init; }
    public string? City { get; init; }
    public string? Address { get; init; }
    public int OrderCount { get; init; }
    public decimal TotalSpent { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CustomerWriteDto
{
    public string Name { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string? Email { get; init; }
    public string? City { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}
