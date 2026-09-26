using System.Net;
using Lod.CompanionBot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// "새로 캐릭터를 만들어 보니 봇이 한 방향만 보고 걸어서 따라오지 않는다"(사용자, 2026-09-26). 막 만든 주인이 끊겼다 다시 들어와도
/// 봇이 새 접속을 따라와야 한다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionNewOwnerTests : IDisposable
{
    private const string OwnerName = "newowner";
    private const int NoviceVillage = 20373;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 폰이 잠들거나 앱이 닫히면 로그아웃 없이 소켓만 끊긴다. 다시 들어온 주인(새 serial)을 봇이 따라와야 한다 — 클라우드에서 봇은
    /// 끊긴 전 접속의 serial 을 주인으로 쥔 채 마을에 서 있었다(2026-09-26 21:47, 봇 메모리 덤프).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_bot_follows_its_owner_again_after_the_owner_drops_and_comes_back(bool socketClosed)
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceVillage, 26, 21));
        CompanionCallTests.Configure(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);

        WorldClient first = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        List<string> did = [];
        _ = new CompanionRunner(bot, new MapWalls(HadesWorkspace.MapLayoutFolder), new CompanionSettings(), line =>
        {
            lock (did)
            {
                did.Add(line);
            }
        }).RunAsync(_deadline.Token);

        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await first.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.NotNull(bot.Master);

        // 서버가 전 접속을 한 번 저장할 때까지(SaveRate 10초) — 저장된 serial 이 전 접속의 serial 과 같아진다(클라우드와 같은 조건).
        await Task.Delay(TimeSpan.FromSeconds(11), _deadline.Token);

        // 로그아웃 없이 끊긴다. 망이 끊긴 폰은 소켓을 닫지도 못한다 — 서버에는 전 접속이 살아 있는 채로 다시 들어온다.
        if (socketClosed)
        {
            first.Dispose();
            await Task.Delay(3000, _deadline.Token);
        }

        WorldClient owner = await Enter(server, OwnerName);

        // 같은 이름의 남은 접속은 서버가 뺀다 — 남으면 봇의 짝이 그쪽에 붙어 있다.
        await Waiting.Until(() => first.IsDisposed || first.Broke is not null, "남은 전 접속이 빠지지 않았습니다.", _deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        for (int tries = 0; tries < 10 && bot.Master?.Serial != owner.Serial; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.Equal(owner.Serial, bot.Master?.Serial);

        for (int step = 0; step < 6; step++)
        {
            await owner.WalkAsync(Direction.East, _deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }

        await Waiting.Until(() =>
                Where(bot, owner.Serial) is { } o && Where(owner, bot.Serial) is { } b &&
                Math.Abs(o.X - b.X) + Math.Abs(o.Y - b.Y) <= 3,
            $"봇이 따라오지 않았습니다: 봇이 보는 주인 {Where(bot, owner.Serial)} · 주인이 보는 봇 {Where(owner, bot.Serial)} · 봇 자신 {bot.State?.Where} · 한 일 {Joined(did)}",
            _deadline.Token, TimeSpan.FromSeconds(10));
    }

    private static Tile? Where(WorldClient viewer, uint serial) =>
        viewer.Others.FirstOrDefault(one => one.Serial == serial)?.Where;

    private static string Joined(List<string> lines)
    {
        lock (lines)
        {
            return string.Join(" | ", lines);
        }
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == NoviceVillage && world.Serial != 0, $"{who} 가 서지 못했습니다.", _deadline.Token);
        return world;
    }
}
