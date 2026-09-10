using Godot;
using Lod.Mobile.Core.Art;

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
    // The figure stands a little below the middle of its tile, and a dropped thing sits on the same spot.
    private const float Standing = 8;

    /// <summary>What the thing looks like, when that has been cut from the archive.</summary>
    public Texture2D? Picture { get; init; }

    public override void _Draw()
    {
        if (Picture is { } picture)
        {
            // Lying on the tile rather than standing on it: the middle of the picture goes where the
            // middle of the tile is, the way a dropped coin sits flat on the floor.
            Vector2 size = picture.GetSize();

            DrawTexture(picture, new Vector2(-size.X / 2, -Standing - (size.Y / 2)));

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
    }
}
