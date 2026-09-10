using Godot;

namespace LodClient;

/// <summary>
/// The greybox palette. Deliberately greyscale: this stage judges layout, reach and text, and colour would
/// invite opinions on art that the wireframes put out of scope.
/// </summary>
public static class Greybox
{
    public static readonly Color Muted = new(0.62f, 0.62f, 0.64f);

    private static readonly Color SurfaceFill = new(0.16f, 0.16f, 0.18f);
    private static readonly Color SurfaceEdge = new(0.30f, 0.30f, 0.33f);
    private static readonly Color WorldFill = new(0.11f, 0.11f, 0.12f);
    private static readonly Color BarFill = new(0.55f, 0.55f, 0.58f);

    public static StyleBoxFlat Surface() => new()
    {
        BgColor = SurfaceFill,
        BorderColor = SurfaceEdge,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1
    };

    /// <summary>
    /// A panel that has to stay readable over the map. The greybox surface was made for a flat grey
    /// background; over gold floor tiles its text disappears, so this one is nearly opaque.
    /// </summary>
    public static StyleBoxFlat Plate() => new()
    {
        BgColor = new Color(0.08f, 0.09f, 0.12f, 0.9f),
        BorderColor = SurfaceEdge,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
        ContentMarginTop = 4,
        ContentMarginBottom = 4
    };

    /// <summary>The play area behind the HUD. Flat on purpose: the map is not this stage's question.</summary>
    public static StyleBoxFlat World() => new() { BgColor = WorldFill };

    /// <summary>Filled portion of a bar. Always paired with numbers, never read by shade alone.</summary>
    public static StyleBoxFlat Fill() => new() { BgColor = BarFill };

    /// <summary>
    /// Marks a zone the layout has to respect. A greybox guide, not part of the game: it goes away once the
    /// rule it shows has been checked on a device.
    /// </summary>
    public static StyleBoxFlat Outline() => new()
    {
        BgColor = new Color(0, 0, 0, 0),
        BorderColor = new Color(0.38f, 0.38f, 0.42f, 0.55f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1
    };
}
