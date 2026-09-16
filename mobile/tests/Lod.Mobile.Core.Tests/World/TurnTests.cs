using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Somebody turning on the spot (0x11 from the server, ServerFormat11): whose serial, then which way. A
/// monster sends it just before it swings, to face whoever it is about to hit — without it the blow is drawn
/// towards wherever it last walked.
/// </summary>
public sealed class TurnTests
{
    [Theory]
    [InlineData(0, Direction.North)]
    [InlineData(1, Direction.East)]
    [InlineData(2, Direction.South)]
    [InlineData(3, Direction.West)]
    public void A_turn_names_somebody_and_the_way_they_now_face(byte way, Direction facing)
    {
        Assert.Equal((900u, facing), WorldClient.ReadTurn([0x00, 0x00, 0x03, 0x84, way]));
    }
}
