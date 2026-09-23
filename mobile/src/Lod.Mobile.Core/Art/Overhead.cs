using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Art;

/// <summary>
/// What stands over somebody's head, bottom to top: the head slot where brief head effects play (Miss, 일음지, the
/// coma), then the health bar when they were struck, then one row of badges for what is on them (user + lead
/// decision, 2026-09-24). With the bar hidden the badges drop to where the bar would be, so nothing floats.
/// </summary>
/// <remarks>
/// The original draws coma 24, 일음지 42 and Miss 33/115 in the band just over the head — their sheet cells put the
/// drawing some 45–63 pixels above the feet anchor — so that band is kept for them and everything else stacks on
/// top of it. All positions are pixels from the feet, up being negative, as the actor draws them.
/// </remarks>
public static class Overhead
{
    /// <summary>Space between the head, the slot, the bar and the badges.</summary>
    public const int Gap = 2;

    /// <summary>
    /// How tall the head slot is: the tallest of the brief head effects the server sends — coma 24 is 19 rows,
    /// Miss 33 and 115 are 20.
    /// </summary>
    public const int SlotHeight = 20;

    /// <summary>
    /// How far above the feet anchor an effect's lowest drawn row must be for it to count as a head effect. Every
    /// head effect in the sheets clears 45 (coma 24, 일음지 42 and its kin 25·40·41·118·391, Miss 33·115, and 32);
    /// the tallest body effect that stops short of the feet stops at 29 (68), so 40 splits them with room.
    /// </summary>
    public const int HeadClassClearance = 40;

    /// <summary>The coma's badge (<c>0x3A</c> for us, <c>0x5C</c> for others) and the picture the server repeats every tick.</summary>
    public const int ComaIcon = 89;

    public const int ComaEffect = 24;

    /// <summary>How many badges are drawn before the rest are summed up as "+N".</summary>
    public const int MostBadges = 5;

    /// <summary>
    /// Whether an effect is a head effect: its whole drawing sits high in its cell, so it plays over the head
    /// rather than on the body or the ground.
    /// </summary>
    /// <param name="sheet">The effect's sheet, whose anchor lands on the feet.</param>
    /// <param name="drawnBottom">The lowest row with anything drawn on it in any frame, in cell pixels.</param>
    public static bool IsHeadClass(EffectSheet sheet, int drawnBottom) =>
        sheet.AnchorY - drawnBottom >= HeadClassClearance;

    /// <summary>
    /// How far to move a head effect from the feet so its lowest drawn row sits <see cref="Gap" /> above the head —
    /// in the head slot whatever the figure's height, never on the face.
    /// </summary>
    /// <param name="headTop">The top of the drawn head, from the feet (negative).</param>
    public static float Shift(EffectSheet sheet, int drawnBottom, float headTop) =>
        headTop - Gap - (drawnBottom - sheet.AnchorY);

    /// <summary>Where the bar and the badge row stand (their top edges) over a head whose top is at <paramref name="headTop" />.</summary>
    /// <param name="barShown">Whether the health bar is up now; without it the badges take its place.</param>
    /// <param name="barHeight">How tall the bar is.</param>
    /// <param name="rowHeight">How tall the badge row is.</param>
    public static (float Bar, float Badges) Place(float headTop, bool barShown, float barHeight, float rowHeight)
    {
        float slotTop = headTop - Gap - SlotHeight;
        float bar = slotTop - Gap - barHeight;
        float badges = (barShown ? bar : slotTop) - Gap - rowHeight;

        return (bar, badges);
    }

    /// <summary>Whether these are somebody's statuses in a coma.</summary>
    public static bool InComa(IEnumerable<Ailment> ailments) => ailments.Any(one => one.Icon == ComaIcon);

    /// <summary>
    /// The badges to draw and how many are left over for "+N": the ones running out soonest first, so the one a
    /// player must act on is never pushed off the end. In a coma there are none — the coma owns the head.
    /// </summary>
    public static (IReadOnlyList<Ailment> Shown, int More) Badges(IEnumerable<Ailment> ailments)
    {
        List<Ailment> all = [.. ailments];

        if (InComa(all))
        {
            return ([], 0);
        }

        List<Ailment> sorted = [.. all.OrderBy(one => one.Left).ThenBy(one => one.Icon)];

        return ([.. sorted.Take(MostBadges)], Math.Max(0, sorted.Count - MostBadges));
    }
}
