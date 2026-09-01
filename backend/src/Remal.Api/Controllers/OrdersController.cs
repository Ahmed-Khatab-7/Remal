using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remal.Application.Common.Models;
using Remal.Application.Features.Orders;
using Remal.Application.Features.Orders.Dtos;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _svc;
    public OrdersController(IOrderService svc) => _svc = svc;

    /// <summary>List orders (Partners only).</summary>
    [HttpGet, Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderListDto>>>> List([FromQuery] OrderFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<OrderListDto>>.Ok(await _svc.GetListAsync(filter, ct)));

    /// <summary>Order details (Partners).</summary>
    [HttpGet("{id:guid}"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<OrderDetailDto>>> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<OrderDetailDto>.Ok(await _svc.GetByIdAsync(id, ct)));

    /// <summary>Public lookup by order code (used by storefront tracking page).</summary>
    [HttpGet("by-code/{code}"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderDetailDto>>> GetByCode(string code, CancellationToken ct)
        => Ok(ApiResponse<OrderDetailDto>.Ok(await _svc.GetByCodeAsync(code, ct)));

    /// <summary>Lightweight tracking — for the public tracking page.</summary>
    [HttpGet("track/{code}"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderTrackingDto>>> Track(string code, CancellationToken ct)
        => Ok(ApiResponse<OrderTrackingDto>.Ok(await _svc.TrackAsync(code, ct)));

    /// <summary>Place a new order (storefront).</summary>
    [HttpPost, AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderDetailDto>>> Place(OrderCreateDto dto, CancellationToken ct)
    {
        var result = await _svc.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse<OrderDetailDto>.Ok(result, "تم استلام طلبك"));
    }

    /// <summary>Update order status.</summary>
    [HttpPost("{id:guid}/status"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<OrderDetailDto>>> UpdateStatus(Guid id, OrderStatusUpdateDto dto, CancellationToken ct)
        => Ok(ApiResponse<OrderDetailDto>.Ok(await _svc.UpdateStatusAsync(id, dto, ct), "تم تحديث الحالة"));
}
