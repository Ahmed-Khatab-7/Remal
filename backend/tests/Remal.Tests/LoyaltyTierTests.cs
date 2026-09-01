using Remal.Domain.Entities;
using Xunit;

namespace Remal.Tests;

/// <summary>يغطي §6: حدود مستويات الولاء (499/500، 1499/1500، 2999/3000) — منطق pure على LoyaltyAccount.Tier.</summary>
public class LoyaltyTierTests
{
    [Theory]
    [InlineData(0, LoyaltyTier.Grain)]
    [InlineData(499, LoyaltyTier.Grain)]
    [InlineData(500, LoyaltyTier.SandHill)]
    [InlineData(1499, LoyaltyTier.SandHill)]
    [InlineData(1500, LoyaltyTier.Dune)]
    [InlineData(2999, LoyaltyTier.Dune)]
    [InlineData(3000, LoyaltyTier.Desert)]
    [InlineData(999999, LoyaltyTier.Desert)]
    public void Tier_boundaries_are_inclusive_at_threshold(int balance, LoyaltyTier expected)
    {
        var acct = new LoyaltyAccount { Balance = balance };
        Assert.Equal(expected, acct.Tier);
    }
}
