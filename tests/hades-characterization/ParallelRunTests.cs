using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Two isolated servers at once. The object server's address was written into the code, so a second server
/// found the port taken and came up quietly without its login listener — which is why the suite had to run
/// one test at a time.
/// </summary>
public sealed class ParallelRunTests
{
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    [Fact]
    public async Task Two_servers_run_side_by_side_and_each_serves_its_own_world()
    {
        using IsolatedHadesServer first = IsolatedHadesServer.Prepare();
        using IsolatedHadesServer second = IsolatedHadesServer.Prepare();

        Assert.NotEqual(first.ObjectPort, second.ObjectPort);
        Assert.NotEqual(first.LoginPort, second.LoginPort);

        first.Start(TimeSpan.FromMinutes(2));
        second.Start(TimeSpan.FromMinutes(2));

        // Both listeners online is the part that used to fail: the second server's login server never
        // started because the object server could not bind.
        await Enters(first, "lodfirst");
        await Enters(second, "lodsecond");
    }

    private async Task Enters(IsolatedHadesServer server, string name)
    {
        LoginFlow.TryCreateAccount(server, name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            name,
            LoginFlow.SyntheticSecret,
            progress: null,
            _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (world.State is null && DateTime.UtcNow < giveUp)
        {
            await Task.Delay(50, _deadline.Token);
        }

        Assert.NotNull(world.State);
        Assert.Equal(server.GamePort, session.Character.Port);
    }
}
