using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Promotions;

public record PromotionDto
{
    public Guid Id { get; init; }
    public string NameAr { get; init; } = null!;
    public string? NameEn { get; init; }
    public PromotionType Type { get; init; }
    public Guid? TriggerProductId { get; init; }
    public string? TriggerProductName { get; init; }
    public string? TriggerVolume { get; init; }
    public int BuyQuantity { get; init; }
    public decimal MinSpend { get; init; }
    public Guid? RewardProductId { get; init; }
    public string? RewardProductName { get; init; }
    public string? RewardVolume { get; init; }
    public int RewardQuantity { get; init; }
    public decimal RewardPercentOff { get; init; }
    public bool IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int Priority { get; init; }
}

public record PromotionWriteDto
{
    public string NameAr { get; init; } = null!;
    public string? NameEn { get; init; }
    public PromotionType Type { get; init; } = PromotionType.BuyXGetYFree;
    public Guid? TriggerProductId { get; init; }
    public string? TriggerVolume { get; init; }
    public int BuyQuantity { get; init; } = 2;
    public decimal MinSpend { get; init; }
    public Guid? RewardProductId { get; init; }
    public string? RewardVolume { get; init; }
    public int RewardQuantity { get; init; } = 1;
    public decimal RewardPercentOff { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime? ExpiresAt { get; init; }
    public int Priority { get; init; }
}

public interface IPromotionService
{
    /// <summary>All promotions (admin view).</summary>
    Task<List<PromotionDto>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Active, non-expired promotions only (public storefront view).</summary>
    Task<List<PromotionDto>> GetActiveAsync(CancellationToken ct = default);
    Task<PromotionDto> CreateAsync(PromotionWriteDto dto, CancellationToken ct = default);
    Task<PromotionDto> UpdateAsync(Guid id, PromotionWriteDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class PromotionService : IPromotionService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;
    public PromotionService(IApplicationDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<List<PromotionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Promotions.AsNoTracking().OrderByDescending(p => p.Priority).ThenByDescending(p => p.CreatedAt).ToListAsync(ct);
        return await MapManyAsync(list, ct);
    }

    public async Task<List<PromotionDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var list = await _db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && (p.ExpiresAt == null || p.ExpiresAt > now))
            .OrderByDescending(p => p.Priority).ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return await MapManyAsync(list, ct);
    }

    public async Task<PromotionDto> CreateAsync(PromotionWriteDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        var p = new Promotion
        {
            NameAr = dto.NameAr, NameEn = dto.NameEn, Type = dto.Type,
            TriggerProductId = dto.TriggerProductId, TriggerVolume = NormVol(dto.TriggerVolume),
            BuyQuantity = dto.BuyQuantity, MinSpend = dto.MinSpend,
            RewardProductId = dto.RewardProductId, RewardVolume = NormVol(dto.RewardVolume),
            RewardQuantity = Math.Max(1, dto.RewardQuantity), RewardPercentOff = dto.RewardPercentOff,
            IsActive = dto.IsActive, ExpiresAt = dto.ExpiresAt, Priority = dto.Priority,
        };
        _db.Promotions.Add(p);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "CREATE_PROMOTION", $"أضاف عرض: {p.NameAr}", entityName: nameof(Promotion), entityId: p.Id.ToString(), ct: ct);
        return (await MapManyAsync(new List<Promotion> { p }, ct))[0];
    }

    public async Task<PromotionDto> UpdateAsync(Guid id, PromotionWriteDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        var p = await _db.Promotions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
        p.NameAr = dto.NameAr; p.NameEn = dto.NameEn; p.Type = dto.Type;
        p.TriggerProductId = dto.TriggerProductId; p.TriggerVolume = NormVol(dto.TriggerVolume);
        p.BuyQuantity = dto.BuyQuantity; p.MinSpend = dto.MinSpend;
        p.RewardProductId = dto.RewardProductId; p.RewardVolume = NormVol(dto.RewardVolume);
        p.RewardQuantity = Math.Max(1, dto.RewardQuantity); p.RewardPercentOff = dto.RewardPercentOff;
        p.IsActive = dto.IsActive; p.ExpiresAt = dto.ExpiresAt; p.Priority = dto.Priority;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "UPDATE_PROMOTION", $"عدّل عرض: {p.NameAr}", entityName: nameof(Promotion), entityId: id.ToString(), ct: ct);
        return (await MapManyAsync(new List<Promotion> { p }, ct))[0];
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.Promotions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
        _db.Promotions.Remove(p);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Coupon, "DELETE_PROMOTION", $"حذف عرض: {p.NameAr}", entityId: id.ToString(), ct: ct);
    }

    private static void Validate(PromotionWriteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NameAr)) throw new BadRequestException("اسم العرض مطلوب");
        if (dto.Type == PromotionType.BuyXGetYFree)
        {
            if (dto.BuyQuantity < 1) throw new BadRequestException("الكمية المطلوب شراؤها لازم 1 على الأقل");
            if (dto.RewardProductId == null) throw new BadRequestException("اختر المنتج الهدية");
        }
        if (dto.Type == PromotionType.FreeGiftOverAmount && dto.RewardProductId == null)
            throw new BadRequestException("اختر المنتج الهدية");
        if ((dto.Type == PromotionType.BuyXGetPercentOff || dto.Type == PromotionType.OrderPercentOver) && dto.RewardPercentOff <= 0)
            throw new BadRequestException("نسبة الخصم لازم أكبر من صفر");
    }

    private static string? NormVol(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        return v.Trim().ToUpperInvariant();
    }

    private async Task<List<PromotionDto>> MapManyAsync(List<Promotion> list, CancellationToken ct)
    {
        var ids = list.SelectMany(p => new[] { p.TriggerProductId, p.RewardProductId })
                      .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        string? Name(Guid? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : null;
        return list.Select(p => new PromotionDto
        {
            Id = p.Id, NameAr = p.NameAr, NameEn = p.NameEn, Type = p.Type,
            TriggerProductId = p.TriggerProductId, TriggerProductName = Name(p.TriggerProductId), TriggerVolume = p.TriggerVolume,
            BuyQuantity = p.BuyQuantity, MinSpend = p.MinSpend,
            RewardProductId = p.RewardProductId, RewardProductName = Name(p.RewardProductId), RewardVolume = p.RewardVolume,
            RewardQuantity = p.RewardQuantity, RewardPercentOff = p.RewardPercentOff,
            IsActive = p.IsActive, ExpiresAt = p.ExpiresAt, Priority = p.Priority,
        }).ToList();
    }
}
