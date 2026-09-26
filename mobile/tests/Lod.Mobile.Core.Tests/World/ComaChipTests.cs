using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>코마디움 칸의 규칙(<see cref="ComaChip" />) — 원작 5.99 코마디움·엑스코마디움 스크립트 그대로.</summary>
public sealed class ComaChipTests
{
    private static InventoryItem Item(int slot, string name, int stacks = 3) => new(slot, 0, 0, name, stacks, 0, 0);

    private static readonly IReadOnlyList<InventoryItem> Both = [Item(4, "코마디움"), Item(6, "엑스코마디움", 1)];

    [Fact]
    public void In_a_coma_i_use_ex_comadium_on_myself()
    {
        Assert.Equal(new ComaChoice(ComaUse.UseOnSelf, 6), ComaChip.Choose(true, true, Both));
    }

    [Fact]
    public void Plain_comadium_does_not_work_on_myself()
    {
        Assert.Equal(ComaUse.Missing, ComaChip.Choose(true, false, [Item(4, "코마디움")]).Use);
    }

    [Fact]
    public void A_comatose_bot_is_woken_even_without_comadium()
    {
        Assert.Equal(ComaUse.WakeBot, ComaChip.Choose(false, true, Both).Use);
        Assert.Equal(ComaUse.WakeBot, ComaChip.Choose(false, true, []).Use);
    }

    [Fact]
    public void Nobody_in_a_coma_is_said_so()
    {
        Assert.Equal(new ComaChoice(ComaUse.NotComatose, Why: "혼수 상태가 아닙니다."), ComaChip.Choose(false, false, Both));
    }

    [Fact]
    public void The_count_is_both_kinds()
    {
        Assert.Equal(4, ComaChip.Count(Both));
    }
}
