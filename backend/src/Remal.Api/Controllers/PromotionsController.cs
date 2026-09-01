using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remal.Application.Common.Models;
using Remal.Application.Features.Promotions;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/promotions")]
[Produces("application/json")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _svc;
    public PromotionsController(IPromotionService svc) => _svc = svc;

    /// <summary>Public: active offers for the storefront.</summary>
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<PromotionDto>>>> Active(CancellationToken ct)
        => Ok(ApiResponse<List<PromotionDto>>.Ok(await _svc.GetActiveAsync(ct)));

    /// <summary>Admin: all offers.</summary>
    [HttpGet]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<List<PromotionDto>>>> All(CancellationToken ct)
        => Ok(ApiResponse<List<PromotionDto>>.Ok(await _svc.GetAllAsync(ct)));

    [HttpPost]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<PromotionDto>>> Create(PromotionWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<PromotionDto>.Ok(await _svc.CreateAsync(dto, ct), "تم إضافة العرض"));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<PromotionDto>>> Update(Guid id, PromotionWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<PromotionDto>.Ok(await _svc.UpdateAsync(id, dto, ct), "تم الحفظ"));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }
}
