using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Cart;
using Remal.Application.Features.Loyalty;
using Remal.Application.Features.Public;
using Remal.Application.Features.Wishlist;

namespace Remal.Api.Controllers;

[ApiController, Route("api/wishlist"), Authorize, Tags("Wishlist"), Produces("application/json")]
public class WishlistController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    public WishlistController(IMediator m, ICurrentUserService u) { _mediator = m; _currentUser = u; }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<WishlistItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<WishlistItemDto>>>> GetMine(CancellationToken ct)
        => Ok(ApiResponse<List<WishlistItemDto>>.Ok(await _mediator.Send(new GetMyWishlistQuery(_currentUser.UserId!), ct)));

    [HttpPost("{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WishlistItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WishlistItemDto>>> Add(Guid productId, CancellationToken ct)
        => Ok(ApiResponse<WishlistItemDto>.Ok(await _mediator.Send(new AddToWishlistCommand(_currentUser.UserId!, productId), ct), "تم الإضافة للمفضلة"));

    [HttpDelete("{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Remove(Guid productId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveFromWishlistCommand(_currentUser.UserId!, productId), ct);
        return Ok(ApiResponse.Ok("اتشال من المفضلة"));
    }
}

[ApiController, Route("api/cart"), Authorize, Tags("Cart"), Produces("application/json")]
public class CartController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    public CartController(IMediator m, ICurrentUserService u) { _mediator = m; _currentUser = u; }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CartDto>>> GetMine(CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _mediator.Send(new GetMyCartQuery(_currentUser.UserId!), ct)));

    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CartItemDto>>> Add(AddCartItemDto body, CancellationToken ct)
        => Ok(ApiResponse<CartItemDto>.Ok(await _mediator.Send(new AddToCartCommand(_currentUser.UserId!, body), ct), "اتضاف للعربة"));

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<CartItemDto>>> Update(Guid itemId, UpdateCartItemDto body, CancellationToken ct)
        => Ok(ApiResponse<CartItemDto>.Ok(await _mediator.Send(new UpdateCartItemCommand(_currentUser.UserId!, itemId, body.Quantity), ct)));

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse>> Remove(Guid itemId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveCartItemCommand(_currentUser.UserId!, itemId), ct);
        return Ok(ApiResponse.Ok("اتشال"));
    }

    [HttpDelete]
    public async Task<ActionResult<ApiResponse>> Clear(CancellationToken ct)
    {
        await _mediator.Send(new ClearCartCommand(_currentUser.UserId!), ct);
        return Ok(ApiResponse.Ok("العربة فاضية دلوقت"));
    }
}

[ApiController, Route("api/loyalty"), Authorize, Tags("Loyalty"), Produces("application/json")]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    public LoyaltyController(IMediator m, ICurrentUserService u) { _mediator = m; _currentUser = u; }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(ApiResponse<LoyaltyBalanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LoyaltyBalanceDto>>> Balance(CancellationToken ct)
        => Ok(ApiResponse<LoyaltyBalanceDto>.Ok(await _mediator.Send(new GetMyLoyaltyQuery(_currentUser.UserId!), ct)));

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(ApiResponse<List<PointsTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<PointsTransactionDto>>>> Transactions(int page = 1, int pageSize = 50, CancellationToken ct = default)
        => Ok(ApiResponse<List<PointsTransactionDto>>.Ok(await _mediator.Send(new GetMyLoyaltyTransactionsQuery(_currentUser.UserId!, page, pageSize), ct)));
}

[ApiController, Route("api"), Tags("Public"), Produces("application/json")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;
    public PublicController(IMediator m) { _mediator = m; }

    [HttpGet("settings/public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicSettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PublicSettingsDto>>> GetPublicSettings(CancellationToken ct)
        => Ok(ApiResponse<PublicSettingsDto>.Ok(await _mediator.Send(new GetPublicSettingsQuery(), ct)));

    [HttpGet("products/featured")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<FeaturedSectionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<FeaturedSectionDto>>>> GetFeatured(CancellationToken ct)
        => Ok(ApiResponse<List<FeaturedSectionDto>>.Ok(await _mediator.Send(new GetFeaturedHomepageQuery(), ct)));

    [HttpGet("products/{id:guid}/related")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<Guid>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Guid>>>> GetRelated(Guid id, int take = 4, CancellationToken ct = default)
        => Ok(ApiResponse<List<Guid>>.Ok(await _mediator.Send(new GetRelatedProductsQuery(id, take), ct)));

    [HttpPost("newsletter/subscribe")]
    [AllowAnonymous]
    [EnableRateLimiting("public-write")] // M2 — منع إغراق النشرة باشتراكات مزيّفة
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> SubscribeNewsletter([FromBody] SubscribeNewsletterRequest req, CancellationToken ct)
    {
        await _mediator.Send(new SubscribeNewsletterCommand(req.Email, req.Source), ct);
        return Ok(ApiResponse.Ok("تم الاشتراك في النشرة 🤍"));
    }

    [HttpPost("contact")]
    [AllowAnonymous]
    [EnableRateLimiting("public-write")] // M2 — منع سبام رسائل تواصل معنا
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Contact(ContactMessageDto dto, CancellationToken ct)
    {
        await _mediator.Send(new SendContactMessageCommand(dto), ct);
        return Ok(ApiResponse.Ok("وصلتنا رسالتك — هنرد عليك في أسرع وقت"));
    }
}

[ApiController, Route("api/admin/settings"), Tags("Admin Settings"), Produces("application/json")]
[Authorize(Policy = "Partner")]
public class AdminSettingsController : ControllerBase
{
    private readonly Remal.Application.Common.Interfaces.IApplicationDbContext _db;
    private readonly Remal.Application.Common.Interfaces.ICacheService _cache;
    public AdminSettingsController(Remal.Application.Common.Interfaces.IApplicationDbContext db, Remal.Application.Common.Interfaces.ICacheService cache)
    { _db = db; _cache = cache; }

    /// <summary>
    /// المفاتيح اللي قيمتها سرّية — بترجع مقنّعة حتى للأدمن، فما تظهرش في شاشة
    /// ولا في لوج متصفح. اللوحة محتاجة تعرف إن فيه قيمة محفوظة بس، مش القيمة نفسها.
    /// </summary>
    private static readonly HashSet<string> MaskedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "meta_capi_token", "tiktok_events_token",
        // توكن بوت تليجرام: أي حد يوصله يقدر يبعت رسائل باسم البوت
        "telegram_bot_token",
    };

    /// <summary>Admin: list ALL app settings (for the dashboard settings page).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AppSettingKvDto>>>> All(CancellationToken ct)
    {
        var list = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(_db.AppSettings.AsQueryable(), ct);
        var dtos = list.Select(s => new AppSettingKvDto(
            s.Key,
            MaskedKeys.Contains(s.Key) ? (string.IsNullOrWhiteSpace(s.Value) ? "" : "••••••") : s.Value,
            s.Description,
            s.DataType)).ToList();
        return Ok(ApiResponse<List<AppSettingKvDto>>.Ok(dtos));
    }

    /// <summary>Admin: upsert a batch of {key,value} settings. Invalidates the public-settings cache.</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse>> Upsert([FromBody] List<AppSettingKvWriteDto> items, CancellationToken ct)
    {
        if (items == null || items.Count == 0) return Ok(ApiResponse.Ok("لا يوجد ما يتم حفظه"));
        var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(_db.AppSettings.AsQueryable(), ct);
        foreach (var i in items)
        {
            if (string.IsNullOrWhiteSpace(i.Key)) continue;
            var match = existing.FirstOrDefault(e => e.Key == i.Key);
            if (match == null)
            {
                _db.AppSettings.Add(new Remal.Domain.Entities.AppSettingItem
                { Key = i.Key.Trim(), Value = i.Value, Description = i.Description, DataType = string.IsNullOrWhiteSpace(i.DataType) ? "string" : i.DataType! });
            }
            else
            {
                match.Value = i.Value;
                if (i.Description != null) match.Description = i.Description;
                if (!string.IsNullOrWhiteSpace(i.DataType)) match.DataType = i.DataType!;
            }
        }
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(Remal.Application.Common.Interfaces.CacheKeys.PublicSettings, ct);
        return Ok(ApiResponse.Ok("تم الحفظ"));
    }
}

public record AppSettingKvDto(string Key, string? Value, string? Description, string DataType);
public record AppSettingKvWriteDto
{
    public string Key { get; init; } = null!;
    public string? Value { get; init; }
    public string? Description { get; init; }
    public string? DataType { get; init; }
}

public record SubscribeNewsletterRequest(string Email, string? Source = null);
