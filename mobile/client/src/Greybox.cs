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

    /// <summary>
    /// Rounds a thumb button — the movement pad and the fan round the attack — into a disc. The plates stay as opaque as
    /// every other button's, because the glyphs on them sit over gold floor tiles too.
    /// </summary>
    public static void Disc(Button button)
    {
        foreach ((string state, StyleBoxFlat box) in new[]
                 {
                     ("normal", Plate()), ("hover", Plate()), ("pressed", Surface()), ("focus", Plate()), ("disabled", Surface())
                 })
        {
            // 반지름이 한 변의 절반 이상이면 왼쪽·오른쪽 조각이 가운데서 겹친다. 불투명할 때는 안 보이지만 흐리게 하면
            // (빈 칸 · 걷는 중의 방향판) 겹친 줄이 짙게 드러났다 — 절반보다 1 작게. 크기를 먼저 정하고 부른다.
            box.SetCornerRadiusAll(Mathf.RoundToInt(button.CustomMinimumSize.X / 2) - 1);

            // 지름 48 의 원 안에 드는 네모가 34 다 — 원작 아이콘(35)이 둥근 칸 밖으로 삐져나오지 않게 7 씩 들인다.
            box.SetContentMarginAll(7);
            button.AddThemeStyleboxOverride(state, box);
        }
    }

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
