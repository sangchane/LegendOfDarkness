using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-13: a frame that stops half way is indistinguishable from a slow sender by shape alone, so only a time
/// limit clears it. An idle connection between frames is not stalled and must be left alone.
/// </summary>
public sealed class IncompleteFrameTimeoutTests
{
    [Fact]
    public void A_connection_that_stops_mid_frame_is_closed()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        // Control: a connection that simply waits between frames must survive the same period.
        using (Hades718TestClient idle = Hades718TestClient.Connect(server.LoginPort))
        {
            idle.Receive();

            Assert.False(
                idle.WaitForServerToClose(Grace),
                "An idle connection was dropped, so the limit is cutting more than stalled frames.");
        }

        using Hades718TestClient stalled = Hades718TestClient.Connect(server.LoginPort);
        stalled.Receive();

        // Declares a sixteen byte body and sends one, then goes quiet.
        stalled.SendRaw(0xAA, 0x00, 0x10, 0x00);

        Assert.True(
            stalled.WaitForServerToClose(Grace),
            "A connection that stopped half way through a frame was held open.");
    }

    private static TimeSpan Grace =>
        TimeSpan.FromSeconds(IsolatedHadesServer.IncompleteFrameTimeoutSeconds * 3);
}
