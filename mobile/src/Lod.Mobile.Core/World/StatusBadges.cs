namespace Lod.Mobile.Core.World;

/// <summary>One small status icon: its picture (spell sheet), roughly how long is left, its time grade, and whether it harms.</summary>
/// <remarks>Seconds is only a band's floor for the original's icons (0x3A says a grade, never seconds) — draw the grade.</remarks>
public sealed record StatusBadge(int Icon, int Seconds, int Grade, bool Harmful);

/// <summary>
/// The status icon strip in my plate and the bot's slot (사용자, 2026-09-26: 버프·디버프 아이콘을 체력·마력 표시한 곳에). Two sources:
/// the original's own status icons (0x3A — a picture and a time grade) and the states the server tells in 0x5E kind 3
/// (name · seconds · picture), which also carries the 5.99 script states (호르라마·에나르마) that never come as 0x3A.
/// </summary>
public static class StatusBadges
{
    /// <summary>The original's grades from seconds — 6 over ninety, 5 sixty to ninety, 4 thirty, 3 twenty, 2 ten, 1 under ten.</summary>
    public static int Grade(int seconds) => seconds switch
    {
        >= 90 => 6,
        >= 60 => 5,
        >= 30 => 4,
        >= 20 => 3,
        >= 10 => 2,
        _ => 1
    };

    /// <summary>
    /// Both put together, soonest to run out first. A told state whose picture already came as 0x3A is not drawn twice, and
    /// one with no picture (an old server, or a state with no spell of its own) is left out — a blank badge says nothing.
    /// </summary>
    public static IReadOnlyList<StatusBadge> Of(IEnumerable<Ailment> original, IEnumerable<CompanionStatus>? told)
    {
        List<StatusBadge> badges = [.. original.Where(one => one.Left > 0).Select(one => new StatusBadge(one.Icon, one.Seconds, one.Left, false))];
        HashSet<int> seen = [.. badges.Select(one => one.Icon)];

        foreach (CompanionStatus state in told ?? [])
        {
            if (state.Icon <= 0 || state.Seconds <= 0)
            {
                continue;
            }

            if (!seen.Add(state.Icon))
            {
                // 0x3A 로 이미 온 것 — 해로운지는 서버 말이 더 정확하다.
                int at = badges.FindIndex(one => one.Icon == state.Icon);
                badges[at] = badges[at] with { Harmful = badges[at].Harmful || state.Harmful };
                continue;
            }

            badges.Add(new StatusBadge(state.Icon, state.Seconds, Grade(state.Seconds), state.Harmful));
        }

        return [.. badges.OrderBy(one => one.Seconds)];
    }

    /// <summary>
    /// A party member's pictures (0x5E kind 6) — the server sends no time or harm for them, and the frame shows icons
    /// alone anyway; a blank picture is left out.
    /// </summary>
    public static IReadOnlyList<StatusBadge> OfIcons(IEnumerable<int> icons) =>
        [.. icons.Where(icon => icon > 0).Select(icon => new StatusBadge(icon, 90, 6, false))];
}
