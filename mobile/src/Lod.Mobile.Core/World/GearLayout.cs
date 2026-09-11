namespace Lod.Mobile.Core.World;

/// <summary>
/// Where each worn place sits in the equipment panel, and which of the empty-slot drawings fills it while
/// nothing is on. Straight from the original's own screen layout — <c>setoa.dat</c> → <c>_nui_eq.txt</c>,
/// the newer of its two equipment windows, whose eighteen places match the server's <c>ItemSlots</c> one
/// for one. The coordinates and the frame numbers are written down in docs/original-equipment-window.md.
/// </summary>
/// <remarks>
/// The original is not a grid but a ring: the paper doll stands in the middle and the places go round it,
/// with the top and bottom rows stepping inward over the doll's head and feet. Five columns by six rows
/// holds that shape while staying something a phone can lay out, and the middle column is left free for
/// the doll wherever the doll actually stands.
/// </remarks>
public static class GearLayout
{
    /// <summary>How many empty-slot drawings <c>_nui_eqi.spf</c> holds. Four places borrow a neighbour's.</summary>
    public const int Drawings = 14;

    /// <summary>Columns across the panel, outermost first.</summary>
    public const int Columns = 5;

    /// <summary>Rows down the panel.</summary>
    public const int Rows = 6;

    /// <summary>The column the paper doll stands in.</summary>
    public const int DollColumn = 2;

    /// <summary>The rows the paper doll reaches across, so nothing else may sit in the middle of them.</summary>
    public const int DollFirstRow = 2;

    /// <summary>The last row the paper doll reaches.</summary>
    public const int DollLastRow = 4;

    private static readonly Dictionary<int, (int Column, int Row, int Drawing)> Places = new()
    {
        [4] = (2, 0, 0),    // 투구 HEAD
        [16] = (2, 1, 0),   // 겉투구 HEAD2 — 투구 그림을 같이 쓴다
        [5] = (1, 1, 1),    // 귀고리 EAR
        [6] = (3, 1, 2),    // 목걸이 NECK
        [14] = (0, 2, 3),   // 장신구 ARMOR2 — 갑옷 그림을 같이 쓴다
        [2] = (1, 2, 3),    // 갑옷 ARMOR
        [15] = (3, 2, 4),   // 겉옷 CAPE
        [17] = (4, 2, 4),   // 장신구2 CAPE2 — 겉옷 그림을 같이 쓴다
        [1] = (1, 3, 6),    // 무기 WEAPON
        [3] = (3, 3, 7),    // 방패 SHIELD
        [18] = (4, 3, 4),   // 장신구3 CAPE3 — 겉옷 그림을 같이 쓴다
        [9] = (0, 4, 5),    // 왼팔 LARM
        [7] = (1, 4, 9),    // 왼손 LHAND
        [8] = (3, 4, 10),   // 오른손 RHAND
        [10] = (4, 4, 8),   // 오른팔 RARM
        [12] = (1, 5, 11),  // 다리 LEG
        [13] = (2, 5, 13),  // 신발 FOOT
        [11] = (3, 5, 12),  // 허리 BELT
    };

    /// <summary>Whether this is a place the panel knows how to show.</summary>
    public static bool Has(int slot) => Places.ContainsKey(slot);

    /// <summary>Where a place sits and what fills it while it is empty.</summary>
    /// <exception cref="KeyNotFoundException">The server named a place the original window has no room for.</exception>
    public static (int Column, int Row, int Drawing) Of(int slot) => Places[slot];

    /// <summary>Every place, so a panel can build its cells without knowing the numbers itself.</summary>
    public static IEnumerable<int> Slots => Places.Keys;
}
