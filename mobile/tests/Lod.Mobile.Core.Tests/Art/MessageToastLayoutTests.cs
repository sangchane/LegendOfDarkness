using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

public sealed class MessageToastLayoutTests
{
    [Fact]
    public void Landscape_toast_is_no_wider_than_the_three_column_direction_pad()
    {
        Assert.Equal(152, MessageToastLayout.DirectionPadWidth(touchTarget: 48, gap: 4));
    }
}
