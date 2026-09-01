using Remal.Application.Features.Orders;
using Remal.Application.Features.Orders.Dtos;
using Remal.Domain.Entities;
using Remal.Domain.Enums;
using Remal.Domain.Identity;
using Remal.Infrastructure.Persistence;
using Xunit;

namespace Remal.Tests;

/// <summary>
/// يغطي D1: منح نقاط الولاء عند تحوّل الطلب إلى Delivered عبر مطابقة رقم الهاتف —
/// حالة مطابقة ناجحة، حالة بدون مطابقة (زائر)، وعدم التكرار (idempotency).
/// </summary>
public class LoyaltyOnDeliveryTests
{
    private static OrderService NewSvc(ApplicationDbContext ctx)
        => new(ctx, new FakeAudit(), new FakeNotifier(), new FakePush());

    private const string Phone = "01000000000";

    private static (ApplicationDbContext ctx, Guid orderId) SeedOrder(decimal price50 = 1000, string phone = Phone)
    {
        var ctx = TestDb.New();
        var p = new Product { Name = "عطر", NameEn = "P", Status = ProductStatus.Active };
        p.Sizes.Add(new ProductSize { Volume = "50ML", Price = price50, Stock = 10 });
        ctx.Products.Add(p);
        ctx.SaveChanges();

        var dto = new OrderCreateDto
        {
            CustomerName = "أحمد", CustomerPhone = phone, CustomerAddress = "القاهرة",
            Items = new[] { new OrderItemWriteDto { ProductId = p.Id, Volume = "50ML", Quantity = 1 } }
        };
        var created = NewSvc(ctx).CreateAsync(dto).GetAwaiter().GetResult();
        return (ctx, created.Id);
    }

    private static void RegisterUser(ApplicationDbContext ctx, string phone)
    {
        ctx.Users.Add(new ApplicationUser
        {
            UserName = "u@remal.eg", Email = "u@remal.eg", PhoneNumber = phone, FullName = "أحمد",
        });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Delivered_awards_one_point_per_ten_pounds_when_phone_matches()
    {
        var (ctx, orderId) = SeedOrder(price50: 1000); // subtotal 1000 → 100 نقطة
        RegisterUser(ctx, Phone);

        await NewSvc(ctx).UpdateStatusAsync(orderId, new OrderStatusUpdateDto(OrderStatus.Delivered, null));

        var user = ctx.Users.First(u => u.PhoneNumber == Phone);
        var acct = ctx.LoyaltyAccounts.First(a => a.UserId == user.Id);
        Assert.Equal(100, acct.Balance);
        Assert.Equal(100, acct.LifetimeEarned);
        var tx = ctx.PointsTransactions.Single(t => t.OrderId == orderId);
        Assert.Equal(PointsTransactionType.Earn, tx.Type);
        Assert.Equal(100, tx.Points);
        Assert.True(ctx.Orders.First(o => o.Id == orderId).PointsAwarded);
    }

    [Fact]
    public async Task Delivered_awards_no_points_when_no_matching_user()
    {
        // طلب زائر برقم غير مسجّل → لا حساب ولاء ولا نقاط، وبدون خطأ يعطّل تحديث الحالة.
        var (ctx, orderId) = SeedOrder(price50: 1000, phone: "01099999999");

        var result = await NewSvc(ctx).UpdateStatusAsync(orderId, new OrderStatusUpdateDto(OrderStatus.Delivered, null));

        Assert.Equal(OrderStatus.Delivered, result.Status); // الحالة اتحدّثت عادي
        Assert.Empty(ctx.LoyaltyAccounts);
        Assert.Empty(ctx.PointsTransactions);
        Assert.False(ctx.Orders.First(o => o.Id == orderId).PointsAwarded);
    }

    [Fact]
    public async Task Delivered_twice_does_not_award_points_twice()
    {
        var (ctx, orderId) = SeedOrder(price50: 1000);
        RegisterUser(ctx, Phone);
        var svc = NewSvc(ctx);

        await svc.UpdateStatusAsync(orderId, new OrderStatusUpdateDto(OrderStatus.Delivered, null));
        // رجّع الحالة لـ Shipping ثم Delivered تاني — لازم ميتمنحش نقاط إضافية
        await svc.UpdateStatusAsync(orderId, new OrderStatusUpdateDto(OrderStatus.Shipping, null));
        await svc.UpdateStatusAsync(orderId, new OrderStatusUpdateDto(OrderStatus.Delivered, null));

        var user = ctx.Users.First(u => u.PhoneNumber == Phone);
        var acct = ctx.LoyaltyAccounts.First(a => a.UserId == user.Id);
        Assert.Equal(100, acct.Balance); // مش 200
        Assert.Single(ctx.PointsTransactions.Where(t => t.OrderId == orderId));
    }

    [Fact]
    public async Task Subtotal_under_ten_pounds_awards_zero_points()
    {
        var (ctx, orderId) = SeedOrder(price50: 5); // subtotal 5 → floor(5/10) = 0
        RegisterUser(ctx, Phone);

        await NewSvc(ctx).UpdateStatusAsync(orderId, new OrderStatusUpdateDto(OrderStatus.Delivered, null));

        Assert.Empty(ctx.PointsTransactions);
        Assert.False(ctx.Orders.First(o => o.Id == orderId).PointsAwarded);
    }
}
