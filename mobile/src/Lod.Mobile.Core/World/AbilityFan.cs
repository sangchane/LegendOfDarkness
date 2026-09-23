namespace Lod.Mobile.Core.World;

/// <summary>
/// The skill and spell buttons fanned round the attack button in the bottom-right corner — one page of six at a time,
/// a switch between skills and spells, and a button for the next page. Centres are in a box whose bottom-right corner
/// is the attack button's.
/// </summary>
/// <remarks>
/// The box is as wide as a 360-wide portrait screen leaves beside the movement pad (360 − 8 − 8 − 152 − 8 = 184), so
/// the one shape serves both orientations. A button is pressed by its square, so the rows step up and inward like a
/// staircase rather than lying on a true circle: on a circle the squares of neighbours overlap. The nearest three sit
/// against the attack button, where the thumb already is.
/// </remarks>
public static class AbilityFan
{
    public const int Width = 184;
    public const int Height = 248;
    public const int AttackSide = 64;
    public const int ButtonSide = 48;
    public const int PerPage = 6;

    public static (int X, int Y) Attack { get; } = (152, 216);

    /// <summary>Switches between skills and spells — beside the attack button, on its row.</summary>
    public static (int X, int Y) Switch { get; } = (24, 216);

    /// <summary>The next page — at the top of the fan, because it is pressed least. The right edge is the potions'.</summary>
    public static (int X, int Y) Next { get; } = (104, 40);

    /// <summary>Where the six buttons of a page go, nearest the attack button first.</summary>
    public static IReadOnlyList<(int X, int Y)> Slots { get; } =
    [
        (80, 216),
        (104, 152),
        (160, 152),
        (40, 152),
        (64, 96),
        (120, 96)
    ];

    /// <summary>
    /// The two automatic-potion switches (health, mana). They need not be as big as a skill (사용자, 2026-09-23: "스킬창
    /// 만큼 클 필요 없으니까") — 32, below the theme's 44 on purpose; they are touched rarely and never in a hurry.
    /// </summary>
    public const int PotionSide = 32;

    /// <summary>Where the two potion switches go: one above the other against the right edge, clear of the skills.</summary>
    public static IReadOnlyList<(int X, int Y)> Potions { get; } =
    [
        (168, 32),
        (168, 80)
    ];

    public static int Pages(int learned) => Paging.Pages(learned, PerPage);

    /// <summary>The page after this one, back to the first after the last.</summary>
    public static int After(int page, int learned) => Paging.After(page, learned, PerPage);

    /// <summary>The page still to show once some have gone — the last one left.</summary>
    public static int Kept(int page, int learned) => Paging.Kept(page, learned, PerPage);

    /// <summary>The six on a page, in the server's order, with nothing where the learned ones run out.</summary>
    public static IReadOnlyList<T?> Page<T>(IReadOnlyList<T> learned, int page) where T : class =>
        Paging.Page(learned, page, PerPage);
}
