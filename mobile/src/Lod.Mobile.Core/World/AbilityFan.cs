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

    /// <summary>The next page — at the far end of the fan, because it is pressed least.</summary>
    public static (int X, int Y) Next { get; } = (160, 40);

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
    /// The two automatic-potion switches (health, mana) — a little smaller than a skill button, as asked (사용자,
    /// 2026-09-23), but not below the 44 the theme sets as the least a thumb can press (docs/original-ui-451.md 치수).
    /// </summary>
    public const int PotionSide = 44;

    /// <summary>Where the two potion switches go: above the highest skills, left of the next-page button.</summary>
    public static IReadOnlyList<(int X, int Y)> Potions { get; } =
    [
        (48, 36),
        (104, 36)
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
