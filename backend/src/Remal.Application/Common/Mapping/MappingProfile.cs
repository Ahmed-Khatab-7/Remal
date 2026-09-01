using AutoMapper;
using Remal.Application.Features.Accounting.Dtos;
using Remal.Application.Features.Audit.Dtos;
using Remal.Application.Features.Auth.Dtos;
using Remal.Application.Features.Bundles.Dtos;
using Remal.Application.Features.Collections.Dtos;
using Remal.Application.Features.Coupons.Dtos;
using Remal.Application.Features.Customers.Dtos;
using Remal.Application.Features.Orders.Dtos;
using Remal.Application.Features.Products.Dtos;
using Remal.Application.Features.Reviews.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Identity;

namespace Remal.Application.Common.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Products
        CreateMap<ProductSize, ProductSizeDto>()
            .ConstructUsing(s => new ProductSizeDto(s.Id, s.Volume, s.Price, s.Stock, s.OldPrice));
        CreateMap<Product, ProductListDto>()
            .ForMember(d => d.TotalStock, o => o.MapFrom(s => s.Sizes.Sum(x => x.Stock)))
            .ForMember(d => d.MinPrice, o => o.MapFrom(s => s.Sizes.Min(x => (decimal?)x.Price) ?? 0))
            .ForMember(d => d.MaxPrice, o => o.MapFrom(s => s.Sizes.Max(x => (decimal?)x.Price) ?? 0));
        CreateMap<Product, ProductDetailDto>()
            .IncludeBase<Product, ProductListDto>();

        // Bundles
        CreateMap<Bundle, BundleListDto>()
            .ForMember(d => d.Savings, o => o.MapFrom(s => s.Savings));

        // Collections
        CreateMap<Collection, CollectionListDto>();
        CreateMap<CollectionItem, CollectionItemDto>()
            .ConstructUsing(c => new CollectionItemDto(c.Id, c.ProductId, c.Product != null ? c.Product.Name : "", c.Product != null ? c.Product.NameEn : null, c.Product != null ? c.Product.ImageUrl : null, c.Order));

        // Customers
        CreateMap<Customer, CustomerDto>();

        // Coupons
        CreateMap<Coupon, CouponDto>()
            .ForMember(d => d.IsExpired, o => o.MapFrom(s => s.IsExpired));

        // Reviews
        CreateMap<Review, ReviewDto>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product != null ? s.Product.Name : ""))
            .ForMember(d => d.ProductImageUrl, o => o.MapFrom(s => s.Product != null ? s.Product.ImageUrl : null));

        // Orders
        CreateMap<Order, OrderListDto>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Sum(i => i.Quantity)));
        CreateMap<Order, OrderDetailDto>()
            .IncludeBase<Order, OrderListDto>();
        CreateMap<OrderItem, OrderItemDto>()
            .ConstructUsing((i, _) => new OrderItemDto(i.Id, i.ProductId, i.BundleId, i.CollectionId, i.ItemName, i.Volume, i.Quantity, i.UnitPrice, i.UnitPrice * i.Quantity, i.Product != null ? i.Product.ImageUrl : null));

        // Accounting
        CreateMap<Expense, ExpenseDto>()
            .ForMember(d => d.PaidByName, o => o.MapFrom(s => s.PaidBy != null ? s.PaidBy.FullName : "—"));

        // Audit
        CreateMap<AuditLog, AuditLogDto>();

        // Identity
        CreateMap<ApplicationUser, UserProfileDto>();
    }
}
