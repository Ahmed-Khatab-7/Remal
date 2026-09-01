using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;
using Remal.Domain.Enums;

namespace Remal.Application.Features.Wishlist;

// ---------- DTOs ----------
public record WishlistItemDto(Guid Id, Guid ProductId, string ProductName, string? ImageUrl, decimal MinPrice, DateTime AddedAt);

// ---------- Queries ----------
public record GetMyWishlistQuery(string UserId) : IRequest<List<WishlistItemDto>>;

public class GetMyWishlistHandler : IRequestHandler<GetMyWishlistQuery, List<WishlistItemDto>>
{
    private readonly IApplicationDbContext _db;
    public GetMyWishlistHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<WishlistItemDto>> Handle(GetMyWishlistQuery req, CancellationToken ct)
    {
        return await _db.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == req.UserId)
            .Include(w => w.Product).ThenInclude(p => p.Sizes)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => new WishlistItemDto(
                w.Id, w.ProductId, w.Product.Name, w.Product.ImageUrl,
                w.Product.Sizes.Min(s => (decimal?)s.Price) ?? 0,
                w.AddedAt))
            .ToListAsync(ct);
    }
}

// ---------- Commands ----------
public record AddToWishlistCommand(string UserId, Guid ProductId) : IRequest<WishlistItemDto>;

public class AddToWishlistValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class AddToWishlistHandler : IRequestHandler<AddToWishlistCommand, WishlistItemDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AddToWishlistHandler(IApplicationDbContext db, IAuditService audit)
    { _db = db; _audit = audit; }

    public async Task<WishlistItemDto> Handle(AddToWishlistCommand req, CancellationToken ct)
    {
        var product = await _db.Products.Include(p => p.Sizes).FirstOrDefaultAsync(p => p.Id == req.ProductId, ct)
            ?? throw new NotFoundException("Product", req.ProductId);

        var existing = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == req.UserId && w.ProductId == req.ProductId, ct);
        if (existing != null)
            return new WishlistItemDto(existing.Id, existing.ProductId, product.Name, product.ImageUrl,
                product.Sizes.Min(s => (decimal?)s.Price) ?? 0, existing.AddedAt);

        var item = new WishlistItem { UserId = req.UserId, ProductId = req.ProductId };
        _db.WishlistItems.Add(item);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditCategory.Customer, "WISHLIST_ADD", $"عميل أضاف {product.Name} للمفضلة", ct: ct);
        return new WishlistItemDto(item.Id, item.ProductId, product.Name, product.ImageUrl,
            product.Sizes.Min(s => (decimal?)s.Price) ?? 0, item.AddedAt);
    }
}

public record RemoveFromWishlistCommand(string UserId, Guid ProductId) : IRequest;

public class RemoveFromWishlistHandler : IRequestHandler<RemoveFromWishlistCommand>
{
    private readonly IApplicationDbContext _db;
    public RemoveFromWishlistHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveFromWishlistCommand req, CancellationToken ct)
    {
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == req.UserId && w.ProductId == req.ProductId, ct);
        if (item is null) return;
        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
