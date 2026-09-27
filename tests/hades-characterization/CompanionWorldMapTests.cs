using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// "월드맵 이동 같이 화면에서 캐릭터가 사라지면 봇을 보내지도 부르지도 못한다"(사용자, 2026-09-27 클라우드 11:02 — 서버 기록
/// <c>봇 동료사제: 짝을 풂 (주인 없음, 봇 접속)</c>, 주인 Monk5 는 접속해 있었다). 가장자리를 밟아 월드맵이 열리면 서버는 주인을
/// 심연(<c>Aisling.Abyss</c>)에 넣어 남에게서 감추고, 세상 목록 조회는 심연에 든 사람을 돌려주지 않는다 — 봇 짝은 주인이
/// 나간 줄 알고 풀렸고, 월드맵을 닫기만(취소) 하면 심연이 풀리지 않아 다시 불러도 곧 다시 풀렸다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionWorldMapTests : IDisposable
{
    private const string OwnerName = "mapowner";
    private const int WoodlandGate = 20028;
    private const int SuomiTown = 20355;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task The_bot_stays_with_an_owner_who_opens_the_world_map_and_answers_calls_after(bool byEdge, bool travel)
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 21));
        CompanionCallTests.Configure(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);

        WorldClient owner = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);

        await CallUntil(owner, () => bot.Master?.Serial == owner.Serial);

        // 아래 가장자리(9~11,23)를 밟거나 [월드맵] 단추(0xF0)로 연다 — 열린 채 잠시 둔다(서버 짝 점검은 0.5초마다).
        // 봇은 주인 남쪽 칸(10,22)에 서므로 한 칸 서쪽으로 비켜 내려간다. 봇 프로그램은 돌리지 않는다(짝만 본다).
        if (byEdge)
        {
            await owner.WalkAsync(Direction.West, _deadline.Token);
            await Task.Delay(600, _deadline.Token);
            await Waiting.WalkUntil(owner, Direction.South, () => owner.Field is not null, _deadline.Token);
        }
        else
        {
            await owner.OpenFieldAsync(_deadline.Token);
            await Waiting.Until(() => owner.Field is not null, "월드맵이 오지 않았습니다.", _deadline.Token);
        }

        Assert.NotNull(owner.Field);
        await Task.Delay(2500, _deadline.Token);
        Assert.True(bot.Master?.Serial == owner.Serial, $"월드맵을 연 사이 짝이 풀렸습니다: {owner.Said}");

        int landed = WoodlandGate;
        if (travel)
        {
            await owner.ChooseFieldAsync(SuomiTown, _deadline.Token);
            landed = SuomiTown;
        }
        else
        {
            await owner.ChooseFieldAsync(0, _deadline.Token);
        }

        await Waiting.Until(() => owner.Field is null && owner.State?.Map.Id == landed, $"월드맵에서 {landed} 로 돌아오지 않았습니다: {owner.State}", _deadline.Token);
        await Task.Delay(2500, _deadline.Token);
        Assert.True(bot.Master?.Serial == owner.Serial, $"월드맵을 닫은 뒤 짝이 풀렸습니다: {owner.Said}");
        await Waiting.Until(() => bot.State?.Map.Id == landed, $"봇이 주인 맵({landed})으로 오지 않았습니다: {bot.State}", _deadline.Token, TimeSpan.FromSeconds(10));

        // 보내기·부르기가 먹는다.
        await owner.DismissCompanionAsync(_deadline.Token);
        await Waiting.Until(() => bot.Master is null && owner.Said.Contains("보냈습니다", StringComparison.Ordinal),
            $"봇 보내기가 먹지 않았습니다: {owner.Said}", _deadline.Token, TimeSpan.FromSeconds(10));

        await CallUntil(owner, () => bot.Master?.Serial == owner.Serial && bot.State?.Map.Id == landed);
        await Task.Delay(2500, _deadline.Token);
        Assert.True(bot.Master?.Serial == owner.Serial, $"다시 부른 봇이 곧 떠났습니다: {owner.Said}");
    }

    private async Task CallUntil(WorldClient owner, Func<bool> done)
    {
        for (int tries = 0; tries < 10 && !done(); tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.True(done(), $"봇 부르기가 먹지 않았습니다: {owner.Said}");
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == WoodlandGate && world.Serial != 0, $"{who} 가 서지 못했습니다.", _deadline.Token);
        return world;
    }
}
