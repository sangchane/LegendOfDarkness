using Godot;

namespace LodClient;

/// <summary>
/// An NPC with no picture: a small sign standing where it stands, so it can be found and tapped.
/// </summary>
/// <remarks>
/// The 5.99 pack places script NPCs it never gives a picture (the notice boards, the warp grandfather, the temple
/// that changes classes). The original drew nothing there — the map's own art is the board or the door — and a click on
/// the spot opened the script. On a phone an unmarked spot cannot be found, so it gets a sign (user decision,
/// 2026-09-17: a marker only, no stand-in figure).
/// </remarks>
public sealed partial class NpcMark : Node2D
{
    /// <summary>How high the sign floats — about where a figure's waist is, which is what a tap is measured against.</summary>
    public const float Waist = 32;

    public override void _Draw()
    {
        const float radius = 9;
        Vector2 middle = new(0, -Waist);

        // 바닥 무늬가 금빛이라 어두운 테두리를 두르고 밝게 채운다(바닥 물건 표식과 같은 까닭).
        DrawCircle(middle, radius + 2, new Color(0, 0, 0, 0.8f));
        DrawCircle(middle, radius, new Color(1, 0.87f, 0.45f));
        DrawLine(middle + new Vector2(0, -5), middle + new Vector2(0, 2), new Color(0.15f, 0.1f, 0.05f), 3);
        DrawCircle(middle + new Vector2(0, 5), 1.6f, new Color(0.15f, 0.1f, 0.05f));
    }
}
