namespace Lod.Mobile.Core.Ui;

/// <summary>
/// Where each worn place sits in the equipment panel, and which of the empty-slot drawings fills it while
/// nothing is on. The panel is the original 4.51 picture itself (<c>equip01.epf</c>), cut only to leave out the
/// rings on its left — so a place is the dark square the picture already
/// has, measured in that cut picture's own pixels (사용자 결정 2026-09-30, data/original-ui/451.json 「장비」).
/// </summary>
/// <remarks>
/// The picture has thirteen squares; the server names eighteen places. The five it has no square for — the
/// cloak, the overhelm and the three trinkets came after 4.51 — are not shown yet (사용자 2026-10-01: 따로 추가한다).
/// Which square is which comes from their shape and the older window's layout (docs/original-equipment-window.md
/// 2절): the three big ones across the chest are weapon, armour and shield, the one at the waist is the belt.
/// The empty-slot drawing in each square is what says so on screen.
/// </remarks>
public static class GearLayout
{
    /// <summary>How many empty-slot drawings <c>_nui_eqi.spf</c> holds. Four places borrow a neighbour's.</summary>
    public const int Drawings = 14;

    /// <summary>The cut picture's width: <c>equip01.epf</c> from (22, 0) to its right and bottom edges.</summary>
    public const int PictureWidth = 270;

    /// <summary>The cut picture's height.</summary>
    public const int PictureHeight = 302;

    private static readonly Dictionary<int, (int X, int Y, int Width, int Height)> Painted = new()
    {
        [4] = (129, 90, 19, 20),     // 투구 — 맨 위 가운데
        [5] = (97, 111, 20, 21),     // 귀고리 — 머리 왼쪽
        [6] = (162, 124, 19, 20),    // 목걸이 — 머리 오른쪽
        [1] = (50, 148, 40, 40),     // 무기 — 가슴 왼쪽 큰 칸
        [2] = (117, 148, 41, 40),    // 갑옷 — 가슴 가운데 큰 칸
        [3] = (184, 148, 41, 40),    // 방패 — 가슴 오른쪽 큰 칸
        [9] = (70, 194, 21, 20),    // 왼팔
        [11] = (128, 192, 21, 21),  // 허리 — 몸통 아래
        [10] = (185, 193, 21, 21),  // 오른팔
        [7] = (98, 206, 20, 21),    // 왼손
        [8] = (160, 215, 20, 20),   // 오른손
        [12] = (98, 232, 21, 21),   // 다리
        [13] = (141, 254, 21, 20),  // 신발 — 맨 아래
    };

    private static readonly Dictionary<int, int> EmptyDrawing = new()
    {
        [1] = 6, [2] = 3, [3] = 7, [4] = 0, [5] = 1, [6] = 2, [7] = 9, [8] = 10, [9] = 5,
        [10] = 8, [11] = 12, [12] = 11, [13] = 13,
        [14] = 3,  // 장신구 — 갑옷 그림을 같이 쓴다
        [15] = 4,  // 겉옷
        [16] = 0,  // 겉투구 — 투구 그림을 같이 쓴다
        [17] = 4,  // 장신구2 — 겉옷 그림을 같이 쓴다
        [18] = 4,  // 장신구3 — 겉옷 그림을 같이 쓴다
    };

    /// <summary>The places the picture has no square for — not shown yet.</summary>
    public static IReadOnlyList<int> Spare { get; } = [16, 15, 14, 17, 18];

    /// <summary>The picture's number box for armour; the fighting figures run down beside the chest.</summary>
    public static readonly (int X, int Y, int Width, int Height) Armor = (229, 94, 25, 13);

    /// <summary>The picture's number box for damage.</summary>
    public static readonly (int X, int Y, int Width, int Height) Damage = (229, 110, 25, 13);

    /// <summary>The picture's number box for hit.</summary>
    public static readonly (int X, int Y, int Width, int Height) Hit = (229, 126, 25, 13);

    /// <summary>The picture's box under 「Next Lev」 — what is left to earn before the next level.</summary>
    public static readonly (int X, int Y, int Width, int Height) NextLevel = (21, 240, 64, 13);

    /// <summary>The picture's box at the top left — the class.</summary>
    public static readonly (int X, int Y, int Width, int Height) Class = (24, 18, 56, 17);

    /// <summary>The picture's framed bar at the top — the name. The two lines under it (guild, guild title) come later.</summary>
    public static readonly (int X, int Y, int Width, int Height) Name = (120, 19, 116, 15);

    /// <summary>The person button at the bottom right — group requests on or off (<c>equip05</c> draws its two states).</summary>
    public static readonly (int X, int Y, int Width, int Height) Group = (204, 227, 36, 30);

    /// <summary>Where the original Close button (<c>butt001</c>) sits — the bare stone under the person button.</summary>
    public static readonly (int X, int Y, int Width, int Height) Close = (183, 268, 68, 20);

    /// <summary>Whether this is a place the panel knows how to show.</summary>
    public static bool Has(int slot) => EmptyDrawing.ContainsKey(slot);

    /// <summary>Every place, so a panel can build its cells without knowing the numbers itself.</summary>
    public static IEnumerable<int> Slots => EmptyDrawing.Keys;

    /// <summary>The dark square a place fills in the picture, or nothing for a place the picture has no square for.</summary>
    public static (int X, int Y, int Width, int Height)? Square(int slot) =>
        Painted.TryGetValue(slot, out var square) ? square : null;

    /// <summary>Which <c>_nui_eqi.spf</c> drawing a place shows while it is empty.</summary>
    /// <exception cref="KeyNotFoundException">The server named a place the panel has no room for.</exception>
    public static int Drawing(int slot) => EmptyDrawing[slot];
}
