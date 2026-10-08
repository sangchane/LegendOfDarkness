using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The 길 찾기 map: reading the exits and NPCs, sorting what is about into dots, a finger on the map, and the way
/// to an exit. Floors are drawn by hand — <c>#</c> a wall, <c>.</c> floor.
/// </summary>
public sealed class TabMapTests
{
    private static Func<Tile, bool> Floor(params string[] rows) =>
        tile => tile.Y < 0 || tile.Y >= rows.Length
                || tile.X < 0 || tile.X >= rows[tile.Y].Length
                || rows[tile.Y][tile.X] == '#';

    private const string Guide = """
        # 머리 줄은 건너뛴다
        exit 20373 69 26 노비스평원A
        exit 20373 69 27 노비스평원A
        exit 20373 69 28 노비스평원A
        exit 20373 28 26 노비스민가1
        exit 20373 31 25 노비스민가1
        exit 20373 63 49 월드맵
        npc 20373 40 34 멜로린
        npc 20373 3 10 자르반 3세
        about 20373 40 34 판매: 사과, 치즈
        role 20373 40 34 물약
        room 20373 노비스마을식당|카르마 — 판매: 사과 / 베이가 — 판매: 와인
        exit 20393 0 0 노비스마을
        """;

    /// <summary>
    /// Warp tiles side by side that go to the same place are one exit; the same place reached by two doors far
    /// apart is two exits — both are real ways in.
    /// </summary>
    [Fact]
    public void Touching_warp_tiles_to_one_place_are_one_exit()
    {
        IReadOnlyList<MapExit> exits = MapGuide.Read(Guide).ExitsOn(20373);

        Assert.Equal(4, exits.Count);

        MapExit plain = Assert.Single(exits, exit => exit.To == "노비스평원A");
        Assert.Equal(3, plain.Tiles.Count);
        Assert.Equal(new Tile(69, 27), plain.Middle);

        Assert.Equal(2, exits.Count(exit => exit.To == "노비스민가1"));
        Assert.Contains(exits, exit => exit.To == "월드맵");
    }

    /// <summary>Names keep their spaces, other maps keep their own, and a map with nothing written has nothing.</summary>
    [Fact]
    public void Each_map_reads_its_own_lines()
    {
        MapGuide guide = MapGuide.Read(Guide);

        Assert.Contains(guide.SignsOn(20373), sign => sign.Name == "자르반 3세" && sign.Where == new Tile(3, 10) && sign.About == "");
        Assert.Contains(guide.SignsOn(20373), sign => sign.Name == "멜로린" && sign.About == "판매: 사과, 치즈");
        Assert.Equal(new MapRoom("노비스마을식당", "카르마 — 판매: 사과\n베이가 — 판매: 와인"), Assert.Single(guide.RoomsOn(20373)));
        Assert.Empty(guide.RoomsOn(20393));
        Assert.Single(guide.ExitsOn(20393));
        Assert.Empty(guide.ExitsOn(1));
        Assert.Empty(guide.SignsOn(1));
    }

    /// <summary>
    /// Monsters are dots with no words, merchants carry their name without the "@place#x,y" the ported NPCs come
    /// with, the group is told apart from strangers, and a template NPC already in sight is not drawn twice.
    /// </summary>
    [Fact]
    public void What_is_about_is_sorted_into_kinds()
    {
        Creature wasp = new(1, new Tile(5, 5), Direction.South, 0x4001, CreatureKind.Hostile, "말벌");
        Creature cook = new(2, new Tile(40, 35), Direction.South, 0x4002, CreatureKind.Merchant, "멜로린@노비스마을#40,34");
        Creature bush = new(3, new Tile(9, 9), Direction.South, 0x4003, CreatureKind.Passable, "덤불");
        Character friend = new(4, new Tile(10, 10), Direction.North, Name: "monk");
        Character stranger = new(5, new Tile(11, 10), Direction.North, Name: "watch");
        MapGuide guide = MapGuide.Read(Guide);

        IReadOnlyList<TabMarker> markers = TabMap.Markers(
            new Tile(20, 20), [friend, stranger], [wasp, cook, bush], ["nov", "monk"],
            guide.ExitsOn(20373), guide.SignsOn(20373));

        Assert.Equal(string.Empty, Assert.Single(markers, one => one.Kind == TabMarkerKind.Monster).Label);
        Assert.Equal("monk", Assert.Single(markers, one => one.Kind == TabMarkerKind.Party).Label);
        Assert.Single(markers, one => one.Kind == TabMarkerKind.Person);

        // 멜로린은 보이는 자리(40,35) 하나만 — 템플릿 자리(40,34)는 겹쳐 그리지 않는다.
        TabMarker melorin = Assert.Single(markers, one => one.Label == "멜로린");
        Assert.Equal(new Tile(40, 35), melorin.Where);
        Assert.Contains(markers, one => one.Kind == TabMarkerKind.Npc && one.Label == "자르반 3세");

        Assert.DoesNotContain(markers, one => one.Where == bush.Where);
        Assert.Equal(TabMarkerKind.Me, markers[^1].Kind);
    }

    /// <summary>
    /// Roles(2026-10-08): an NPC carries its <c>role</c> line — a merchant in sight finds it by name — and a door into a
    /// building carries the roles inside, each once. An NPC with no role line, or a word the app does not know, is 안내.
    /// </summary>
    [Fact]
    public void Npcs_and_doors_carry_their_roles()
    {
        // 원문 끝에는 줄바꿈이 없다 — 이어 붙이려면 하나 넣는다.
        MapGuide guide = MapGuide.Read(Guide + "\n" + """
            exit 20373 10 10 수오미무기점
            room 20373 수오미무기점|가이 — 판매: 에페 / 오리아나 — 은행 / 델란 — 판매: 단검|무기,은행,무기
            role 20373 3 10 춤
            """);
        Creature cook = new(2, new Tile(40, 35), Direction.South, 0x4002, CreatureKind.Merchant, "멜로린@노비스마을#40,34");

        IReadOnlyList<TabMarker> markers = TabMap.Markers(
            new Tile(20, 20), [], [cook], [], guide.ExitsOn(20373), guide.SignsOn(20373), guide.RoomsOn(20373));

        Assert.Equal([NpcRole.Potion], Assert.Single(markers, one => one.Label == "멜로린").Roles);
        Assert.Equal([NpcRole.Talk], Assert.Single(markers, one => one.Label == "자르반 3세").Roles);
        Assert.Equal([NpcRole.Weapon, NpcRole.Bank], Assert.Single(markers, one => one.Label == "수오미무기점").Roles);
        Assert.Empty(Assert.Single(markers, one => one.Label == "노비스평원A").Roles);
        Assert.Equal("무기 상점", NpcRoles.Title(NpcRole.Weapon));
        Assert.Equal("은행", NpcRoles.Word(NpcRole.Bank));
    }

    /// <summary>
    /// The shipped <c>guide.txt</c> (<c>build-client-guide.py</c>) gives the shops and the bank the roles a player looks
    /// for: 수오미 무기점·방어구점 doors, 마인 은행·제작상점 doors, 보석상여주인 and 메린 where they stand.
    /// </summary>
    [Fact]
    public void The_shipped_guide_tells_what_the_town_npcs_do()
    {
        string root = Path.GetFullPath(Path.Combine(HairMotionTests.Parts(), "..", ".."));   // mobile/client/assets
        MapGuide guide = MapGuide.Read(File.ReadAllText(Path.Combine(root, "world", "guide.txt")));

        Assert.Equal([NpcRole.Weapon], guide.RoomsOn(20355).Single(room => room.To == "수오미무기점").Kinds);
        Assert.Equal([NpcRole.Armor], guide.RoomsOn(20355).Single(room => room.To == "수오미방어구점").Kinds);
        Assert.Equal([NpcRole.Bank], guide.RoomsOn(20304).Single(room => room.To == "마인은행").Kinds);
        Assert.Equal([NpcRole.Craft], guide.RoomsOn(20304).Single(room => room.To == "마인제조상점").Kinds);
        Assert.Equal(NpcRole.Accessory, guide.SignsOn(20028).Single(sign => sign.Name == "보석상여주인").Role);
        Assert.Equal(NpcRole.Craft, guide.SignsOn(20308).Single(sign => sign.Name == "메린").Role);
    }

    /// <summary>The way to an exit ends on whichever of its tiles is nearest on foot, round the walls.</summary>
    [Fact]
    public void The_way_to_an_exit_takes_its_nearest_tile()
    {
        Func<Tile, bool> floor = Floor(
            ".....",
            ".###.",
            ".....");

        // 출구는 오른쪽 위(4,0)와 오른쪽 아래(4,2). 아래에서 출발하면 아래쪽이 가깝다.
        IReadOnlyList<Tile>? way = TabMap.WayToAny(new Tile(0, 2), [new Tile(4, 0), new Tile(4, 2)], floor);

        Assert.NotNull(way);
        Assert.Equal(4, way!.Count);
        Assert.Equal(new Tile(4, 2), way[^1]);
        Assert.Equal(Direction.East, TabMap.StepOf(new Tile(0, 2), way[0]));
    }

    /// <summary>Standing on the exit already is an empty way; an exit behind a closed wall is no way at all.</summary>
    [Fact]
    public void Arrived_and_unreachable_are_told_apart()
    {
        Func<Tile, bool> floor = Floor(
            "..#.",
            "..#.");

        Assert.Empty(TabMap.WayToAny(new Tile(0, 0), [new Tile(0, 0)], floor)!);
        Assert.Null(TabMap.WayToAny(new Tile(0, 0), [new Tile(3, 0), new Tile(3, 1)], floor));
        Assert.Null(TabMap.WayToAny(new Tile(0, 0), [new Tile(2, 0)], floor));
    }

    /// <summary>
    /// A finger near an exit means the exit, even a few pixels off; a finger on open floor far from anything means
    /// that tile; a finger on a wall means nothing.
    /// </summary>
    [Fact]
    public void A_finger_picks_the_named_place_first_then_the_floor()
    {
        Func<Tile, bool> floor = Floor(
            "..........",
            "..........",
            "..........",
            ".....#....",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........");
        TabMapProjection map = TabMapProjection.Fit(10, 10, 400, 200);
        TabMarker exit = new(new Tile(9, 5), TabMarkerKind.Exit, "노비스평원A", [new Tile(9, 4), new Tile(9, 5)]);
        TabMarker monster = new(new Tile(1, 1), TabMarkerKind.Monster, string.Empty, []);

        (float ex, float ey) = map.Centre(9, 5);
        TabGoal? onExit = TabMap.Pick(map, ex + 6, ey - 4, [exit, monster], floor);
        Assert.Equal("노비스평원A", onExit!.Label);
        Assert.Equal(2, onExit.Goals.Count);

        // 괴물은 고를 거리가 아니다 — 그 자리는 그냥 바닥이다.
        (float mx, float my) = map.Centre(1, 1);
        TabGoal? onFloor = TabMap.Pick(map, mx, my, [exit, monster], floor);
        Assert.Equal(new Tile(1, 1), Assert.Single(onFloor!.Goals));

        (float wx, float wy) = map.Centre(5, 3);
        Assert.Null(TabMap.Pick(map, wx, wy, [], floor));
    }
}
