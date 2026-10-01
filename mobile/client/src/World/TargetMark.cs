using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// Which figure is picked out: a ring on the tile they stand on and a wedge over their head.
/// </summary>
/// <remarks>
/// Two marks of a shape rather than one of a colour. Somebody who cannot tell the colours apart still has
/// to be able to see which figure is chosen, and a ring on a patterned floor is easy to lose on its own.
/// </remarks>
public sealed partial class TargetMark : Node2D
{
    private const float OverHead = 74;

    // The figure stands a little below the middle of its tile, so the ring has to be lifted to sit on it.
    private const float Standing = 8;

    public override void _Draw()
    {
        float across = IsometricFloor.TileWidth / 2f;
        float down = IsometricFloor.TileHeight / 2f;

        Vector2[] ring =
        [
            new(0, -Standing - down),
            new(across, -Standing),
            new(0, -Standing + down),
            new(-across, -Standing),
            new(0, -Standing - down)
        ];

        // Drawn twice: a dark line first so the ring reads on a pale floor as well as a dark one.
        DrawPolyline(ring, new Color(0, 0, 0, 0.7f), 4);
        DrawPolyline(ring, Colors.White, 2);

        Vector2[] wedge = [new(-6, -OverHead), new(6, -OverHead), new(0, -OverHead + 8)];

        DrawColoredPolygon(wedge, new Color(0, 0, 0, 0.7f));
        DrawColoredPolygon([wedge[0] + new Vector2(1.5f, 1.5f), wedge[1] + new Vector2(-1.5f, 1.5f), wedge[2] - new Vector2(0, 1.5f)], Colors.White);
    }
}
