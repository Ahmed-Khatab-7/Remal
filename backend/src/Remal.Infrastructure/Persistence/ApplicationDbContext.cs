using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Common;
using Remal.Domain.Entities;
using Remal.Domain.Identity;

namespace Remal.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUser;
    private readonly IDateTimeService? _dateTime;
    private readonly IEnumerable<IInterceptor>? _interceptors;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUser = null,
        IDateTimeService? dateTime = null,
        IEnumerable<IInterceptor>? interceptors = null)
        : base(options)
    {
        _currentUser = currentUser;
        _dateTime = dateTime;
        _interceptors = interceptors;
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSize> ProductSizes => Set<ProductSize>();
    public DbSet<Bundle> Bundles => Set<Bundle>();
    public DbSet<BundleItem> BundleItems => Set<BundleItem>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<PartnerWithdrawal> PartnerWithdrawals => Set<PartnerWithdrawal>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSettingItem> AppSettings => Set<AppSettingItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();
    public DbSet<AddressBookEntry> AddressBookEntries => Set<AddressBookEntry>();
    public DbSet<NewsletterSubscription> NewsletterSubscriptions => Set<NewsletterSubscription>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_interceptors != null)
            optionsBuilder.AddInterceptors(_interceptors);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("dbo");
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Identity table renames (cleaner)
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");

        // Soft-delete global query filters on parents that implement ISoftDeletable.
        builder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Bundle>().HasQueryFilter(b => !b.IsDeleted);
        builder.Entity<Collection>().HasQueryFilter(c => !c.IsDeleted);
        builder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);

        // Matching child filters so EF doesn't warn (and so children of a soft-deleted parent
        // are not returned in queries either).
        builder.Entity<ProductSize>().HasQueryFilter(ps => !ps.Product.IsDeleted);
        builder.Entity<Review>().HasQueryFilter(r => !r.Product.IsDeleted);
        builder.Entity<BundleItem>().HasQueryFilter(bi => !bi.Bundle.IsDeleted && !bi.Product.IsDeleted);
        builder.Entity<CollectionItem>().HasQueryFilter(ci => !ci.Collection.IsDeleted && !ci.Product.IsDeleted);
        builder.Entity<WishlistItem>().HasQueryFilter(w => !w.Product.IsDeleted);
        builder.Entity<CartItem>().HasQueryFilter(c =>
            (c.ProductId == null || !c.Product!.IsDeleted) &&
            (c.BundleId == null  || !c.Bundle!.IsDeleted) &&
            (c.CollectionId == null || !c.Collection!.IsDeleted));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Auto-fill auditable timestamps
        var now = _dateTime?.UtcNow ?? DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedById = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedById = userId;
                    break;
            }
        }

        return await base.SaveChangesAsync(ct);
    }
}
