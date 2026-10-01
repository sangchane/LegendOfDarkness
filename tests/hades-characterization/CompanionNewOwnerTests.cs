using System.Net;
using Lod.CompanionBot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
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

    /// <summary>
    /// 클라우드 2026-09-27 03:47~04:18(무도가 Monk5, 우드랜드입구): 월드맵으로 옮겨 다닌 주인이 로그아웃 없이 끊겼다 다시 들어오자 서버가
    /// 그 주인을 **1초에 150번** 저장했고(<c>Aisling Monk5 data has been saved.</c>), 봇은 따라오지 않았으며 앱의 봇 칸은 빈 검은 네모였다.
    /// 월드맵으로 옮기면 전 맵 목록에서 빠지지 않은 채 새 맵에도 들어간다 — 끊긴 뒤 전 맵에 남은 옛 캐릭터가 "같은 이름의 다른 접속"으로
    /// 남아, 새 접속이 한 걸음마다 그것을 치우며 저장하고(<c>GameClient.ObjectCheckPoint</c>), 봇 짝(<c>Companions.FindOnline</c>)은 그 옛
    /// 캐릭터를 주인으로 집어 봇을 그 곁으로 옮기고 체력을 그 끊긴 접속에 보냈다. 다시 들어온 주인에게 봇이 새 serial 로 돌아와야 한다.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_returning_owner_who_travelled_by_world_map_gets_the_bot_back_and_is_not_saved_over_and_over(bool socketClosed)
    {
        const int Miles = 20287;

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceVillage, 26, 21));
        CompanionCallTests.Configure(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);

        WorldClient first = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        _ = new CompanionRunner(bot, new MapWalls(HadesWorkspace.MapLayoutFolder), new CompanionSettings()).RunAsync(_deadline.Token);

        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await first.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.NotNull(bot.Master);

        await first.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => first.Field is not null, "월드맵이 오지 않았습니다.", _deadline.Token);
        await first.ChooseFieldAsync(Miles, _deadline.Token);
        await Waiting.Until(() => first.State?.Map.Id == Miles, "밀레스로 가지 않았습니다.", _deadline.Token);
        await Task.Delay(2000, _deadline.Token);

        if (socketClosed)
        {
            first.Dispose();
            await Task.Delay(3000, _deadline.Token);
        }

        WorldClient owner = await Enter(server, OwnerName, Miles);
        await Waiting.Until(() => first.IsDisposed || first.Broke is not null, "남은 전 접속이 빠지지 않았습니다.", _deadline.Token);

        int before = Saves(server);
        await Task.Delay(3000, _deadline.Token);
        int during = Saves(server) - before;

        Assert.True(during <= 3, $"다시 들어온 주인을 3초에 {during}번 저장했습니다.");

        // 주인이 없던 틈에 서버가 짝을 풀었으면(문서의 "나가면 돌아감") 봇은 마을로 가 있다 — 그때는 [봇 부르기] 를 다시 누른다.
        // 짝이 남아 있었으면 서버가 새 serial 을 스스로 다시 알린다(Companions.Tick).
        await Task.Delay(1500, _deadline.Token);

        for (int tries = 0; tries < 10 && bot.Master?.Serial != owner.Serial; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        await Waiting.Until(() => bot.Master?.Serial == owner.Serial,
            $"봇이 다시 들어온 주인을 모릅니다: 봇의 주인 {bot.Master?.Serial} · 새 주인 {owner.Serial}", _deadline.Token, TimeSpan.FromSeconds(5));
        await Waiting.Until(() => owner.Companion?.Serial == bot.Serial && owner.CompanionLife?.Serial == bot.Serial,
            $"앱의 봇 칸이 비어 있습니다: 짝 {owner.Companion} · 체력 {owner.CompanionLife}", _deadline.Token, TimeSpan.FromSeconds(5));
        await Waiting.Until(() => bot.State?.Map.Id == Miles && Where(owner, bot.Serial) is not null,
            $"봇이 주인 곁에 오지 않았습니다: 봇 맵 {bot.State?.Map.Id} · 주인이 보는 봇 {Where(owner, bot.Serial)}", _deadline.Token, TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// "봇 소환했다가 앱 종료하고 다시 접속해도 봇이 마지막 자리에 좀비처럼 있다"(사용자, 2026-09-27 클라우드 05:23). 짝은 서버 기억에만
    /// 있어, 짝을 맺은 채 서버가 다시 뜨면 짝은 사라지고 봇은 저장된 마지막 자리(주인 곁, 우드랜드입구)로 들어와 주인 없이 서 있었다.
    /// 짝이 없는 봇은 대기 장소로 돌아가야 한다.
    /// </summary>
    [Fact]
    public async Task A_bot_without_an_owner_goes_home_instead_of_standing_where_it_was_saved()
    {
        const int Mileth = 20287;

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceVillage, 26, 21));
        CompanionCallTests.Configure(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);

        // 서버가 다시 뜬 뒤처럼 — 봇이 대기 장소가 아닌 곳(주인 곁이던 곳)에 저장된 채 들어온다. 짝을 맺는 사람은 없다.
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);

        await Waiting.Until(() => bot.State?.Map.Id == Mileth,
            $"주인 없는 봇이 제자리에 남았습니다: 맵 {bot.State?.Map.Id} {bot.State?.Where}", _deadline.Token, TimeSpan.FromSeconds(30));
        Assert.Null(bot.Master);
    }

    private static int Saves(IsolatedHadesServer server) =>
        server.ConsoleOutput.Split('\n').Count(line => line.Contains($"Aisling {OwnerName} data has been saved", StringComparison.Ordinal));

    private static Tile? Where(WorldClient viewer, uint serial) =>
        viewer.Others.FirstOrDefault(one => one.Serial == serial)?.Where;

    private static string Joined(List<string> lines)
    {
        lock (lines)
        {
            return string.Join(" | ", lines);
        }
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who, int map = NoviceVillage)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == map && world.Serial != 0, $"{who} 가 서지 못했습니다.", _deadline.Token);
        return world;
    }
}
