using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The little action row that opens beside a picked item: what its main button says, the one line under its name, and
/// the double tap that does the main thing at once.
/// </summary>
public sealed class ItemActionsTests
{
    /// <summary>Something with wear on it is gear and is put on; a potion or a bundle has none and is used.</summary>
    [Fact]
    public void Gear_is_put_on_and_everything_else_is_used()
    {
        InventoryItem sword = new(1, 32905, 0, "설단검", 1, 30, 100);
        InventoryItem potion = new(2, 32813, 0, "쿠룸", 12, 0, 0);

        Assert.Equal("장착", ItemActions.Primary(sword, []));
        Assert.Equal("사용", ItemActions.Primary(potion, []));
        Assert.True(ItemActions.IsGear(sword));
        Assert.False(ItemActions.IsGear(potion));
    }

    [Fact]
    public void The_line_under_the_name_says_how_many_or_how_worn()
    {
        Assert.Equal("12개", ItemActions.Line(new InventoryItem(2, 32813, 0, "쿠룸", 12, 0, 0)));
        Assert.Equal("내구 30/100", ItemActions.Line(new InventoryItem(1, 32905, 0, "설단검", 1, 30, 100)));
        Assert.Equal(string.Empty, ItemActions.Line(new InventoryItem(3, 32813, 0, "편지", 1, 0, 0)));
        Assert.Equal("신발 · 내구 30/100", ItemActions.Line(new WornItem(13, 32882, "장화", "장화", 30, 100)));
        Assert.Equal("무기", ItemActions.Line(new WornItem(1, 32882, "막대", "막대", 0, 0)));
    }

    /// <summary>Two taps on the same thing close together are a double tap; on another thing, or slow, they are not.</summary>
    [Fact]
    public void A_double_tap_is_the_same_thing_twice_quickly()
    {
        DoubleTap tap = new();

        Assert.False(tap.Tap(5, 10.0));
        Assert.True(tap.Tap(5, 10.2));

        // 세 번째는 새 첫 번째다 — 세 번 누르면 두 번 쓰지 않는다.
        Assert.False(tap.Tap(5, 10.3));

        Assert.False(tap.Tap(6, 10.4));
        Assert.False(tap.Tap(6, 11.0));
    }

    private static ItemStats Numbers(int ac = 0, int str = 0, int place = 2) =>
        new(ac, 0, 0, str, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, place);

    /// <summary>Set beside what is worn in the same place: ▲ better, ▼ worse — and lower armour class is better.</summary>
    [Fact]
    public void Gear_is_weighed_against_what_is_worn_in_its_place()
    {
        Assert.Equal(["방어 -10 ▲4", "힘 0 ▼2"], ItemActions.Stats(Numbers(ac: -10), Numbers(ac: -6, str: 2)).Select(line => line.Text));
        Assert.Equal(["방어 -10"], ItemActions.Stats(Numbers(ac: -10)).Select(line => line.Text));
    }

    /// <summary>교체 when something is on in that place, 장착 when it is empty — a ring has two places and fills an empty one.</summary>
    [Fact]
    public void The_main_button_says_whether_it_takes_a_place_or_fills_one()
    {
        InventoryItem armour = new(1, 1, 0, "갑옷", 1, 30, 100, Numbers(place: 2));
        InventoryItem ring = new(2, 1, 0, "반지", 1, 30, 100, Numbers(place: 7));
        WornItem coat = new(2, 1, "옷", "옷", 30, 100, Numbers(ac: -3));
        WornItem left = new(7, 1, "반지", "반지", 30, 100);

        Assert.Equal("장착", ItemActions.Primary(armour, []));
        Assert.Equal("교체", ItemActions.Primary(armour, [coat]));
        Assert.Same(coat, ItemActions.WornInstead(armour, [coat]));
        Assert.Equal("장착", ItemActions.Primary(ring, [left]));
        Assert.Equal("교체", ItemActions.Primary(ring, [left, left with { Slot = 8 }]));
    }
}
