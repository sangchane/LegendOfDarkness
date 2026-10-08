using Godot;
using Lod.Mobile.Core.Ui;

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

    /// <summary>
    /// What the NPC is for — the sign is its role icon (2026-10-08). It used to be a yellow 「!」 for all of them, which
    /// would now read as a quest, since quests have that icon.
    /// </summary>
    public NpcRole Role
    {
        get => _role;
        set
        {
            if (value != _role)
            {
                _role = value;
                QueueRedraw();
            }
        }
    }

    private NpcRole _role;

    public override void _Draw() => RoleIcon.Draw(this, Role, new Vector2(0, -Waist), 10);
}
