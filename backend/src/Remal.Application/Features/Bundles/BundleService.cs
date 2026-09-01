using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Bundles.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Bundles;

public interface IBundleService
{
    Task<PagedResult<BundleListDto>> GetListAsync(int page = 1, int pageSize = 20, string? search = null, BundleStatus? status = null, CancellationToken ct = default);
    Task<BundleListDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BundleListDto> CreateAsync(BundleCreateDto dto, CancellationToken ct = default);
    Task<BundleListDto> UpdateAsync(Guid id, BundleUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class BundleService : IBundleService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public BundleService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<BundleListDto>> GetListAsync(int page = 1, int pageSize = 20, string? search = null, BundleStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Bundles
            .AsNoTracking()
            .Include(b => b.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => EF.Functions.Like(b.Name, $"%{search}%"));
        if (status.HasValue) query = query.Where(b => b.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => Map(b))
            .ToListAsync(ct);

        return PagedResult<BundleListDto>.Create(items, total, page, pageSize);
    }

    public async Task<BundleListDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.Bundles
            .AsNoTracking()
            .Include(b => b.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Bundle", id);
        return Map(b);
    }

    public async Task<BundleListDto> CreateAsync(BundleCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Items.Count < 2)
            throw new BadRequestException("الباقة لازم تحتوي على منتجين على الأقل");

        await EnsureProductsExistAsync(dto.Items.Select(i => i.ProductId), ct);

        var bundle = new Bundle
        {
            Name = dto.Name,
            NameEn = dto.NameEn,
            Description = dto.Description,
            DescriptionEn = dto.DescriptionEn,
            Tag = dto.Tag,
            TagEn = dto.TagEn,
            ImageUrl = dto.ImageUrl,
            ImageUrl2 = dto.ImageUrl2,
            ImageUrl3 = dto.ImageUrl3,
            OriginalPrice = dto.OriginalPrice,
            FinalPrice = dto.FinalPrice,
            Stock = dto.Stock,
            Status = dto.Status,
            BadgeArabic = dto.BadgeArabic, BadgeEnglish = dto.BadgeEnglish, BadgeKind = dto.BadgeKind,
            TickerLine1Ar = dto.TickerLine1Ar, TickerLine1En = dto.TickerLine1En,
            TickerLine2Ar = dto.TickerLine2Ar, TickerLine2En = dto.TickerLine2En,
            TickerLine3Ar = dto.TickerLine3Ar, TickerLine3En = dto.TickerLine3En,
            TickerLine4Ar = dto.TickerLine4Ar, TickerLine4En = dto.TickerLine4En,
            TickerLine5Ar = dto.TickerLine5Ar, TickerLine5En = dto.TickerLine5En,
            TickerLine6Ar = dto.TickerLine6Ar, TickerLine6En = dto.TickerLine6En,
            TickerJson = dto.TickerJson,
            DetailJson = dto.DetailJson,
        };
        var idx = 0;
        foreach (var i in dto.Items)
        {
            bundle.Items.Add(new BundleItem { ProductId = i.ProductId, Volume = NormalizeVol(i.Volume), Order = idx++ });
        }

        _db.Bundles.Add(bundle);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Bundle, "CREATE_BUNDLE",
            $"أضاف باقة: {bundle.Name}",
            entityName: nameof(Bundle), entityId: bundle.Id.ToString(),
            after: new { bundle.Name, bundle.FinalPrice, Items = bundle.Items.Select(i => new { i.ProductId, i.Volume }) }, ct: ct);

        return await GetByIdAsync(bundle.Id, ct);
    }

    public async Task<BundleListDto> UpdateAsync(Guid id, BundleUpdateDto dto, CancellationToken ct = default)
    {
        // Load bundle WITHOUT .Include(b => b.Items) — see ProductService.UpdateAsync
        // for the full rationale; tl;dr: keeping old children out of the tracker is
        // the only reliable way to swap them out cleanly.
        var bundle = await _db.Bundles.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Bundle", id);

        await EnsureProductsExistAsync(dto.Items.Select(i => i.ProductId), ct);

        var oldItemsSnapshot = await _db.BundleItems
            .AsNoTracking()
            .Where(bi => bi.BundleId == id)
            .Select(i => new { i.ProductId, i.Volume })
            .ToListAsync(ct);
        var before = new { bundle.Name, bundle.FinalPrice, Items = oldItemsSnapshot };

        // PHASE 1: bulk-delete old bundle items via raw SQL.
        await _db.BundleItems.Where(bi => bi.BundleId == id).ExecuteDeleteAsync(ct);

        // PHASE 2: assign parent fields + insert fresh items (new Guids).
        bundle.Name = dto.Name;
        bundle.NameEn = dto.NameEn;
        bundle.Description = dto.Description;
        bundle.DescriptionEn = dto.DescriptionEn;
        bundle.Tag = dto.Tag;
        bundle.TagEn = dto.TagEn;
        bundle.ImageUrl = dto.ImageUrl;
        bundle.ImageUrl2 = dto.ImageUrl2;
        bundle.ImageUrl3 = dto.ImageUrl3;
        bundle.BadgeArabic = dto.BadgeArabic; bundle.BadgeEnglish = dto.BadgeEnglish; bundle.BadgeKind = dto.BadgeKind;
        bundle.TickerLine1Ar = dto.TickerLine1Ar; bundle.TickerLine1En = dto.TickerLine1En;
        bundle.TickerLine2Ar = dto.TickerLine2Ar; bundle.TickerLine2En = dto.TickerLine2En;
        bundle.TickerLine3Ar = dto.TickerLine3Ar; bundle.TickerLine3En = dto.TickerLine3En;
        bundle.TickerLine4Ar = dto.TickerLine4Ar; bundle.TickerLine4En = dto.TickerLine4En;
        bundle.TickerLine5Ar = dto.TickerLine5Ar; bundle.TickerLine5En = dto.TickerLine5En;
        bundle.TickerLine6Ar = dto.TickerLine6Ar; bundle.TickerLine6En = dto.TickerLine6En;
        bundle.TickerJson = dto.TickerJson;
        bundle.DetailJson = dto.DetailJson;
        bundle.OriginalPrice = dto.OriginalPrice;
        bundle.FinalPrice = dto.FinalPrice;
        bundle.Stock = dto.Stock;
        bundle.Status = dto.Status;

        var idx = 0;
        var newItems = new List<BundleItem>();
        foreach (var i in dto.Items)
            newItems.Add(new BundleItem { BundleId = bundle.Id, ProductId = i.ProductId, Volume = NormalizeVol(i.Volume), Order = idx++ });
        _db.BundleItems.AddRange(newItems);
        bundle.Items = newItems;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Bundle, "UPDATE_BUNDLE",
            $"عدّل الباقة: {bundle.Name}",
            entityName: nameof(Bundle), entityId: bundle.Id.ToString(),
            before: before,
            after: new { bundle.Name, bundle.FinalPrice, Items = bundle.Items.Select(i => new { i.ProductId, i.Volume }) }, ct: ct);

        return await GetByIdAsync(bundle.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var bundle = await _db.Bundles.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Bundle", id);
        bundle.IsDeleted = true;
        bundle.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Bundle, "DELETE_BUNDLE",
            $"حذف باقة: {bundle.Name}", entityName: nameof(Bundle), entityId: id.ToString(), ct: ct);
    }

    private async Task EnsureProductsExistAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idSet = ids.ToHashSet();
        var found = await _db.Products.Where(p => idSet.Contains(p.Id)).CountAsync(ct);
        if (found != idSet.Count) throw new BadRequestException("بعض المنتجات غير موجودة");
    }

    private static string NormalizeVol(string vol) => vol?.ToUpperInvariant() switch
    {
        "30ML" or "50ML" or "100ML" => vol!.ToUpperInvariant(),
        _ => "50ML",
    };

    private static BundleListDto Map(Bundle b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        NameEn = b.NameEn,
        Description = b.Description,
        DescriptionEn = b.DescriptionEn,
        Tag = b.Tag,
        TagEn = b.TagEn,
        ImageUrl = b.ImageUrl,
        ImageUrl2 = b.ImageUrl2,
        ImageUrl3 = b.ImageUrl3,
        OriginalPrice = b.OriginalPrice,
        FinalPrice = b.FinalPrice,
        Savings = b.Savings,
        Stock = b.Stock,
        Status = b.Status,
        BadgeArabic = b.BadgeArabic, BadgeEnglish = b.BadgeEnglish, BadgeKind = b.BadgeKind,
        TickerLine1Ar = b.TickerLine1Ar, TickerLine1En = b.TickerLine1En,
        TickerLine2Ar = b.TickerLine2Ar, TickerLine2En = b.TickerLine2En,
        TickerLine3Ar = b.TickerLine3Ar, TickerLine3En = b.TickerLine3En,
        TickerLine4Ar = b.TickerLine4Ar, TickerLine4En = b.TickerLine4En,
        TickerLine5Ar = b.TickerLine5Ar, TickerLine5En = b.TickerLine5En,
        TickerLine6Ar = b.TickerLine6Ar, TickerLine6En = b.TickerLine6En,
        TickerJson = b.TickerJson,
        DetailJson = b.DetailJson,
        Items = b.Items.OrderBy(i => i.Order).Select(i =>
        {
            // Resolve price for the chosen volume from product sizes
            decimal price = 0;
            if (i.Product != null)
            {
                var sz = i.Product.Sizes?.FirstOrDefault(s => s.Volume == i.Volume);
                price = sz?.Price ?? 0;
            }
            return new BundleItemDto(i.Id, i.ProductId, i.Product?.Name ?? "", i.Product?.NameEn, i.Product?.ImageUrl, i.Volume, price);
        }).ToList(),
        CreatedAt = b.CreatedAt,
    };
}
