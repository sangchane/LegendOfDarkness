using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵을 숨은 칸이 아니라 **말 한 마디로** 연다. 사냥터(괴물이 있는 맵)에서도 열리고(사용자 결정,
/// 2026-09-23 — 예전엔 거절했다), 열었다가 그냥 닫을 수도 있다(사용자 결정, 2026-09-19).
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
    public async Task Asking_for_it_where_monsters_are_is_allowed()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlain, 25, 25));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NovicePlain, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);

        // 괴물이 젠될 때까지 기다린다 — 괴물이 실제로 서 있는 상태에서 열리는지 재는 시험이다.
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "노비스평원A 에 괴물이 나오지 않았습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null, "사냥터에서 지도 단추를 눌렀는데 월드맵이 오지 않았습니다.", _deadline.Token);

        await world.CloseFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is null, "닫았는데 월드맵이 그대로입니다.", _deadline.Token);

        // 닫힌 뒤 갇히면 안 된다 — 걸음이 그대로 닿는다.
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

        // 꾸밈용 주민(노비스주민1·2)이 눈에 들어오면 그 상태에서 묻는다 — 그래야 "꾸밈용 괴물이 지도를
        // 막지 않는다" 를 실제로 재는 판이 된다. 다만 **못 기다린다고 실패시키지는 않는다**: 클라이언트는
        // 눈에 보이는 범위의 생물만 알고, 주민은 70x70 마을 어디에든 설 수 있어 시작 자리(37,29)에서 영영
        // 안 보일 수 있다. 그것까지 요구하면 코드가 멀쩡해도 시험이 떨어진다(2026-09-19 실제로 그랬다).
        // 꾸밈용 괴물이 막지 않는다는 것 자체는 서버의 식 `(Template.Exp ?? 1) > 0` 과 자료(맵 20373 의
        // 괴물 템플릿은 그 둘뿐, 경험치 0)가 지킨다. Creatures 에는 NPC(Mundane)도 들어가므로 괴물만 센다
        // (ServerFormat07.cs:65-75 · WorldClient.cs:1321).
        try
        {
            await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
                "주민이 안 보인다", _deadline.Token, within: TimeSpan.FromSeconds(20));
        }
        catch (TimeoutException)
        {
            // 안 보여도 그냥 묻는다 — 아래가 이 시험의 본론이다.
        }

        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null, "마을에서 월드맵을 달라고 했는데 오지 않았습니다.", _deadline.Token);
        Assert.Contains(world.Field!.Nodes, node => node.Name == "수오미");

        await world.CloseFieldAsync(_deadline.Token);

        await Waiting.Until(() => world.Field is null, "닫았는데 월드맵이 그대로입니다.", _deadline.Token);

        // 화면(GameScreen.cs:466)은 닫는 동안 "지도" 단추를 막아 이 조합을 못 보내지만, 알맹이로는
        // 그대로 보낼 수 있다 — 닫은 직후 곧바로 다시 열어도(서버가 순서대로 처리해 재개장될 뿐,
        // 갇히지는 않는다) 다시 닫으면 걸음은 그래도 닿아야 한다.
        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null, "닫은 직후 다시 열었는데 월드맵이 오지 않았습니다.", _deadline.Token);

        await world.CloseFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is null, "다시 닫았는데 월드맵이 그대로입니다.", _deadline.Token);

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
    /// 계정·캐릭터는 <see cref="HadesLoginClient.CreateCharacterAsync" /> 가 **고른 값**(성별 2(여)·머리
    /// 31·색 40 — 기본값이 아닌 것으로 눈에 띄게 고름)으로 만든다. 머리 32 는 남자 전용
    /// (`data/character-creation/hairstyles.json`)이라 여자로 만들면서 그 번호를 주면 원작에 없는 조합이
    /// 된다 — 서버(`ClientFormat04`)는 이 조합을 검사하지 않으므로 고르는 쪽(여기)이 지켜 31을 쓴다.
    /// 만들기 화면(<c>mobile/client/src/CreateScreen.cs</c>)이 사람 손으로 하는 것과 같은 일을 알맹이로
    /// 한다. 고른 값이 그대로 저장됐는지는 아래에서 <c>aislings/&lt;이름&gt;.json</c> 을 읽어 확인한다.
    ///
    /// 수오미마을 → 포테의숲1존은 5.99 에서 레벨 21~51 이다(docs/pote-forest.md). 그 레벨도 실제로 싸워
    /// 올리는 것이 아니라 <see cref="WorldMapTests" /> 가 하는 그대로 저장 파일의 <c>ExpLevel</c> 을 그
    /// 값으로 덮어쓴 것이다(실제로 싸워 올리는 것은 <c>NovicePlay</c> 가 따로 덮는다). 이 시험 하나가
    /// 3~4분 걸릴 수 있어(147칸) 공용 <see cref="_deadline" /> 대신 저희만의 마감을 쓴다.
    ///
    /// 도착 자리(33,47)는 수오미의 (99,24)~(99,27) 네 칸 모두가 향하는 고정된 한 칸이다(서버
    /// `templates/warps/warp 수오미마을(99,2* ) to 포테의숲1존(33,47).json`). 다만 그 칸에 마침 몹이
    /// 서 있으면 서버가 둘레 세 칸까지 넓혀 빈 자리를 찾는다(`Area.cs:93-107 FreeSpotNear`) — 실제로 한
    /// 번은 (32,46) 에 내렸다(2026-09-19). 그래서 이 시험은 딱 그 칸이 아니라 **언저리 세 칸**으로 잰다.
    /// 도착 칸이 정확히 (33,47) 인 것은 `PoteForestTests` 가 따로 지킨다.
    /// </remarks>
    [Fact]
    public async Task A_fresh_character_reaches_pote_forest_through_the_map_button()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(8));

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));

        // 기본값(0x01,0x01,0x01)이 아닌, 눈에 띄는 값으로 고른다 — 성별 2(여)·머리 31(여자에도 있는
        // 번호, 32는 남자 전용)·색 40. 만들기 화면이 사람 손으로 누르는 것과 같은 길(Task A).
        const byte hairStyle = 31;
        const byte gender = 2;
        const byte hairColor = 40;

        await HadesLoginClient.CreateCharacterAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            hairStyle, gender, hairColor, progress: null, deadline.Token);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        System.Text.Json.Nodes.JsonNode character = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(saved))!;

        // 서버에 고른 값이 그대로 박혔는지 — Gender 는 enum 이름 문자열로, HairStyle·HairColor 는
        // 숫자로 저장된다(MobileClientProtocolTests 와 같은 확인).
        Assert.Equal(hairStyle, (byte)character["HairStyle"]!.GetValue<int>());
        Assert.Equal(((Darkages.Types.Gender)gender).ToString(), character["Gender"]!.GetValue<string>());
        Assert.Equal(hairColor, (byte)character["HairColor"]!.GetValue<int>());

        // 수오미마을 → 포테의숲1존은 레벨 21~51 이다. 실제로 싸워서 올리는 것이 아니라 WorldMapTests 와
        // 같은 방식으로 저장 파일의 레벨 값만 고친다.
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

        // 딱 (33,47) 을 요구하지 않는다 — 그 칸에 몹이 서 있으면 서버가 둘레 세 칸까지 빈 자리를 찾아
        // 내려놓는다(`Area.FreeSpotNear`). 여기서 재는 것은 "그 워프로 1존에 들어왔나" 이고,
        // 도착 칸이 정확히 (33,47) 인 것은 PoteForestTests 가 따로 지킨다.
        await Waiting.Until(() => world.State is { } state && state.Map.Id == ForestOne && Math.Abs(state.Where.X - 33) <= 3 && Math.Abs(state.Where.Y - 47) <= 3,
            $"포테의숲1존 33,47 언저리로 가지 않았습니다. 마지막: {world.State}", deadline.Token);

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
