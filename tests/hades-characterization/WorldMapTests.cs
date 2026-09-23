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
    private const int NoviceVillage = 20373;

    private const string Name = "mapwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 우드랜드 아래 가장자리를 밟으면 창이 뜨고(수오미가 그 안에 있고) → 수오미를 고르면 실제로
    /// 수오미마을에 내려놓는다. 두 가지 다 같은 접속으로 이어서 확인한다 — 창을 여는 것과 창에서
    /// 고르는 것을 따로 서버를 띄워 두 번 볼 까닭이 없다(사용자 결정, 2026-09-19).
    /// </summary>
    [Fact]
    public async Task Stepping_onto_the_woodland_edge_opens_the_world_map_and_suomi_takes_you_there()
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

        Assert.True(world.Field is not null, $"우드랜드 아래 가장자리를 밟았는데 월드맵이 오지 않았습니다. 마지막: {world.State}");

        WorldMapInfo field = world.Field!;

        Assert.Equal("field001", field.Field);
        Assert.Equal(3, field.Nodes.Count);

        // 들어가면 못 나오는 곳은 목록에 두지 않는다 — 드라큐라의성(20399)·크리스마스마을(20711) 에는
        // 밟을 수 있는 워프가 하나도 없어 걸어 나갈 수도 월드맵을 다시 열 수도 없다.
        Assert.DoesNotContain(field.Nodes, node => node.AreaId is 20399 or 20711);

        WorldMapNode suomi = Assert.Single(field.Nodes, node => node.Name == "수오미");

        Assert.Equal(SuomiTown, suomi.AreaId);
        Assert.Equal(40, suomi.X);
        Assert.Equal(11, suomi.Y);

        // 새 캐릭터가 시작하는 곳이 지도에 없었다(하데스 1241e297c, NEXT.md: "새 캐릭터 시작 = 노비스마을
        // 37,29") — 노비스마을 노드를 더했다.
        WorldMapNode novice = Assert.Single(field.Nodes, node => node.Name == "노비스마을");

        Assert.Equal(NoviceVillage, novice.AreaId);
        Assert.Equal(34, novice.X);
        Assert.Equal(34, novice.Y);

        await world.ChooseFieldAsync(SuomiTown, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            $"수오미를 골랐는데 수오미마을로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // 창은 맵이 바뀌면 닫힌다 — 안 닫히면 그 뒤로 걸음이 서버에 닿지 않는다.
        Assert.Null(world.Field);
    }

    /// <summary>
    /// 미리 구한 길을 한 칸씩 따라간다. 걸은 뒤 자리는 서버에 새로 물어야 안다 — 걷기 성공은 스스로 알려
    /// 오지 않는다. <paramref name="onMap" /> 을 벗어나면(동쪽 끝 99,24~27 을 밟아 포테의숲으로 넘어가면)
    /// 남은 길은 걷지 않고 곧바로 멈춘다 — 맵이 바뀐 뒤에도 수오미 기준 길을 계속 걸으면 엉뚱한 데로 간다.
    /// </summary>
    internal static async Task WalkTheWay(WorldClient world, IReadOnlyList<Tile> way, int onMap, CancellationToken token)
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
    /// <remarks>
    /// 맵 파일만 본다 — 서버가 그 위에 덮는 템플릿의 <c>Blocks</c> 와 남이 선 칸(`Sprite.Walk`)은 안 본다
    /// (`Area.cs:205-207`). 지금 수오미마을에는 그것이 없어서 이 시험이 맞다 — NPC 가 하나 더 서거나
    /// 운영자가 칸을 막으면 147칸 길이 <see cref="Assert.Fail" /> 로 죽을 수 있다.
    /// </remarks>
    internal static Func<Tile, bool> Walled(IsolatedHadesServer server, int mapId, int columns, int rows)
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
