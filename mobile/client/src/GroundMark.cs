using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// Something lying on the floor.
/// </summary>
/// <remarks>
/// The original draws the thing itself and nothing else, and so do we. The marker underneath is only for a
/// number no picture has been cut for: without it such a drop would be invisible, which is worse than a
/// shape that says "something is here" without saying what.
/// </remarks>
public sealed partial class GroundMark : Node2D
{
    /// <summary>
    /// How far above the tile's own point a thing lying on it is drawn. The figure stands a little below
    /// the middle of its tile and a dropped thing sits on the same spot, so a thumb aiming at the picture
    /// is aiming here — which is why this is not private.
    /// </summary>
    public const float Standing = 8;

    /// <summary>What the thing looks like, when that has been cut from the archive.</summary>
    public Texture2D? Picture { get; init; }

    /// <summary>Which tile it lies on, so a tap on it can ask the server for that tile.</summary>
    public Tile Where { get; set; }

    /// <summary>The number the server calls it, said out loud when a run has nobody watching.</summary>
    public int Sprite { get; set; }

    /// <summary>
    /// The middle of the drawing, measured from this node's own point. A thumb aims at the picture, not at
    /// the tile under it, so this is what a tap is judged against — and what a hands-free run aims at.
    /// </summary>
    public Vector2 Middle => new(0, -Standing - (Half(Picture) / 2));

    private static float Half(Texture2D? picture) => picture?.GetSize().Y ?? 2 * Standing;

    private int _count;

    /// <summary>How many the bundle holds (0x07). Two or more is written beside the picture as "x3".</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                QueueRedraw();
            }
        }
    }

    public override void _Draw()
    {
        if (Picture is { } picture)
        {
            // Lying on the tile rather than standing on it: the middle of the picture goes where the
            // middle of the tile is, the way a dropped coin sits flat on the floor.
            Vector2 size = picture.GetSize();

            DrawTexture(picture, new Vector2(-size.X / 2, -Standing - (size.Y / 2)));
            DrawCount(new Vector2((size.X / 2) - 4, -Standing + (size.Y / 2)));

            return;
        }

        float across = IsometricFloor.TileWidth / 4f;
        float down = IsometricFloor.TileHeight / 4f;

        Vector2[] diamond =
        [
            new(0, -Standing - down),
            new(across, -Standing),
            new(0, -Standing + down),
            new(-across, -Standing)
        ];

        // 바닥 무늬가 금빛이라 어두운 표식은 묻힌다: 밝게 채우고 어두운 테두리로 띄운다.
        DrawPolyline([.. diamond, diamond[0]], new Color(0, 0, 0, 0.8f), 4);
        DrawColoredPolygon(diamond, new Color(1, 0.87f, 0.45f));
        DrawCount(new Vector2(across - 4, -Standing + down));
    }

    /// <summary>A bundle's count at the picture's lower right, the way the pack writes a stack's.</summary>
    private void DrawCount(Vector2 at)
    {
        if (_count < 2)
        {
            return;
        }

        const int size = 10;
        string text = $"x{_count}";
        DrawStringOutline(ThemeDB.FallbackFont, at, text, HorizontalAlignment.Left, -1, size, 3, Colors.Black);
        DrawString(ThemeDB.FallbackFont, at, text, HorizontalAlignment.Left, -1, size, Colors.White);
    }
}
