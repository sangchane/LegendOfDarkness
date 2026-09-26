using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 기술 목록의 "N레벨에 배움"(사용자 요청 2026-09-26) — 서버와 같은 자동 습득 표(<c>assets/world/auto-learn.txt</c>)로 내 직업의
/// 배운 것과 아직 못 배운 것을 레벨 순으로 늘어놓는다(<see cref="LearnLadder" />).
/// </summary>
public sealed class LearnLadderTests
{
    private const string Table = """
        # tools: scripts/build-auto-learn.py
        # 직업	레벨	skill|spell	이름	그림
        5	11	spell	주먹단련	7
        5	11	spell	쿠로토	0
        5	15	skill	이형환위	42
        5	31	skill	단각	2
        5	31	spell	장풍	0
        5	41	spell	금강불괴	0
        5	50	skill	구양신공	9
        1	5	skill	숏블레이드	1
        """;

    private static readonly LearnLadder Ladder = LearnLadder.Read(Table);

    [Fact]
    public void The_table_is_read_without_its_comments()
    {
        Assert.Equal(8, Ladder.Steps.Count);
        Assert.Equal(new LadderStep(5, 15, false, "이형환위", 42), Ladder.Steps[2]);
        Assert.Equal(new LadderStep(5, 11, true, "주먹단련", 7), Ladder.Steps[0]);
    }

    [Fact]
    public void A_monk_sees_what_he_has_and_what_comes_later_in_level_order()
    {
        IReadOnlyList<RosterRow> rows = Ladder.Roster(
            path: 5,
            level: 31,
            skills: [new LearnedSkill(1, 1, "Assail (Lev:1/100)"), new LearnedSkill(2, 2, "단각 (Lev:1/100)"), new LearnedSkill(3, 42, "이형환위 (Lev:1/100)")],
            spells: [new LearnedSpell(1, 0, SpellTargetType.NoTarget, "장풍 (Lev:1/100)", string.Empty, 1),
                new LearnedSpell(2, 7, SpellTargetType.NoTarget, "주먹단련 (Lev:1/100)", string.Empty, 1),
                new LearnedSpell(3, 0, SpellTargetType.NoTarget, "쿠로토 (Lev:1/100)", string.Empty, 1)]);

        // 표에 없는 배운 것(Assail)이 먼저, 그 뒤 레벨 순 — 같은 레벨은 기술 먼저. 못 배운 것은 끝에 레벨과 함께.
        Assert.Equal(
            ["Assail", "주먹단련", "쿠로토", "이형환위", "단각", "장풍", "금강불괴", "구양신공"],
            rows.Select(row => LearnLadderTests.Bare(row.Name)));
        Assert.All(rows.Take(6), row => Assert.True(row.Learned));

        RosterRow later = rows[6];
        Assert.False(later.Learned);
        Assert.Equal(("금강불괴", true, 0, 41), (later.Name, later.Spell, later.Icon, later.Level));
        Assert.Null(later.Slot);

        Assert.Equal((false, 50, 9), (rows[7].Spell, rows[7].Level, rows[7].Icon));
        Assert.Equal((2, false), (rows.Single(row => row.Name.StartsWith("단각")).Slot, rows.Single(row => row.Name.StartsWith("단각")).Spell));
    }

    /// <summary>레벨이 넘었는데 없는 것(윗 기술을 가져 서버가 주지 않는 단각 따위)은 "배움" 으로 적지 않는다 — 올 일이 없다.</summary>
    [Fact]
    public void Something_already_past_but_missing_is_not_promised()
    {
        IReadOnlyList<RosterRow> rows = Ladder.Roster(5, 31, [new LearnedSkill(4, 60, "연천단각 (Lev:1/100)")], []);

        Assert.DoesNotContain(rows, row => row.Name == "단각");
        Assert.Contains(rows, row => row.Name == "금강불괴" && !row.Learned);
    }

    [Fact]
    public void Another_class_or_an_unknown_one_shows_nothing_locked()
    {
        Assert.DoesNotContain(Ladder.Roster(5, 1, [], []), row => row.Name == "숏블레이드");
        Assert.Empty(Ladder.Roster(null, 1, [], []));
        Assert.Equal(["Assail"], Ladder.Roster(null, 1, [new LearnedSkill(1, 1, "Assail")], []).Select(row => row.Name));
    }

    /// <summary>직업은 프로필(0x39) 끝, 그룹 글 뒤 세 번째 바이트다(Hades <c>ServerFormat39</c>: 그룹 상태 · 0 · 직업).</summary>
    [Fact]
    public void The_profile_says_which_class_i_am()
    {
        byte[] alone =
        [
            0x01,
            .. LegacyKoreanEncoding.EncodeStringA("어둠"),
            0x07, 0, 0, 0, 0, 0, 0, 1,
            .. LegacyKoreanEncoding.EncodeStringA("Adventuring Alone"),
            0x00, 0x00, 0x05, 0x01
        ];

        Assert.Equal(5, LearnLadder.PathFromProfile(alone));
        Assert.Null(LearnLadder.PathFromProfile([0x01, 0x00]));
    }

    private static string Bare(string name) => CompanionSpells.Bare(name);
}
