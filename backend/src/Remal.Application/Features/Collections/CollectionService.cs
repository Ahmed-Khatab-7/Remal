using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Features.Collections.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Collections;

public interface ICollectionService
{
    Task<List<CollectionListDto>> GetAllAsync(CancellationToken ct = default);
    Task<CollectionListDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CollectionListDto> CreateAsync(CollectionWriteDto dto, CancellationToken ct = default);
    Task<CollectionListDto> UpdateAsync(Guid id, CollectionWriteDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class CollectionService : ICollectionService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CollectionService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<CollectionListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Collections
            .AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<CollectionListDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Collections
            .AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Collection", id);
        return Map(c);
    }

    public async Task<CollectionListDto> CreateAsync(CollectionWriteDto dto, CancellationToken ct = default)
    {
        var collection = new Collection
        {
            Name = dto.Name,
            NameEn = dto.NameEn,
            Description = dto.Description,
            DescriptionEn = dto.DescriptionEn,
            ImageUrl = dto.ImageUrl,
            ImageUrl2 = dto.ImageUrl2,
            ImageUrl3 = dto.ImageUrl3,
            OriginalPrice = dto.OriginalPrice,
            FinalPrice = dto.FinalPrice,
            Stock = dto.Stock,
            SampleVolume = dto.SampleVolume,
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
        foreach (var i in dto.Items)
            collection.Items.Add(new CollectionItem { ProductId = i.ProductId, Order = i.Order });

        _db.Collections.Add(collection);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Collection, "CREATE_COLLECTION",
            $"أضاف مجموعة: {collection.Name}", entityName: nameof(Collection), entityId: collection.Id.ToString(), ct: ct);

        return await GetByIdAsync(collection.Id, ct);
    }

    public async Task<CollectionListDto> UpdateAsync(Guid id, CollectionWriteDto dto, CancellationToken ct = default)
    {
        // Load WITHOUT .Include(c => c.Items) — same pattern as ProductService/BundleService.
        var c = await _db.Collections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Collection", id);

        // PHASE 1: bulk-delete via raw SQL.
        await _db.CollectionItems.Where(ci => ci.CollectionId == id).ExecuteDeleteAsync(ct);

        // PHASE 2: update parent + add fresh items with new Guids.
        c.Name = dto.Name;
        c.NameEn = dto.NameEn;
        c.Description = dto.Description;
        c.DescriptionEn = dto.DescriptionEn;
        c.ImageUrl = dto.ImageUrl;
        c.ImageUrl2 = dto.ImageUrl2;
        c.ImageUrl3 = dto.ImageUrl3;
        c.BadgeArabic = dto.BadgeArabic; c.BadgeEnglish = dto.BadgeEnglish; c.BadgeKind = dto.BadgeKind;
        c.TickerLine1Ar = dto.TickerLine1Ar; c.TickerLine1En = dto.TickerLine1En;
        c.TickerLine2Ar = dto.TickerLine2Ar; c.TickerLine2En = dto.TickerLine2En;
        c.TickerLine3Ar = dto.TickerLine3Ar; c.TickerLine3En = dto.TickerLine3En;
        c.TickerLine4Ar = dto.TickerLine4Ar; c.TickerLine4En = dto.TickerLine4En;
        c.TickerLine5Ar = dto.TickerLine5Ar; c.TickerLine5En = dto.TickerLine5En;
        c.TickerLine6Ar = dto.TickerLine6Ar; c.TickerLine6En = dto.TickerLine6En;
        c.TickerJson = dto.TickerJson;
        c.DetailJson = dto.DetailJson;
        c.OriginalPrice = dto.OriginalPrice;
        c.FinalPrice = dto.FinalPrice;
        c.Stock = dto.Stock;
        c.SampleVolume = dto.SampleVolume;
        c.Status = dto.Status;
        var newItems = new List<CollectionItem>();
        foreach (var i in dto.Items)
            newItems.Add(new CollectionItem { CollectionId = c.Id, ProductId = i.ProductId, Order = i.Order });
        _db.CollectionItems.AddRange(newItems);
        c.Items = newItems;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Collection, "UPDATE_COLLECTION",
            $"عدّل مجموعة: {c.Name}", entityName: nameof(Collection), entityId: c.Id.ToString(), ct: ct);

        return await GetByIdAsync(c.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Collections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Collection", id);
        c.IsDeleted = true;
        c.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Collection, "DELETE_COLLECTION", $"حذف مجموعة: {c.Name}", entityId: id.ToString(), ct: ct);
    }

    private static CollectionListDto Map(Collection c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        NameEn = c.NameEn,
        Description = c.Description,
        DescriptionEn = c.DescriptionEn,
        ImageUrl = c.ImageUrl,
        ImageUrl2 = c.ImageUrl2,
        ImageUrl3 = c.ImageUrl3,
        OriginalPrice = c.OriginalPrice,
        FinalPrice = c.FinalPrice,
        Stock = c.Stock,
        SampleVolume = c.SampleVolume,
        Status = c.Status,
        BadgeArabic = c.BadgeArabic, BadgeEnglish = c.BadgeEnglish, BadgeKind = c.BadgeKind,
        TickerLine1Ar = c.TickerLine1Ar, TickerLine1En = c.TickerLine1En,
        TickerLine2Ar = c.TickerLine2Ar, TickerLine2En = c.TickerLine2En,
        TickerLine3Ar = c.TickerLine3Ar, TickerLine3En = c.TickerLine3En,
        TickerLine4Ar = c.TickerLine4Ar, TickerLine4En = c.TickerLine4En,
        TickerLine5Ar = c.TickerLine5Ar, TickerLine5En = c.TickerLine5En,
        TickerLine6Ar = c.TickerLine6Ar, TickerLine6En = c.TickerLine6En,
        TickerJson = c.TickerJson,
        DetailJson = c.DetailJson,
        Items = c.Items.OrderBy(i => i.Order).Select(i =>
            new CollectionItemDto(i.Id, i.ProductId, i.Product?.Name ?? "", i.Product?.NameEn, i.Product?.ImageUrl, i.Order)).ToList(),
    };
}
