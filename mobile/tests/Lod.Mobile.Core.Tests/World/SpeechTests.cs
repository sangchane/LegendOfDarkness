using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// What somebody near us said (0x0D) — the kind of speech, who said it, and the words. Hades writes it as
/// <c>ServerFormat0D</c>: the kind, the speaker's serial big-endian, then the line.
/// </summary>
public sealed class SpeechTests
{
    [Fact]
    public void A_line_keeps_the_speaker_and_the_words()
    {
        Spoken spoken = WorldClient.ReadSpoken([
            0x00,
            0x00, 0x00, 0x30, 0x39,
            0x05, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o'
        ]);

        Assert.Equal(SpeechKind.Normal, spoken.Kind);
        Assert.Equal(12345u, spoken.Serial);
        Assert.Equal("hello", spoken.Text);
    }

    [Fact]
    public void A_shout_says_so()
    {
        Spoken spoken = WorldClient.ReadSpoken([0x01, 0x00, 0x00, 0x00, 0x07, 0x02, (byte)'h', (byte)'i']);

        Assert.Equal(SpeechKind.Shout, spoken.Kind);
        Assert.Equal(7u, spoken.Serial);
        Assert.Equal("hi", spoken.Text);
    }

    /// <summary>A chant is a spell being cast aloud, not somebody talking; the screen may want to leave it out.</summary>
    [Fact]
    public void A_chant_says_so()
    {
        Spoken spoken = WorldClient.ReadSpoken([0x02, 0x00, 0x00, 0x00, 0x07, 0x01, (byte)'a']);

        Assert.Equal(SpeechKind.Chant, spoken.Kind);
    }

    [Fact]
    public void A_line_too_short_to_hold_anything_is_refused()
    {
        Assert.Throws<Lod.Mobile.Core.Protocol.ProtocolException>(() => WorldClient.ReadSpoken([0x00, 0x00, 0x01]));
    }
}
