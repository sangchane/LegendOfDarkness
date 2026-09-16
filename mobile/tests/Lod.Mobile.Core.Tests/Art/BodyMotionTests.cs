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

    [Fact]
    public void The_plain_blow_is_the_file_ending_02()
    {
        Assert.Equal(new BodyMotion("02", 0, 2), BodyMotion.Of(1));
    }

    /// <summary>Nothing we know how to draw: no motion, a hands-up (6) and the emotes, and past the table.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
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
    /// drawing. Read as the whole motion in hundredths of a second, split over its drawings, it lands there.
    /// </summary>
    [Fact]
    public void Speed_is_the_whole_motion_in_hundredths_split_over_its_drawings()
    {
        Assert.Equal(0.15, BodyMotion.Of(1)!.SecondsPerFrame(30), 3);
        Assert.Equal(0.125, BodyMotion.Of(133)!.SecondsPerFrame(50), 3);
    }

    [Fact]
    public void No_speed_still_plays_at_the_pace_of_a_blow()
    {
        Assert.Equal(0.14, BodyMotion.Of(131)!.SecondsPerFrame(0), 3);
    }
}
