using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵을 숨은 칸이 아니라 **말 한 마디로** 연다. 마을에서만 열리고(사냥터에서 열면 싸우다 갇힌다),
/// 열었다가 그냥 닫을 수도 있다(사용자 결정, 2026-09-19).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WorldMapMenuTests : IDisposable
{
    private const int NoviceTown = 20373;
    private const int NovicePlain = 20393;
    private const int SuomiTown = 20355;
    private const int ForestOne = 20263;

    private const string Name = "menuwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Asking_for_it_where_monsters_are_is_refused()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlain, 25, 25));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NovicePlain, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);

        // 괴물이 젠될 때까지 기다린다 — 젠 관리자가 세우기 전에 물으면 마을처럼 보인다.
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "노비스평원A 에 괴물이 나오지 않았습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        Assert.Null(world.Field);

        // 거절당했어도 갇히면 안 된다 — 걸음이 그대로 닿는다.
        // 원작 걸음은 성공해도 자기에게는 아무 말도 오지 않는다(ServerFormat0C 는
        // Scope.NearbyAislingsExludingSelf) — 그래서 걷고 나서 RefreshAsync 로 서버에게
        // 있는 자리를 다시 물어야 world.State.Where 가 실제로 바뀐 값을 받는다.
        Tile before = world.State!.Where;
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Where != before, _deadline.Token);
        await world.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Where != before, "걸었는데 서버가 다른 자리를 말하지 않습니다.", _deadline.Token);
        Assert.NotEqual(before, world.State!.Where);
    }

    /// <summary>
    /// 지도 단추로 열면(마을이니까 됨 · 수오미가 목록에 있음) → 닫기를 누르면 취소가 가고 조작이
    /// 돌아온다. 여는 것과 닫는 것을 따로 서버를 띄워 두 번 볼 까닭이 없어 한 접속으로 잇는다
    /// (사용자 결정, 2026-09-19).
    /// </summary>
    /// <remarks>
    /// 노비스마을에는 노비스주민1·2 가 괴물로 서 있다(경험치 0·체력 2147483647, 꾸밈용). 지도를 달라고
    /// 하기 전에 그 주민이 실제로 나타날 때까지 기다린다 — 젠 전에 물으면 이 시험이 우연히 통과한다
    /// (실제로 그래서 한 번 그랬다, 2026-09-19). 노비스주민1·2 가 괴물로 서 있어도 지도가 열려야
    /// 한다 — 꾸밈용(경험치 0)이기 때문이다(`GameServerHandlers.FormatF0Handler`).
    /// </remarks>
    [Fact]
    public async Task Opening_the_world_map_from_the_menu_and_closing_it_gives_movement_back()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NoviceTown, "노비스마을에 들어가지 못했습니다.", _deadline.Token);

        // 꾸밈용 주민(노비스주민1·2)이 괴물로 젠될 때까지 기다린다 — 젠 전에 물으면 이 시험이
        // 우연히 통과해 버려 재발을 못 잡는다.
        await Waiting.Until(() => world.Creatures.Any(), "노비스마을에 주민이 나오지 않았습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null, "마을에서 월드맵을 달라고 했는데 오지 않았습니다.", _deadline.Token);
        Assert.Contains(world.Field!.Nodes, node => node.Name == "수오미");

        await world.CloseFieldAsync(_deadline.Token);

        await Waiting.Until(() => world.Field is null, "닫았는데 월드맵이 그대로입니다.", _deadline.Token);

        // 여기가 요점이다 — 닫은 뒤 걸음이 서버에 닿아야 한다.
        // 원작 걸음은 성공해도 자기에게는 아무 말도 오지 않는다(ServerFormat0C 는
        // Scope.NearbyAislingsExludingSelf) — 그래서 걷고 나서 RefreshAsync 로 서버에게
        // 있는 자리를 다시 물어야 world.State.Where 가 실제로 바뀐 값을 받는다.
        Tile before = world.State!.Where;
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Where != before, _deadline.Token);
        await world.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Where != before, "걸었는데 서버가 다른 자리를 말하지 않습니다.", _deadline.Token);
        Assert.NotEqual(before, world.State!.Where);
        Assert.Equal(NoviceTown, world.State!.Map.Id);
    }

    /// <summary>
    /// 새 캐릭터가 노비스마을 (37,29) 에서 시작해 → "지도" 단추(<see cref="WorldClient.OpenFieldAsync"/>) →
    /// 수오미 고르기 → 수오미마을 가로지르기(길찾기) → 포테의숲1존 (33,47) → 괴물 둘. Task 1~3 이 실제로
    /// 이어지는지 한 줄로 확인한다. 시작 자리는 이미 노비스마을이다
    /// (scripts/server-config/LoruleConfig.template.json:27-31) — startTogether 는 다른 시험과 꼴을 맞추려는
    /// 것뿐이다. 수오미 가로지르기는 <see cref="WorldMapTests" /> 의 벽 읽기·길찾기를 그대로 쓴다(복사하지
    /// 않음): <see cref="WorldMapTests.Walled" />·<see cref="WorldMapTests.WalkTheWay" />.
    /// </summary>
    /// <remarks>
    /// 계정·캐릭터는 <see cref="LoginFlow.TryCreateAccount" /> 가 고정값(머리 모양 1·성별 1·머리색 1,
    /// `ClientFormat04` 세 바이트)으로 만든다 — 사람이 머리 모양·성별·머리색을 골라 만드는 화면은 모바일
    /// 쪽에 아직 없고(`mobile/client/src/LoginScreen.cs` 는 계정·비밀번호 칸과 "로그인" 단추뿐이다),
    /// 알맹이(<see cref="HadesLoginClient" />)도 로그인만 알아 계정·캐릭터 만들기를 보낼 길이 없다.
    /// 그래서 이 시험이 실제로 덮는 것은 **계정이 이미 있는 다음부터**다.
    ///
    /// 수오미마을 → 포테의숲1존은 5.99 에서 레벨 21~51 이다(docs/pote-forest.md). 그 레벨도 실제로 싸워
    /// 올리는 것이 아니라 <see cref="WorldMapTests" /> 가 하는 그대로 저장 파일의 <c>ExpLevel</c> 을 그
    /// 값으로 덮어쓴 것이다(실제로 싸워 올리는 것은 <c>NovicePlay</c> 가 따로 덮는다). 이 시험 하나가
    /// 3~4분 걸릴 수 있어(147칸) 공용 <see cref="_deadline" /> 대신 저희만의 마감을 쓴다.
    ///
    /// 도착 자리(33,47)는 수오미의 (99,24)~(99,27) 네 칸 모두가 향하는 고정된 한 칸이다(서버
    /// `templates/warps/warp 수오미마을(99,2* ) to 포테의숲1존(33,47).json`). 다만 그 칸에 마침 몹이
    /// 서 있으면 서버가 둘레 세 칸까지 넓혀 빈 자리를 찾는다(`Area.cs:93-107 FreeSpotNear`) — 실제로 한
    /// 번은 (32,46) 에 내려 이 시험이 떨어졌었고, 다시 돌리니 통과했다(2026-09-19, 자리 뜸이 겹친
    /// 우연). 다시 떨어지면 이 까닭부터 본다 — 코드가 아니라 그 순간 몹이 그 자리에 있었는지부터.
    /// </remarks>
    [Fact]
    public async Task A_fresh_character_reaches_pote_forest_through_the_map_button()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(8));

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));

        // 사람이 머리 모양·성별·머리색을 고르는 화면은 없다 — 고정값으로 계정과 캐릭터를 만든다.
        LoginFlow.TryCreateAccount(server, Name);

        // 수오미마을 → 포테의숲1존은 레벨 21~51 이다. 실제로 싸워서 올리는 것이 아니라 WorldMapTests 와
        // 같은 방식으로 저장 파일의 레벨 값만 고친다.
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        System.Text.Json.Nodes.JsonNode character = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 21;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NoviceTown, "노비스마을에 들어가지 못했습니다.", deadline.Token);

        await world.OpenFieldAsync(deadline.Token);
        await Waiting.Until(() => world.Field is not null, "지도 단추를 눌렀는데 월드맵이 오지 않았습니다.", deadline.Token);

        await world.ChooseFieldAsync(SuomiTown, deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            $"수오미를 골랐는데 수오미마을로 가지 않았습니다. 마지막: {world.State}", deadline.Token);

        WorldEntry entry = world.State!;
        Tile goal = new(99, 25);

        Func<Tile, bool> blocked = WorldMapTests.Walled(server, SuomiTown, entry.Map.Columns, entry.Map.Rows);

        // reach 기본값 40 으로는 147칸 길에 null 이 돌아온다(WorldMapTests 와 같은 함정).
        IReadOnlyList<Tile>? way = Pathing.Way(entry.Where, goal, blocked, reach: 200);

        Assert.True(way is not null, $"{entry.Where} 에서 {goal} 로 길을 못 찾았습니다.");

        await WorldMapTests.WalkTheWay(world, way, SuomiTown, deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == ForestOne && state.Where == new Tile(33, 47),
            $"포테의숲1존 33,47 로 가지 않았습니다. 마지막: {world.State}", deadline.Token);

        HashSet<uint> met = [];

        for (int tick = 0; tick < 120 && met.Count < 2; tick++)
        {
            foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
            {
                met.Add(mob.Serial);
            }

            await world.RefreshAsync(deadline.Token);
            await Task.Delay(500, deadline.Token);
        }

        Assert.True(met.Count >= 2, $"포테의숲1존 입구에 1분 서 있는 동안 괴물이 {met.Count}마리만 보였습니다.");
    }
}
