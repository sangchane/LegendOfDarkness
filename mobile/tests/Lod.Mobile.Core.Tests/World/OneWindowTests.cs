using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Only one big window at a time: opening one names the one that has to shut first, shutting one that is not open
/// changes nothing, and only the windows that lie over the world stop it taking taps and steps.
/// </summary>
public sealed class OneWindowTests
{
    [Fact]
    public void Opening_a_second_window_shuts_the_first()
    {
        OneWindow windows = new();

        Assert.Null(windows.Open(GameWindow.Pack));
        Assert.Equal(GameWindow.Pack, windows.Open(GameWindow.Settings));
        Assert.Equal(GameWindow.Settings, windows.Current);
        Assert.False(windows.IsOpen(GameWindow.Pack));
    }

    /// <summary>Opening the one already open asks nothing to shut — it would shut itself.</summary>
    [Fact]
    public void Opening_the_same_window_again_shuts_nothing()
    {
        OneWindow windows = new();
        windows.Open(GameWindow.TabMap);

        Assert.Null(windows.Open(GameWindow.TabMap));
        Assert.True(windows.IsOpen(GameWindow.TabMap));
    }

    /// <summary>
    /// A window shut late — the server's close (0x30) arriving after the pack was already opened over it — must not shut
    /// the window that is open now.
    /// </summary>
    [Fact]
    public void Shutting_a_window_that_is_not_open_leaves_the_open_one()
    {
        OneWindow windows = new();
        windows.Open(GameWindow.Talk);
        windows.Open(GameWindow.Pack);

        windows.Shut(GameWindow.Talk);

        Assert.Equal(GameWindow.Pack, windows.Current);

        windows.Shut(GameWindow.Pack);

        Assert.Null(windows.Current);
    }

    /// <summary>
    /// The windows that lie over the world (pack, an NPC's talk, the log) stop it; the others leave the pad and the fan
    /// alive — settings, the 길 찾기 map (the character walks while it is open), the world map (the server holds us
    /// anyway) and the bot's gear.
    /// </summary>
    [Theory]
    [InlineData(GameWindow.Pack, true)]
    [InlineData(GameWindow.Talk, true)]
    [InlineData(GameWindow.Chat, true)]
    [InlineData(GameWindow.Settings, false)]
    [InlineData(GameWindow.TabMap, false)]
    [InlineData(GameWindow.WorldMap, false)]
    [InlineData(GameWindow.BotGear, false)]
    public void Only_windows_over_the_world_freeze_it(GameWindow window, bool freezes)
    {
        OneWindow windows = new();
        windows.Open(window);

        Assert.Equal(freezes, OneWindow.Freezes(window));
        Assert.Equal(freezes, windows.Freezing);
    }

    [Fact]
    public void Nothing_open_freezes_nothing()
    {
        Assert.False(new OneWindow().Freezing);
    }
}
