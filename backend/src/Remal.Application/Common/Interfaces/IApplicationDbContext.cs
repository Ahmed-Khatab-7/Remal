using Microsoft.EntityFrameworkCore;
using Remal.Domain.Entities;
using Remal.Domain.Identity;

namespace Remal.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the DbContext used by Application services.
/// Infrastructure provides the concrete implementation.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>مستخدمو الهوية — للقراءة (مطابقة رقم الهاتف عند منح نقاط الشراء).</summary>
    DbSet<ApplicationUser> Users { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductSize> ProductSizes { get; }
    DbSet<Bundle> Bundles { get; }
    DbSet<BundleItem> BundleItems { get; }
    DbSet<Collection> Collections { get; }
    DbSet<CollectionItem> CollectionItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<PartnerWithdrawal> PartnerWithdrawals { get; }
    DbSet<Promotion> Promotions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppSettingItem> AppSettings { get; }

    // New entities
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<LoyaltyAccount> LoyaltyAccounts { get; }
    DbSet<PointsTransaction> PointsTransactions { get; }
    DbSet<AddressBookEntry> AddressBookEntries { get; }
    DbSet<NewsletterSubscription> NewsletterSubscriptions { get; }
    DbSet<ContactMessage> ContactMessages { get; }
    DbSet<PushSubscription> PushSubscriptions { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
