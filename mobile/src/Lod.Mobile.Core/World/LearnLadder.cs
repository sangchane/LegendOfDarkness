using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.World;

/// <summary>One line of the auto-learn table: this class learns this skill or spell at this level.</summary>
/// <param name="Path">Hades <c>Class</c> number — 1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 무도가 (the profile's class byte).</param>
/// <param name="Icon">The template's picture, the same number the server sends when it is learned (0 when the template has none).</param>
public sealed record LadderStep(int Path, int Level, bool Spell, string Name, int Icon);

/// <summary>A row of the skill picker — learned (with its <see cref="Slot" />) or still to come at <see cref="Level" />.</summary>
public sealed record RosterRow(string Name, bool Spell, int Icon, int? Slot, int? Level)
{
    public bool Learned => Slot is not null;
}

/// <summary>
/// 레벨이 되면 저절로 배우는 표(서버 <c>AutoLearnTable.cs</c> 와 같은 것, <c>scripts/build-auto-learn.py</c> →
/// <c>assets/world/auto-learn.txt</c>)로 기술 목록을 짠다 — 배운 것 + 아직 못 배운 내 직업 것을 레벨 순으로
/// (사용자 요청 2026-09-26: "아직 레벨이 낮아서 활성화 안 된 것까지 몇 레벨에 배울 수 있는지").
/// </summary>
public sealed class LearnLadder
{
    public static LearnLadder Empty { get; } = new([]);

    private LearnLadder(IReadOnlyList<LadderStep> steps) => Steps = steps;

    public IReadOnlyList<LadderStep> Steps { get; }

    /// <summary>Lines of <c>직업 · 레벨 · skill|spell · 이름 · 그림</c>, tab-separated; <c>#</c> lines and anything malformed are skipped.</summary>
    public static LearnLadder Read(string text)
    {
        List<LadderStep> steps = [];

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] cells = line.Split('\t');

            if (cells.Length >= 5 && int.TryParse(cells[0], out int path) && int.TryParse(cells[1], out int level)
                && cells[2] is "skill" or "spell" && int.TryParse(cells[4], out int icon))
            {
                steps.Add(new LadderStep(path, level, cells[2] == "spell", cells[3], icon));
            }
        }

        return new LearnLadder(steps);
    }

    /// <summary>
    /// Everything learned, then my class's steps not learned yet and still above my level, in level order (learned ones
    /// the table does not know come first; at one level skills before spells). A step already past but missing — the
    /// server keeps it back because a higher one is held (연천단각 → 단각) — is not promised. An unknown class shows only
    /// what is learned.
    /// </summary>
    public IReadOnlyList<RosterRow> Roster(int? path, int level, IReadOnlyList<LearnedSkill> skills, IReadOnlyList<LearnedSpell> spells)
    {
        List<LadderStep> mine = [.. Steps.Where(step => step.Path == path)];

        int? LevelOf(string name, bool spell) =>
            mine.FirstOrDefault(step => step.Spell == spell && step.Name == CompanionSpells.Bare(name))?.Level;

        List<RosterRow> rows =
        [
            .. skills.Select(skill => new RosterRow(skill.Name, false, skill.Icon, skill.Slot, LevelOf(skill.Name, false))),
            .. spells.Select(spell => new RosterRow(spell.Name, true, spell.Icon, spell.Slot, LevelOf(spell.Name, true)))
        ];

        HashSet<string> known = [.. rows.Select(row => CompanionSpells.Bare(row.Name))];

        rows.AddRange(mine
            .Where(step => step.Level > level && !known.Contains(step.Name))
            .Select(step => new RosterRow(step.Name, step.Spell, step.Icon, null, step.Level)));

        return [.. rows.OrderBy(row => row.Level ?? 0).ThenBy(row => row.Learned ? 0 : 1).ThenBy(row => row.Spell ? 1 : 0)];
    }

    /// <summary>
    /// The class byte of a profile (0x39) — Hades <c>ServerFormat39</c> writes, after the group text, the group status,
    /// a zero and then <c>Aisling.Path</c>. Null when the profile is cut short.
    /// </summary>
    public static int? PathFromProfile(ReadOnlySpan<byte> body)
    {
        const int fixedAfterClan = 8;

        try
        {
            if (body.Length < 2)
            {
                return null;
            }

            LegacyKoreanEncoding.DecodeStringA(body[1..], out int clan);
            int at = 1 + clan + fixedAfterClan;

            if (at >= body.Length)
            {
                return null;
            }

            LegacyKoreanEncoding.DecodeStringA(body[at..], out int group);
            int path = at + group + 2;

            return path < body.Length ? body[path] : null;
        }
        catch (ProtocolException)
        {
            return null;
        }
    }
}
