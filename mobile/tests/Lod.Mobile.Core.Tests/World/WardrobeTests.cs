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
    public void A_man_in_nothing_is_still_a_body()
    {
        Assert.Equal(["mb001"], Names(Wearing()));
    }

    /// <summary>
    /// 유령(몸 종류 3·4 — ServerFormat33 이 죽은 사람에게 0x30·0x40 을 보낸다)은 5.99 아카이브의 몸 002, 고리와 날개를 단
    /// 유령 한 장이다. 벗은 몸(001)을 그리면 옷이 벗겨진 것처럼 보였다(사용자, 2026-09-24).
    /// </summary>
    [Theory]
    [InlineData(0x30, "mb002")]
    [InlineData(0x40, "wb002")]
    public void A_ghost_is_the_ghost_drawing_and_nothing_else(int body, string drawing)
    {
        Assert.Equal([drawing], Names(Wearing(body: body, hairColour: 5)));
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
    [InlineData(48, "mb002")] // 유령 — A_ghost_is_the_ghost_drawing_and_nothing_else
    [InlineData(64, "wb002")]
    [InlineData(144, "wb001")]
    [InlineData(176, "wb001")]
    public void The_body_says_which_archive_the_drawings_come_from(int body, string expected)
    {
        Assert.Equal(expected, Names(Wearing(body: body))[0]);
    }

    /// <summary>
    /// 바지는 몸 바이트 아래 반쪽이 0 이 아닐 때만 입는다 — 5.99 Legend.exe 0x54ffaa..0x55006b 가 그 반쪽을 떼어
    /// 0 이 아니면 바지 번호 1, 0 이면 0 으로 적고(0x55005d · 0x550069) 그리는 함수는 번호 0 인 부위를 건너뛴다
    /// (0x4e85d1). 색은 그 반쪽 그대로다(0x54ffb4). 천지도복(갑옷 8)을 입은 무도가가 속옷 차림으로 보였다(사용자,
    /// 2026-09-26) — 도복은 윗도리만 그리고 하의는 이 바지(mn001)가 그린다. 여자 아카이브에는 바지 그림이 없다.
    /// </summary>
    [Fact]
    public void A_man_whose_body_byte_carries_a_colour_wears_trousers_dyed_with_it()
    {
        IReadOnlyList<Piece> pieces = Wardrobe.Pieces(Wearing(body: 16 + 1, armor: 8));

        Assert.Equal(["mb001", "mn001", "mu008", "ma008"], pieces.Select(piece => piece.Name));
        Assert.Equal(1, pieces.Single(piece => piece.Name == "mn001").Colour);
    }

    [Fact]
    public void With_no_colour_in_the_body_byte_there_are_no_trousers()
    {
        Assert.Equal(["mb001", "mu008", "ma008"], Names(Wearing(body: 16, armor: 8)));
        Assert.Equal(["wb001"], Names(Wearing(body: 32 + 5)));
    }

    /// <summary>천지도복 차림을 무도가 동작까지 그릴 그림이 다 있다 — 바지·도복·팔의 서기·평타·도가 기술(d).</summary>
    [Fact]
    public void The_earth_garb_and_its_trousers_are_cut_for_the_monk()
    {
        string parts = HairMotionTests.Parts();
        string[] missing =
        [
            .. new[] { "mn001", "mu008", "ma008" }
                .SelectMany(piece => new[] { "", "02", "d" }.Select(move => $"{piece}{move}.png"))
                .Where(name => !File.Exists(Path.Combine(parts, name)))
        ];

        Assert.Empty(missing);
    }

    /// <summary>The head, the boots and the trousers are dyed; the shield is not.</summary>
    [Fact]
    public void The_head_and_the_boots_carry_a_colour()
    {
        IReadOnlyList<Piece> pieces = Wardrobe.Pieces(
            Wearing(head: 3, body: 16 + 5, boots: 1, shield: 6, hairColour: 11, bootColour: 4));

        Assert.Equal(11, pieces.Single(piece => piece.Name == "mh003").Colour);
        Assert.Equal(4, pieces.Single(piece => piece.Name == "ml001").Colour);
        Assert.Equal(5, pieces.Single(piece => piece.Name == "mn001").Colour);
        Assert.Equal(0, pieces.Single(piece => piece.Name == "ms006").Colour);
    }
}
