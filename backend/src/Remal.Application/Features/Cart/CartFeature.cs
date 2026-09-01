using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;

namespace Remal.Application.Features.Cart;

// ---------- DTOs ----------
public record CartItemDto(Guid Id, Guid? ProductId, Guid? BundleId, Guid? CollectionId,
    string Name, string? Volume, string? ImageUrl, int Quantity, decimal UnitPrice, decimal LineTotal);

public record CartDto(IReadOnlyList<CartItemDto> Items, decimal Subtotal, int Count);

public record AddCartItemDto(Guid? ProductId, Guid? BundleId, Guid? CollectionId, string? Volume, int Quantity = 1);

public record UpdateCartItemDto(int Quantity);

// ---------- Get ----------
public record GetMyCartQuery(string UserId) : IRequest<CartDto>;

public class GetMyCartHandler : IRequestHandler<GetMyCartQuery, CartDto>
{
    private readonly IApplicationDbContext _db;
    public GetMyCartHandler(IApplicationDbContext db) => _db = db;

    public async Task<CartDto> Handle(GetMyCartQuery req, CancellationToken ct)
    {
        var items = await _db.CartItems.AsNoTracking()
            .Where(c => c.UserId == req.UserId)
            .Include(c => c.Product).ThenInclude(p => p!.Sizes)
            .Include(c => c.Bundle)
            .Include(c => c.Collection)
            .OrderByDescending(c => c.AddedAt)
            .ToListAsync(ct);

        var dtos = items.Select(CartFeatureHelpers.MapCartItem).ToList();
        var subtotal = dtos.Sum(i => i.LineTotal);
        return new CartDto(dtos, subtotal, dtos.Sum(i => i.Quantity));
    }
}

// ---------- Add ----------
public record AddToCartCommand(string UserId, AddCartItemDto Body) : IRequest<CartItemDto>;

public class AddToCartValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Body.Quantity).GreaterThan(0).LessThanOrEqualTo(20);
        RuleFor(x => x.Body)
            .Must(b => b.ProductId.HasValue || b.BundleId.HasValue || b.CollectionId.HasValue)
            .WithMessage("لازم تحدد منتج أو باقة أو مجموعة");
        RuleFor(x => x.Body.Volume)
            .NotEmpty().When(b => b.Body.ProductId.HasValue)
            .WithMessage("الحجم مطلوب للمنتج");
    }
}

public class AddToCartHandler : IRequestHandler<AddToCartCommand, CartItemDto>
{
    private readonly IApplicationDbContext _db;
    public AddToCartHandler(IApplicationDbContext db) => _db = db;

    public async Task<CartItemDto> Handle(AddToCartCommand req, CancellationToken ct)
    {
        var b = req.Body;
        CartItem? existing = null;

        if (b.ProductId.HasValue)
            existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == req.UserId && c.ProductId == b.ProductId && c.Volume == b.Volume, ct);
        else if (b.BundleId.HasValue)
            existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == req.UserId && c.BundleId == b.BundleId, ct);
        else if (b.CollectionId.HasValue)
            existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == req.UserId && c.CollectionId == b.CollectionId, ct);

        if (existing != null)
        {
            existing.Quantity += b.Quantity;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new CartItem
            {
                UserId = req.UserId,
                ProductId = b.ProductId,
                BundleId = b.BundleId,
                CollectionId = b.CollectionId,
                Volume = b.Volume,
                Quantity = b.Quantity,
            };
            _db.CartItems.Add(existing);
        }
        await _db.SaveChangesAsync(ct);

        // Reload with navigation for mapping
        var saved = await _db.CartItems.AsNoTracking()
            .Include(c => c.Product).ThenInclude(p => p!.Sizes)
            .Include(c => c.Bundle)
            .Include(c => c.Collection)
            .FirstAsync(c => c.Id == existing.Id, ct);

        return CartFeatureHelpers.MapCartItem(saved);
    }
}

// ---------- Update ----------
public record UpdateCartItemCommand(string UserId, Guid ItemId, int Quantity) : IRequest<CartItemDto>;

public class UpdateCartItemHandler : IRequestHandler<UpdateCartItemCommand, CartItemDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateCartItemHandler(IApplicationDbContext db) => _db = db;

    public async Task<CartItemDto> Handle(UpdateCartItemCommand req, CancellationToken ct)
    {
        var item = await _db.CartItems
            .Include(c => c.Product).ThenInclude(p => p!.Sizes)
            .Include(c => c.Bundle).Include(c => c.Collection)
            .FirstOrDefaultAsync(c => c.UserId == req.UserId && c.Id == req.ItemId, ct)
            ?? throw new NotFoundException("Cart item", req.ItemId);
        item.Quantity = Math.Max(1, req.Quantity);
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return CartFeatureHelpers.MapCartItem(item);
    }
}

// ---------- Remove ----------
public record RemoveCartItemCommand(string UserId, Guid ItemId) : IRequest;

public class RemoveCartItemHandler : IRequestHandler<RemoveCartItemCommand>
{
    private readonly IApplicationDbContext _db;
    public RemoveCartItemHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveCartItemCommand req, CancellationToken ct)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == req.UserId && c.Id == req.ItemId, ct);
        if (item is null) return;
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}

// ---------- Clear ----------
public record ClearCartCommand(string UserId) : IRequest;

public class ClearCartHandler : IRequestHandler<ClearCartCommand>
{
    private readonly IApplicationDbContext _db;
    public ClearCartHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ClearCartCommand req, CancellationToken ct)
    {
        var items = await _db.CartItems.Where(c => c.UserId == req.UserId).ToListAsync(ct);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync(ct);
    }
}

internal static class CartFeatureHelpers
{
    public static CartItemDto MapCartItem(CartItem c)
    {
        if (c.ProductId.HasValue && c.Product is not null)
        {
            var size = c.Product.Sizes?.FirstOrDefault(s => s.Volume == c.Volume) ?? c.Product.Sizes?.FirstOrDefault();
            var price = size?.Price ?? 0;
            return new CartItemDto(c.Id, c.ProductId, null, null, c.Product.Name, c.Volume, c.Product.ImageUrl, c.Quantity, price, price * c.Quantity);
        }
        if (c.BundleId.HasValue && c.Bundle is not null)
            return new CartItemDto(c.Id, null, c.BundleId, null, c.Bundle.Name, null, c.Bundle.ImageUrl, c.Quantity, c.Bundle.FinalPrice, c.Bundle.FinalPrice * c.Quantity);
        if (c.CollectionId.HasValue && c.Collection is not null)
            return new CartItemDto(c.Id, null, null, c.CollectionId, c.Collection.Name, c.Collection.SampleVolume, c.Collection.ImageUrl, c.Quantity, c.Collection.FinalPrice, c.Collection.FinalPrice * c.Quantity);
        return new CartItemDto(c.Id, null, null, null, "—", c.Volume, null, c.Quantity, 0, 0);
    }
}
