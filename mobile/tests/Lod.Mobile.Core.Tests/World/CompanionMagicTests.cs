using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 봇의 마법사 마법(<see cref="CompanionBrain" /> 돕기) — 싸우는 괴물에게 저주, 주인이 치지 않는 괴물에게 나르콜리.
/// 걸린 것에 다시 걸어 마력을 버리지 않고, 기본공격에 깬 괴물을 곧 다시 재우지 않는다(사용자, 2026-10-03).
/// </summary>
public sealed class CompanionMagicTests
{
    private const uint Me = 7;
    private const uint Owner = 42;
    private const uint Hit = 100;
    private const uint Add = 101;
    private static readonly Tile Here = new(10, 10);
    private static readonly Tile OwnerAt = new(11, 10);
    private static readonly CompanionSettings Defaults = new();

    private static LearnedSpell Spell(int slot, string name) => new(slot, 0, SpellTargetType.ChooseTarget, name, string.Empty, 0);

    private static readonly IReadOnlyList<LearnedSpell> Level41 =
        [Spell(1, "쿠로"), Spell(10, "렌토 (Lev:3/100)"), Spell(11, "바르도"), Spell(12, "나르콜리")];

    /// <summary>주인이 치는 괴물(오른쪽 옆)과 주인을 치는 다른 괴물(위 옆).</summary>
    private static Foe Struck(bool cursed = false, bool asleep = false) => new(Hit, new Tile(12, 10), cursed, asleep, OwnerHits: true, HitsOwner: false);

    private static Foe Biter(bool cursed = true, bool asleep = false) => new(Add, new Tile(11, 9), cursed, asleep, OwnerHits: false, HitsOwner: true);

    private static CompanionSight Sight(IReadOnlyList<Foe> foes, double seconds = 100, int mana = 500,
        IReadOnlyList<LearnedSpell>? spells = null) => new()
    {
        Me = Me,
        Master = Owner,
        Standing = Here,
        Vitals = Vitals.Unknown with { Health = 500, MaximumHealth = 500, Mana = mana, MaximumMana = 500 },
        OwnerAt = OwnerAt,
        HealthOf = _ => 100,
        Spells = spells ?? Level41,
        Foes = foes,
        Now = TimeSpan.FromSeconds(seconds),
    };

    [Fact]
    public void The_monster_the_owner_fights_gets_the_strongest_curse_it_can_pay_for()
    {
        CompanionStep step = new CompanionBrain().Next(Sight([Struck()]), Defaults);

        Assert.Equal((CompanionAct.Cast, 11, Hit), (step.Act, step.Slot, step.Target)); // 바르도(25) > 렌토(15)

        // 21레벨 전에는 렌토뿐.
        Assert.Equal(10, new CompanionBrain().Next(Sight([Struck()], spells: [Spell(10, "렌토")]), Defaults).Slot);
    }

    [Fact]
    public void A_cursed_monster_is_not_cursed_again_and_a_fresh_cast_waits_for_the_report()
    {
        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([Struck(cursed: true)], spells: [Spell(11, "바르도")]), Defaults).Act);

        CompanionBrain brain = new();
        Assert.Equal(CompanionAct.Cast, brain.Next(Sight([Struck()], spells: [Spell(11, "바르도")]), Defaults).Act);
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight([Struck()], 102, spells: [Spell(11, "바르도")]), Defaults).Act);
    }

    [Fact]
    public void Narcoli_goes_to_the_monster_the_owner_is_not_hitting()
    {
        CompanionStep step = new CompanionBrain().Next(Sight([Struck(cursed: true), Biter()]), Defaults);

        Assert.Equal((CompanionAct.Cast, 12, Add), (step.Act, step.Slot, step.Target));

        // 주인이 치는 것뿐이면 재우지 않는다 — 다음 한 대에 깬다.
        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([Struck(cursed: true)]), Defaults).Act);
    }

    [Fact]
    public void A_monster_woken_by_a_blow_is_not_put_back_to_sleep_at_once()
    {
        CompanionBrain brain = new();
        Assert.Equal(Add, brain.Next(Sight([Biter()], 100), Defaults).Target);
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight([Biter(asleep: true)], 102), Defaults).Act);

        // 주인이 쳐서 깼다 — 잠든 것을 본 때(102)부터 20초 안에는 다시 안 건다.
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight([Biter()], 105), Defaults).Act);
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight([Biter()], 121), Defaults).Act);
        Assert.Equal(Add, brain.Next(Sight([Biter()], 123), Defaults).Target);
    }

    [Fact]
    public void A_sleeper_seen_while_busy_healing_still_counts()
    {
        CompanionBrain brain = new();
        Assert.Equal(Add, brain.Next(Sight([Biter()], 100), Defaults).Target);

        // 잠든 동안 봇은 주인 회복에 바쁘다(돕기가 돌지 않는다) — 그래도 잠든 것은 적힌다.
        CompanionSight busy = Sight([Biter(asleep: true)], 102) with { HealthOf = _ => 30 };
        Assert.StartsWith("주인 회복", brain.Next(busy, Defaults).Why, StringComparison.Ordinal);

        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight([Biter()], 106), Defaults).Act);
    }

    [Fact]
    public void A_dodged_narcoli_is_tried_again_after_the_report_wait()
    {
        CompanionBrain brain = new();
        Assert.Equal(Add, brain.Next(Sight([Biter()], 100), Defaults).Target);
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight([Biter()], 102), Defaults).Act);
        Assert.Equal(Add, brain.Next(Sight([Biter()], 103.5), Defaults).Target);
    }

    [Fact]
    public void Switched_off_it_casts_neither()
    {
        CompanionSettings off = Defaults with { Curse = false, Sleep = false };

        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([Struck(), Biter(cursed: false)]), off).Act);
        Assert.Equal(Add, new CompanionBrain().Next(Sight([Struck(), Biter(cursed: false)]), Defaults with { Curse = false }).Target);
    }

    [Fact]
    public void Below_half_mana_it_keeps_the_mana_for_heals()
    {
        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([Struck(), Biter()], mana: 240), Defaults).Act);
    }

    [Fact]
    public void A_monster_far_from_the_fight_is_left_alone()
    {
        Foe stranger = new(Add, new Tile(15, 15), false, false, OwnerHits: false, HitsOwner: false);

        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([stranger]), Defaults).Act);
    }

    [Fact]
    public void Switches_go_out_as_kind_six_and_come_back_on_the_master_tie()
    {
        Assert.Equal(new byte[] { 6, 1 }, Companion.Magic(curse: true, sleep: false));
        Assert.Equal(new byte[] { 6, 2 }, Companion.Magic(curse: false, sleep: true));

        byte[] old = [1, 0, 0, 0, 42, 1, (byte)'a'];
        Assert.Equal((true, true), (Companion.ReadTie(old).Tie!.Curse, Companion.ReadTie(old).Tie!.Sleep));

        byte[] tailed = [1, 0, 0, 0, 42, 1, (byte)'a', 2];
        CompanionTie tie = Companion.ReadTie(tailed).Tie!;
        Assert.Equal(("a", false, true), (tie.Name, tie.Curse, tie.Sleep));
    }
}
