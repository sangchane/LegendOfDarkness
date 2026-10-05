using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>방향판 위 속성 목걸이 칸 — 무엇을 가리키고, 어느 가방 칸을 쓰고, 전환 칸이 다음에 무엇을 끼나.</summary>
public sealed class NecklaceSwapTests
{
    private static ItemStats Necklace(Element element) =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (int)element, 0, 0, NecklaceSwap.Place);

    private static readonly InventoryItem Sea = new(3, 100, 0, "바다의목걸이", 1, 10, 10, Necklace(Element.Water));
    private static readonly InventoryItem SeaStar = new(4, 101, 0, "바다의별목걸이", 1, 10, 10, Necklace(Element.Water));
    private static readonly InventoryItem Life = new(5, 102, 0, "생명의목걸이", 1, 10, 10, Necklace(Element.Light));
    private static readonly InventoryItem Dark = new(6, 103, 0, "암흑의목걸이", 1, 10, 10, Necklace(Element.Dark));
    private static readonly InventoryItem FireSword = new(7, 104, 0, "광단검화", 1, 10, 10,
        new ItemStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (int)Element.Fire, 0, 0, 1));

    private static WornItem Wearing(InventoryItem item) =>
        new(NecklaceSwap.Place, item.Icon, item.Name, item.Name, 10, 10, item.Stats);

    [Fact]
    public void Choices_are_necklaces_of_that_element_worn_or_carried_once_each()
    {
        IReadOnlyList<InventoryItem> pack = [Sea, SeaStar, Life, FireSword, Sea with { Slot = 9 }];

        Assert.Equal(["바다의목걸이", "바다의별목걸이"],
            NecklaceSwap.Choices(pack, [], [Element.Water]).Select(one => one.Name));
        Assert.Empty(NecklaceSwap.Choices(pack, [], [Element.Fire])); // 칼은 목걸이가 아니다
        Assert.Equal(["암흑의목걸이", "생명의목걸이"],
            NecklaceSwap.Choices([Life], [Wearing(Dark)], NecklaceSwap.Pair).Select(one => one.Name));
    }

    [Fact]
    public void Chosen_is_the_pick_or_else_the_first_owned()
    {
        Assert.Equal("바다의별목걸이", NecklaceSwap.Chosen("바다의별목걸이", [Sea], [], Element.Water));
        Assert.Equal("바다의목걸이", NecklaceSwap.Chosen(null, [Sea, SeaStar], [], Element.Water));
        Assert.Null(NecklaceSwap.Chosen(null, [Life], [], Element.Earth));
    }

    [Fact]
    public void Slot_is_the_pack_slot_holding_that_necklace()
    {
        Assert.Equal(4, NecklaceSwap.SlotOf([Sea, SeaStar], "바다의별목걸이"));
        Assert.Null(NecklaceSwap.SlotOf([Sea], "바다의별목걸이"));
    }

    [Fact]
    public void The_switch_puts_on_life_after_dark_and_dark_otherwise()
    {
        Assert.Equal(Element.Light, NecklaceSwap.Next([Wearing(Dark)]));
        Assert.Equal(Element.Dark, NecklaceSwap.Next([Wearing(Life)]));
        Assert.Equal(Element.Dark, NecklaceSwap.Next([Wearing(Sea)]));
        Assert.Equal(Element.Dark, NecklaceSwap.Next([]));
    }
}
