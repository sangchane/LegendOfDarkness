using System.Net;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 운영자 상인 — 어둠템에 없는 센 팩 장비(체·마 +1000 이상, 레벨 99 밑)만 판다(<c>scripts/gen/items/build-operator-shop.py</c>,
/// 사용자 2026-10-09 「5번장비만 취급하는 npc 따로 만들어둬 운영자 권한으로만 … 구매할 수 있게」). 운영자(<c>GameMasters</c>)가 아니면
/// 메뉴도 일괄 사기도 안 되고, 다른 상점에 이름만 대고 사는 옛 길(<c>shop1</c> 0x0004 — 물목을 보지 않았다)도 막혔다.
/// </summary>
public sealed class OperatorShopTests : IDisposable
{
    private const int SuomiArmorShop = 20357;
    private static readonly Tile Operator = new(2, 5);
    private static readonly Tile Adol = new(4, 5);
    private const string Robe = "전통한복(남)"; // 체·마 +15,000 · 어둠템에 없음 — 운영자 상인만

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Only_operators_buy_the_strong_pack_gear()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (SuomiArmorShop, 2, 6));
        Waiting.MakeGameMaster(server, "opgm");
        server.Start(TimeSpan.FromMinutes(2));
        foreach (string who in new[] { "opguest", "opgm" })
        {
            LoginFlow.TryCreateAccount(server, who);
            CompanionCallTests.Edit(server, who, saved => saved["GoldPoints"] = 100_000_000);
        }

        WorldClient guest = Pump(await Login(server, "opguest"));
        Creature shop = await Standing(guest, Operator, "운영자 상인");
        Creature adol = await Standing(guest, Adol, "아돌");

        int seen = guest.TalkCount;
        await guest.ClickAsync(shop.Serial, _deadline.Token);
        await Waiting.Until(() => guest.TalkCount > seen, "운영자 상인이 답하지 않았습니다.", _deadline.Token);
        Assert.Contains("운영자만", guest.Talking!.What);
        Assert.Empty(guest.Talking.Options);

        await guest.BulkTradeAsync(shop.Serial, selling: false, [(Robe, 0, 1)], _deadline.Token);
        await guest.AnswerAsync(shop.Serial, 0x0004, Robe, _deadline.Token);
        await guest.AnswerAsync(adol.Serial, 0x0004, Robe, _deadline.Token); // 아돌 물목에 없다 — 이름만 대고 사던 길
        await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);
        Assert.DoesNotContain(guest.Pack, item => item.Name == Robe);
        Assert.Equal(100_000_000, guest.Vitals!.Gold);

        WorldClient gm = Pump(await Login(server, "opgm"));
        Creature mine = await Standing(gm, Operator, "운영자 상인");
        await gm.BulkTradeAsync(mine.Serial, selling: false, [(Robe, 0, 1)], _deadline.Token);
        await Waiting.Until(() => gm.Pack.Any(item => item.Name == Robe), "운영자가 운영자 상인에게서 사지 못했습니다.", _deadline.Token);
    }

    private Task<WorldSession> Login(IsolatedHadesServer server, string who) =>
        HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

    private WorldClient Pump(WorldSession session)
    {
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return world;
    }

    private async Task<Creature> Standing(WorldClient world, Tile where, string who)
    {
        await Waiting.Until(() => world.Creatures.Any(one => one.Where == where), $"{who}이(가) 보이지 않습니다.", _deadline.Token);
        return world.Creatures.First(one => one.Where == where);
    }
}
