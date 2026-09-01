using Remal.Application.Common.Interfaces;
using Remal.Application.Features.Orders;
using Remal.Application.Features.Orders.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Infrastructure.Persistence;
using Xunit;

namespace Remal.Tests;

/// <summary>
/// البريد الإلكتروني اختياري في صفحة الدفع: لو اتكتب يتحفظ مع الطلب ويتبعت عليه
/// تأكيد الطلب، ولو اتساب فاضي الطلب يعدّي عادي من غير أي إيميل ولا خطأ.
/// </summary>
public class OrderEmailTests
{
    private sealed class SpyEmail : IEmailService
    {
        public List<(string To, string Name, string Code, decimal Total)> Sent { get; } = [];
        public bool Throw { get; set; }

        public Task SendOrderConfirmationAsync(string toEmail, string fullName, OrderEmailSummary order, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("سيرفر البريد واقع");
            Sent.Add((toEmail, fullName, order.OrderCode, order.Total));
            return Task.CompletedTask;
        }

        public Task SendEmailConfirmationAsync(string toEmail, string fullName, string url, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string toEmail, string fullName, string url, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendWelcomeAsync(string toEmail, string fullName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (ApplicationDbContext ctx, Guid pid) Seed()
    {
        var ctx = TestDb.New();
        var p = new Product { Name = "عطر", NameEn = "P", Status = ProductStatus.Active };
        p.Sizes.Add(new ProductSize { Volume = "50ML", Price = 700, Stock = 10 });
        ctx.Products.Add(p);
        ctx.AppSettings.Add(new AppSettingItem { Key = "shipping_fee", Value = "60", DataType = "decimal" });
        ctx.AppSettings.Add(new AppSettingItem { Key = "free_shipping_threshold", Value = "2000", DataType = "decimal" });
        ctx.SaveChanges();
        return (ctx, p.Id);
    }

    private static OrderCreateDto Dto(Guid pid, string? email) => new()
    {
        CustomerName = "أحمد خطاب", CustomerPhone = "01114545419", CustomerAddress = "شارع ١",
        City = "القاهرة", CustomerEmail = email,
        Items = new[] { new OrderItemWriteDto { ProductId = pid, Volume = "50ML", Quantity = 1 } }
    };

    private static OrderService NewSvc(ApplicationDbContext ctx, IEmailService email)
        => new(ctx, new FakeAudit(), new FakeNotifier(), new FakePush(), null, email);

    [Fact]
    public async Task Sends_confirmation_when_the_customer_gave_an_email()
    {
        var (ctx, pid) = Seed();
        var email = new SpyEmail();
        var order = await NewSvc(ctx, email).CreateAsync(Dto(pid, "customer@example.com"));

        Assert.Single(email.Sent);
        Assert.Equal("customer@example.com", email.Sent[0].To);
        Assert.Equal(order.Code, email.Sent[0].Code);
        Assert.Equal(order.Total, email.Sent[0].Total);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Skips_quietly_when_no_email_was_entered(string? email)
    {
        var (ctx, pid) = Seed();
        var spy = new SpyEmail();
        var order = await NewSvc(ctx, spy).CreateAsync(Dto(pid, email));

        Assert.Empty(spy.Sent);
        Assert.False(string.IsNullOrWhiteSpace(order.Code));   // الطلب اتسجّل عادي
    }

    [Fact]
    public async Task Email_is_saved_on_the_order()
    {
        var (ctx, pid) = Seed();
        var order = await NewSvc(ctx, new SpyEmail()).CreateAsync(Dto(pid, "customer@example.com"));
        Assert.Equal("customer@example.com", order.CustomerEmail);
    }

    [Fact]
    public async Task A_failing_mail_server_never_fails_the_order()
    {
        // الطلب اتسجّل والعميل شايف رقمه — سقوط سيرفر البريد ما ينفعش يرجّع خطأ
        var (ctx, pid) = Seed();
        var spy = new SpyEmail { Throw = true };
        var order = await NewSvc(ctx, spy).CreateAsync(Dto(pid, "customer@example.com"));
        Assert.False(string.IsNullOrWhiteSpace(order.Code));
        Assert.Equal(760m, order.Total);
    }

    [Fact]
    public async Task Works_without_an_email_service_at_all()
    {
        var (ctx, pid) = Seed();
        var svc = new OrderService(ctx, new FakeAudit(), new FakeNotifier(), new FakePush());
        var order = await svc.CreateAsync(Dto(pid, "customer@example.com"));
        Assert.False(string.IsNullOrWhiteSpace(order.Code));
    }
}
