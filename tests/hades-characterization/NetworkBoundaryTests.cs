using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-10: a hostile or truncated frame must cost its own connection and nothing else. The server and every
/// other connection keep working.
/// </summary>
public sealed class NetworkBoundaryTests
{
    public static TheoryData<string, byte[]> HostileFrames() => new()
    {
        { "wrong magic", [0xBB, 0x00, 0x02, 0x00, 0x00] },
        { "zero length", [0xAA, 0x00, 0x00] },
        { "truncated body", [0xAA, 0x00, 0x10, 0x00] },
        { "length beyond anything sent", [0xAA, 0xFF, 0xFF, 0x00] },
        { "unregistered command", [0xAA, 0x00, 0x02, 0xEE, 0x00] },
    };

    [Fact]
    public void Hostile_frames_leave_the_server_serving_normal_clients()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        foreach (object[] row in HostileFrames().Select(row => row.ToArray()))
        {
            byte[] frame = (byte[])row[1];

            using Hades718TestClient hostile = Hades718TestClient.Connect(server.LoginPort);
            hostile.Receive();
            hostile.SendRaw(frame);
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
