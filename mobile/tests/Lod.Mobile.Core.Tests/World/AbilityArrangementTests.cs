using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// A long press moves a learned skill or spell to a chosen ability-bar slot (사용자 요청, 2026-09-25). These
/// tests are the rule book for that — no Godot, so the swap/clear/autofill logic is checked without a screen.
/// </summary>
public sealed class AbilityArrangementTests
{
    private sealed record Item(int Slot, string Name);

    private static IReadOnlyList<Item> Learned(params int[] slots) =>
        [.. slots.Select(slot => new Item(slot, $"기술 {slot}"))];

    private static string?[] Names(IReadOnlyList<Item?> items) => [.. items.Select(item => item?.Name)];

    [Fact]
    public void An_untouched_arrangement_fills_in_the_servers_own_order()
    {
        AbilityArrangement arrangement = new();
        IReadOnlyList<Item?> filled = arrangement.Fill(Learned(1, 2, 3), item => item.Slot, 6);

        string?[] expected = ["기술 1", "기술 2", "기술 3", null, null, null];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void Placing_something_into_an_empty_slot_moves_it_there_and_leaves_its_old_spot_empty()
    {
        AbilityArrangement arrangement = new();

        // 3자리 슬롯(0..2)에서 "기술 1"을 5번 자리로 옮긴다. 5번 자리는 원래 비어 있었다(displaced: null).
        arrangement.Place(5, slot: 1, displaced: null);

        IReadOnlyList<Item?> filled = arrangement.Fill(Learned(1, 2, 3), item => item.Slot, 6);

        // 기술 1 은 이제 5번 자리, 남은 둘(2, 3)이 0·1번을 채운다 — 원래 0번 자리(기술 1)는 비게 되지 않고 당겨진다.
        string?[] expected = ["기술 2", "기술 3", null, null, null, "기술 1"];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void Picking_something_already_placed_elsewhere_swaps_the_two_slots()
    {
        AbilityArrangement arrangement = new();
        arrangement.Assign(0, slot: 1);
        arrangement.Assign(1, slot: 2);

        // 1번 자리(기술 2)에서 기술 1을 고른다 — 지금 1번 자리에 있던 기술 2(displaced)가 기술 1의 옛 자리로 간다.
        arrangement.Place(position: 1, slot: 1, displaced: 2);

        IReadOnlyList<Item?> filled = arrangement.Fill(Learned(1, 2, 3), item => item.Slot, 6);

        string?[] expected = ["기술 2", "기술 1", "기술 3", null, null, null];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void An_explicitly_cleared_slot_stays_empty_even_though_more_are_learned()
    {
        AbilityArrangement arrangement = new();
        arrangement.Clear(0);

        IReadOnlyList<Item?> filled = arrangement.Fill(Learned(1, 2, 3), item => item.Slot, 6);

        // 0번 자리는 비운 채로 있고, 나머지 배운 것들이 그다음 자리부터 채운다.
        string?[] expected = [null, "기술 1", "기술 2", "기술 3", null, null];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void A_newly_learned_skill_lands_in_the_next_untouched_slot_without_disturbing_the_arrangement()
    {
        AbilityArrangement arrangement = new();
        arrangement.Assign(3, slot: 1); // 사람이 기술 1을 3번 자리로 옮겨 두었다.

        // 기술 2를 막 배웠다 — 손댄 적 없는 자리(0번)로 들어가야지, 3번 자리를 밀어내면 안 된다.
        IReadOnlyList<Item?> filled = arrangement.Fill(Learned(1, 2), item => item.Slot, 6);

        string?[] expected = ["기술 2", null, null, "기술 1", null, null];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void A_forgotten_skills_old_slot_is_reclaimed_by_the_pool_instead_of_staying_stuck_empty()
    {
        AbilityArrangement arrangement = new();
        arrangement.Assign(0, slot: 1); // 기술 1을 0번에 뒀었는데, 이제 기술 1을 잊었다.

        IReadOnlyList<Item?> filled = arrangement.Fill(Learned(2), item => item.Slot, 6);

        Assert.Equal("기술 2", filled[0]?.Name);
    }

    [Fact]
    public void Clearing_a_never_touched_slot_does_not_shift_the_others_or_bring_the_cleared_skill_back()
    {
        AbilityArrangement arrangement = new();
        IReadOnlyList<Item> learned = Learned(1, 2, 3);

        // 배치 — 아직 아무도 손대지 않아 서버 순서 그대로: 0번 기술1 · 1번 기술2 · 2번 기술3.
        IReadOnlyList<Item?> before = arrangement.Fill(learned, item => item.Slot, 6);
        string?[] beforeExpected = ["기술 1", "기술 2", "기술 3", null, null, null];
        Assert.Equal(beforeExpected, Names(before));

        // 0번 자리(기술 1)를 길게 눌러 "비우기" — 화면은 지금 그 자리에 있던 슬롯 번호(1)를 함께 넘긴다.
        arrangement.Clear(0, slot: 1);

        // 서버가 기술 목록을 다시 보낸다 — 같은 목록.
        IReadOnlyList<Item?> after = arrangement.Fill(learned, item => item.Slot, 6);

        // 버그: 예전에는 기술 1이 1번 자리로 다시 들어오고 기술 2·3이 한 칸씩 밀렸다. 고친 뒤에는 0번만
        // 비고 1·2번은 그대로다 — 기술 1은 어디에도 다시 나타나지 않는다.
        string?[] afterExpected = [null, "기술 2", "기술 3", null, null, null];
        Assert.Equal(afterExpected, Names(after));

        // 저장하고 다시 읽어도 같다.
        AbilityArrangement spells = new();
        string[] lines = [.. AbilitySlotSave.ToLines(arrangement, spells)];

        AbilityArrangement reread = new();
        AbilityArrangement rereadSpells = new();
        AbilitySlotSave.Parse(lines, reread, rereadSpells);

        IReadOnlyList<Item?> reloaded = reread.Fill(learned, item => item.Slot, 6);
        Assert.Equal(Names(after), Names(reloaded));
    }

    [Fact]
    public void Picking_a_removed_skill_again_from_the_list_brings_it_back()
    {
        AbilityArrangement arrangement = new();
        IReadOnlyList<Item> learned = Learned(1, 2, 3);

        arrangement.Fill(learned, item => item.Slot, 6);
        arrangement.Clear(0, slot: 1); // 기술 1을 뺐다.

        // 목록에서 기술 1을 다시 골라 3번 자리에 놓는다 — 3번 자리는 비어 있었다(displaced: null).
        arrangement.Place(3, slot: 1, displaced: null);

        IReadOnlyList<Item?> filled = arrangement.Fill(learned, item => item.Slot, 6);

        // 기술 1이 3번 자리로 돌아왔고, 0·1·2번(기술 2·기술 3)은 그대로다.
        string?[] expected = [null, "기술 2", "기술 3", "기술 1", null, null];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void Save_lines_round_trip_through_parse()
    {
        AbilityArrangement skills = new();
        skills.Assign(0, 4);
        skills.Clear(2);

        AbilityArrangement spells = new();
        spells.Assign(1, 7);

        string[] lines = [.. AbilitySlotSave.ToLines(skills, spells)];

        AbilityArrangement readSkills = new();
        AbilityArrangement readSpells = new();
        AbilitySlotSave.Parse(lines, readSkills, readSpells);

        Assert.Equal(skills.Positions, readSkills.Positions);
        Assert.Equal(spells.Positions, readSpells.Positions);
    }

    [Fact]
    public void Removed_slots_round_trip_through_save_lines_too()
    {
        AbilityArrangement skills = new();
        skills.Clear(0, slot: 1); // 비운 기술 1 — 다시 채워지면 안 된다.

        AbilityArrangement spells = new();

        string[] lines = [.. AbilitySlotSave.ToLines(skills, spells)];

        AbilityArrangement readSkills = new();
        AbilityArrangement readSpells = new();
        AbilitySlotSave.Parse(lines, readSkills, readSpells);

        Assert.Equal(skills.Positions, readSkills.Positions);
        Assert.Equal(skills.RemovedSlots, readSkills.RemovedSlots);

        // 다시 읽은 뒤 Fill 을 돌려도 기술 1은 여전히 나타나지 않는다.
        IReadOnlyList<Item?> filled = readSkills.Fill(Learned(1, 2, 3), item => item.Slot, 6);
        string?[] expected = [null, "기술 2", "기술 3", null, null, null];
        Assert.Equal(expected, Names(filled));
    }

    [Fact]
    public void Parse_ignores_lines_it_does_not_understand()
    {
        AbilityArrangement skills = new();
        AbilityArrangement spells = new();

        AbilitySlotSave.Parse(["", "garbage", "skill notanumber 4", "skill 0 notanumber", "potion 0 5"], skills, spells);

        Assert.Empty(skills.Positions);
        Assert.Empty(spells.Positions);
    }
}
