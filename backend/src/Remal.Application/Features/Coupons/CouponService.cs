using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Features.Coupons.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Coupons;

public interface ICouponService
{
    Task<List<CouponDto>> GetAllAsync(CancellationToken ct = default);
    Task<CouponDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CouponDto> CreateAsync(CouponWriteDto dto, CancellationToken ct = default);
    Task<CouponDto> UpdateAsync(Guid id, CouponWriteDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<CouponDto> ToggleAsync(Guid id, CancellationToken ct = default);
    Task<CouponValidationResult> ValidateAsync(CouponValidateDto dto, CancellationToken ct = default);
}

public class CouponService : ICouponService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CouponService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<CouponDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Coupons.AsNoTracking().OrderByDescending(c => c.CreatedAt).Select(c => Map(c)).ToListAsync(ct);

    public async Task<CouponDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Coupon", id);
        return Map(c);
    }

    public async Task<CouponDto> CreateAsync(CouponWriteDto dto, CancellationToken ct = default)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await _db.Coupons.AnyAsync(c => c.Code == code, ct))
            throw new ConflictException($"الكود {code} موجود بالفعل");

        var coupon = new Coupon
        {
            Code = code,
            Type = dto.Type,
            Value = dto.Value,
            MinOrderAmount = dto.MinOrderAmount,
            MaxUses = dto.MaxUses,
            ExpiresAt = dto.ExpiresAt,
            IsActive = dto.IsActive,
        };
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "CREATE_COUPON", $"أضاف كوبون {coupon.Code}", entityId: coupon.Id.ToString(), ct: ct);
        return Map(coupon);
    }

    public async Task<CouponDto> UpdateAsync(Guid id, CouponWriteDto dto, CancellationToken ct = default)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Coupon", id);
        c.Code = dto.Code.Trim().ToUpperInvariant();
        c.Type = dto.Type;
        c.Value = dto.Value;
        c.MinOrderAmount = dto.MinOrderAmount;
        c.MaxUses = dto.MaxUses;
        c.ExpiresAt = dto.ExpiresAt;
        c.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "UPDATE_COUPON", $"عدّل كوبون {c.Code}", entityId: id.ToString(), ct: ct);
        return Map(c);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Coupon", id);
        _db.Coupons.Remove(c);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "DELETE_COUPON", $"حذف كوبون {c.Code}", entityId: id.ToString(), ct: ct);
    }

    public async Task<CouponDto> ToggleAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Coupon", id);
        c.IsActive = !c.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "TOGGLE_COUPON",
            $"{(c.IsActive ? "تفعيل" : "تعطيل")} كوبون {c.Code}", entityId: id.ToString(), ct: ct);
        return Map(c);
    }

    public async Task<CouponValidationResult> ValidateAsync(CouponValidateDto dto, CancellationToken ct = default)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (coupon == null) return new(false, 0, "الكود غير موجود");
        if (!coupon.IsActive) return new(false, 0, "الكوبون معطل");
        if (coupon.IsExpired) return new(false, 0, "الكوبون منتهي");
        if (coupon.Uses >= coupon.MaxUses) return new(false, 0, "الكوبون استُخدم بالكامل");
        if (dto.OrderAmount < coupon.MinOrderAmount)
            return new(false, 0, $"الحد الأدنى للطلب {coupon.MinOrderAmount} ج.م");
        var discount = coupon.Type == CouponType.Percent
            ? Math.Round(dto.OrderAmount * (coupon.Value / 100m), 2)
            : Math.Min(coupon.Value, dto.OrderAmount);
        return new(true, discount, null);
    }

    private static CouponDto Map(Coupon c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Type = c.Type,
        Value = c.Value,
        MinOrderAmount = c.MinOrderAmount,
        MaxUses = c.MaxUses,
        Uses = c.Uses,
        ExpiresAt = c.ExpiresAt,
        IsActive = c.IsActive,
        IsExpired = c.IsExpired,
        CreatedAt = c.CreatedAt,
    };
}
