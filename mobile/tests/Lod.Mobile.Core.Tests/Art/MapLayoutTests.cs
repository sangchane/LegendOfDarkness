using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// The layout tools/dat-extract layout writes beside a map's floor: which cells block and what stands where.
/// </summary>
public sealed class MapLayoutTests
{
    // 가로 3 · 세로 2. (1,0) 은 벽, (2,1) 에는 왼쪽·오른쪽 반쪽에 그림이 하나씩 선다.
    private const string Written = """
        # 시험 — tools/dat-extract layout 이 만든다.
        size 3 2
        blocked
        .#.
        ...
        floor
        5949 5950 0
        5950 5949 5949
        tile 5949 0 0
        tile 5950 56 0
        picture 13 0 0 90 0
        picture 20414 28 0 40 1
        object 2 1 left 13
        object 2 1 right 20414
        """;

    [Fact]
    public void A_wall_blocks_and_open_floor_does_not()
    {
        MapLayout layout = MapLayout.Read(Written);

        Assert.True(layout.Blocks(new Tile(1, 0)));
        Assert.False(layout.Blocks(new Tile(0, 0)));
        Assert.False(layout.Blocks(new Tile(2, 1)));
    }

    [Fact]
    public void Off_the_map_blocks_as_the_server_says()
    {
        // 서버 Area.IsWall 은 맵 밖을 벽으로 본다 — 한 걸음 내디뎠다 되돌려지는 대신 처음부터 서 있는다.
        MapLayout layout = MapLayout.Read(Written);

        Assert.True(layout.Blocks(new Tile(-1, 0)));
        Assert.True(layout.Blocks(new Tile(3, 0)));
        Assert.True(layout.Blocks(new Tile(0, 2)));
    }

    [Fact]
    public void Each_cell_names_its_floor_tile_and_an_empty_cell_names_none()
    {
        MapLayout layout = MapLayout.Read(Written);

        Assert.Equal(5950, layout.Floor(1, 0));
        Assert.Equal(5950, layout.Floor(0, 1));
        Assert.Equal(5949, layout.Floor(2, 1));
        Assert.Equal(0, layout.Floor(2, 0));
        Assert.Equal((56, 0), layout.Tiles[5950]);
    }

    [Fact]
    public void Objects_name_their_picture_and_half()
    {
        MapLayout layout = MapLayout.Read(Written);

        Assert.Equal((3, 2), (layout.Columns, layout.Rows));
        Assert.Equal(
            [new MapObject(2, 1, false, 13), new MapObject(2, 1, true, 20414)],
            layout.Objects);
        Assert.Equal(new MapPicture(28, 0, 40, true), layout.Pictures[20414]);
    }

    [Fact]
    public void A_left_picture_hangs_from_the_cell_s_bottom_corner_and_a_right_one_half_a_tile_over()
    {
        // da-lib Graphics.RenderMap: 왼쪽 그림의 왼쪽 끝은 칸의 왼쪽 꼭짓점, 오른쪽은 반 칸 더 가서, 둘 다 칸의 아래 꼭짓점에 매달린다.
        const int rows = 70;
        (int cornerX, int cornerY) = IsometricFloor.Corner(37, 29, rows);

        Assert.Equal((cornerX, cornerY + 26), IsometricFloor.ObjectFoot(37, 29, rows, right: false));
        Assert.Equal((cornerX + 28, cornerY + 26), IsometricFloor.ObjectFoot(37, 29, rows, right: true));
    }

    [Fact]
    public void Someone_behind_an_object_sorts_before_it_and_someone_in_front_after()
    {
        const int rows = 70;
        int foot = IsometricFloor.ObjectFoot(10, 10, rows, right: false).Y;

        Assert.True(IsometricFloor.Stand(10, 9, rows).Y < foot);
        Assert.True(IsometricFloor.Stand(10, 11, rows).Y > foot);
    }
}
