using Lod.Mobile.Core.Automation;

namespace Lod.Mobile.Core.Model;

/// <summary>
/// 직업마다 화면에 보일 기술·마법과, 마법이 누구에게 나가나(사용자 2026-10-04 — 무도가는 열 가지만, 다라밀공은 괴물에). 표
/// <c>assets/world/class-kit.txt</c> 한 줄이 <c>직업 · skill|spell · 이름 · 대상</c>, 대상은 <c>적</c>(괴물을 골라 쏜다 —
/// 탭은 고른 괴물이나 가장 가까운 괴물, 끌면 조준) · <c>나</c> · <c>-</c>. 다섯째 칸이 <c>수동</c> 이면 자동 사냥은 쓰지 않는다(다라밀공 —
/// 쓰면 체력 1·마력 0). 표에 줄이 하나도 없는 직업은 배운 것이 다 보인다 —
/// 다른 직업은 줄을 더하면 같은 규칙을 탄다.
/// </summary>
public sealed class ClassKit
{
    public static ClassKit Empty { get; } = new([], []);

    private readonly Dictionary<int, Dictionary<(bool Spell, string Name), string>> _lines;
    private readonly HashSet<(int Path, string Name)> _byHand;

    private ClassKit(Dictionary<int, Dictionary<(bool, string), string>> lines, HashSet<(int, string)> byHand) =>
        (_lines, _byHand) = (lines, byHand);

    /// <summary>Tab-separated lines; <c>#</c> lines and anything malformed are skipped.</summary>
    public static ClassKit Read(string text)
    {
        Dictionary<int, Dictionary<(bool, string), string>> lines = [];
        HashSet<(int, string)> byHand = [];

        foreach (string raw in text.Split('\n'))
        {
            string[] cells = raw.Trim().Split('\t');

            if (cells.Length >= 4 && int.TryParse(cells[0], out int path) && cells[1] is "skill" or "spell")
            {
                (lines.TryGetValue(path, out var mine) ? mine : lines[path] = [])[(cells[1] == "spell", cells[2])] = cells[3];

                if (cells.Length >= 5 && cells[4] == "수동")
                {
                    byHand.Add((path, cells[2]));
                }
            }
        }

        return new ClassKit(lines, byHand);
    }

    /// <summary>Whether this learned skill or spell is shown for <paramref name="path" /> — everything when the class has no lines.</summary>
    public bool Shows(int? path, bool spell, string name) =>
        path is not { } p || !_lines.TryGetValue(p, out var mine) || mine.ContainsKey((spell, CompanionSpells.Bare(name)));

    /// <summary>Whether this spell is aimed at a monster (대상 <c>적</c>).</summary>
    public bool AimsAtEnemy(int? path, string name) =>
        path is { } p && _lines.TryGetValue(p, out var mine)
        && mine.TryGetValue((true, CompanionSpells.Bare(name)), out string? aim) && aim == "적";

    /// <summary>Whether auto-hunt may cast it on its own — not when the table says 수동.</summary>
    public bool AutoCasts(int? path, string name) => path is not { } p || !_byHand.Contains((p, CompanionSpells.Bare(name)));
}
