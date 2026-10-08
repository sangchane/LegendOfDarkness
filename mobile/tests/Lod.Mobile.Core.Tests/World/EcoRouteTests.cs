using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>맵 사이 걷기의 길찾기(autopilot/eco-bots/walk-SPEC.md) — links.txt 줄을 읽어 워프·월드맵을 잇는다.</summary>
public sealed class EcoRouteTests
{
    // 마을 1 ─(5,0)·(6,0)→ 2 ─(9,9)→ 3, 마을 1 의 (0,5) 는 월드맵 칸 → 4(레벨 11~)·5(레벨 1~10), 2 → 6 은 레벨 41 부터.
    private const string Text = """
        # 주석
        link 1 5 0 2 5 9 0 0
        link 1 6 0 2 6 9 0 0
        link 2 9 9 3 1 1 0 0
        link 2 0 0 6 1 1 41 0
        gate 1 0 5
        field 4 3 3 11 0
        field 5 3 3 1 10
        block 3 4 4
        틀린 줄
        """;

    private static readonly EcoLinks Links = EcoLinks.Read(Text);

    [Fact]
    public void Follows_warps_map_by_map()
    {
        EcoLeg first = EcoRoute.Plan(Links, from: 1, to: 3, level: 20)!;

        Assert.Equal(2, first.ToMap);
        Assert.Equal(0, first.Field);
        Assert.Equal([new Tile(5, 0), new Tile(6, 0)], first.Tiles);
        Assert.Equal([new Tile(9, 9)], EcoRoute.Plan(Links, 2, 3, 20)!.Tiles);
    }

    [Fact]
    public void Steps_on_the_world_map_tile_and_picks_the_place()
    {
        EcoLeg leg = EcoRoute.Plan(Links, 1, 4, level: 20)!;

        Assert.Equal([new Tile(0, 5)], leg.Tiles);
        Assert.Equal(4, leg.Field);
        Assert.Equal(4, leg.ToMap);
    }

    [Fact]
    public void Leaves_out_warps_and_places_the_level_does_not_allow()
    {
        Assert.Null(EcoRoute.Plan(Links, 1, 4, level: 5));
        Assert.NotNull(EcoRoute.Plan(Links, 1, 5, level: 5));
        Assert.Null(EcoRoute.Plan(Links, 1, 5, level: 20));
        Assert.Null(EcoRoute.Plan(Links, 2, 6, level: 40));
        Assert.Equal(6, EcoRoute.Plan(Links, 2, 6, level: 41)!.ToMap);
    }

    [Fact]
    public void Goes_around_a_blocked_exit_or_gives_up()
    {
        HashSet<(int, Tile)> avoid = [(1, new Tile(5, 0))];
        Assert.Equal([new Tile(6, 0)], EcoRoute.Plan(Links, 1, 3, 20, avoid)!.Tiles);

        avoid.Add((1, new Tile(6, 0)));
        Assert.Null(EcoRoute.Plan(Links, 1, 3, 20, avoid));
        Assert.Null(EcoRoute.Plan(Links, 3, 1, 20));
        Assert.Null(EcoRoute.Plan(Links, 1, 1, 20));
    }

    [Fact]
    public void Knows_every_exit_tile_on_a_map()
    {
        Assert.Equal([new Tile(0, 5), new Tile(5, 0), new Tile(6, 0)], Links.TilesOn(1).OrderBy(tile => tile.X));
        Assert.Equal([new Tile(4, 4)], Links.TilesOn(3));   // NPC 스크립트 워프는 피하기만 — 길로는 안 쓴다
    }

    [Fact]
    public void Every_hunting_ground_has_a_walk_from_town_and_back()
    {
        // 실제 자료(생성기 build-eco-links.py · build-eco-grounds.py) — 마을 20041 에서 사냥터마다 어느 레벨로든 오가는 길이 있다.
        // 적정 레벨로는 못 가는 곳도 있다: 아벨해안4-A·4-C 는 적정 99 인데 해안 입구가 51~80 만 들인다(「늙었습니다」) — 봇은 그런 곳을 안 고른다.
        string world = Path.GetFullPath(Path.Combine(HairMotionTests.Parts(), "..", "..", "world"));   // mobile/client/assets/world
        EcoLinks links = EcoLinks.Read(File.ReadAllText(Path.Combine(world, "links.txt")));
        IReadOnlyList<EcoGround> grounds = EcoGrounds.Read(File.ReadAllText(Path.Combine(world, "eco-grounds.txt")));

        Assert.NotEmpty(grounds);
        Assert.All(grounds, ground =>
        {
            Assert.True(Enumerable.Range(1, 99).Any(level => Walk(links, 20041, ground.Map, level) && Walk(links, ground.Map, 20041, level)),
                $"마을 ↔ {ground.Name}({ground.Map}) 길 없음");
        });
        Assert.Equal(128, grounds.Count(ground => Walk(links, 20041, ground.Map, ground.Level)));
    }

    /// <summary>첫 걸음만 주므로 도착할 때까지 이어 본다(맵 안은 다 이어진다고 친다).</summary>
    private static bool Walk(EcoLinks links, int from, int to, int level)
    {
        for (int map = from, legs = 0; legs < 64; legs++)
        {
            if (map == to)
            {
                return true;
            }

            if (EcoRoute.Plan(links, map, to, level) is not { } leg)
            {
                return false;
            }

            map = leg.ToMap;
        }

        return false;
    }
}
