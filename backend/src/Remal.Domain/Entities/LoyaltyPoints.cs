using Remal.Domain.Common;
using Remal.Domain.Identity;

namespace Remal.Domain.Entities;

/// <summary>Per-user loyalty account. Created on registration.</summary>
public class LoyaltyAccount : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public int Balance { get; set; }
    public int LifetimeEarned { get; set; }
    public int LifetimeSpent { get; set; }

    public ICollection<PointsTransaction> Transactions { get; set; } = new List<PointsTransaction>();

    public LoyaltyTier Tier => Balance switch
    {
        >= 3000 => LoyaltyTier.Desert,
        >= 1500 => LoyaltyTier.Dune,
        >= 500 => LoyaltyTier.SandHill,
        _ => LoyaltyTier.Grain
    };

    public string TierName => Tier switch
    {
        LoyaltyTier.Desert => "صحراء",
        LoyaltyTier.Dune => "كثيب",
        LoyaltyTier.SandHill => "تل رملي",
        _ => "رملة"
    };
}

public enum LoyaltyTier { Grain = 0, SandHill = 1, Dune = 2, Desert = 3 }

public enum PointsTransactionType
{
    Earn = 0,
    Spend = 1,
    Welcome = 2,
    ReviewReward = 3,
    BirthdayBonus = 4,
    Refund = 5,
    Adjustment = 6,
}

public class PointsTransaction : BaseEntity
{
    public Guid LoyaltyAccountId { get; set; }
    public LoyaltyAccount LoyaltyAccount { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public PointsTransactionType Type { get; set; }
    public int Points { get; set; } // positive earns, negative spends
    public string Description { get; set; } = null!;
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
}
