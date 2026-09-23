using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 남에게 걸린 것을 둘레 사람에게 알린다(0x5C, 우리 확장). 원작은 당사자에게만 0x3A 로 알린다 —
/// 5.99 클라이언트의 <c>SSpelled</c> 는 제 상태 막대(<c>SpelledViewPane</c>)에만 그린다.
/// </summary>
/// <remarks>
/// 둘이 같은 칸에서 시작한다. 첫째가 dion(버프, 아이콘 53)을 걸면 둘째가 첫째 번호로 받고, 괴물에게 프라보를
/// 걸면 둘 다 그 괴물 번호로 저주(아이콘 82) · 해로움 · 프라보가 그 괴물에 그린 그림 257 을 받는다 —
/// 괴물을 물들일 색이 그 그림에서 온다.
/// </remarks>
public sealed class StatusBroadcastTests : IDisposable
{
    private const string First = "stfirst";
    private const string Second = "stsecond";
    private const int WoodlandOneOne = 20015;
    private const int Dion = 53;
    private const int Curse = 82;
    private const int PraboOnTarget = 257;
    private static readonly Tile Start = new(2, 35);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Bystanders_are_told_what_is_on_a_player_and_on_a_monster()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMasters(server);
        server.Start(TimeSpan.FromMinutes(2));

        WorldClient first = await Enter(server, First);
        WorldClient second = await Enter(server, Second);

        await Until(() => second.Others.Any(one => one.Serial == first.Serial), "둘째가 첫째를 보지 못했습니다.");

        // dion 은 25% 로 실패한다(dion.cs) — 걸릴 때까지 다시 외운다.
        await first.SayAsync("/spell \"dion\" 1", _deadline.Token);
        int dion = await Slot(() => first.Spells.FirstOrDefault(s => s.Name.StartsWith("dion"))?.Slot, "dion");

        for (int tries = 0; tries < 12 && !second.AilmentsOf(first.Serial).Any(one => one.Icon == Dion); tries++)
        {
            await first.UseSpellAsync(dion, first.Serial, _deadline.Token);
            await Task.Delay(1500, _deadline.Token);
        }

        SeenAilment stone = second.AilmentsOf(first.Serial).Single(one => one.Icon == Dion);
        Assert.False(stone.Harmful);
        Assert.InRange(stone.Left, 1, 6);

        // 자기 것은 0x3A 로만 온다 — 0x5C 는 당사자에게 가지 않는다.
        Assert.Empty(first.AilmentsOf(first.Serial));
        Assert.Contains(first.Ailments, one => one.Icon == Dion);

        Creature mob = await AnyMonster(first);

        await first.SayAsync("/spell \"프라보\" 1", _deadline.Token);
        int prabo = await Slot(() => first.Spells.FirstOrDefault(s => s.Name.StartsWith("프라보"))?.Slot, "프라보");
        await first.UseSpellAsync(prabo, mob.Serial, _deadline.Token);

        foreach (WorldClient looking in new[] { first, second })
        {
            await Until(() => looking.AilmentsOf(mob.Serial).Any(one => one.Icon == Curse && one.Effect == PraboOnTarget),
                $"괴물 {mob.Serial} 에 걸린 저주(82 · 그림 257)가 오지 않았습니다: " +
                string.Join(", ", looking.AilmentsOf(mob.Serial)));
            Assert.True(looking.AilmentsOf(mob.Serial).Single(one => one.Icon == Curse).Harmful);
        }
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string name)
    {
        LoginFlow.TryCreateAccount(server, name);
        string path = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        saved["_MaximumHp"] = 5000;
        saved["CurrentHp"] = 5000;
        saved["_MaximumMp"] = 5000;
        saved["CurrentMp"] = 5000;
        File.WriteAllText(path, saved.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is { Map.Id: WoodlandOneOne }, $"{name} 이 우드랜드1-1 에 서지 못했습니다.");
        return world;
    }

    private static void MakeGameMasters(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(First, Second);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<Creature> AnyMonster(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Hostile) is { } mob)
            {
                return mob;
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        throw new TimeoutException("2분을 기다렸는데 괴물이 한 마리도 보이지 않았습니다.");
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
