using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>생태계 파티의 성직자(결정 19) — <see cref="CompanionBrain" /> 가 주인(파티장) 말고 파티원도 회복·깨우기·버프하고 그 곁 괴물에 저주한다.</summary>
public sealed class CompanionPartyTests
{
    private const uint Me = 7;
    private const uint Leader = 42;
    private const uint Rogue = 43;
    private const uint Monk = 44;
    private static readonly CompanionSettings Defaults = new();

    private static LearnedSpell Spell(int slot, string name) => new(slot, 0, SpellTargetType.ChooseTarget, name, string.Empty, 0);

    private static CompanionSight Sight(IReadOnlyDictionary<uint, int> health, IReadOnlyDictionary<uint, Tile> mates,
        IReadOnlyList<LearnedSpell>? spells = null, Func<uint, IReadOnlyCollection<string>?>? statuses = null, IReadOnlyList<Foe>? foes = null) => new()
    {
        Me = Me,
        Master = Leader,
        Standing = new Tile(10, 10),
        Vitals = Vitals.Unknown with { Health = 500, MaximumHealth = 500, Mana = 500, MaximumMana = 500 },
        OwnerAt = new Tile(11, 10),
        HealthOf = serial => health.GetValueOrDefault(serial, 100),
        Spells = spells ?? [Spell(1, "쿠라노"), Spell(2, "쿠러스")],
        StatusesOf = statuses ?? (_ => null),
        Foes = foes ?? [],
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

    [Fact]
    public void Walks_to_and_wakes_a_comatose_mate()
    {
        IReadOnlyCollection<string>? Statuses(uint serial) => serial == Monk ? ["skulled"] : [];
        Dictionary<uint, int> fine = [];

        CompanionStep wake = new CompanionBrain().Next(Sight(fine, new Dictionary<uint, Tile> { [Monk] = new Tile(10, 11) }, statuses: Statuses), Defaults);
        Assert.Equal(CompanionAct.WakeOwner, wake.Act);
        Assert.Equal(Monk, wake.Target);

        Assert.Equal(CompanionAct.Walk, new CompanionBrain().Next(Sight(fine, new Dictionary<uint, Tile> { [Monk] = new Tile(10, 14) }, statuses: Statuses), Defaults).Act);
    }

    [Fact]
    public void Buffs_a_mate_once_the_leader_and_itself_have_it()
    {
        IReadOnlyCollection<string>? Statuses(uint serial) => serial == Rogue ? [] : ["enare"];

        CompanionStep step = new CompanionBrain().Next(Sight(new Dictionary<uint, int>(), Near, [Spell(3, "에나르마")], Statuses), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(3, step.Slot);
        Assert.Equal(Rogue, step.Target);
    }

    [Fact]
    public void Curses_a_monster_beside_a_mate_away_from_the_leader()
    {
        const uint wolf = 900;
        Foe foe = new(wolf, new Tile(15, 14), Cursed: false, Asleep: false, OwnerHits: false, HitsOwner: false);

        CompanionStep step = new CompanionBrain().Next(Sight(new Dictionary<uint, int>(), new Dictionary<uint, Tile> { [Rogue] = new Tile(14, 14) }, [Spell(4, "렌토")], _ => [], [foe]), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(wolf, step.Target);
    }
}
