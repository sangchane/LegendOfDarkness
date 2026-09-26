using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 동료 봇(성직자)의 판단(<see cref="CompanionBrain" />)과 선(0xF1 부르기·보내기, 0x5E 동료 사이).
/// 우선순위: 멈춤(혼수·죽음) &gt; 주인 회복 &gt; 자기 회복 &gt; 버프 유지 &gt; 따라가기 &gt; 쉬기·대기.
/// </summary>
public sealed class CompanionTests
{
    private const uint Me = 7;
    private const uint Owner = 42;
    private static readonly Tile Here = new(10, 10);
    private static readonly CompanionSettings Defaults = new();

    private static LearnedSpell Spell(int slot, string name) => new(slot, 0, SpellTargetType.ChooseTarget, name, string.Empty, 0);

    private static readonly IReadOnlyList<LearnedSpell> Level21 =
    [
        Spell(1, "쿠로"), Spell(2, "신성력강화"), Spell(3, "쿠러스"), Spell(4, "호르라마"), Spell(5, "에나르마"), Spell(6, "쿠라노"),
    ];

    private static CompanionSight Sight(
        int ownerHealth = 100,
        Tile? owner = null,
        int health = 500,
        int mana = 500,
        IReadOnlyList<LearnedSpell>? spells = null,
        bool comatose = false,
        double seconds = 100,
        uint master = Owner,
        IReadOnlyCollection<Tile>? walls = null) => new()
    {
        Me = Me,
        Master = master,
        Standing = Here,
        Vitals = Vitals.Unknown with { Health = health, MaximumHealth = 500, Mana = mana, MaximumMana = 500 },
        Comatose = comatose,
        OwnerAt = owner ?? new Tile(11, 10),
        HealthOf = serial => serial == Owner ? ownerHealth : null,
        Spells = spells ?? Level21,
        Blocked = tile => walls?.Contains(tile) ?? false,
        Now = TimeSpan.FromSeconds(seconds),
    };

    /// <summary>버프는 이미 걸었다고 쳐 둔다 — 회복·따라가기만 보려는 시험에서.</summary>
    private static CompanionBrain Buffed(double at = 99)
    {
        CompanionBrain brain = new();
        brain.Next(Sight(seconds: at - 5), Defaults);
        brain.Next(Sight(seconds: at - 4), Defaults);
        brain.Next(Sight(seconds: at - 3), Defaults);
        brain.Next(Sight(seconds: at - 2), Defaults);
        return brain;
    }

    [Fact]
    public void Without_a_master_it_waits()
    {
        Assert.Equal(CompanionAct.Wait, new CompanionBrain().Next(Sight(master: 0, ownerHealth: 10), Defaults).Act);
    }

    [Fact]
    public void In_a_coma_or_dead_it_stops_everything()
    {
        Assert.Equal(CompanionAct.Stop, new CompanionBrain().Next(Sight(comatose: true, ownerHealth: 10), Defaults).Act);
        Assert.Equal(CompanionAct.Stop, new CompanionBrain().Next(Sight(health: 0, ownerHealth: 10), Defaults).Act);
    }

    [Fact]
    public void A_hurt_owner_gets_the_strongest_heal_it_can_pay_for()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 50), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(6, step.Slot); // 쿠라노(위즈×20, 30마력) — 쿠로(×15)보다 세다
        Assert.Equal(Owner, step.Target);
    }

    [Fact]
    public void Short_of_mana_it_falls_back_to_a_cheaper_heal()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 50, mana: 25), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(1, step.Slot); // 쿠로 — 신성력강화가 있으니 22마력
    }

    [Fact]
    public void The_owner_comes_before_itself()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 40, health: 100), Defaults);

        Assert.Equal(Owner, step.Target);
    }

    [Fact]
    public void Both_hurt_takes_a_group_heal_when_there_is_one()
    {
        IReadOnlyList<LearnedSpell> spells = [Spell(1, "쿠로"), Spell(3, "쿠러스")];

        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 40, health: 100, spells: spells), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(3, step.Slot);
    }

    [Fact]
    public void It_heals_itself_when_the_owner_is_fine()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 95, health: 100), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(Me, step.Target);
    }

    [Fact]
    public void A_full_owner_is_not_healed_and_buffs_come_next()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 100), Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(4, step.Slot); // 호르라마 — 주인부터
        Assert.Equal(Owner, step.Target);
    }

    /// <summary>
    /// 5.99 스크립트의 지속 시간(호르라마 120초 · 에나르마 150초)이 끝나기 전엔 다시 걸지 않는다 — 걸린 사람에게
    /// "이미 걸려있습니다." 가 간다.
    /// </summary>
    [Fact]
    public void A_buff_is_kept_up_for_its_whole_length_and_then_cast_again()
    {
        CompanionBrain brain = Buffed(at: 100);

        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight(seconds: 200), Defaults).Act);

        CompanionStep again = brain.Next(Sight(seconds: 100 - 5 + 121), Defaults);
        Assert.Equal(CompanionAct.Cast, again.Act);
        Assert.Equal(4, again.Slot);
    }

    [Fact]
    public void Heals_wait_for_the_last_to_land()
    {
        CompanionBrain brain = new();
        Assert.Equal(CompanionAct.Cast, brain.Next(Sight(ownerHealth: 50, seconds: 100), Defaults).Act);
        Assert.NotEqual(CompanionAct.Cast, brain.Next(Sight(ownerHealth: 50, seconds: 100.3), Defaults).Act);
        Assert.Equal(CompanionAct.Cast, brain.Next(Sight(ownerHealth: 50, seconds: 102), Defaults).Act);
    }

    [Fact]
    public void A_far_owner_is_followed_round_a_wall()
    {
        CompanionBrain brain = Buffed();

        // 동쪽 바로 옆이 벽 — 돌아간다.
        CompanionStep step = brain.Next(Sight(owner: new Tile(15, 10), walls: [new Tile(11, 10)]), Defaults);

        Assert.Equal(CompanionAct.Walk, step.Act);
        Assert.NotEqual(Direction.East, step.Toward);
    }

    [Fact]
    public void Close_enough_it_stands_still()
    {
        CompanionBrain brain = Buffed();

        Assert.Equal(CompanionAct.Wait, brain.Next(Sight(owner: new Tile(12, 10)), Defaults).Act);
    }

    [Fact]
    public void Steps_are_paced()
    {
        CompanionBrain brain = Buffed();

        Assert.Equal(CompanionAct.Walk, brain.Next(Sight(owner: new Tile(16, 10), seconds: 100), Defaults).Act);
        Assert.Equal(CompanionAct.Wait, brain.Next(Sight(owner: new Tile(16, 10), seconds: 100.1), Defaults).Act);
    }

    [Fact]
    public void Out_of_mana_it_rests_instead_of_casting()
    {
        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 100, mana: 5), Defaults);

        Assert.Equal(CompanionAct.Rest, step.Act);
    }

    [Fact]
    public void An_owner_out_of_sight_is_not_healed()
    {
        CompanionSight sight = Sight(ownerHealth: 10) with { OwnerAt = null };

        Assert.NotEqual(Owner, new CompanionBrain().Next(sight, Defaults).Target);
    }

    /// <summary>서버는 마법 이름 뒤에 숙련도를 붙여 보낸다(격리 서버에서 본 그대로: "쿠라노 (Lev:1/100)").</summary>
    [Fact]
    public void Spell_names_are_read_without_the_skill_level_tail()
    {
        IReadOnlyList<LearnedSpell> spells = [Spell(1, "쿠로 (Lev:1/100)"), Spell(6, "쿠라노 (Lev:1/100)")];

        CompanionStep step = new CompanionBrain().Next(Sight(ownerHealth: 50, spells: spells), Defaults);

        Assert.Equal(6, step.Slot);
        Assert.Equal("리젠(Lev1)", CompanionSpells.Bare("리젠(Lev1)"));
    }

    [Fact]
    public void Calling_and_sending_away_are_one_byte()
    {
        Assert.Equal(new byte[] { 1 }, Companion.Call());
        Assert.Equal(new byte[] { 0 }, Companion.Dismiss());
    }

    [Fact]
    public void The_tie_names_whose_companion_or_master()
    {
        byte[] body = [2, 0, 0, 0, 9, .. LegacyKoreanEncoding.EncodeStringA("동료사제")];

        (byte kind, CompanionTie? tie) = Companion.ReadTie(body);

        Assert.Equal(Companion.CompanionKind, kind);
        Assert.Equal(new CompanionTie(9, "동료사제"), tie);
    }

    [Fact]
    public void Serial_zero_means_the_tie_is_over()
    {
        (byte kind, CompanionTie? tie) = Companion.ReadTie([1, 0, 0, 0, 0, 0]);

        Assert.Equal(Companion.MasterKind, kind);
        Assert.Null(tie);
    }
}
