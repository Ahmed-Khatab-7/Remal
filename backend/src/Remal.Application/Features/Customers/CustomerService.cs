using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Customers.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Customers;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetListAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default);
    Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> UpsertAsync(CustomerWriteDto dto, CancellationToken ct = default);
}

public class CustomerService : ICustomerService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CustomerService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<CustomerDto>> GetListAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{search}%") || EF.Functions.Like(c.Phone, $"%{search}%"));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.TotalSpent)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => Map(c)).ToListAsync(ct);
        return PagedResult<CustomerDto>.Create(items, total, page, pageSize);
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Customer", id);
        return Map(c);
    }

    public async Task<CustomerDto> UpsertAsync(CustomerWriteDto dto, CancellationToken ct = default)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == dto.Phone, ct);
        if (existing != null)
        {
            existing.Name = dto.Name;
            existing.Email = dto.Email;
            existing.City = dto.City;
            existing.Address = dto.Address;
            existing.Notes = dto.Notes;
            await _db.SaveChangesAsync(ct);
            return Map(existing);
        }
        var customer = new Customer
        {
            Name = dto.Name,
            Phone = dto.Phone,
            Email = dto.Email,
            City = dto.City,
            Address = dto.Address,
            Notes = dto.Notes,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Customer, "CREATE_CUSTOMER", $"عميل جديد: {customer.Name}", entityId: customer.Id.ToString(), ct: ct);
        return Map(customer);
    }

    private static CustomerDto Map(Customer c) => new()
    {
        Id = c.Id, Name = c.Name, Phone = c.Phone, Email = c.Email, City = c.City, Address = c.Address,
        OrderCount = c.OrderCount, TotalSpent = c.TotalSpent, CreatedAt = c.CreatedAt,
    };
}
