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

    // ── 2단계: 서버가 알리는 상태 · 해제 · 포션 · 봇 장비 선 ──────────────────

    private static InventoryItem Item(int slot, string name, int stacks = 5) => new(slot, 0, 0, name, stacks, 0, 0);

    private static Func<uint, IReadOnlyCollection<string>?> On(IReadOnlyCollection<string> owner, IReadOnlyCollection<string>? self = null) =>
        serial => serial == Owner ? owner : serial == Me ? self ?? [] : null;

    [Fact]
    public void With_statuses_known_a_buff_the_owner_still_has_is_not_cast_again()
    {
        CompanionSight sight = Sight() with { StatusesOf = On(["horrama", "enare"], ["horrama", "enare"]) };

        Assert.NotEqual(CompanionAct.Cast, new CompanionBrain().Next(sight, Defaults).Act);
    }

    /// <summary>시계가 아직 남았어도 서버가 "없다" 고 하면 다시 건다(리베라토로 지워졌거나 죽었다 살아난 경우).</summary>
    [Fact]
    public void A_buff_gone_from_the_owner_is_cast_again_at_once()
    {
        CompanionBrain brain = Buffed(at: 100);

        CompanionStep step = brain.Next(Sight(seconds: 110) with { StatusesOf = On(["enare"], ["horrama", "enare"]) }, Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(4, step.Slot); // 호르라마
        Assert.Equal(Owner, step.Target);
    }

    [Fact]
    public void Just_cast_it_waits_for_the_status_report_before_casting_again()
    {
        CompanionBrain brain = new();
        CompanionSight bare = Sight(seconds: 100) with { StatusesOf = On([], ["horrama", "enare"]) };

        Assert.Equal(4, brain.Next(bare, Defaults).Slot);
        CompanionStep next = brain.Next(bare with { Now = TimeSpan.FromSeconds(101.5) }, Defaults);
        Assert.False(next.Act == CompanionAct.Cast && next.Slot == 4 && next.Target == Owner);
    }

    [Fact]
    public void An_asleep_owner_is_woken_with_dinarcoli()
    {
        IReadOnlyList<LearnedSpell> spells = [.. Level21, Spell(7, "디나르콜리"), Spell(8, "디소루마")];
        CompanionSight sight = Sight(spells: spells) with { StatusesOf = On(["sleep", "horrama", "enare"], ["horrama", "enare"]) };

        CompanionStep step = new CompanionBrain().Next(sight, Defaults);

        Assert.Equal(CompanionAct.Cast, step.Act);
        Assert.Equal(7, step.Slot);
        Assert.Equal(Owner, step.Target);
    }

    [Fact]
    public void Low_on_health_it_drinks_before_spending_mana_on_itself()
    {
        CompanionSight sight = Sight(health: 150) with { Pack = [Item(3, "쿠룸"), Item(4, "마라디움")] };

        CompanionStep step = new CompanionBrain().Next(sight, Defaults);

        Assert.Equal(CompanionAct.Drink, step.Act);
        Assert.Equal(3, step.Slot);
    }

    [Fact]
    public void The_owner_is_healed_before_it_drinks()
    {
        CompanionSight sight = Sight(ownerHealth: 40, health: 150) with { Pack = [Item(3, "쿠룸")] };

        Assert.Equal(CompanionAct.Cast, new CompanionBrain().Next(sight, Defaults).Act);
    }

    /// <summary>모자란 350 — 쿠룸(250)으론 모자라고 최하급(500)이 맞다. 상급(3000)은 아낀다.</summary>
    [Fact]
    public void It_picks_the_smallest_potion_that_covers_what_is_missing()
    {
        CompanionSight sight = Sight(health: 150) with
        {
            Pack = [Item(1, "상급체력포션"), Item(2, "쿠룸"), Item(3, "최하급체력포션")],
        };

        Assert.Equal(3, new CompanionBrain().Next(sight, Defaults).Slot);
    }

    [Fact]
    public void Out_of_mana_it_drinks_instead_of_resting()
    {
        CompanionSight sight = Sight(mana: 5) with { Pack = [Item(4, "마라디움")] };

        CompanionStep step = new CompanionBrain().Next(sight, Defaults);

        Assert.Equal(CompanionAct.Drink, step.Act);
        Assert.Equal(4, step.Slot);
    }

    [Fact]
    public void Drinks_are_paced()
    {
        CompanionBrain brain = new();
        CompanionSight sight = Sight(mana: 5, seconds: 100) with { Pack = [Item(4, "마라디움")] };

        Assert.Equal(CompanionAct.Drink, brain.Next(sight, Defaults).Act);
        Assert.NotEqual(CompanionAct.Drink, brain.Next(sight with { Now = TimeSpan.FromSeconds(100.5) }, Defaults).Act);
    }

    [Fact]
    public void Giving_and_taking_off_go_out_as_kinds_two_and_three()
    {
        Assert.Equal(new byte[] { 2, 7, 0, 5 }, Companion.Give(7, 5));
        Assert.Equal(new byte[] { 3, 1 }, Companion.TakeOff(1));
    }

    [Fact]
    public void Statuses_read_name_seconds_and_harm()
    {
        byte[] body = [3, 0, 0, 0, 42, 2, .. LegacyKoreanEncoding.EncodeStringA("enare"), 0, 150, 0, .. LegacyKoreanEncoding.EncodeStringA("sleep"), 0, 9, 1];

        (uint serial, IReadOnlyList<CompanionStatus> listed) = Companion.ReadStatuses(body);

        Assert.Equal(42u, serial);
        Assert.Equal([new CompanionStatus("enare", 150, false), new CompanionStatus("sleep", 9, true)], listed);
    }

    /// <summary>
    /// 새 서버는 목록 뒤에 그림 번호(2바이트)를 하나씩 덧붙인다 — 상태 아이콘 줄이 쓴다. 옛 봇은 뒤를 안 읽어 그대로 돈다.
    /// </summary>
    [Fact]
    public void Statuses_read_the_picture_numbers_appended_after_the_list()
    {
        byte[] body = [3, 0, 0, 0, 42, 2, .. LegacyKoreanEncoding.EncodeStringA("horrama"), 0, 120, 0, .. LegacyKoreanEncoding.EncodeStringA("sleep"), 0, 9, 1, 0, 11, 0, 0];

        (_, IReadOnlyList<CompanionStatus> listed) = Companion.ReadStatuses(body);

        Assert.Equal([new CompanionStatus("horrama", 120, false, 11), new CompanionStatus("sleep", 9, true, 0)], listed);
    }

    /// <summary>0x5E 종류 6 — 그룹원 한 사람: serial · 체력 % · 마력 % · 상태 그림 개수와 그림들. serial 0 은 "그룹 끝".</summary>
    [Fact]
    public void A_member_is_health_mana_and_status_pictures()
    {
        PartyMemberStatus member = Companion.ReadMember([6, 0, 0, 0, 9, 55, 80, 2, 0, 11, 0, 82, .. LegacyKoreanEncoding.EncodeStringA("동료")]);

        Assert.Equal(9u, member.Serial);
        Assert.Equal(55, member.HealthPercent);
        Assert.Equal(80, member.ManaPercent);
        Assert.Equal([11, 82], member.Icons);
        Assert.Equal("동료", member.Name);
    }

    [Fact]
    public void Life_is_two_percentages()
    {
        Assert.Equal(new CompanionLife(9, 80, 35), Companion.ReadLife([4, 0, 0, 0, 9, 80, 35]));
    }

    [Fact]
    public void The_kit_lists_what_the_bot_wears_and_the_potions_it_carries()
    {
        byte[] worn = [1, 0x82, 0x26, 3, .. LegacyKoreanEncoding.EncodeStringA("홀리파나"), .. LegacyKoreanEncoding.EncodeStringA("홀리파나"), 0, 0, 3, 232, 0, 0, 3, 232];
        byte[] body = [5, 0, 0, 0, 9, 1, .. worn, 1, .. LegacyKoreanEncoding.EncodeStringA("쿠룸"), 0x80, 0x2D, 0, 5];

        CompanionKit kit = Companion.ReadKit(body);

        Assert.Equal(new WornItem(1, 0x8226, "홀리파나", "홀리파나", 1000, 1000), Assert.Single(kit.Worn));
        Assert.Equal(new CarriedItem("쿠룸", 0x802D, 5), Assert.Single(kit.Carried));
    }

    /// <summary>벽 파일이 없는 맵 — 동쪽이 막힌 줄 모르고 걸었는데 서버가 되돌렸다(제자리). 다음엔 돌아간다.</summary>
    [Fact]
    public void A_step_the_server_refused_is_remembered_as_a_wall()
    {
        CompanionBrain brain = Buffed();

        Assert.Equal(Direction.East, brain.Next(Sight(owner: new Tile(15, 10), seconds: 100), Defaults).Toward);

        CompanionStep again = brain.Next(Sight(owner: new Tile(15, 10), seconds: 101), Defaults);

        Assert.Equal(CompanionAct.Walk, again.Act);
        Assert.NotEqual(Direction.East, again.Toward);
    }

    /// <summary>수면이면 주인은 아무것도 못 한다 — 체력이 낮아도 해제가 먼저.</summary>
    [Fact]
    public void An_asleep_owner_is_woken_even_before_being_healed()
    {
        IReadOnlyList<LearnedSpell> spells = [.. Level21, Spell(7, "디나르콜리")];
        CompanionSight sight = Sight(ownerHealth: 30, spells: spells) with { StatusesOf = On(["sleep"]) };

        Assert.Equal(7, new CompanionBrain().Next(sight, Defaults).Slot);
    }

    [Fact]
    public void A_ghost_bot_stands_still_until_brought_back()
    {
        CompanionSight sight = Sight(ownerHealth: 30) with { StatusesOf = On([], ["ghost"]) };

        Assert.Equal(CompanionAct.Stop, new CompanionBrain().Next(sight, Defaults).Act);
    }
}
