using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

public sealed class LongPressTests
{
    private static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void Not_yet_down_never_crosses()
    {
        LongPress press = new(Threshold);

        Assert.False(press.CrossedThreshold(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Before_the_threshold_it_has_not_crossed()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);

        Assert.False(press.CrossedThreshold(TimeSpan.FromMilliseconds(499)));
    }

    [Fact]
    public void At_the_threshold_it_crosses_exactly_once()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);

        Assert.True(press.CrossedThreshold(TimeSpan.FromMilliseconds(500)));
        Assert.False(press.CrossedThreshold(TimeSpan.FromMilliseconds(600)));
    }

    [Fact]
    public void A_press_that_crossed_the_threshold_is_not_a_short_press()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);
        press.CrossedThreshold(TimeSpan.FromMilliseconds(500));
        press.Up();

        Assert.False(press.ShortPress);
    }

    [Fact]
    public void A_press_released_before_the_threshold_is_a_short_press()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);
        press.CrossedThreshold(TimeSpan.FromMilliseconds(200));
        press.Up();

        Assert.True(press.ShortPress);
    }

    [Fact]
    public void Releasing_without_ever_checking_is_still_a_short_press()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);
        press.Up();

        Assert.True(press.ShortPress);
    }

    [Fact]
    public void A_new_press_forgets_the_previous_ones_long_press()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);
        press.CrossedThreshold(TimeSpan.FromMilliseconds(500));
        press.Up();

        press.Down(TimeSpan.FromSeconds(5));

        Assert.True(press.ShortPress);
        Assert.False(press.CrossedThreshold(TimeSpan.FromMilliseconds(5499)));
        Assert.True(press.CrossedThreshold(TimeSpan.FromMilliseconds(5500)));
    }

    [Fact]
    public void Lifting_the_finger_stops_it_from_crossing_later()
    {
        LongPress press = new(Threshold);
        press.Down(TimeSpan.Zero);
        press.Up();

        Assert.False(press.CrossedThreshold(TimeSpan.FromMilliseconds(500)));
    }
}
