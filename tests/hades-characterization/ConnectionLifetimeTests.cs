using System.Net.NetworkInformation;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-13: connections that leave before authenticating must cost the server nothing. Sockets the server
/// never closes are visible from the outside as connections stuck in CLOSE_WAIT.
/// </summary>
public sealed class ConnectionLifetimeTests
{
    private const int Cycles = 50;

    [Fact]
    public void Connections_that_leave_before_authenticating_do_not_linger()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        // Control: if the socket enumeration cannot see this server's connections at all, the count below
        // would be zero no matter how badly the server leaked.
        using (Hades718TestClient probe = Hades718TestClient.Connect(server.LoginPort))
        {
            probe.Receive();

            Assert.True(
                CountInState(server.LoginPort, TcpState.Established) > 0,
                "No established connection was visible on the server port, so this test cannot see leaks.");
        }

        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            using Hades718TestClient client = Hades718TestClient.Connect(server.LoginPort);
            client.Receive();
        }

        int lingering = WaitForSocketsToSettle(server.LoginPort, TimeSpan.FromSeconds(15));

        Assert.True(
            lingering == 0,
            $"{lingering} of {Cycles} connections were still held by the server after the client left.");

        // The server must also still be serving: a leak test that only proves the server died is no test.
        LoginFlow.EnterWorld(server);
    }

    /// <summary>
    /// Counts sockets the server has not closed. CLOSE_WAIT means the client is gone and the server side is
    /// still open, which is exactly the leak this test is about.
    /// </summary>
    private static int WaitForSocketsToSettle(int port, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        int lingering;

        do
        {
            lingering = CountCloseWait(port);

            if (lingering == 0)
            {
                return 0;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return lingering;
    }

    private static int CountCloseWait(int port) => CountInState(port, TcpState.CloseWait);

    private static int CountInState(int port, TcpState state) =>
        IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpConnections()
            .Count(connection => connection.LocalEndPoint.Port == port && connection.State == state);
}
