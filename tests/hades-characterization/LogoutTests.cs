using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 나간 사람은 곧 세계에서 빠진다 — 곁의 사람 화면에서 사라지고(0x0E), 괴물이 칠 과녁으로 남지 않는다.
/// 원작 클라이언트는 나갈 때 0x0B 로 먼저 알리고 서버가 바로 빼낸다. 소켓만 끊겨도(앱을 닫음) 서버는 곧 안다(0 바이트 읽기).
/// 아무 말도 없이 조용해진 접속(iOS 가 앱을 멈춘 채 소켓을 쥐고 있는 것)은 심장박동(0x3B)에 답이 없으니 시간 한도로 뺀다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class LogoutTests : IDisposable
{
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Logging_out_from_the_mobile_client_takes_the_character_out_of_the_world_at_once()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        (WorldClient watcher, WorldSession watcherSession) = await Enter(server, "outwatch");
        using WorldSession _ = watcherSession;
        (WorldClient leaver, WorldSession leaverSession) = await Enter(server, "outleave");
        uint leaving = leaver.Serial;
        await Waiting.Until(() => watcher.Others.Any(one => one.Serial == leaving), "곁의 사람이 나갈 사람을 보지 못했습니다.", _deadline.Token);

        // 모바일 [로그아웃] 이 하는 그대로.
        await leaver.LogOutAsync(_deadline.Token);
        Assert.True(leaverSession.IsDisposed);

        await Waiting.Until(() => watcher.Others.All(one => one.Serial != leaving),
            "로그아웃한 캐릭터가 3초 안에 곁의 사람 화면에서 빠지지 않았습니다.", _deadline.Token, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task A_connection_that_goes_silent_is_taken_out_of_the_world_but_a_quiet_player_stays()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        (WorldClient watcher, WorldSession watcherSession) = await Enter(server, "quietwatch");
        using WorldSession _ = watcherSession;

        using CancellationTokenSource frozen = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
        (WorldClient sleeper, WorldSession sleeperSession) = await Enter(server, "quietsleep", frozen.Token);
        using WorldSession __ = sleeperSession;
        uint sleeping = sleeper.Serial;
        await Waiting.Until(() => watcher.Others.Any(one => one.Serial == sleeping), "곁의 사람이 잠들 사람을 보지 못했습니다.", _deadline.Token);

        // iOS 가 앱을 멈춘 것처럼 — 소켓은 열린 채 읽지도 보내지도 않는다(심장박동에도 답하지 않는다).
        frozen.Cancel();
        DateTime froze = DateTime.UtcNow;

        await Waiting.Until(() => watcher.Others.All(one => one.Serial != sleeping),
            $"조용해진 접속이 {IdleLimit.TotalSeconds}초 한도 안에 세계에서 빠지지 않았습니다.", _deadline.Token, IdleLimit + TimeSpan.FromSeconds(15));
        TimeSpan took = DateTime.UtcNow - froze;

        // 가만히 서 있기만 한 사람(심장박동에는 답한다)은 같은 시간이 지나도 남는다.
        Assert.Null(watcher.Broke);
        Assert.False(watcherSession.IsDisposed);
        await watcher.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => watcher.State is not null, "가만히 선 사람이 세계에서 떨어졌습니다.", _deadline.Token);
        Assert.True(took >= TimeSpan.FromSeconds(10), $"조용해진 지 {took.TotalSeconds:0.0}초 만에 뺐습니다 — 한 번 늦은 심장박동으로 빼면 안 됩니다.");
    }

    /// <summary>서버가 심장박동에 답이 없는 접속을 빼는 한도(GameClient.IdleLimit).</summary>
    private static readonly TimeSpan IdleLimit = TimeSpan.FromSeconds(30);

    private async Task<(WorldClient World, WorldSession Session)> Enter(IsolatedHadesServer server, string who, CancellationToken? pump = null)
    {
        LoginFlow.TryCreateAccount(server, who);
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(pump ?? _deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Serial != 0, $"{who} 가 세계에 서지 않았습니다.", _deadline.Token);

        return (world, session);
    }
}
