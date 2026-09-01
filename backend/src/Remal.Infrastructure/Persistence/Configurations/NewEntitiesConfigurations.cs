using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remal.Domain.Entities;

namespace Remal.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.RevokedReason).HasMaxLength(200);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        b.Property(x => x.CreatedByIp).HasMaxLength(45);
        b.Property(x => x.RevokedByIp).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.ExpiresAt);
        b.Ignore(x => x.IsActive);
        b.Ignore(x => x.IsExpired);
        b.Ignore(x => x.IsRevoked);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> b)
    {
        b.ToTable("WishlistItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        b.HasOne(x => x.User).WithMany(u => u.WishlistItems).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("CartItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Volume).HasMaxLength(10);
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.User).WithMany(u => u.CartItems).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Bundle).WithMany().HasForeignKey(x => x.BundleId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> b)
    {
        b.ToTable("LoyaltyAccounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.HasIndex(x => x.UserId).IsUnique();
        b.Ignore(x => x.Tier);
        b.Ignore(x => x.TierName);
        b.HasOne(x => x.User).WithOne(u => u.LoyaltyAccount).HasForeignKey<LoyaltyAccount>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Transactions).WithOne(t => t.LoyaltyAccount).HasForeignKey(t => t.LoyaltyAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointsTransactionConfiguration : IEntityTypeConfiguration<PointsTransaction>
{
    public void Configure(EntityTypeBuilder<PointsTransaction> b)
    {
        b.ToTable("PointsTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).IsRequired().HasMaxLength(300);
        b.HasIndex(x => new { x.LoyaltyAccountId, x.Timestamp });
        b.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AddressBookEntryConfiguration : IEntityTypeConfiguration<AddressBookEntry>
{
    public void Configure(EntityTypeBuilder<AddressBookEntry> b)
    {
        b.ToTable("AddressBookEntries");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Label).IsRequired().HasMaxLength(50);
        b.Property(x => x.RecipientName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        b.OwnsOne(x => x.Address, addr =>
        {
            addr.Property(a => a.Line).IsRequired().HasMaxLength(500).HasColumnName("AddressLine");
            addr.Property(a => a.City).IsRequired().HasMaxLength(100).HasColumnName("City");
            addr.Property(a => a.Governorate).HasMaxLength(100).HasColumnName("Governorate");
            addr.Property(a => a.PostalCode).HasMaxLength(20).HasColumnName("PostalCode");
            addr.Property(a => a.Landmark).HasMaxLength(200).HasColumnName("Landmark");
        });
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.User).WithMany(u => u.Addresses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NewsletterSubscriptionConfiguration : IEntityTypeConfiguration<NewsletterSubscription>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscription> b)
    {
        b.ToTable("NewsletterSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).IsRequired().HasMaxLength(200);
        b.Property(x => x.Source).HasMaxLength(50);
        b.HasIndex(x => x.Email).IsUnique();
        b.Ignore(x => x.IsActive);
    }
}

public class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> b)
    {
        b.ToTable("ContactMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        b.Property(x => x.Reply).HasMaxLength(2000);
        b.HasIndex(x => x.SentAt);
        b.HasIndex(x => x.Read);
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.ToTable("PushSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Endpoint).IsRequired().HasMaxLength(800);
        b.Property(x => x.P256dh).IsRequired().HasMaxLength(200);
        b.Property(x => x.Auth).IsRequired().HasMaxLength(200);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        // Unique on Endpoint so re-subscribing from the same browser replaces (we delete-then-insert in the controller).
        b.HasIndex(x => x.Endpoint).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}
