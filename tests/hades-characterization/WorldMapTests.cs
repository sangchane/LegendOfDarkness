using System.Buffers.Binary;
using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵 — 우드랜드입구 아래 가장자리(9~11,23)를 밟으면 서버가 창을 띄우고(ServerFormat2E),
/// 그 창에서 한 곳을 고르기 전까지는 다른 패킷을 모두 버린다(`NetworkServer.cs:141`).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WorldMapTests : IDisposable
{
    private const int WoodlandGate = 20028;
    private const int SuomiTown = 20355;
    private const int ForestOne = 20263;

    private const string Name = "mapwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Stepping_onto_the_woodland_edge_opens_the_world_map_with_suomi_on_it()
    {
        // 20028 은 40x24 — (10,23) 이 아래 가장자리이고 월드맵을 여는 칸이다. (10,22) 도 (10,23) 도 길이다.
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);

        WorldMapInfo field = world.Field!;

        Assert.Equal("field001", field.Field);
        Assert.Equal(24, field.Nodes.Count);

        WorldMapNode suomi = Assert.Single(field.Nodes, node => node.Name == "수오미");

        Assert.Equal(SuomiTown, suomi.AreaId);
        Assert.Equal(40, suomi.X);
        Assert.Equal(11, suomi.Y);
    }

    [Fact]
    public async Task Choosing_suomi_puts_the_character_down_in_suomi()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);

        await world.ChooseFieldAsync(SuomiTown, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            $"수오미를 골랐는데 수오미마을로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // 창은 맵이 바뀌면 닫힌다 — 안 닫히면 그 뒤로 걸음이 서버에 닿지 않는다.
        Assert.Null(world.Field);
    }

    /// <summary>
    /// 우드랜드입구 → 월드맵 → 수오미마을 → (길찾기로) 포테의숲1존 입구 → 33,47 → 괴물 둘.
    /// 이 시험 하나가 3~4분 걸릴 수 있어(147칸) 공용 <see cref="_deadline"/> 대신 저희만의 마감을 쓴다 —
    /// 다른 시험의 마감은 그대로 5분으로 둔다.
    /// </summary>
    /// <remarks>
    /// 월드맵이 내려놓는 (40,11) 에서 (99,25) 까지 남쪽·동쪽으로 곧장 걸으면 (40,25)→(45,25) 가 벽이라
    /// 닿지 않는다(`lod20355.map` 을 서버 규칙대로 직접 읽어 확인함). 그래서 알맹이의 길찾기
    /// (<see cref="Pathing.Way"/>) 로 147칸짜리 길을 구해 한 칸씩 걷는다. 자리는 걸을 때마다 서버에
    /// 새로 물어야 안다 — 걷기가 성공했다는 말은 스스로 오지 않는다(`PoteDungeonTests.StepTo` 와 같은 까닭,
    /// `Aisling.Walk` 가 걸음 알림을 자신을 뺀 근처에만 보낸다).
    /// </remarks>
    [Fact]
    public async Task The_whole_way_from_woodland_to_the_first_zone_of_pote_forest()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(8));

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        // 수오미마을 → 포테의숲1존은 5.99 에서 레벨 21~51 이다(docs/pote-forest.md).
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        System.Text.Json.Nodes.JsonNode character = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 21;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, deadline.Token);
        await world.ChooseFieldAsync(SuomiTown, deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            "수오미마을로 가지 않았습니다.", deadline.Token);

        WorldEntry entry = world.State!;
        Tile goal = new(99, 25);

        Func<Tile, bool> blocked = Walled(server, SuomiTown, entry.Map.Columns, entry.Map.Rows);

        // reach 기본값 40 으로는 147칸 길에 null 이 돌아온다.
        IReadOnlyList<Tile>? way = Pathing.Way(entry.Where, goal, blocked, reach: 200);

        Assert.True(way is not null, $"{entry.Where} 에서 {goal} 로 길을 못 찾았습니다.");

        await WalkTheWay(world, way, SuomiTown, deadline.Token);

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

    /// <summary>
    /// 미리 구한 길을 한 칸씩 따라간다. 걸은 뒤 자리는 서버에 새로 물어야 안다 — 걷기 성공은 스스로 알려
    /// 오지 않는다. <paramref name="onMap" /> 을 벗어나면(동쪽 끝 99,24~27 을 밟아 포테의숲으로 넘어가면)
    /// 남은 길은 걷지 않고 곧바로 멈춘다 — 맵이 바뀐 뒤에도 수오미 기준 길을 계속 걸으면 엉뚱한 데로 간다.
    /// </summary>
    private static async Task WalkTheWay(WorldClient world, IReadOnlyList<Tile> way, int onMap, CancellationToken token)
    {
        foreach (Tile next in way)
        {
            if (world.State?.Map.Id != onMap)
            {
                return;
            }

            for (int attempt = 0; attempt < 10; attempt++)
            {
                // 걸은 뒤 자리는 서버에 새로 물어야 안다(PoteDungeonTests.StepTo 와 같은 순서: 먼저 새로
                // 묻고, 그 자리를 보고서야 방향을 정해서 걷는다 — 걷고 나서 곧바로 물으면 서버가
                // "새로 고치는 동안" 걸음을 버린다).
                await world.RefreshAsync(token);
                await Task.Delay(400, token);

                if (world.State is not { } me || me.Map.Id != onMap || me.Where == next)
                {
                    break;
                }

                Direction direction = next.X > me.Where.X ? Direction.East
                    : next.X < me.Where.X ? Direction.West
                    : next.Y > me.Where.Y ? Direction.South
                    : Direction.North;

                await world.WalkAsync(direction, token);
                await Task.Delay(300, token);
            }

            if (world.State?.Map.Id == onMap && world.State?.Where != next)
            {
                Assert.Fail($"{next} 에 서지 못했습니다. 마지막: {world.State}");
            }
        }
    }

    /// <summary>
    /// 벽 여부. 서버의 <c>Area.ParseMapWalls</c> 그대로다
    /// (sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Types/Area.cs:165-250):
    /// 칸마다 6바이트 — 앞 2바이트는 건너뛰고 short 둘을 작은 끝 먼저로 읽어 지형표(<c>static/sotp.dat</c>)를
    /// 찾는다. 파일이 모자라 못 읽은 칸과 맵 밖은 벽으로 친다(<see cref="Pathing" /> 이 그렇게 요구한다).
    /// </summary>
    private static Func<Tile, bool> Walled(IsolatedHadesServer server, int mapId, int columns, int rows)
    {
        byte[] sotp = File.ReadAllBytes(Path.Combine(server.ContentLocation, "static", "sotp.dat"));
        byte[] map = File.ReadAllBytes(Path.Combine(server.ContentLocation, "maps", $"lod{mapId}.map"));

        bool[,] wall = new bool[columns, rows];
        int at = 0;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (at + 6 > map.Length)
                {
                    wall[x, y] = true;
                    continue;
                }

                short left = BinaryPrimitives.ReadInt16LittleEndian(map.AsSpan(at + 2, 2));
                short right = BinaryPrimitives.ReadInt16LittleEndian(map.AsSpan(at + 4, 2));
                at += 6;

                wall[x, y] = IsWall(left, right, sotp);
            }
        }

        return tile => tile.X < 0 || tile.Y < 0 || tile.X >= columns || tile.Y >= rows || wall[tile.X, tile.Y];
    }

    private static bool IsWall(short left, short right, byte[] sotp)
    {
        if (left == 0 && right == 0)
        {
            return false;
        }

        if (left == 0)
        {
            return sotp[right - 1] == 0x0F;
        }

        if (right == 0)
        {
            return sotp[left - 1] == 0x0F;
        }

        return sotp[left - 1] == 0x0F || sotp[right - 1] == 0x0F;
    }
}
