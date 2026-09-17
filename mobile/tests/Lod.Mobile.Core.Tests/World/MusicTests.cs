using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 0x19 carries two different things. The original client splits them by the number: under 128 it is a sound effect
/// from <c>Legend.dat</c>, 128 and over is the map's music (number − 128), and 228 turns music off
/// (<c>Legend.exe 0x54c440</c>). Hades sends the map's music the same way after the fix of 2026-09-18.
/// </summary>
public sealed class MusicTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    public void A_small_number_is_a_sound_effect(int number)
    {
        Assert.Equal(number, Music.Effect(number));
        Assert.Null(Music.Song(number));
    }

    [Theory]
    [InlineData(128, 0)]
    [InlineData(173, 45)]
    [InlineData(145, 17)]
    [InlineData(191, 63)]
    public void A_big_number_is_a_song(int number, int song)
    {
        Assert.Equal(song, Music.Song(number));
        Assert.Null(Music.Effect(number));
    }

    /// <summary>228 is the one that stops the music rather than starting a song.</summary>
    [Fact]
    public void The_number_that_turns_music_off_names_no_song()
    {
        Assert.Equal(Music.Silence, Music.Song(228));
        Assert.Null(Music.Effect(228));
    }

    /// <summary>The maps the 5.99 pack added ask for songs nobody has — 900 would be song 772.</summary>
    [Fact]
    public void A_song_we_do_not_have_is_still_a_song()
    {
        Assert.Equal(772, Music.Song(900));
    }
}
