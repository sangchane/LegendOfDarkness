namespace Lod.Mobile.Core.Art;

/// <summary>
/// The column the landscape windows stand in, against the right edge: the pack, an NPC's talk, the chat. It takes a bit
/// over a third of the width, more where a window needs more — the gear ring is wider than a third on a 16:9 screen.
/// </summary>
public static class SideColumn
{
    /// <summary>
    /// Where the column starts, as the anchor its holder is given. Anchors are shares of the width the HUD is laid in —
    /// the screen less its safe margins — not of the screen itself.
    /// </summary>
    public static float LeftAnchor(float screen, float left, float right, float window, float most) =>
        Math.Min(most, 1f - (window / (screen - left - right)));
}
