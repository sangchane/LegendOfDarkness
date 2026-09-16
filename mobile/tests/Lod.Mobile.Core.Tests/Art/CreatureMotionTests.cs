using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// Which drawing a creature shows while it stands, walks and swings. Every creature numbers its own frames
/// differently — the file says so in its header — so this is read per creature rather than assumed. The
/// numbers below are the real ones, out of <c>hades.dat</c>: see docs/original-sprite-animation.md 4절.
/// </summary>
public sealed class CreatureMotionTests
{
    // 말벌 MNS001: 8프레임, 서기 0+2 · 걷기 0+2 · 공격 4+2
    private static readonly CreatureMotion Wasp = CreatureMotion.Read("frames 8\nstand 0 2\nwalk 0 2\nattack 4 2\n")!;

    // MNS053: 8프레임, 서기 없음 · 걷기 0+3 · 공격 6+1
    private static readonly CreatureMotion Restless = CreatureMotion.Read("frames 8\nstand 0 0\nwalk 0 3\nattack 6 1\n")!;

    // MNS197: 14프레임, 서기 없음 · 걷기 0+5 · 공격 10+2
    private static readonly CreatureMotion Big = CreatureMotion.Read("frames 14\nstand 0 0\nwalk 0 5\nattack 10 2\n")!;

    [Fact]
    public void The_header_is_read_as_written()
    {
        Assert.Equal(8, Wasp.Frames);
        Assert.Equal(14, Big.Frames);
    }

    [Fact]
    public void Walking_runs_through_that_creatures_own_frames_and_starts_again()
    {
        Assert.Equal([0, 1, 0, 1], Steps(Wasp, Side.Back, walk: true, count: 4));
        Assert.Equal([0, 1, 2, 0, 1, 2], Steps(Restless, Side.Back, walk: true, count: 6));
        Assert.Equal([0, 1, 2, 3, 4, 0], Steps(Big, Side.Back, walk: true, count: 6));
    }

    [Fact]
    public void Swinging_starts_where_that_creatures_blow_starts()
    {
        Assert.Equal([4, 5, 4], Steps(Wasp, Side.Back, walk: false, count: 3));
        Assert.Equal([6, 6, 6], Steps(Restless, Side.Back, walk: false, count: 3));
        Assert.Equal([10, 11, 10], Steps(Big, Side.Back, walk: false, count: 3));
    }

    /// <summary>
    /// Every stretch is drawn twice, back first and then front, the same count each — the header gives the
    /// start and the count of one of them. That is why each sheet above holds exactly twice its walk and
    /// blow: the wasp's 2 + 2 is 8, the big one's 5 + 2 is 14. A creature coming towards us is on the second.
    /// </summary>
    [Fact]
    public void Seen_from_the_front_a_creature_uses_the_second_drawing_of_each_stretch()
    {
        Assert.Equal([2, 3, 2, 3], Steps(Wasp, Side.Front, walk: true, count: 4));
        Assert.Equal([3, 4, 5, 3], Steps(Restless, Side.Front, walk: true, count: 4));
        Assert.Equal([5, 6, 7, 8, 9, 5], Steps(Big, Side.Front, walk: true, count: 6));

        Assert.Equal([6, 7, 6], Steps(Wasp, Side.Front, walk: false, count: 3));
        Assert.Equal([7, 7, 7], Steps(Restless, Side.Front, walk: false, count: 3));
        Assert.Equal([12, 13, 12], Steps(Big, Side.Front, walk: false, count: 3));
    }

    [Fact]
    public void Standing_shows_the_standing_frame_when_there_is_one()
    {
        Assert.Equal(0, Wasp.Stand(Side.Back));
        Assert.Equal(2, Wasp.Stand(Side.Front));
    }

    /// <summary>
    /// A creature with no standing frames may not stand still — the original's <c>fStop</c> is 0 for a wasp
    /// because it has to keep its wings going, and its file gives it nothing to stand on. Showing the first
    /// walking frame is what the original does; showing frame zero of nothing would be a blank tile.
    /// </summary>
    [Fact]
    public void A_creature_that_cannot_stand_still_shows_its_first_walking_frame()
    {
        Assert.Equal(0, Restless.Stand(Side.Back));
        Assert.Equal(0, Big.Stand(Side.Back));
        Assert.Equal(5, Big.Stand(Side.Front));
    }

    [Fact]
    public void No_frame_is_ever_asked_for_outside_the_sheet()
    {
        foreach (CreatureMotion one in new[] { Wasp, Restless, Big })
        {
            foreach (Side side in new[] { Side.Back, Side.Front })
            {
                Assert.InRange(one.Stand(side), 0, one.Frames - 1);

                for (int step = 0; step < 20; step++)
                {
                    Assert.InRange(one.Walk(side, step), 0, one.Frames - 1);
                    Assert.InRange(one.Strike(side, step), 0, one.Frames - 1);
                }
            }
        }
    }

    /// <summary>A step that has gone backwards must not ask for a negative frame.</summary>
    [Fact]
    public void Walking_backwards_stays_inside_the_sheet()
    {
        Assert.InRange(Wasp.Walk(Side.Back, -1), 0, Wasp.Frames - 1);
        Assert.InRange(Big.Walk(Side.Front, -7), 0, Big.Frames - 1);
    }

    /// <summary>A creature whose file says nothing still has to draw something rather than nothing.</summary>
    [Fact]
    public void A_file_that_says_nothing_still_draws_the_first_frame()
    {
        CreatureMotion silent = CreatureMotion.Read("frames 1\nstand 0 0\nwalk 0 0\nattack 0 0\n")!;

        foreach (Side side in new[] { Side.Back, Side.Front })
        {
            Assert.Equal(0, silent.Stand(side));
            Assert.Equal(0, silent.Walk(side, 3));
            Assert.Equal(0, silent.Strike(side, 2));
        }
    }

    /// <summary>Nothing at all is not a creature. Better to say so than to draw an empty tile.</summary>
    [Fact]
    public void An_empty_file_is_refused()
    {
        Assert.Null(CreatureMotion.Read(string.Empty));
        Assert.Null(CreatureMotion.Read("stand 0 2\n"));
    }

    private static int[] Steps(CreatureMotion motion, Side side, bool walk, int count)
    {
        int[] frames = new int[count];

        for (int step = 0; step < count; step++)
        {
            frames[step] = walk ? motion.Walk(side, step) : motion.Strike(side, step);
        }

        return frames;
    }
}
