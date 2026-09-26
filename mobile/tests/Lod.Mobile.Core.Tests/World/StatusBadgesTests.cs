using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The small status icons in my plate and in the bot's slot: the original's own icons (0x3A) and the states the server
/// tells in 0x5E kind 3 (5.99 호르라마·에나르마, which never come as 0x3A) put together into one list of badges.
/// </summary>
public sealed class StatusBadgesTests
{
    /// <summary>Seconds become the original's time grades, so the same strip draws both.</summary>
    [Theory]
    [InlineData(120, 6)]
    [InlineData(90, 6)]
    [InlineData(75, 5)]
    [InlineData(45, 4)]
    [InlineData(25, 3)]
    [InlineData(12, 2)]
    [InlineData(3, 1)]
    public void Seconds_become_grades(int seconds, int grade)
    {
        Assert.Equal(grade, StatusBadges.Grade(seconds));
    }

    /// <summary>A told state whose picture already came as 0x3A is not drawn twice; one with no picture is left out.</summary>
    [Fact]
    public void Mine_puts_both_together_without_doubles()
    {
        Ailment[] original = [new(82, 4)];
        CompanionStatus[] told =
        [
            new("horrama", 110, false, 11),
            new("curse", 40, true, 82),
            new("mystery", 30, false, 0),
        ];

        IReadOnlyList<StatusBadge> shown = StatusBadges.Of(original, told);

        Assert.Equal([82, 11], shown.Select(one => one.Icon));
        Assert.True(shown[0].Harmful);
        Assert.Equal(110, shown[1].Seconds);
        Assert.Equal(6, shown[1].Grade);
    }

    /// <summary>The ones running out first come first; nothing at all is an empty strip.</summary>
    [Fact]
    public void Soonest_first_and_empty_when_nothing()
    {
        CompanionStatus[] told = [new("enare", 140, false, 52), new("horrama", 20, false, 11)];

        Assert.Equal([11, 52], StatusBadges.Of([], told).Select(one => one.Icon));
        Assert.Empty(StatusBadges.Of([], null));
    }
}
