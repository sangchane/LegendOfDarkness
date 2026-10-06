using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>생태계 파티의 성직자(결정 19) — <see cref="CompanionBrain" /> 가 주인(파티장) 말고 파티원도 회복한다.</summary>
public sealed class CompanionPartyTests
{
    private const uint Me = 7;
    private const uint Leader = 42;
    private const uint Rogue = 43;
    private const uint Monk = 44;
    private static readonly CompanionSettings Defaults = new();

    private static LearnedSpell Spell(int slot, string name) => new(slot, 0, SpellTargetType.ChooseTarget, name, string.Empty, 0);

    private static CompanionSight Sight(IReadOnlyDictionary<uint, int> health, IReadOnlyDictionary<uint, Tile> mates) => new()
    {
        Me = Me,
        Master = Leader,
        Standing = new Tile(10, 10),
        Vitals = Vitals.Unknown with { Health = 500, MaximumHealth = 500, Mana = 500, MaximumMana = 500 },
        OwnerAt = new Tile(11, 10),
        HealthOf = serial => health.GetValueOrDefault(serial, 100),
        Spells = [Spell(1, "쿠라노"), Spell(2, "쿠러스")],
        Mates = mates,
        Now = TimeSpan.FromSeconds(100),
    };

    private static readonly Dictionary<uint, Tile> Near = new() { [Rogue] = new Tile(12, 12), [Monk] = new Tile(8, 9) };

    [Fact]
    public void Heals_the_most_hurt_party_mate_in_sight()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(new Dictionary<uint, int> { [Rogue] = 40, [Monk] = 90 }, Near), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(1, step.Slot);
        Assert.Equal(Rogue, step.Target);
    }

    [Fact]
    public void Two_hurt_mates_get_the_group_heal()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(new Dictionary<uint, int> { [Rogue] = 40, [Monk] = 30 }, Near), Defaults);

        Assert.Equal(2, step.Slot);
        Assert.Equal(Monk, step.Target);
    }

    [Fact]
    public void A_mate_out_of_reach_or_no_mates_is_left_alone()
    {
        Dictionary<uint, int> hurt = new() { [Rogue] = 10 };

        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight(hurt, new Dictionary<uint, Tile> { [Rogue] = new Tile(30, 30) }), Defaults).Act);
        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight(hurt, new Dictionary<uint, Tile>()), Defaults).Act);
    }
}
