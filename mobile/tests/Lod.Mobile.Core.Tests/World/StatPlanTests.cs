using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

public sealed class StatPlanTests
{
    private const int Monk = 5;
    private const int Warrior = 1;

    private static readonly Vitals Fresh = Vitals.Unknown with { Unspent = 2, Str = 3, Int = 3, Wis = 3, Con = 3, Dex = 3 };

    [Fact]
    public void Monk_spends_on_the_attribute_furthest_from_its_target()
    {
        Assert.Equal(Stat.Str, StatPlan.Next(Monk, Fresh));
        Assert.Equal(Stat.Con, StatPlan.Next(Monk, Fresh with { Str = 77 }));
    }

    [Fact]
    public void Classes_without_a_plan_keep_their_points()
    {
        Assert.Null(StatPlan.Next(Warrior, Fresh));
        Assert.Null(StatPlan.Next(null, Fresh));
    }

    [Fact]
    public void Nothing_to_spend_or_targets_met_spends_nothing()
    {
        Assert.Null(StatPlan.Next(Monk, Fresh with { Unspent = 0 }));
        Assert.Null(StatPlan.Next(Monk, Fresh with { Str = 77, Con = 65, Int = 43, Wis = 36 }));
    }
}
