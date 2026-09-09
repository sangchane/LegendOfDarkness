using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-13: the connected-client collection is a plain dictionary mutated from socket callbacks, and the lock
/// that appears to guard it locks a list rebuilt on every access. This drives connections at it concurrently
/// and requires the server to come out serving, with nothing thrown into its log.
/// </summary>
public sealed class ConnectionConcurrencyTests
{
    private const int Connections = 150;

    [Fact]
    public void Concurrent_connections_leave_the_server_serving()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        Parallel.For(0, Connections, _ =>
        {
            try
            {
                using Hades718TestClient client = Hades718TestClient.Connect(server.LoginPort);
                client.Receive();

                // Sending makes the server walk its client collection while other threads are adding to and
                // removing from it, which is where the missing synchronisation shows.
                client.Send(0x00, [0x02, 0xCE, 0x4C, 0x4B]);
                client.Receive();
            }
            catch (Exception)
            {
                // A refused or reset connection is the server's choice to make; what matters is the state
                // it is left in, which the assertions below cover.
            }
        });

        LoginFlow.EnterWorld(server);

        Assert.DoesNotContain("InvalidOperationException", server.ConsoleOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("IndexOutOfRangeException", server.ConsoleOutput, StringComparison.Ordinal);
    }
}
