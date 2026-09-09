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

    public static StyleBoxFlat Surface() => new()
    {
        BgColor = SurfaceFill,
        BorderColor = SurfaceEdge,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1
    };
}
