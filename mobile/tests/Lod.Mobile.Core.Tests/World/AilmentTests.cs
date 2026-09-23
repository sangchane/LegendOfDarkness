using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The server tells the one afflicted what is on them with <c>0x3A</c>: a picture number and a grade for how
/// much longer it lasts. It does not send seconds — <c>Debuff.Display</c> turns the time left into one of six
/// bands, and sends grade 0 when the thing wears off.
/// </summary>
public sealed class AilmentTests
{
    /// <summary>The picture number takes two bytes, most significant first, and the grade one.</summary>
    [Fact]
    public void A_status_is_a_picture_and_a_grade()
    {
        Ailment told = WorldClient.ReadAilment([0x00, 0x52, 0x04]);

        Assert.Equal(82, told.Icon);
        Assert.Equal(4, told.Left);
    }

    /// <summary>Grade zero is how the server says it is over (<c>Debuff.OnEnded</c>).</summary>
    [Fact]
    public void Grade_zero_means_it_has_worn_off()
    {
        Ailment gone = WorldClient.ReadAilment([0x00, 0x52, 0x00]);

        Assert.Equal(0, gone.Left);
        Assert.Equal(0, gone.Seconds);
    }

    /// <summary>
    /// What each grade is worth in seconds — the floor of its band, so a bar drawn from it is never longer than
    /// the truth. The bands are the ones in <c>Debuff.Display</c>.
    /// </summary>
    [Theory]
    [InlineData(6, 90)]
    [InlineData(5, 60)]
    [InlineData(4, 30)]
    [InlineData(3, 20)]
    [InlineData(2, 10)]
    [InlineData(1, 1)]
    public void A_grade_says_roughly_how_long_is_left(int grade, int seconds)
    {
        Assert.Equal(seconds, new Ailment(82, grade).Seconds);
    }

    /// <summary>A packet too short to hold both is a broken one, not a status with something missing.</summary>
    [Fact]
    public void A_short_packet_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadAilment([0x00, 0x52]));
    }

    /// <summary>
    /// Somebody else's status (0x5C, our own packet): who, the same picture and grade as 0x3A, whether it harms,
    /// and the effect picture a monster is tinted after — 프라보 on a monster is curse picture 82 and effect 257.
    /// </summary>
    [Fact]
    public void Somebody_elses_status_names_who_and_the_effect_it_came_with()
    {
        SeenAilment seen = WorldClient.ReadSeenAilment([0x00, 0x01, 0x02, 0x03, 0x00, 0x52, 0x06, 0x01, 0x01, 0x01]);

        Assert.Equal(0x00010203u, seen.Serial);
        Assert.Equal(82, seen.Icon);
        Assert.Equal(6, seen.Left);
        Assert.True(seen.Harmful);
        Assert.Equal(257, seen.Effect);
        Assert.Equal(new Ailment(82, 6), seen.Badge);
    }

    /// <summary>A buff says so with a zero, and an unknown effect is zero too.</summary>
    [Fact]
    public void A_buff_on_somebody_else_is_not_harmful()
    {
        SeenAilment seen = WorldClient.ReadSeenAilment([0x00, 0x00, 0x00, 0x09, 0x00, 0x35, 0x00, 0x00, 0x00, 0x00]);

        Assert.False(seen.Harmful);
        Assert.Equal(0, seen.Left);
        Assert.Equal(0, seen.Effect);
    }

    [Fact]
    public void A_short_status_of_somebody_else_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadSeenAilment([0x00, 0x00, 0x00, 0x09, 0x00, 0x35, 0x06, 0x01, 0x01]));
    }
}
