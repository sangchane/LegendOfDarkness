using Darkages.Network.Game;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The gap the server keeps between one attack and the next. It read the other way round, so the limit never
/// applied and a client could attack as fast as it could send packets.
/// </summary>
public sealed class AssailTimingTests
{
    private const double Delay = 500;

    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(499, false)]
    [InlineData(500, true)]
    [InlineData(501, true)]
    [InlineData(5000, true)]
    public void The_boundary_falls_exactly_on_the_configured_delay(double sinceMilliseconds, bool ready)
    {
        DateTime last = Now.AddMilliseconds(-sinceMilliseconds);

        Assert.Equal(ready, GameServer.AssailIsReady(last, Now, Delay));
    }

    [Fact]
    public void An_attack_stamped_in_the_future_waits_rather_than_letting_everything_through()
    {
        // A clock that moved backwards, or a stamp written ahead. Either way this must not read as ready.
        Assert.False(GameServer.AssailIsReady(Now.AddSeconds(10), Now, Delay));
    }

    [Fact]
    public void No_delay_configured_allows_every_attack()
    {
        Assert.True(GameServer.AssailIsReady(Now, Now, 0));
    }
}
