using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>⑤ 99 뒤 쌓인 경험치로 체력·마력(<c>autopilot/eco-bots/vitality-SPEC.md</c>) — 세오·칸에게 몇 번 살 수 있나.</summary>
public sealed class EcoVitalityTests
{
    [Fact]
    public void Each_buy_costs_the_maximum_of_that_moment_times_five_hundred()
    {
        long two = 1000 * 500 + 1050 * 500;

        Assert.Equal(2, EcoVitality.Times(1000, two, EcoVitality.Health));
        Assert.Equal(1, EcoVitality.Times(1000, two - 1, EcoVitality.Health));
        Assert.Equal(0, EcoVitality.Times(1000, 1000 * 500 - 1, EcoVitality.Health));
        Assert.Equal(3, EcoVitality.Times(400, 400 * 500 + 425 * 500 + 450 * 500, EcoVitality.Mana));
    }

    [Fact]
    public void One_visit_buys_at_most_so_many()
    {
        Assert.Equal(EcoVitality.MostAtOnce, EcoVitality.Times(1000, long.MaxValue / 2, EcoVitality.Health));
    }

    [Fact]
    public void Only_a_level_99_has_banked_experience_split_over_two_fields()
    {
        Vitals banked = Vitals.Unknown with { Level = 99, Experience = 705_032_704, ExperienceToGo = 1 };

        Assert.Equal(5_000_000_000, banked.Banked);
        Assert.Equal(0, (banked with { Level = 98 }).Banked);
    }
}
