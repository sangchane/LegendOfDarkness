using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-10: a hostile or truncated frame must cost its own connection and nothing else. The server and every
/// other connection keep working.
/// </summary>
public sealed class NetworkBoundaryTests
{
    /// <summary>Frames that cannot be a 7.18 packet under any reading, so the server can reject them outright.</summary>
    private static readonly (string Name, byte[] Bytes)[] StructurallyInvalidFrames =
    [
        ("wrong magic", [0xBB, 0x00, 0x02, 0x00, 0x00]),
        ("zero length", [0xAA, 0x00, 0x00]),
        ("length beyond the receive buffer", [0xAA, 0xFF, 0xFF, 0x00]),
    ];

    /// <summary>
    /// Everything above, plus frames the server cannot judge from their shape alone: a truncated body reads
    /// like a slow sender (closing it needs the handshake time limit in P0-13), and an unregistered command
    /// is a well-formed frame the server simply does not implement.
    /// </summary>
    private static readonly (string Name, byte[] Bytes)[] HostileFrames =
    [
        .. StructurallyInvalidFrames,
        ("truncated body", [0xAA, 0x00, 0x10, 0x00]),
        ("unregistered command", [0xAA, 0x00, 0x02, 0xEE, 0x00]),
    ];

    [Fact]
    public void A_structurally_invalid_frame_closes_its_own_connection()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        // Control: without this, a detector that reports every connection as closed would pass silently.
        using (Hades718TestClient wellBehaved = Hades718TestClient.Connect(server.LoginPort))
        {
            wellBehaved.Receive();

            Assert.False(
                wellBehaved.WaitForServerToClose(TimeSpan.FromSeconds(3)),
                "An idle well-behaved connection was reported as closed, so this test cannot judge the others.");
        }

        List<string> leftOpen = [];

        foreach ((string name, byte[] bytes) in StructurallyInvalidFrames)
        {
            using Hades718TestClient hostile = Hades718TestClient.Connect(server.LoginPort);
            hostile.Receive();
            hostile.SendRaw(bytes);

            if (!hostile.WaitForServerToClose(TimeSpan.FromSeconds(3)))
            {
                leftOpen.Add(name);
            }
        }

        Assert.Equal([], leftOpen);
    }

    [Fact]
    public void Hostile_frames_leave_the_server_serving_normal_clients()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        foreach ((_, byte[] bytes) in HostileFrames)
        {
            using Hades718TestClient hostile = Hades718TestClient.Connect(server.LoginPort);
            hostile.Receive();
            hostile.SendRaw(bytes);
        }

        try
        {
            LoginFlow.EnterWorld(server);
        }
        catch (Exception failure)
        {
            throw new InvalidOperationException(
                $"A normal client could not log in after the hostile frames.{Environment.NewLine}" +
                $"--- server output ---{Environment.NewLine}{server.ConsoleOutput}",
                failure);
        }
    }
}
