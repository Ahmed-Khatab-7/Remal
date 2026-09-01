using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remal.Application.Common.Models;
using Remal.Application.Features.Products;
using Remal.Application.Features.Products.Dtos;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    /// <summary>Public list — used by storefront and dashboard.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductListDto>>>> List([FromQuery] ProductFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<ProductListDto>>.Ok(await _service.GetListAsync(filter, ct)));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPost]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Create(ProductCreateDto dto, CancellationToken ct)
    {
        var data = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = data.Id }, ApiResponse<ProductDetailDto>.Ok(data, "تم إضافة المنتج"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Update(Guid id, ProductUpdateDto dto, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.UpdateAsync(id, dto, ct), "تم الحفظ"));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }

    [HttpPost("{id:guid}/stock")]
    [Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> AdjustStock(Guid id, ProductStockBulkAdjustDto dto, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.AdjustStockAsync(id, dto, ct), "تم تحديث المخزون"));
}
