using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

public sealed class WardrobeTests
{
    private static Appearance Wearing(
        int head = 0,
        int body = 16,
        int armor = 0,
        int boots = 0,
        int shield = 0,
        int overCoat = 0,
        int weapon = 0,
        int accessory = 0) =>
        new(head, body, armor, boots, shield, weapon, 0, 0, accessory, 0, 0, 0, overCoat);

    [Fact]
    public void A_man_in_nothing_is_still_a_body()
    {
        Assert.Equal(["mb001"], Wardrobe.Pieces(Wearing()));
    }

    [Fact]
    public void The_shield_goes_behind_and_the_head_on_top()
    {
        IReadOnlyList<string> pieces = Wardrobe.Pieces(Wearing(
            head: 3, body: 16 + 2, armor: 61, boots: 1, shield: 6, overCoat: 7, weapon: 20, accessory: 9));

        Assert.Equal(
            ["ms006", "mb001", "mn002", "ml001", "mu061", "mi007", "mw020", "mh003", "mc009"],
            pieces);
    }

    /// <summary>The head number is a hat above a hundred and hair below it, in the same family of files.</summary>
    [Fact]
    public void A_helmet_and_a_hairstyle_are_drawn_from_the_same_place()
    {
        Assert.Contains("mh285", Wardrobe.Pieces(Wearing(head: 285)));
        Assert.Contains("mh003", Wardrobe.Pieces(Wearing(head: 3)));
    }

    /// <summary>The body byte says which kind of body, and women's drawings are in their own archive.</summary>
    [Theory]
    [InlineData(16, "mb001")]
    [InlineData(32, "wb001")]
    [InlineData(48, "mb001")]
    [InlineData(64, "wb001")]
    [InlineData(144, "wb001")]
    [InlineData(176, "wb001")]
    public void The_body_says_which_archive_the_drawings_come_from(int body, string expected)
    {
        Assert.Equal([expected], Wardrobe.Pieces(Wearing(body: body)));
    }

    /// <summary>Trousers live in the bottom half of the same byte.</summary>
    [Fact]
    public void The_body_byte_carries_the_trousers_too()
    {
        Assert.Equal(["mb001", "mn005"], Wardrobe.Pieces(Wearing(body: 16 + 5)));
    }
}
