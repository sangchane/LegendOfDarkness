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
}
