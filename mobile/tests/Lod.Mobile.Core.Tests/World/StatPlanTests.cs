using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

public sealed class StatPlanTests
{
    private const int Monk = 5;
    private const int Warrior = 1;
    private const int Rogue = 2;
    private const int Wizard = 3;

    private static readonly Vitals Fresh = Vitals.Unknown with { Unspent = 2, Str = 3, Int = 3, Wis = 3, Con = 3, Dex = 3 };

    [Fact]
    public void Monk_spends_on_the_attribute_furthest_from_its_target()
    {
        Assert.Equal(Stat.Str, StatPlan.Next(Monk, Fresh));
        Assert.Equal(Stat.Con, StatPlan.Next(Monk, Fresh with { Str = 77 }));
    }

    [Fact]
    public void Warrior_fills_con_to_64_then_puts_everything_into_str()
    {
        Assert.Equal(Stat.Con, StatPlan.Next(Warrior, Fresh));
        Assert.Equal(Stat.Con, StatPlan.Next(Warrior, Fresh with { Con = 63, Str = 3 }));
        Assert.Equal(Stat.Str, StatPlan.Next(Warrior, Fresh with { Con = 64 }));
        Assert.Equal(Stat.Str, StatPlan.Next(Warrior, Fresh with { Con = 64, Str = 254 }));
        Assert.Null(StatPlan.Next(Warrior, Fresh with { Con = 64, Str = 255 }));
    }

    [Fact]
    public void Rogue_fills_wis_con_str_dex_int_in_that_order()
    {
        Assert.Equal(Stat.Wis, StatPlan.Next(Rogue, Fresh));
        Assert.Equal(Stat.Con, StatPlan.Next(Rogue, Fresh with { Wis = 23 }));
        Assert.Equal(Stat.Str, StatPlan.Next(Rogue, Fresh with { Wis = 23, Con = 41 }));
        Assert.Equal(Stat.Dex, StatPlan.Next(Rogue, Fresh with { Wis = 23, Con = 41, Str = 78 }));
        Assert.Equal(Stat.Int, StatPlan.Next(Rogue, Fresh with { Wis = 23, Con = 41, Str = 78, Dex = 49 }));
        Assert.Null(StatPlan.Next(Rogue, Fresh with { Wis = 23, Con = 41, Str = 78, Dex = 49, Int = 20 }));
    }

    [Fact]
    public void Classes_without_a_plan_keep_their_points()
    {
        Assert.Null(StatPlan.Next(Wizard, Fresh));
        Assert.Null(StatPlan.Next(null, Fresh));
    }

    [Fact]
    public void Nothing_to_spend_or_targets_met_spends_nothing()
    {
        Assert.Null(StatPlan.Next(Monk, Fresh with { Unspent = 0 }));
        Assert.Null(StatPlan.Next(Monk, Fresh with { Str = 77, Con = 65, Int = 43, Wis = 36 }));
    }
}
