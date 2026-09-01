using FluentValidation;
using Remal.Application.Features.Accounting.Dtos;
using Remal.Application.Features.Auth.Dtos;
using Remal.Application.Features.Bundles.Dtos;
using Remal.Application.Features.Coupons.Dtos;
using Remal.Application.Features.Customers.Dtos;
using Remal.Application.Features.Orders.Dtos;
using Remal.Application.Features.Products.Dtos;
using Remal.Application.Features.Reviews.Dtos;

namespace Remal.Application.Validators;

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ProductCreateValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Sizes).NotEmpty().WithMessage("لازم تحدد المقاسات (٣٠/٥٠/١٠٠ مل)");
        RuleForEach(x => x.Sizes).ChildRules(s =>
        {
            s.RuleFor(x => x.Volume).Must(v => v == "30ML" || v == "50ML" || v == "100ML")
                .WithMessage("المقاس لازم يكون 30ML أو 50ML أو 100ML");
            s.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            s.RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        });
    }
}

public class BundleCreateValidator : AbstractValidator<BundleCreateDto>
{
    public BundleCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.OriginalPrice).GreaterThan(0);
        RuleFor(x => x.FinalPrice).GreaterThan(0).LessThanOrEqualTo(x => x.OriginalPrice);
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count >= 2).WithMessage("الباقة لازم تحتوي على منتجين على الأقل");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}

public class CollectionWriteValidator : AbstractValidator<Features.Collections.Dtos.CollectionWriteDto>
{
    public CollectionWriteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.OriginalPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FinalPrice).GreaterThan(0);
    }
}

public class OrderCreateValidator : AbstractValidator<OrderCreateDto>
{
    public OrderCreateValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.CustomerPhone).NotEmpty().MinimumLength(10).MaximumLength(20);
        RuleFor(x => x.CustomerAddress).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("الطلب لازم يحتوي على منتجات");
    }
}

public class CustomerWriteValidator : AbstractValidator<CustomerWriteDto>
{
    public CustomerWriteValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty().MinimumLength(10);
    }
}

public class CouponWriteValidator : AbstractValidator<CouponWriteDto>
{
    public CouponWriteValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Z0-9_-]{3,30}$");
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.MaxUses).GreaterThan(0);
    }
}

public class ReviewWriteValidator : AbstractValidator<ReviewWriteDto>
{
    public ReviewWriteValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class ExpenseWriteValidator : AbstractValidator<ExpenseWriteDto>
{
    public ExpenseWriteValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaidById).NotEmpty();
    }
}

public class SettlementWriteValidator : AbstractValidator<SettlementWriteDto>
{
    public SettlementWriteValidator()
    {
        RuleFor(x => x.FromUserId).NotEmpty();
        RuleFor(x => x.ToUserId).NotEmpty().NotEqual(x => x.FromUserId)
            .WithMessage("الشريك لازم يحوّل لشريك تاني");
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
