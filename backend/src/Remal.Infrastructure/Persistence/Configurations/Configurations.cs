using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remal.Domain.Entities;

namespace Remal.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.NameEn).IsRequired().HasMaxLength(120);
        b.Property(x => x.InspiredBy).HasMaxLength(120);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.NotesTop).HasMaxLength(500);
        b.Property(x => x.NotesHeart).HasMaxLength(500);
        b.Property(x => x.NotesBase).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Rating).HasPrecision(3, 2);
        b.Property(x => x.CostOil).HasPrecision(18, 2);
        b.Property(x => x.CostAlcohol).HasPrecision(18, 2);
        b.Property(x => x.CostPackaging).HasPrecision(18, 2);
        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.Status);
        b.HasMany(x => x.Sizes).WithOne(s => s.Product).HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Reviews).WithOne(r => r.Product).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductSizeConfiguration : IEntityTypeConfiguration<ProductSize>
{
    public void Configure(EntityTypeBuilder<ProductSize> b)
    {
        b.ToTable("ProductSizes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Volume).IsRequired().HasMaxLength(10);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.OldPrice).HasPrecision(18, 2);   // السعر قبل الخصم (اختياري)
        b.HasIndex(x => new { x.ProductId, x.Volume }).IsUnique();
    }
}

public class BundleConfiguration : IEntityTypeConfiguration<Bundle>
{
    public void Configure(EntityTypeBuilder<Bundle> b)
    {
        b.ToTable("Bundles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.NameEn).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Tag).HasMaxLength(50);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.OriginalPrice).HasPrecision(18, 2);
        b.Property(x => x.FinalPrice).HasPrecision(18, 2);
        b.HasMany(x => x.Items).WithOne(i => i.Bundle).HasForeignKey(i => i.BundleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BundleItemConfiguration : IEntityTypeConfiguration<BundleItem>
{
    public void Configure(EntityTypeBuilder<BundleItem> b)
    {
        b.ToTable("BundleItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.Volume).IsRequired().HasMaxLength(10);
        b.HasOne(x => x.Product).WithMany(p => p.BundleItems).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BundleId, x.ProductId, x.Volume }).IsUnique();
    }
}

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> b)
    {
        b.ToTable("Collections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.NameEn).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.SampleVolume).HasMaxLength(10);
        b.Property(x => x.OriginalPrice).HasPrecision(18, 2);
        b.Property(x => x.FinalPrice).HasPrecision(18, 2);
        b.HasMany(x => x.Items).WithOne(i => i.Collection).HasForeignKey(i => i.CollectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CollectionItemConfiguration : IEntityTypeConfiguration<CollectionItem>
{
    public void Configure(EntityTypeBuilder<CollectionItem> b)
    {
        b.ToTable("CollectionItems");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Product).WithMany(p => p.CollectionItems).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CollectionId, x.ProductId }).IsUnique();
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.TotalSpent).HasPrecision(18, 2);
        b.HasIndex(x => x.Phone);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.CustomerName).IsRequired().HasMaxLength(150);
        b.Property(x => x.CustomerPhone).IsRequired().HasMaxLength(20);
        b.Property(x => x.CustomerAddress).IsRequired().HasMaxLength(500);
        b.Property(x => x.CustomerEmail).HasMaxLength(150);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CouponCode).HasMaxLength(30);
        b.Property(x => x.PaymentReference).HasMaxLength(100);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.ShippingFee).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PlacedAt);
        b.HasOne(x => x.Customer).WithMany(c => c.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Volume).HasMaxLength(20);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Ignore(x => x.LineTotal);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Bundle).WithMany().HasForeignKey(x => x.BundleId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.ToTable("Coupons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(30);
        b.Property(x => x.Value).HasPrecision(18, 2);
        b.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
        b.Ignore(x => x.IsExpired);
        b.Ignore(x => x.IsUsable);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("Reviews");
        b.HasKey(x => x.Id);
        b.Property(x => x.CustomerName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Text).HasMaxLength(2000);
        b.Property(x => x.ModerationNote).HasMaxLength(500);
        b.HasIndex(x => new { x.ProductId, x.Status });
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("Expenses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).IsRequired().HasMaxLength(500);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.ReceiptUrl).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.PaidBy).WithMany(u => u.ExpensesPaid).HasForeignKey(x => x.PaidById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Date);
        b.HasIndex(x => x.Category);
    }
}

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> b)
    {
        b.ToTable("Settlements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne(x => x.FromUser).WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToUser).WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PartnerWithdrawalConfiguration : IEntityTypeConfiguration<PartnerWithdrawal>
{
    public void Configure(EntityTypeBuilder<PartnerWithdrawal> b)
    {
        b.ToTable("PartnerWithdrawals");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PartnerId);
        b.HasIndex(x => x.Date);
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("Promotions");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(160);
        b.Property(x => x.NameEn).HasMaxLength(160);
        b.Property(x => x.TriggerVolume).HasMaxLength(10);
        b.Property(x => x.RewardVolume).HasMaxLength(10);
        b.Property(x => x.MinSpend).HasPrecision(18, 2);
        b.Property(x => x.RewardPercentOff).HasPrecision(5, 2);
        b.HasIndex(x => x.IsActive);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserName).HasMaxLength(150);
        b.Property(x => x.Action).IsRequired().HasMaxLength(80);
        b.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        b.Property(x => x.EntityName).HasMaxLength(80);
        b.Property(x => x.EntityId).HasMaxLength(80);
        b.Property(x => x.Before).HasColumnType("nvarchar(max)");
        b.Property(x => x.After).HasColumnType("nvarchar(max)");
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.HasIndex(x => x.Timestamp);
        b.HasIndex(x => new { x.Category, x.Timestamp });
        b.HasIndex(x => x.UserId);
    }
}

public class AppSettingItemConfiguration : IEntityTypeConfiguration<AppSettingItem>
{
    public void Configure(EntityTypeBuilder<AppSettingItem> b)
    {
        b.ToTable("AppSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).IsRequired().HasMaxLength(100);
        b.Property(x => x.Value).HasMaxLength(2000);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DataType).HasMaxLength(20);
        b.HasIndex(x => x.Key).IsUnique();
    }
}
