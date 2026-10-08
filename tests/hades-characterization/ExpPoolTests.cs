using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 99레벨 보유경험치(2026-10-05) — 레벨 1부터 쌓인 총 경험치를 세오·칸에게 팔아 체력·마력을 산다. 0x08 첫 칸(총 경험치)으로 앱 EXP 막대에
/// 「보유」로 보인다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class ExpPoolTests : IDisposable
{
    private const string Name = "exppool";
    private static readonly (int Map, int X, int Y) Town = (20373, 37, 29);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_level_99_character_banks_experience_and_the_app_is_told()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Waiting.MakeGameMaster(server, Name);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 99;
        character["ExpBank"] = 5_000_000_000; // 32비트(약 42억)를 넘는다
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.Vitals is { Level: 99 } v && ((v.ExperienceToGo << 32) | v.Experience) == 5_000_000_000,
            $"쌓인 경험치 50억이 오지 않았습니다: {world.Vitals}", _deadline.Token);
    }

    /// <summary>
    /// 99 가 세오에게 입은 채로 체력을 산다 — 값은 장비·버프를 뺀 본체력 × 500, 한 번에 본체력 +50(사용자 2026-10-08 「옷을 입건 버프
    /// 디버프가 걸려있건 본체력을 알 수 있으니까 거기에 맞춰서」, autopilot/eco-bots/vitality-SPEC.md). 5.99 는 다 벗게 했고(get_ac),
    /// 하데스엔 get_ac 가 없어 늘 「무장해제하시고」만 했다.
    /// </summary>
    [Fact]
    public async Task A_dressed_level_99_buys_health_from_seo_at_the_price_of_the_bare_body()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20299, 6, 6));
        Waiting.MakeGameMaster(server, Name);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        const long Spare = 7;
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 99;
        character["_MaximumHp"] = 1000;
        character["ExpBank"] = 1000 * 500 + 1050 * 500 + Spare;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.Vitals is { Level: 99, MaximumHealth: 1000 }, $"99 · 최대 체력 1000 으로 들어오지 않았습니다: {world.Vitals}", _deadline.Token);

        // 체력 +200 반지를 낀다 — 입은 최대 1200 으로 값을 매기면(1200·1250 × 500) 쌓인 것으로 한 번밖에 못 산다.
        await world.SayAsync("/give \"세피라링(Lev2)\" 1", _deadline.Token);
        await Waiting.Until(() => world.Pack.Any(item => item.Name == "세피라링(Lev2)"), $"세피라링(Lev2)이 오지 않았습니다: {world.Said}", _deadline.Token);
        await world.UseAsync(world.Pack.First(item => item.Name == "세피라링(Lev2)").Slot, _deadline.Token);
        await Waiting.Until(() => world.Vitals is { MaximumHealth: 1200 }, $"반지를 끼고 최대 체력 1200 이 아닙니다: {world.Vitals}", _deadline.Token);

        // 세오는 이름표(세오@세오신전#3,4)로 온다 — 봇처럼 자리로 찾는다.
        Tile seoAt = new(3, 4);
        await world.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => world.Creatures.Any(one => one.Kind == CreatureKind.Merchant && one.Where == seoAt), "세오가 보이지 않습니다.", _deadline.Token);
        Creature seo = world.Creatures.First(one => one.Kind == CreatureKind.Merchant && one.Where == seoAt);

        await Buy(world, seo, "2");
        await Waiting.Until(() => world.Vitals is { MaximumHealth: 1300 } v && v.Banked == Spare,
            $"입은 채로 체력 +100 · 경험치 {Spare} 가 아닙니다: {world.Vitals} · 세오가 한 말: {world.Talking?.What}", _deadline.Token);
    }

    /// <summary>세오를 눌러 「체력을 산다.」 → 횟수.</summary>
    private async Task Buy(WorldClient world, Creature seo, string times)
    {
        int seen = world.TalkCount;
        await world.ClickAsync(seo.Serial, _deadline.Token);
        await Waiting.Until(() => world.TalkCount > seen && world.Talking?.Options.Any(option => option.Text == "체력을 산다.") == true,
            $"세오의 메뉴가 오지 않았습니다: {world.Talking?.What} · {world.Said}", _deadline.Token);

        seen = world.TalkCount;
        await world.AnswerAsync(seo.Serial, world.Talking!.Options.First(option => option.Text == "체력을 산다.").Step, _deadline.Token);
        await Waiting.Until(() => world.TalkCount > seen && world.Talking?.Kind == DialogueKind.TextInput,
            $"몇 번 살지 묻지 않았습니다: {world.Talking?.What} · {world.Said}", _deadline.Token);

        await world.AnswerAsync(seo.Serial, world.Talking!.Step, times, _deadline.Token);
    }
}
