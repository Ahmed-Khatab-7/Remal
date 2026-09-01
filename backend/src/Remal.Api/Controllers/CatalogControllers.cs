using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Remal.Application.Common.Models;
using Remal.Application.Features.Bundles;
using Remal.Application.Features.Bundles.Dtos;
using Remal.Application.Features.Collections;
using Remal.Application.Features.Collections.Dtos;
using Remal.Application.Features.Coupons;
using Remal.Application.Features.Coupons.Dtos;
using Remal.Application.Features.Customers;
using Remal.Application.Features.Customers.Dtos;
using Remal.Application.Features.Reviews;
using Remal.Application.Features.Reviews.Dtos;
using Remal.Domain.Enums;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/bundles")]
public class BundlesController : ControllerBase
{
    private readonly IBundleService _svc;
    public BundlesController(IBundleService svc) => _svc = svc;

    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<BundleListDto>>>> List(int page = 1, int pageSize = 20, string? search = null, BundleStatus? status = null, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<BundleListDto>>.Ok(await _svc.GetListAsync(page, pageSize, search, status, ct)));

    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BundleListDto>>> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<BundleListDto>.Ok(await _svc.GetByIdAsync(id, ct)));

    [HttpPost, Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<BundleListDto>>> Create(BundleCreateDto dto, CancellationToken ct)
        => Ok(ApiResponse<BundleListDto>.Ok(await _svc.CreateAsync(dto, ct), "تم إضافة الباقة"));

    [HttpPut("{id:guid}"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<BundleListDto>>> Update(Guid id, BundleUpdateDto dto, CancellationToken ct)
        => Ok(ApiResponse<BundleListDto>.Ok(await _svc.UpdateAsync(id, dto, ct), "تم الحفظ"));

    [HttpDelete("{id:guid}"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }
}

[ApiController]
[Route("api/collections")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _svc;
    public CollectionsController(ICollectionService svc) => _svc = svc;

    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CollectionListDto>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<CollectionListDto>>.Ok(await _svc.GetAllAsync(ct)));

    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CollectionListDto>>> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<CollectionListDto>.Ok(await _svc.GetByIdAsync(id, ct)));

    [HttpPost, Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<CollectionListDto>>> Create(CollectionWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<CollectionListDto>.Ok(await _svc.CreateAsync(dto, ct), "تم الإضافة"));

    [HttpPut("{id:guid}"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<CollectionListDto>>> Update(Guid id, CollectionWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<CollectionListDto>.Ok(await _svc.UpdateAsync(id, dto, ct), "تم الحفظ"));

    [HttpDelete("{id:guid}"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }
}

[ApiController]
[Route("api/coupons")]
[Authorize(Policy = "Partner")]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _svc;
    public CouponsController(ICouponService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CouponDto>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<CouponDto>>.Ok(await _svc.GetAllAsync(ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CouponDto>>> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<CouponDto>.Ok(await _svc.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CouponDto>>> Create(CouponWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<CouponDto>.Ok(await _svc.CreateAsync(dto, ct), "تم الإضافة"));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CouponDto>>> Update(Guid id, CouponWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<CouponDto>.Ok(await _svc.UpdateAsync(id, dto, ct), "تم الحفظ"));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<ApiResponse<CouponDto>>> Toggle(Guid id, CancellationToken ct)
        => Ok(ApiResponse<CouponDto>.Ok(await _svc.ToggleAsync(id, ct)));

    [HttpPost("validate"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CouponValidationResult>>> Validate(CouponValidateDto dto, CancellationToken ct)
        => Ok(ApiResponse<CouponValidationResult>.Ok(await _svc.ValidateAsync(dto, ct)));
}

[ApiController]
[Route("api/customers")]
[Authorize(Policy = "Partner")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _svc;
    public CustomersController(ICustomerService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> List(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(await _svc.GetListAsync(page, pageSize, search, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<CustomerDto>.Ok(await _svc.GetByIdAsync(id, ct)));
}

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _svc;
    public ReviewsController(IReviewService svc) => _svc = svc;

    [HttpGet, Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<List<ReviewDto>>>> List(ReviewStatus? status, CancellationToken ct)
        => Ok(ApiResponse<List<ReviewDto>>.Ok(await _svc.GetAllAsync(status, ct)));

    [HttpGet("by-product/{productId:guid}"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ReviewDto>>>> ByProduct(Guid productId, CancellationToken ct)
        => Ok(ApiResponse<List<ReviewDto>>.Ok(await _svc.GetByProductAsync(productId, ct)));

    [HttpPost, AllowAnonymous]
    [EnableRateLimiting("public-write")] // M2 — منع إغراق المنتجات بمراجعات مزيّفة/سبام
    public async Task<ActionResult<ApiResponse<ReviewDto>>> Create(ReviewWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<ReviewDto>.Ok(await _svc.CreateAsync(dto, ct), "اتبعت تقييمك للمراجعة"));

    [HttpPost("{id:guid}/moderate"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> Moderate(Guid id, ReviewModerateDto dto, CancellationToken ct)
        => Ok(ApiResponse<ReviewDto>.Ok(await _svc.ModerateAsync(id, dto, ct)));

    [HttpDelete("{id:guid}"), Authorize(Policy = "Partner")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }
}
