using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Application.Features.Reviews.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Reviews;

public interface IReviewService
{
    Task<List<ReviewDto>> GetAllAsync(ReviewStatus? status = null, CancellationToken ct = default);
    Task<List<ReviewDto>> GetByProductAsync(Guid productId, CancellationToken ct = default);
    Task<ReviewDto> CreateAsync(ReviewWriteDto dto, CancellationToken ct = default);
    Task<ReviewDto> ModerateAsync(Guid id, ReviewModerateDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class ReviewService : IReviewService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly IDashboardNotifier _notifier;

    public ReviewService(IApplicationDbContext db, IAuditService audit, ICurrentUserService currentUser, IDashboardNotifier notifier)
    {
        _db = db; _audit = audit; _currentUser = currentUser; _notifier = notifier;
    }

    public async Task<List<ReviewDto>> GetAllAsync(ReviewStatus? status = null, CancellationToken ct = default)
    {
        var q = _db.Reviews.AsNoTracking().Include(r => r.Product).AsQueryable();
        if (status.HasValue) q = q.Where(r => r.Status == status);
        return await q.OrderByDescending(r => r.CreatedAt).Select(r => Map(r)).ToListAsync(ct);
    }

    public async Task<List<ReviewDto>> GetByProductAsync(Guid productId, CancellationToken ct = default) =>
        await _db.Reviews.AsNoTracking().Include(r => r.Product)
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedAt).Select(r => Map(r)).ToListAsync(ct);

    public async Task<ReviewDto> CreateAsync(ReviewWriteDto dto, CancellationToken ct = default)
    {
        if (dto.Rating < 1 || dto.Rating > 5) throw new BadRequestException("التقييم لازم بين ١ و ٥");
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId, ct)
            ?? throw new NotFoundException("Product", dto.ProductId);

        var review = new Review
        {
            ProductId = dto.ProductId,
            OrderId = dto.OrderId,
            CustomerName = dto.CustomerName,
            Rating = dto.Rating,
            Text = dto.Text,
            Status = ReviewStatus.Pending,
            IsVerifiedPurchase = dto.OrderId.HasValue,
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Review, "CREATE_REVIEW",
            $"تقييم جديد على {product.Name} ({dto.Rating}★)", entityId: review.Id.ToString(), ct: ct);

        // Realtime: notify the dashboard of the new (pending) review
        await _notifier.NewReviewAsync(new NewReviewNotification(
            review.Id, product.Name, dto.CustomerName, dto.Rating, review.CreatedAt), ct);

        return await Get(review.Id, ct);
    }

    public async Task<ReviewDto> ModerateAsync(Guid id, ReviewModerateDto dto, CancellationToken ct = default)
    {
        var r = await _db.Reviews.Include(r => r.Product).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Review", id);
        r.Status = dto.Status;
        r.ModeratedAt = DateTime.UtcNow;
        r.ModeratedById = _currentUser.UserId;
        r.ModerationNote = dto.Note;
        await _db.SaveChangesAsync(ct);

        // Recalc product rating
        if (dto.Status == ReviewStatus.Approved)
            await RecalcProductRating(r.ProductId, ct);

        await _audit.LogAsync(AuditCategory.Review, $"REVIEW_{dto.Status}",
            $"{(dto.Status == ReviewStatus.Approved ? "قبل" : "رفض")} تقييم من {r.CustomerName}", entityId: id.ToString(), ct: ct);

        return Map(r);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review", id);
        _db.Reviews.Remove(r);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditCategory.Review, "DELETE_REVIEW", $"حذف تقييم {r.CustomerName}", entityId: id.ToString(), ct: ct);
    }

    private async Task RecalcProductRating(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null) return;
        var approved = await _db.Reviews.Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved).ToListAsync(ct);
        product.ReviewCount = approved.Count;
        product.Rating = approved.Count > 0 ? Math.Round((decimal)approved.Average(r => r.Rating), 1) : 0;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ReviewDto> Get(Guid id, CancellationToken ct)
    {
        var r = await _db.Reviews.Include(r => r.Product).FirstAsync(x => x.Id == id, ct);
        return Map(r);
    }

    private static ReviewDto Map(Review r) => new()
    {
        Id = r.Id, ProductId = r.ProductId, ProductName = r.Product?.Name ?? "",
        ProductImageUrl = r.Product?.ImageUrl, CustomerName = r.CustomerName,
        Rating = r.Rating, Text = r.Text, Status = r.Status,
        IsVerifiedPurchase = r.IsVerifiedPurchase, CreatedAt = r.CreatedAt,
    };
}
