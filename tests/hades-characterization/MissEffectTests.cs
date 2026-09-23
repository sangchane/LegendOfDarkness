using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 헛친 기술은 휘두른 칸에 「Miss」 그림(원작 efct115, 파랑)을 띄우고 — 마법이 빗나가면 원작처럼 efct033(빨강) —,
/// 혼자 쓴 beag ioc fein 은 자기 몸에 그림 4 를 띄우며 체력을 채운다.
/// </summary>
public sealed class MissEffectTests : IDisposable
{
    private const string Name = "missfx";
    private const int WoodlandOneOne = 20015;
    private const int SkillMiss = 115;
    private static readonly Tile Start = new(2, 35);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_skill_that_hits_nobody_shows_miss_on_the_tile_it_swung_at_and_a_lone_heal_shows_on_the_caster()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["_MaximumHp"] = 1000;
            saved["CurrentHp"] = 500;
            saved["_MaximumMp"] = 1000;
            saved["CurrentMp"] = 1000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");

        List<Effect> flashes = [];

        void Drain()
        {
            while (world.TakeEffect(out Effect? flash))
            {
                flashes.Add(flash);
            }
        }

        // 평타(Assail 스크립트) · 무도가 기술(MonkStrike) — 둘 다 앞칸이 비어 있다.
        foreach (string skill in new[] { "양의신권", "단각" })
        {
            await world.SayAsync($"/skill \"{skill}\" 1", _deadline.Token);
            int slot = await Slot(() => world.Skills.FirstOrDefault(s => s.Name.StartsWith(skill))?.Slot, skill);
            Drain();
            flashes.Clear();
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(() =>
            {
                Drain();
                return flashes.Any(f => f.At is { } at && f.TargetAnimation == SkillMiss
                                        && Math.Abs(at.X - Start.X) + Math.Abs(at.Y - Start.Y) == 1);
            }, $"{skill}: 헛친 칸에 Miss(115)가 오지 않았습니다: {string.Join(", ", flashes)}");
        }

        // 혼자 쓴 beag ioc fein — 무리가 없어도 나를 채우고 내 위에 4 를 그린다.
        {
            await world.SayAsync("/spell \"beag ioc fein\" 1", _deadline.Token);
            int slot = await Slot(() => world.Spells.FirstOrDefault(s => s.Name.StartsWith("beag ioc fein"))?.Slot,
                "beag ioc fein");
            await Until(() => world.Vitals is not null, "체력을 받지 못했습니다.");
            int health = world.Vitals!.Health;
            Drain();
            flashes.Clear();
            await world.UseSpellAsync(slot, 0, _deadline.Token);
            await Until(() =>
            {
                Drain();
                return flashes.Any(f => f.Target == world.Serial && f.TargetAnimation == 4);
            }, $"beag ioc fein 그림(4)이 내 위로 오지 않았습니다: {string.Join(", ", flashes)}");
            await Until(() => world.Vitals!.Health > health, $"beag ioc fein 이 체력 {health} 을 채우지 않았습니다.");
        }
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void MakeGameMaster(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<int> Slot(Func<int?> find, string name)
    {
        int? slot = null;
        await Until(() => (slot = find()) is not null, $"{name}이 창에 오지 않았습니다.");
        return slot!.Value;
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure);
    }
}
