using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Remal.Application.Common.Behaviors;
using Remal.Application.Features.Accounting;
using Remal.Application.Features.Audit;
using Remal.Application.Features.Bundles;
using Remal.Application.Features.Collections;
using Remal.Application.Features.Coupons;
using Remal.Application.Features.Customers;
using Remal.Application.Features.Orders;
using Remal.Application.Features.Products;
using Remal.Application.Features.Reports;
using Remal.Application.Features.Reviews;

namespace Remal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // MediatR + pipeline behaviors (Validation → Logging)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // AutoMapper
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        // Service-based handlers (kept for stability; new features use CQRS)
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IBundleService, BundleService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<Features.Promotions.IPromotionService, Features.Promotions.PromotionService>();

        return services;
    }
}
