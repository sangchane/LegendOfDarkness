using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 동료 봇 부르기·보내기(우리 확장 0xF1 → 0x5E). 부르면: 봇이 부른 사람 레벨 −2 의 성직자가 되고(체력·마력은 원작 레벨업
/// 식, 마법은 5.99 사범 레벨까지), 부른 사람 옆 칸으로 오고, 부른 사람이 그룹장인 파티에 든다. 보내면: 파티에서 빠지고
/// 대기 장소(마을)로 간다. 부른 사람이 나가면 봇도 저절로 돌아간다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionCallTests : IDisposable
{
    private const string OwnerName = "compowner";
    internal const string BotName = "companbot";

    private const int NoviceVillage = 20373;
    private const int NovicePlain = 20393;
    private const int Mileth = 20287;
    private static readonly Tile Home = new(53, 46);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Called_the_bot_is_readied_brought_and_grouped_and_sent_home_on_dismiss_or_logout()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        Configure(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, BotName);
        Edit(server, OwnerName, saved =>
        {
            saved["ExpLevel"] = 23;
            saved["CurrentMapId"] = NovicePlain;
            saved["XPos"] = 25;
            saved["YPos"] = 25;
        });

        (WorldSession ownerSession, WorldClient owner, List<string> ownerHeard) = await Enter(server, OwnerName);
        using WorldSession ownerHold = ownerSession;
        await Waiting.Until(() => owner.State?.Map.Id == NovicePlain && owner.Serial != 0, "사람이 노비스평원A 에 서지 못했습니다.", _deadline.Token);

        // 봇이 아직 없다 — 한국어로 알린다.
        await CallUntilHeard(owner, ownerHeard, "지금 부를 수 있는 동료가 없습니다.");

        (WorldSession botSession, WorldClient bot, List<string> botHeard) = await Enter(server, BotName);
        using WorldSession botHold = botSession;
        await Waiting.Until(() => bot.State?.Map.Id == NoviceVillage && bot.Serial != 0, "봇이 노비스마을에 서지 못했습니다.", _deadline.Token);

        // 부른다.
        await owner.CallCompanionAsync(_deadline.Token);
        await Waiting.Until(() => bot.Master is not null && owner.Companion is not null,
            $"0x5E 가 오지 않았습니다. 사람이 들은 것: {Joined(ownerHeard)}", _deadline.Token);

        Assert.Equal(new CompanionTie(owner.Serial, OwnerName), bot.Master);
        Assert.Equal(new CompanionTie(bot.Serial, BotName), owner.Companion);

        // 레벨 23 − 2 = 21. 체력 150 + (콘5+30)×20 = 850 · 마력 200 + Σ(위즈+25), 위즈는 5 에서 레벨마다 +2 → 1180 · 위즈 45.
        await Waiting.Until(() => bot.Vitals is { Level: 21, MaximumHealth: 850, MaximumMana: 1180, Wis: 45 },
            $"봇이 21레벨 성직자로 준비되지 않았습니다: {bot.Vitals}", _deadline.Token);
        Assert.Equal(bot.Vitals!.MaximumHealth, bot.Vitals.Health);
        Assert.Equal(bot.Vitals.MaximumMana, bot.Vitals.Mana);

        string[] priest21 = ["쿠로", "신성력강화", "쿠러스", "호르라마", "에나르마", "쿠라노"];
        await Waiting.Until(() => bot.Spells.Select(s => CompanionSpells.Bare(s.Name)).Order().SequenceEqual(priest21.Order()),
            $"봇 마법이 5.99 사범 21레벨까지가 아닙니다: {string.Join(",", bot.Spells.Select(s => s.Name))}", _deadline.Token);

        // 사람 옆 칸.
        await Waiting.Until(() => bot.State is { Map.Id: NovicePlain } && owner.State is { } o &&
                                  Math.Abs(bot.State.Where.X - o.Where.X) + Math.Abs(bot.State.Where.Y - o.Where.Y) == 1,
            $"봇이 사람 옆 칸에 오지 않았습니다: 봇 {bot.State?.Map.Id} {bot.State?.Where} · 사람 {owner.State?.Where}", _deadline.Token);

        // 파티 — 부른 사람이 그룹장.
        await Roster(owner);
        Assert.Equal([new PartyMember(BotName, false), new PartyMember(OwnerName, true)], owner.Roster.Members.OrderBy(m => m.Name));

        // 보낸다 — 파티에서 빠지고 마을로.
        await owner.DismissCompanionAsync(_deadline.Token);
        await Waiting.Until(() => bot.State?.Map.Id == Mileth && bot.Master is null && owner.Companion is null,
            $"보낸 봇이 밀레스마을로 가지 않았습니다: {bot.State?.Map.Id} {bot.State?.Where}", _deadline.Token);
        Assert.Equal(Home, bot.State!.Where);
        await Roster(owner);
        Assert.False(owner.Roster.Grouped);

        // 다시 불렀다가 사람이 나가면 봇도 돌아간다.
        await owner.CallCompanionAsync(_deadline.Token);
        await Waiting.Until(() => bot.Master is not null && bot.State?.Map.Id == NovicePlain, "다시 부른 봇이 오지 않았습니다.", _deadline.Token);

        await owner.LogOutAsync(_deadline.Token);
        await Waiting.Until(() => bot.State?.Map.Id == Mileth && bot.Master is null,
            $"사람이 나갔는데 봇이 돌아가지 않았습니다: {bot.State?.Map.Id}", _deadline.Token);
    }

    /// <summary>봇 계정 이름과 대기 장소(밀레스마을)를 격리 서버 설정에 적는다.</summary>
    internal static void Configure(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["CompanionBots"] = new JsonArray(BotName);
        config["ServerConfig"]!["CompanionHomeMap"] = Mileth;
        config["ServerConfig"]!["CompanionHomePosition"] = new JsonObject { ["X"] = Home.X, ["Y"] = Home.Y };
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static void Edit(IsolatedHadesServer server, string name, Action<JsonNode> change)
    {
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        change(character);
        File.WriteAllText(saved, character.ToJsonString());
    }

    /// <summary>들어온 직후 서버가 화면을 새로 보내는 동안은 버릴 수 있어, 들릴 때까지 다시 부른다.</summary>
    private async Task CallUntilHeard(WorldClient world, List<string> heard, string expected)
    {
        for (int tries = 0; tries < 10; tries++)
        {
            await world.CallCompanionAsync(_deadline.Token);

            try
            {
                await Waiting.Until(() => Contains(heard, expected), expected, _deadline.Token, TimeSpan.FromSeconds(1));
                return;
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.Fail($"'{expected}' 를 듣지 못했습니다: {Joined(heard)}");
    }

    private static string Joined(List<string> heard)
    {
        lock (heard)
        {
            return string.Join(" | ", heard);
        }
    }

    private static bool Contains(List<string> heard, string line)
    {
        lock (heard)
        {
            return heard.Contains(line);
        }
    }

    private async Task Roster(WorldClient world)
    {
        int before = world.RosterCount;
        await world.AskProfileAsync(_deadline.Token);
        await Waiting.Until(() => world.RosterCount > before, "프로필(0x39)이 오지 않았습니다.", _deadline.Token);
    }

    private async Task<(WorldSession Session, WorldClient World, List<string> Heard)> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        List<string> heard = [];
        _ = Task.Run(async () =>
        {
            while (!_deadline.IsCancellationRequested && !world.IsDisposed)
            {
                while (world.TakeTold(out _, out string text))
                {
                    lock (heard)
                    {
                        heard.Add(text);
                    }
                }

                await Task.Delay(50);
            }
        });

        return (session, world, heard);
    }
}
