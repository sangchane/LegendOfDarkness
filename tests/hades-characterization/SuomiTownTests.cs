using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 수오미마을 건물 문 — 5.99 `warp/Suomi_Warp.txt`(5.99 워프 목록이 싣지 않던 파일)의 마을 쪽 43줄. 여관 문은 마을 62,41·62,42 이고
/// 들어가면 여관 16,7 이다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class SuomiTownTests : IDisposable
{
    private const int SuomiTown = 20355;
    private const int SuomiInn = 20358;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Walking_into_the_inn_door_puts_you_inside_the_inn()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (SuomiTown, 64, 41));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, "suomiguest");

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, "suomiguest", LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == SuomiTown, "수오미마을에 들어가지 못했습니다.", _deadline.Token);

        // 64 → 63 → 62(여관 문).
        await Waiting.WalkUntil(world, Direction.West, () => world.State?.Map.Id == SuomiInn, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiInn && state.Where == new Tile(16, 7),
            $"여관 문을 밟았는데 수오미여관 16,7 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);
    }
}
