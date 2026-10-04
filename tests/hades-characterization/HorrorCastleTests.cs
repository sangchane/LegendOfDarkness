using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 호러캐슬 — 원작식 파티 방(사용자 2026-10-04, SPEC <c>plans/horror-castle-spec-2026-10-04.md</c>). 메인홀 안쪽 문(22~26,10)을 밟으면
/// 5.99 스크립트 `호러캐슬` 이 그룹을 확인하고 그룹 전용 사본(원본 호러캐슬1, 클라이언트에는 20714)을 지어 같은 맵의 그룹원을 함께
/// 옮긴 뒤 괴물 17마리를 세운다(`mob_spawn`). 다 잡고 위 문(9,0)을 밟으면 `호러캐슬다음방` 이 같은 방에 다시 17마리를 세운다.
/// 동료 봇도 하데스 그룹원(Party.AddPartyMember)이라 같은 길이다 — 여기서는 두 번째 사람으로 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class HorrorCastleTests : IDisposable
{
    private const int Hall = 20719;
    private const int Room = 20714;
    private const string Leader = "horrorlead";
    private const string Member = "horrormate";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(10));

    public void Dispose() => _deadline.Dispose();

    /// <summary>사본 이름 — 스크립트가 `"사냥터" + 그룹장 이름 + "호러캐슬"` 로 짓는다.</summary>
    private const string Copy = "사냥터" + Leader + "호러캐슬";

    /// <summary>시야는 맨해튼 거리 12 칸 미만이라 15×15 방을 한 자리에서 다 못 본다 — 네 곳에서 보면 덮인다.</summary>
    private static readonly Tile[] Lookouts = [new(4, 4), new(10, 4), new(10, 10), new(4, 10)];

    [Fact]
    public async Task A_group_stepping_into_the_hall_door_gets_its_own_room_of_seventeen_and_the_top_door_refills_it_once_cleared()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (Hall, 24, 11));
        Waiting.MakeGameMaster(server, Leader);

        // 괴물 체력만 1 로 — 마릿수와 흐름은 그대로 두고 잡는 시간만 줄인다.
        foreach (string path in Directory.GetFiles(Path.Combine(server.ContentLocation, "templates", "monsters", "호러캐슬"), "*.json"))
        {
            JsonNode mob = JsonNode.Parse(File.ReadAllText(path))!;
            mob["MaximumHP"] = 1;
            File.WriteAllText(path, mob.ToJsonString());
        }

        server.Start(TimeSpan.FromMinutes(2));
        WorldClient lead = await Enter(server, Leader);
        WorldClient mate = await Enter(server, Member);
        await Waiting.Until(() => lead.State?.Map.Id == Hall && mate.State?.Map.Id == Hall && mate.Self?.Name is { Length: > 0 },
            "둘이 메인홀에 서지 못했습니다.", _deadline.Token);

        await Group(lead, mate);

        // NPC 는 서버가 켜진 뒤 첫 돌림(MundaneRespawnInterval 10초)에 선다 — 같은 돌림에 서는 게시판알리미(27,13)로 안다.
        // 문을 돌리는 스크립트 주인(0,0)은 시야 밖이다.
        await Waiting.Until(() => lead.Creatures.Any(c => c.Where == new Tile(27, 13)), "메인홀 NPC 가 서지 않았습니다.", _deadline.Token);

        // 문 칸(24,10)으로 한 걸음 — 스크립트가 같은 맵의 그룹원까지 사본 9,2 둘레로 옮긴다.
        await Waiting.WalkUntil(lead, Direction.North, () => lead.State?.Map.Id == Room, _deadline.Token);
        await Waiting.Until(() => lead.State?.Map.Id == Room && mate.State?.Map.Id == Room,
            $"둘 다 방으로 가지 않았습니다. 그룹장 {lead.State} · 그룹원 {mate.State} · 서버가 한 말: {lead.Said} · 서버 기록: {PackLog(server)}",
            _deadline.Token);

        // 그룹 최대 체력이 5만을 넘어(시험 캐릭터 20만) 센 괴물(…2)이 선다 — 마릿수는 같다.
        HashSet<uint> first = await Survey(lead);
        Assert.True(first.Count == 17, $"방에 들어서 본 괴물이 17마리가 아닙니다: {first.Count} · 서버 기록: {PackLog(server)}");

        await KillEverything(lead);

        // 위 문(9,0) — 9,1 에서 북쪽으로. 같은 사본에 새로 17마리.
        await Teleport(lead, new Tile(9, 1));
        await Waiting.WalkUntil(lead, Direction.North, () => Hostiles(lead) > 0, _deadline.Token);
        HashSet<uint> second = await Survey(lead);
        Assert.True(second.Count == 17, $"위 문을 밟은 뒤 본 괴물이 17마리가 아닙니다: {second.Count} · 서버가 한 말: {lead.Said} · 서버 기록: {PackLog(server)}");
        Assert.Empty(second.Intersect(first));
        Assert.Equal(Room, lead.State?.Map.Id);
        Assert.Equal(Room, mate.State?.Map.Id);
        Assert.DoesNotContain("그룹원이 같은맵에없습니다", lead.Said);
        Assert.DoesNotContain("몬스터가 남아있습니다", lead.Said);
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        LoginFlow.TryCreateAccount(server, who);
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 99;
        character["_MaximumHp"] = 200000;
        character["CurrentHp"] = 200000;
        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return world;
    }

    /// <summary>청하고 받아들인다(PartyTests). 들어온 직후 서버가 화면을 다시 보내는 동안은 청을 버리므로 물어 올 때까지 다시 청한다.</summary>
    private async Task Group(WorldClient lead, WorldClient mate)
    {
        string? asker = null;
        for (int tries = 0; tries < 15 && asker is null; tries++)
        {
            await lead.AskToGroupAsync(Member, _deadline.Token);
            try
            {
                await Waiting.Until(() => mate.TakeAsk(out asker), "0x63", _deadline.Token, TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.Equal(Leader, asker);
        await mate.AcceptGroupAsync(Leader, _deadline.Token);
        await Waiting.Until(() => lead.Said.Contains(Member), "그룹이 되지 않았습니다.", _deadline.Token);
    }

    private static int Hostiles(WorldClient world) => world.Creatures.Count(c => c.Kind == CreatureKind.Hostile);

    /// <summary>운영자 순간이동(`/tp 맵 x y`) — 같은 맵 안이면 그 자리로 옮기고 화면을 다시 보낸다. 누가 서 있으면 가까운 빈 칸.</summary>
    private async Task Teleport(WorldClient world, Tile to)
    {
        await world.SayAsync($"/tp \"{Copy}\" {to.X} {to.Y}", _deadline.Token);
        await Task.Delay(1200, _deadline.Token);
    }

    /// <summary>네 곳을 돌며 본 괴물 번호를 모은다.</summary>
    private async Task<HashSet<uint>> Survey(WorldClient world)
    {
        HashSet<uint> seen = [];
        foreach (Tile spot in Lookouts)
        {
            await Teleport(world, spot);
            seen.UnionWith(world.Creatures.Where(c => c.Kind == CreatureKind.Hostile).Select(c => c.Serial));
        }

        return seen;
    }

    /// <summary>보이는 괴물 곁으로 순간이동해 휘두른다. 네 곳 어디서도 안 보이면 끝.</summary>
    private async Task KillEverything(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(5);
        while (DateTime.UtcNow < giveUp)
        {
            Creature? target = world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Hostile);
            if (target is null)
            {
                if ((await Survey(world)).Count == 0)
                    return;
                continue;
            }

            foreach ((int dx, int dy, Direction face) in new[] { (0, 1, Direction.North), (0, -1, Direction.South), (1, 0, Direction.West), (-1, 0, Direction.East) })
            {
                await Teleport(world, new Tile(target.Where.X + dx, target.Where.Y + dy));
                if (world.State?.Where != new Tile(target.Where.X + dx, target.Where.Y + dy))
                    continue;
                await world.TurnAsync(face, _deadline.Token);
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(600, _deadline.Token);
                break;
            }
        }

        Assert.Fail($"5분 안에 다 잡지 못했습니다: {Hostiles(world)}마리 남음 · 나 {world.State?.Where}");
    }

    private static string PackLog(IsolatedHadesServer server) =>
        string.Join(" / ", server.ConsoleOutput.Split('\n').Where(line => line.Contains("멈췄") || line.Contains("옮기지 않은") || line.Contains("rror")).TakeLast(4));
}
