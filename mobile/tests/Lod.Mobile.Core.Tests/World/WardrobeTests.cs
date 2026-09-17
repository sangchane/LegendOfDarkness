using Lod.Mobile.Core.Art;
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
        int accessory = 0,
        int hairColour = 0,
        int bootColour = 0) =>
        new(head, body, armor, boots, shield, weapon, hairColour, bootColour, accessory, 0, 0, 0, overCoat);

    private static string[] Names(Appearance worn) => [.. Wardrobe.Pieces(worn).Select(piece => piece.Name)];

    [Fact]
    public void A_man_in_nothing_is_still_a_body_and_a_pair_of_trousers()
    {
        Assert.Equal(["mb001", "mn001"], Names(Wearing()));
    }

    [Fact]
    public void Every_piece_the_server_names_is_asked_for()
    {
        Assert.Equal(
            ["ms006", "mb001", "mn001", "ml001", "mu061", "ma061", "mi007", "mw020", "mp020", "mh003", "me003", "mf003", "mc009"],
            Names(Wearing(
                head: 3, body: 16 + 2, armor: 61, boots: 1, shield: 6, overCoat: 7, weapon: 20,
                accessory: 9)));
    }

    /// <summary>
    /// The 5.99 client stacks a figure from a fixed table in Legend.exe (0x69c200, 15 slots per direction, letters
    /// "SBNLHUDHAWSCPEF" at 0x86b614). Facing us the weapon is the lowest real layer, so the body and arms cover the
    /// hand that holds it; from behind it goes over the body but under the arms and head. The shield is at the
    /// bottom from behind and near the top facing us.
    /// </summary>
    [Fact]
    public void Facing_us_the_weapon_is_drawn_first_and_the_body_covers_the_hand()
    {
        Assert.Equal(
            ["mw020", "mf003", "mb001", "mn001", "ml001", "mh003", "mu061", "ma061", "me003", "mp020", "ms006", "mc009"],
            Stacked(Side.Front));
    }

    [Fact]
    public void From_behind_the_shield_is_first_and_the_weapon_goes_under_the_arms_and_head()
    {
        Assert.Equal(
            ["ms006", "mb001", "mn001", "ml001", "mu061", "mf003", "mw020", "ma061", "mh003", "me003", "mp020", "mc009"],
            Stacked(Side.Back));
    }

    private static string[] Stacked(Side side) =>
    [
        .. Wardrobe.Pieces(Wearing(head: 3, body: 16 + 2, armor: 61, boots: 1, shield: 6, weapon: 20, accessory: 9))
            .OrderBy(piece => Wardrobe.Rank(piece.Name[1], side))
            .Select(piece => piece.Name)
    ];

    /// <summary>The head's front and back pieces are dyed with the hair, as the head is — the client dyes N L H E F.</summary>
    [Fact]
    public void The_pieces_that_share_the_head_number_share_its_colour()
    {
        IReadOnlyList<Piece> pieces = Wardrobe.Pieces(Wearing(head: 26, hairColour: 7));

        Assert.Equal(7, pieces.Single(piece => piece.Name == "me026").Colour);
        Assert.Equal(7, pieces.Single(piece => piece.Name == "mf026").Colour);
    }

    /// <summary>The head number is a hat above a hundred and hair below it, in the same family of files.</summary>
    [Fact]
    public void A_helmet_and_a_hairstyle_are_drawn_from_the_same_place()
    {
        Assert.Contains("mh285", Names(Wearing(head: 285)));
        Assert.Contains("mh003", Names(Wearing(head: 3)));
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
        Assert.Equal(expected, Names(Wearing(body: body))[0]);
    }

    /// <summary>
    /// Only men are drawn with trousers, and always the same pair. The number in the bottom half of the
    /// body byte says what colour to dye them, which is not a drawing.
    /// </summary>
    [Fact]
    public void Trousers_are_one_drawing_for_men_and_none_for_women()
    {
        Assert.Equal(["mb001", "mn001"], Names(Wearing(body: 16 + 5)));
        Assert.Equal(["wb001"], Names(Wearing(body: 32 + 5)));
    }

    /// <summary>
    /// Only three pieces are dyed, and the trousers take their colour from the bottom half of the body
    /// byte rather than from a colour of their own.
    /// </summary>
    [Fact]
    public void The_head_the_boots_and_the_trousers_carry_a_colour()
    {
        IReadOnlyList<Piece> pieces = Wardrobe.Pieces(
            Wearing(head: 3, body: 16 + 5, boots: 1, shield: 6, hairColour: 11, bootColour: 4));

        Assert.Equal(11, pieces.Single(piece => piece.Name == "mh003").Colour);
        Assert.Equal(4, pieces.Single(piece => piece.Name == "ml001").Colour);
        Assert.Equal(5, pieces.Single(piece => piece.Name == "mn001").Colour);
        Assert.Equal(0, pieces.Single(piece => piece.Name == "ms006").Colour);
    }
}
