using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Products.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Products;

public class ProductService : IProductService
{
    private static readonly string[] StandardSizes = ["30ML", "50ML", "100ML"];

    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IDashboardNotifier _notifier;

    public ProductService(IApplicationDbContext db, IAuditService audit, IDashboardNotifier notifier)
    {
        _db = db;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<PagedResult<ProductListDto>> GetListAsync(ProductFilterDto filter, CancellationToken ct = default)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Sizes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{s}%") ||
                EF.Functions.Like(p.NameEn, $"%{s}%") ||
                EF.Functions.Like(p.InspiredBy ?? "", $"%{s}%"));
        }

        if (filter.Category.HasValue) query = query.Where(p => p.Category == filter.Category);
        if (filter.Status.HasValue) query = query.Where(p => p.Status == filter.Status);
        if (filter.LowStockOnly == true)
            query = query.Where(p => p.Sizes.Sum(s => s.Stock) <= 10); // threshold; can be from settings

        query = (filter.SortBy?.ToLowerInvariant()) switch
        {
            "name" => filter.SortDesc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "sold" => filter.SortDesc ? query.OrderByDescending(p => p.Sold) : query.OrderBy(p => p.Sold),
            "stock" => filter.SortDesc
                ? query.OrderByDescending(p => p.Sizes.Sum(s => s.Stock))
                : query.OrderBy(p => p.Sizes.Sum(s => s.Stock)),
            "createdat" => filter.SortDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt),
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => MapList(p))
            .ToListAsync(ct);

        return PagedResult<ProductListDto>.Create(items, total, filter.Page, filter.PageSize);
    }

    public async Task<ProductDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Sizes)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        return MapDetail(product);
    }

    public async Task<ProductDetailDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default)
    {
        var product = new Product
        {
            Name = dto.Name,
            NameEn = dto.NameEn,
            InspiredBy = dto.InspiredBy,
            InspiredByEn = dto.InspiredByEn,
            Category = dto.Category,
            Status = dto.Status,
            ImageUrl = dto.ImageUrl,
            ImageUrl2 = dto.ImageUrl2,
            ImageUrl3 = dto.ImageUrl3,
            NotesTop = dto.NotesTop,
            NotesTopEn = dto.NotesTopEn,
            NotesHeart = dto.NotesHeart,
            NotesHeartEn = dto.NotesHeartEn,
            NotesBase = dto.NotesBase,
            NotesBaseEn = dto.NotesBaseEn,
            Description = dto.Description,
            DescriptionEn = dto.DescriptionEn,
            PerformanceAr = dto.PerformanceAr,
            PerformanceEn = dto.PerformanceEn,
            CostOil = dto.CostOil,
            CostAlcohol = dto.CostAlcohol,
            CostPackaging = dto.CostPackaging,
            BadgeArabic = dto.BadgeArabic, BadgeEnglish = dto.BadgeEnglish, BadgeKind = dto.BadgeKind,
            TickerLine1Ar = dto.TickerLine1Ar, TickerLine1En = dto.TickerLine1En,
            TickerLine2Ar = dto.TickerLine2Ar, TickerLine2En = dto.TickerLine2En,
            TickerLine3Ar = dto.TickerLine3Ar, TickerLine3En = dto.TickerLine3En,
            TickerLine4Ar = dto.TickerLine4Ar, TickerLine4En = dto.TickerLine4En,
            TickerLine5Ar = dto.TickerLine5Ar, TickerLine5En = dto.TickerLine5En,
            TickerLine6Ar = dto.TickerLine6Ar, TickerLine6En = dto.TickerLine6En,
            TickerJson = dto.TickerJson,
            Sizes = NormalizeSizes(dto.Sizes),
        };

        AutoUpdateStatusFromStock(product);

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Product, "CREATE_PRODUCT",
            $"أضاف منتج: {product.Name}",
            entityName: nameof(Product), entityId: product.Id.ToString(),
            after: new { product.Name, product.Status, Sizes = product.Sizes.Select(s => new { s.Volume, s.Price, s.Stock }) }, ct: ct);

        return MapDetail(product);
    }

    public async Task<ProductDetailDto> UpdateAsync(Guid id, ProductUpdateDto dto, CancellationToken ct = default)
    {
        // Load the product WITHOUT .Include(p => p.Sizes) — keeping the old sizes out
        // of the change tracker avoids the orphan-tracking problem that was causing
        // SaveChanges to emit UPDATE statements with stale Guids (DbUpdateConcurrencyException).
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        // Snapshot the existing sizes for audit BEFORE we delete them — read-only, untracked.
        var oldSizesSnapshot = await _db.ProductSizes
            .AsNoTracking()
            .Where(s => s.ProductId == id)
            .Select(s => new { s.Volume, s.Price, s.Stock })
            .ToListAsync(ct);

        var before = new { product.Name, product.Status, Sizes = oldSizesSnapshot };

        // PHASE 1 — bulk-delete the old sizes via raw SQL.
        // Single DELETE WHERE ProductId=... bypasses the change tracker entirely so EF
        // can't confuse old rows with new ones at SaveChanges time.
        await _db.ProductSizes.Where(s => s.ProductId == id).ExecuteDeleteAsync(ct);

        // PHASE 2: write parent fields + insert freshly-built sizes (each gets a NEW Guid).
        product.Name = dto.Name;
        product.NameEn = dto.NameEn;
        product.InspiredBy = dto.InspiredBy;
        product.InspiredByEn = dto.InspiredByEn;
        product.Category = dto.Category;
        product.Status = dto.Status;
        product.ImageUrl = dto.ImageUrl;
        product.ImageUrl2 = dto.ImageUrl2;
        product.ImageUrl3 = dto.ImageUrl3;
        product.NotesTop = dto.NotesTop;
        product.NotesTopEn = dto.NotesTopEn;
        product.NotesHeart = dto.NotesHeart;
        product.NotesHeartEn = dto.NotesHeartEn;
        product.NotesBase = dto.NotesBase;
        product.NotesBaseEn = dto.NotesBaseEn;
        product.Description = dto.Description;
        product.DescriptionEn = dto.DescriptionEn;
        product.PerformanceAr = dto.PerformanceAr;
        product.PerformanceEn = dto.PerformanceEn;
        product.CostOil = dto.CostOil;
        product.CostAlcohol = dto.CostAlcohol;
        product.CostPackaging = dto.CostPackaging;
        product.BadgeArabic = dto.BadgeArabic; product.BadgeEnglish = dto.BadgeEnglish; product.BadgeKind = dto.BadgeKind;
        product.TickerLine1Ar = dto.TickerLine1Ar; product.TickerLine1En = dto.TickerLine1En;
        product.TickerLine2Ar = dto.TickerLine2Ar; product.TickerLine2En = dto.TickerLine2En;
        product.TickerLine3Ar = dto.TickerLine3Ar; product.TickerLine3En = dto.TickerLine3En;
        product.TickerLine4Ar = dto.TickerLine4Ar; product.TickerLine4En = dto.TickerLine4En;
        product.TickerLine5Ar = dto.TickerLine5Ar; product.TickerLine5En = dto.TickerLine5En;
        product.TickerLine6Ar = dto.TickerLine6Ar; product.TickerLine6En = dto.TickerLine6En;
        product.TickerJson = dto.TickerJson;

        var newSizes = NormalizeSizes(dto.Sizes);
        foreach (var s in newSizes)
            s.ProductId = product.Id;
        _db.ProductSizes.AddRange(newSizes);

        // Sync the in-memory nav collection so AutoUpdateStatusFromStock + MapDetail see the new rows.
        product.Sizes = newSizes;
        AutoUpdateStatusFromStock(product);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Product, "UPDATE_PRODUCT",
            $"عدّل منتج: {product.Name}",
            entityName: nameof(Product), entityId: product.Id.ToString(),
            before: before,
            after: new { product.Name, product.Status, Sizes = product.Sizes.Select(s => new { s.Volume, s.Price, s.Stock }) }, ct: ct);

        return MapDetail(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Product, "DELETE_PRODUCT",
            $"حذف منتج: {product.Name}",
            entityName: nameof(Product), entityId: id.ToString(), ct: ct);
    }

    public async Task<ProductDetailDto> AdjustStockAsync(Guid id, ProductStockBulkAdjustDto dto, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Sizes)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        var before = product.Sizes.Select(s => new { s.Volume, s.Stock }).ToList();
        var changes = new List<string>();

        foreach (var adj in dto.Adjustments)
        {
            var size = product.Sizes.FirstOrDefault(s => s.Volume == adj.Volume);
            if (size == null) continue;
            if (size.Stock != adj.NewStock)
            {
                var diff = adj.NewStock - size.Stock;
                changes.Add($"{adj.Volume}: {(diff >= 0 ? "+" : "")}{diff}");
                size.Stock = adj.NewStock;
            }
        }

        AutoUpdateStatusFromStock(product);
        await _db.SaveChangesAsync(ct);

        if (changes.Count > 0)
        {
            var note = string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" ({dto.Reason})";
            await _audit.LogAsync(AuditCategory.Inventory, "STOCK_CHANGE",
                $"{product.Name} — {string.Join("، ", changes)}{note}",
                entityName: nameof(Product), entityId: id.ToString(),
                before: before,
                after: product.Sizes.Select(s => new { s.Volume, s.Stock }), ct: ct);

            // Realtime: notify the dashboard of any size now under the low-stock threshold (< 5)
            foreach (var adj in dto.Adjustments)
            {
                var size = product.Sizes.FirstOrDefault(s => s.Volume == adj.Volume);
                if (size != null && size.Stock < 5)
                    await _notifier.LowStockAsync(new LowStockNotification(
                        product.Id, product.Name, size.Volume, size.Stock), ct);
            }
        }

        return MapDetail(product);
    }

    // ----------------- Helpers -----------------

    private static List<ProductSize> NormalizeSizes(IEnumerable<ProductSizeWriteDto> input)
    {
        var dict = input.ToDictionary(s => s.Volume.ToUpperInvariant(), s => s);
        var sizes = new List<ProductSize>();
        foreach (var vol in StandardSizes)
        {
            if (dict.TryGetValue(vol, out var s))
                sizes.Add(new ProductSize { Volume = vol, Price = s.Price, Stock = s.Stock, OldPrice = (s.OldPrice > s.Price ? s.OldPrice : null) });
            else
                sizes.Add(new ProductSize { Volume = vol, Price = 0, Stock = 0 });
        }
        return sizes;
    }

    private static void AutoUpdateStatusFromStock(Product product)
    {
        var totalStock = product.Sizes.Sum(s => s.Stock);
        if (totalStock == 0 && product.Status == ProductStatus.Active)
            product.Status = ProductStatus.OutOfStock;
        else if (totalStock > 0 && product.Status == ProductStatus.OutOfStock)
            product.Status = ProductStatus.Active;
    }

    private static ProductListDto MapList(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        NameEn = p.NameEn,
        InspiredBy = p.InspiredBy,
        InspiredByEn = p.InspiredByEn,
        Category = p.Category,
        Status = p.Status,
        ImageUrl = p.ImageUrl,
        ImageUrl2 = p.ImageUrl2,
        ImageUrl3 = p.ImageUrl3,
        Sold = p.Sold,
        Rating = p.Rating,
        ReviewCount = p.ReviewCount,
        TotalStock = p.Sizes.Sum(s => s.Stock),
        MinPrice = p.Sizes.Any() ? p.Sizes.Min(s => s.Price) : 0,
        MaxPrice = p.Sizes.Any() ? p.Sizes.Max(s => s.Price) : 0,
        Sizes = p.Sizes.Select(s => new ProductSizeDto(s.Id, s.Volume, s.Price, s.Stock, s.OldPrice)).ToList(),
        CreatedAt = p.CreatedAt,
        CostOil = p.CostOil,
        CostAlcohol = p.CostAlcohol,
        CostPackaging = p.CostPackaging,
        BadgeArabic = p.BadgeArabic, BadgeEnglish = p.BadgeEnglish, BadgeKind = p.BadgeKind,
        TickerLine1Ar = p.TickerLine1Ar, TickerLine1En = p.TickerLine1En,
        TickerLine2Ar = p.TickerLine2Ar, TickerLine2En = p.TickerLine2En,
        TickerLine3Ar = p.TickerLine3Ar, TickerLine3En = p.TickerLine3En,
        TickerLine4Ar = p.TickerLine4Ar, TickerLine4En = p.TickerLine4En,
        TickerLine5Ar = p.TickerLine5Ar, TickerLine5En = p.TickerLine5En,
        TickerLine6Ar = p.TickerLine6Ar, TickerLine6En = p.TickerLine6En,
        TickerJson = p.TickerJson,
        PerformanceAr = p.PerformanceAr,
        PerformanceEn = p.PerformanceEn,
    };

    private static ProductDetailDto MapDetail(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        NameEn = p.NameEn,
        InspiredBy = p.InspiredBy,
        InspiredByEn = p.InspiredByEn,
        Category = p.Category,
        Status = p.Status,
        ImageUrl = p.ImageUrl,
        ImageUrl2 = p.ImageUrl2,
        ImageUrl3 = p.ImageUrl3,
        Sold = p.Sold,
        Rating = p.Rating,
        ReviewCount = p.ReviewCount,
        TotalStock = p.Sizes.Sum(s => s.Stock),
        MinPrice = p.Sizes.Any() ? p.Sizes.Min(s => s.Price) : 0,
        MaxPrice = p.Sizes.Any() ? p.Sizes.Max(s => s.Price) : 0,
        Sizes = p.Sizes.Select(s => new ProductSizeDto(s.Id, s.Volume, s.Price, s.Stock, s.OldPrice)).ToList(),
        CreatedAt = p.CreatedAt,
        NotesTop = p.NotesTop,
        NotesTopEn = p.NotesTopEn,
        NotesHeart = p.NotesHeart,
        NotesHeartEn = p.NotesHeartEn,
        NotesBase = p.NotesBase,
        NotesBaseEn = p.NotesBaseEn,
        Description = p.Description,
        DescriptionEn = p.DescriptionEn,
        CostOil = p.CostOil,
        CostAlcohol = p.CostAlcohol,
        CostPackaging = p.CostPackaging,
        BadgeArabic = p.BadgeArabic, BadgeEnglish = p.BadgeEnglish, BadgeKind = p.BadgeKind,
        TickerLine1Ar = p.TickerLine1Ar, TickerLine1En = p.TickerLine1En,
        TickerLine2Ar = p.TickerLine2Ar, TickerLine2En = p.TickerLine2En,
        TickerLine3Ar = p.TickerLine3Ar, TickerLine3En = p.TickerLine3En,
        TickerLine4Ar = p.TickerLine4Ar, TickerLine4En = p.TickerLine4En,
        TickerLine5Ar = p.TickerLine5Ar, TickerLine5En = p.TickerLine5En,
        TickerLine6Ar = p.TickerLine6Ar, TickerLine6En = p.TickerLine6En,
        TickerJson = p.TickerJson,
        PerformanceAr = p.PerformanceAr,
        PerformanceEn = p.PerformanceEn,
    };
}
