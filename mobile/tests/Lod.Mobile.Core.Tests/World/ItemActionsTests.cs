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

        Assert.Equal("입기", ItemActions.Primary(sword));
        Assert.Equal("사용", ItemActions.Primary(potion));
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
}
