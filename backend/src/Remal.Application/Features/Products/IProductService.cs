using Remal.Application.Common.Models;
using Remal.Application.Features.Products.Dtos;

namespace Remal.Application.Features.Products;

public interface IProductService
{
    Task<PagedResult<ProductListDto>> GetListAsync(ProductFilterDto filter, CancellationToken ct = default);
    Task<ProductDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDetailDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default);
    Task<ProductDetailDto> UpdateAsync(Guid id, ProductUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProductDetailDto> AdjustStockAsync(Guid id, ProductStockBulkAdjustDto dto, CancellationToken ct = default);
}
