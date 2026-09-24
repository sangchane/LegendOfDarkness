using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// Which drawings a body motion from the server (0x1A) plays. 1 is the plain blow in the file ending 02; 128 and
/// up are the eighteen rows of the original's skill.tbl, 128 + NO — the reference repositories name them the same
/// way (Arbiter BodyAnimation: 128 PriestCast · 131 Kick · 136 WizardCast … 145 Summon). docs/original-sprite-animation.md 3절.
/// </summary>
public sealed class BodyMotionTests
{
    // 파일마다 그림이 들어 있는 칸 수(3.2절) — b 성직자 · c 전사 · d 무도가 · e 도적 · f 마법사.
    private static readonly Dictionary<string, int> Drawn = new() { ["b"] = 14, ["c"] = 30, ["d"] = 18, ["e"] = 36, ["f"] = 12 };

    [Theory]
    [InlineData(128, "b", 0, 3)]  // 성직자 시전
    [InlineData(131, "d", 0, 3)]  // 발차기
    [InlineData(132, "d", 6, 2)]  // 주먹
    [InlineData(133, "d", 10, 4)] // 돌려차기
    [InlineData(136, "f", 0, 2)]  // 마법사 시전
    [InlineData(137, "b", 6, 3)]  // 5.99 스크립트가 가장 많이 쓰는 번호
    [InlineData(145, "f", 4, 4)]  // 소환
    public void A_skill_motion_is_its_row_of_skill_tbl(int number, string file, int start, int count)
    {
        Assert.Equal(new BodyMotion(file, start, count), BodyMotion.Of(number));
    }

    /// <summary>
    /// 무도가의 맨손 평타. 기기에서 일반 휘두르기가 나왔는데(사용자, 2026-09-18) 번호도 옷도 맞으므로,
    /// 틀린 것은 서버 말을 듣기 전에 스스로 그리던 화면 쪽이었다. 여기 값이 그 근거다 — 도복은 갑옷
    /// 번호 3 에 공격모션 132 이고, 132 는 skill.tbl 4번 줄(주먹)이며 그 줄은 3 을 허락한다.
    /// </summary>
    [Fact]
    public void A_monk_in_the_robe_punches()
    {
        Assert.Equal(128, BodyMotion.FirstSkill);
        Assert.True(BodyMotion.Fits(132, 3));

        BodyMotion? fist = BodyMotion.Of(132);

        Assert.NotNull(fist);
        Assert.Equal("d", fist!.File);
        Assert.Equal(6, fist.Start);
        Assert.Equal(2, fist.Count);
    }

    /// <summary>
    /// The original client plays a skill motion only when the armour worn is one skill.tbl lists for that row (the ST
    /// column), and otherwise plays nothing at all — Legend.exe 2005 0x4e1161..0x4e1171, 4.51 0x4494b7. The table is
    /// the 2005 (= 5.99) one: Hades' copy has one more number per class at the end (348~352), which the original lacks.
    /// </summary>
    [Theory]
    [InlineData(133, 3, true)]    // 무도가 돌려차기 · 무도가 옷 3
    [InlineData(133, 2, false)]   // 전사 옷으로는 안 한다
    [InlineData(134, 4, true)]    // 도적 찌르기 · 도적 옷 4
    [InlineData(129, 2, true)]    // 전사 양손 · 전사 옷 2
    [InlineData(129, 4, false)]
    [InlineData(139, 2, false)]   // 검투사 줄 — 전사 옷 목록이 아니다
    [InlineData(139, 268, true)]
    [InlineData(128, 348, false)] // 하데스 사본에만 있는 번호
    [InlineData(128, 0, false)]   // 맨몸
    public void A_skill_motion_plays_only_in_clothes_its_row_lists(int number, int armour, bool plays)
    {
        Assert.Equal(plays, BodyMotion.Fits(number, armour));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void The_plain_blow_does_not_care_what_is_worn(int armour)
    {
        Assert.True(BodyMotion.Fits(1, armour));
    }

    [Fact]
    public void The_plain_blow_is_the_file_ending_02()
    {
        Assert.Equal(new BodyMotion("02", 0, 2), BodyMotion.Of(1));
    }

    /// <summary>
    /// The file ending 03 holds three motions, one per side each: 6 hands up (0 · 1), 21 blowing a kiss (2~3 · 4~5) and
    /// 22 waving (6~7 · 8~9), the last at a third of the interval. Legend.exe 2005 sets them up at 0x4e3657..0x4e36c9
    /// (start, drawings + 1) and picks the drawing at 0x4e23e8 / 0x4e24c5 / 0x4e2526 as start + (count − 1) × side + step.
    /// </summary>
    [Theory]
    [InlineData(6, 0, 1, 1)]
    [InlineData(21, 2, 2, 1)]
    [InlineData(22, 6, 2, 3)]
    public void The_hands_up_the_kiss_and_the_wave_are_stretches_of_the_03_file(int number, int start, int count, int faster)
    {
        Assert.Equal(new BodyMotion("03", start, count, faster), BodyMotion.Of(number));
    }

    [Fact]
    public void Waving_from_the_front_uses_the_last_two_drawings_at_a_third_of_the_interval()
    {
        BodyMotion wave = BodyMotion.Of(22)!;

        Assert.Equal([8, 9], Enumerable.Range(0, 2).Select(step => wave.Frame(Side.Front, step)));
        Assert.Equal(BodyMotion.Of(21)!.SecondsPerFrame(60) / 3, wave.SecondsPerFrame(60), 5);
    }

    /// <summary>Nothing we know how to draw: no motion, the emote balloons, and past the table.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(146)]
    public void A_motion_with_no_drawing_is_nothing(int number)
    {
        Assert.Null(BodyMotion.Of(number));
    }

    [Fact]
    public void From_behind_it_plays_the_first_stretch_and_from_the_front_the_second()
    {
        BodyMotion kick = BodyMotion.Of(133)!;

        Assert.Equal([10, 11, 12, 13], Enumerable.Range(0, 4).Select(step => kick.Frame(Side.Back, step)));
        Assert.Equal([14, 15, 16, 17], Enumerable.Range(0, 4).Select(step => kick.Frame(Side.Front, step)));
    }

    [Fact]
    public void A_step_past_the_end_holds_the_last_drawing()
    {
        BodyMotion punch = BodyMotion.Of(132)!;

        Assert.Equal(9, punch.Frame(Side.Front, 5));
        Assert.Equal(6, punch.Frame(Side.Back, -1));
    }

    /// <summary>
    /// The table is right only if it fills every file exactly: each drawn cell belongs to one row's back or
    /// front stretch, and no row asks for a cell past what the file holds.
    /// </summary>
    [Fact]
    public void Every_file_is_covered_exactly_once()
    {
        foreach ((string file, int drawn) in Drawn)
        {
            List<int> cells = Enumerable.Range(128, 18)
                .Select(BodyMotion.Of)
                .Where(motion => motion!.File == file)
                .SelectMany(motion => Enumerable.Range(motion!.Start, motion.Count * 2))
                .Order()
                .ToList();

            Assert.Equal(Enumerable.Range(0, drawn), cells);
        }
    }

    /// <summary>
    /// The server's speed for a blow is 30 (Hades Assail), which we already drew at about 0.14 seconds a
    /// drawing. Read as the whole motion in hundredths of a second, split over its drawings, it lands there —
    /// then <see cref="BodyMotion" />'s 30% slowdown holds it 1/0.7 longer still (2026-09-24, 사용자: 기술이
    /// 너무 빨라 안 보인다).
    /// </summary>
    [Fact]
    public void Speed_is_the_whole_motion_in_hundredths_split_over_its_drawings()
    {
        Assert.Equal(0.15 / 0.7, BodyMotion.Of(1)!.SecondsPerFrame(30), 3);
        Assert.Equal(0.125 / 0.7, BodyMotion.Of(133)!.SecondsPerFrame(50), 3);
    }

    [Fact]
    public void No_speed_still_plays_at_the_pace_of_a_blow()
    {
        Assert.Equal(0.14 / 0.7, BodyMotion.Of(131)!.SecondsPerFrame(0), 3);
    }

    /// <summary>
    /// The 30% slowdown is one multiplier applied to every motion alike, so a kick stays exactly as much
    /// slower than a blow as it always was — only how long each drawing is held changes, not the shape of
    /// the motion (2026-09-24).
    /// </summary>
    [Fact]
    public void The_slowdown_keeps_every_motion_s_speed_relative_to_the_others()
    {
        double blow = BodyMotion.Of(1)!.SecondsPerFrame(30);
        double kick = BodyMotion.Of(133)!.SecondsPerFrame(50);

        Assert.Equal(0.15 / 0.125, blow / kick, 5);
    }
}
