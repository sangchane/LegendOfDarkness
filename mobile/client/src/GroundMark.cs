using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// Something lying on the floor, until there are pictures for the things themselves.
/// </summary>
/// <remarks>
/// The marker is drawn even when the picture is known: the floor is gold and a small icon alone is easy to
/// walk past, so the diamond says "something is here" and the picture says what.
/// </remarks>
public sealed partial class GroundMark : Node2D
{
    // The figure stands a little below the middle of its tile, and a dropped thing sits on the same spot.
    private const float Standing = 8;

    /// <summary>What the thing looks like, when that has been cut from the archive.</summary>
    public Texture2D? Picture { get; init; }

    public override void _Draw()
    {
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

        if (Picture is not null)
        {
            Vector2 size = Picture.GetSize();
            DrawTexture(Picture, new Vector2(-size.X / 2, -Standing - size.Y));
        }
    }
}
