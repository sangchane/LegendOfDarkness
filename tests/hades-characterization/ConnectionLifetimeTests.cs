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
    /// The same connections, counted a second way. Every connection gets a thread of its own to write with,
    /// and that thread waits on a queue rather than on the socket — so closing the socket does not end it.
    /// A 30-minute soak found this the hard way: 4,070 threads after roughly 3,200 connections, after which
    /// the server still listened and still accepted but could no longer finish a single login.
    /// </summary>
    [Fact]
    public void Connections_that_end_do_not_leave_a_thread_behind()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        // Baseline after one full connection, so the thread a first connection needs is already counted and
        // what this measures is what each further connection leaves behind.
        using (Hades718TestClient warmup = Hades718TestClient.Connect(server.LoginPort))
        {
            warmup.Receive();
        }

        WaitForSocketsToSettle(server.LoginPort, TimeSpan.FromSeconds(15));
        int before = server.ThreadCount;

        Assert.True(before > 0, "The server process reported no threads, so this test cannot see a leak.");

        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            using Hades718TestClient client = Hades718TestClient.Connect(server.LoginPort);
            client.Receive();
        }

        WaitForSocketsToSettle(server.LoginPort, TimeSpan.FromSeconds(15));
        int grew = WaitForThreadsToSettle(server, before, TimeSpan.FromSeconds(15));

        // Not zero: the thread pool grows and shrinks on its own, and a run that happens to need one more
        // worker is not this leak. One thread per connection is, and that would be 50 here.
        Assert.True(
            grew < Cycles / 2,
            $"{Cycles} connections left {grew} extra threads behind ({before} → {before + grew}).");

        // As above: a leak test that only proves the server died is no test.
        LoginFlow.EnterWorld(server);
    }

    /// <summary>
    /// Writer threads finish what is queued before they end, so the count comes back down shortly after the
    /// connections do rather than the instant they do.
    /// </summary>
    private static int WaitForThreadsToSettle(IsolatedHadesServer server, int before, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        int grew;

        do
        {
            grew = Math.Max(0, server.ThreadCount - before);

            if (grew == 0)
            {
                return 0;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return grew;
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
