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
    public void A_bot_in_danger_shields_itself_with_immortal_unless_already_shielded()
    {
        IReadOnlyList<LearnedSpell> spells = [.. Level41, Spell(20, "이모탈")];
        CompanionSight Hurt(bool shielded) => Sight([Struck()], spells: spells) with
        {
            Vitals = Vitals.Unknown with { Health = 100, MaximumHealth = 500, Mana = 500, MaximumMana = 500 },
            StatusesOf = serial => serial == Me && shielded ? ["dion"] : [],
        };

        Assert.Equal((CompanionAct.Cast, 20, Me), (new CompanionBrain().Next(Hurt(false), Defaults) is var step ? (step.Act, step.Slot, step.Target) : default));
        Assert.NotEqual(20, new CompanionBrain().Next(Hurt(true), Defaults).Slot);

        // 체력이 멀쩡해도 괴물이 2칸 안에 붙으면 미리 건다 — 99 사냥터는 반 아래로 내려가면 늦다.
        CompanionSight Near = Sight([Biter()], spells: spells) with { StatusesOf = _ => [] };
        Assert.Equal(20, new CompanionBrain().Next(Near, Defaults).Slot);
    }

    [Fact]
    public void Between_wakes_the_bot_still_heals_instead_of_only_waiting()
    {
        CompanionBrain brain = new();
        CompanionSight Fallen(double seconds) => Sight([], seconds) with
        {
            HealthOf = _ => 1,
            StatusesOf = serial => serial == Owner ? ["skulled"] : [],
        };

        Assert.Equal(CompanionAct.WakeOwner, brain.Next(Fallen(100), Defaults).Act);

        // 깨우기가 안 먹어 아직 혼수 — 주문 사이(1초) 안에는 전처럼 기다리지 않고 다른 할 일을 본다(여기선 걸을 일이 없어 기다림이어도
        // 깨우기 사이 기다림은 아니다).
        Assert.NotEqual("주인 깨우기 사이", brain.Next(Fallen(100.5), Defaults).Why);

        // 1초 뒤 — 다시 깨우기 전에 회복이 먼저(쿠로, 칸 1). 3초가 지나면 다시 깨운다.
        Assert.Equal((CompanionAct.Cast, 1, Owner), (brain.Next(Fallen(101.1), Defaults) is var heal ? (heal.Act, heal.Slot, heal.Target) : default));
        Assert.Equal(CompanionAct.WakeOwner, brain.Next(Fallen(103.2), Defaults).Act);
    }

    [Fact]
    public void While_the_owner_is_hurt_the_bot_heals_and_does_not_curse_between_heals()
    {
        CompanionBrain brain = new();
        CompanionSight Hurt(double seconds) => Sight([Struck()], seconds) with { HealthOf = _ => 20 };

        Assert.Equal((CompanionAct.Cast, 1, Owner), (brain.Next(Hurt(100), Defaults) is var heal ? (heal.Act, heal.Slot, heal.Target) : default));

        // 회복 사이(1.5초) 안, 주문 사이(1초)는 지났다 — 전에는 여기서 저주를 걸었다(사용자 2026-10-04: 쓰러진 주인 옆에서 저주·나르콜리).
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Hurt(101.1), Defaults).Act);
        Assert.Equal((CompanionAct.Cast, 1), (brain.Next(Hurt(101.6), Defaults) is var again ? (again.Act, again.Slot) : default));
    }

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
        // 저주는 한 칸 — 렌토가 걸려 있으면 더 센 바르도도 풀릴 때까지 안 건다.
        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([Struck(cursed: true)]), Defaults).Act);

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
        CompanionSettings off = Defaults with { Magic = CompanionSpells.Magic.None };

        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Sight([Struck(), Biter(cursed: false)]), off).Act);
        Assert.Equal(Add, new CompanionBrain().Next(Sight([Struck(), Biter(cursed: false)]), Defaults with { Magic = CompanionSpells.Magic.Sleep }).Target);
    }

    [Fact]
    public void Only_ticked_curses_are_cast_the_strongest_of_them()
    {
        // 바르도를 끄면 바르도를 배웠어도 렌토.
        Assert.Equal(10, new CompanionBrain().Next(Sight([Struck()]), Defaults with { Magic = CompanionSpells.Magic.Lento }).Slot);

        // 렌토만 끄면 바르도.
        Assert.Equal(11, new CompanionBrain().Next(Sight([Struck()]), Defaults with { Magic = CompanionSpells.Magic.All & ~CompanionSpells.Magic.Lento }).Slot);
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
    public void Untouched_only_the_strongest_curse_the_bot_can_use_is_ticked()
    {
        // 봇 레벨 = 주인 − 2.
        Assert.Equal(CompanionSpells.Magic.Sleep, CompanionSpells.DefaultMagic(12));
        Assert.Equal(CompanionSpells.Magic.Sleep | CompanionSpells.Magic.Lento, CompanionSpells.DefaultMagic(13));
        Assert.Equal(CompanionSpells.Magic.Sleep | CompanionSpells.Magic.Bardo, CompanionSpells.DefaultMagic(43));
        Assert.Equal(CompanionSpells.Magic.Sleep | CompanionSpells.Magic.Depreco, CompanionSpells.DefaultMagic(73));
        Assert.Equal(CompanionSpells.Magic.Sleep | CompanionSpells.Magic.Prabo, CompanionSpells.DefaultMagic(93));
        Assert.Equal(CompanionSpells.Magic.Sleep | CompanionSpells.Magic.Depreco, CompanionSpells.DefaultMagic(92));
        Assert.Equal(CompanionSpells.Magic.Prabo, CompanionSpells.SwitchOf("프라보 (Lev:1/100)"));
    }

    [Fact]
    public void Orders_go_out_as_kind_six_and_come_back_on_the_master_tie()
    {
        Assert.Equal(new byte[] { 6, 5, 3, 2, 255, 5, 40 },
            Companion.Orders(CompanionSpells.Magic.Lento | CompanionSpells.Magic.Bardo,
                CompanionSpells.Priest.Dinarcoli | CompanionSpells.Priest.Disoruma, 2, CompanionSpells.HealOff, 5, 40));

        byte[] old = [1, 0, 0, 0, 42, 1, (byte)'a'];
        CompanionTie plain = Companion.ReadTie(old).Tie!;
        Assert.Equal((CompanionSpells.Magic.All, CompanionSpells.Priest.All, 0, 0, 0), (plain.Magic, plain.Priest, plain.Heal, plain.GroupHeal, plain.Follow));

        byte[] tailed = [1, 0, 0, 0, 42, 1, (byte)'a', 8, 4, 3, 255, 6];
        CompanionTie tie = Companion.ReadTie(tailed).Tie!;
        Assert.Equal(("a", CompanionSpells.Magic.Depreco, CompanionSpells.Priest.Horrama, 3, 255, 6),
            (tie.Name, tie.Magic, tie.Priest, tie.Heal, tie.GroupHeal, tie.Follow));

        // 여섯째 — 주인 회복 % (2026-10-05). 콜라마·벨라르모 비트(16·32)도 그대로 온다.
        byte[] full = [1, 0, 0, 0, 42, 1, (byte)'a', 8, 0x30, 3, 255, 6, 40];
        CompanionTie healer = Companion.ReadTie(full).Tie!;
        Assert.Equal((CompanionSpells.Priest.Colama | CompanionSpells.Priest.Belra, 40, 0), (healer.Priest, healer.HealPercent, tie.HealPercent));
    }

    private static readonly IReadOnlyList<LearnedSpell> Healer =
        [Spell(1, "쿠로"), Spell(2, "쿠라노"), Spell(3, "쿠라노소"), Spell(4, "호르라마"), Spell(5, "디나르콜리")];

    private static CompanionSight Hurt(int ownerHealth = 50) => Sight([]) with { Spells = Healer, HealthOf = serial => serial == Owner ? ownerHealth : null };

    [Fact]
    public void A_heal_pick_is_a_ceiling_and_off_means_none()
    {
        Assert.Equal(3, new CompanionBrain().Next(Hurt(), Defaults).Slot); // 자동 — 쿠라노소
        Assert.Equal(2, new CompanionBrain().Next(Hurt(), Defaults with { Heal = 2 }).Slot); // 쿠라노까지
        Assert.DoesNotContain("회복", new CompanionBrain().Next(Hurt(), Defaults with { Heal = CompanionSpells.HealOff }).Why);
    }

    [Fact]
    public void Switched_off_buffs_and_cures_are_not_cast()
    {
        CompanionSettings none = Defaults with { Priest = CompanionSpells.Priest.None };

        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(Hurt(ownerHealth: 100), none).Act); // 호르라마 꺼짐

        CompanionSight asleep = Hurt(ownerHealth: 100) with
        {
            StatusesOf = serial => serial == Owner ? new HashSet<string> { "sleep", "horrama" } : new HashSet<string> { "horrama" },
        };
        Assert.Equal(5, new CompanionBrain().Next(asleep, Defaults).Slot);
        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(asleep, none).Act);
    }
}
