using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The minimap that always stays up in the top row (사용자, 2026-09-26: 길은 모바일 게임 지도처럼 화면에 미니맵으로):
/// <see cref="Minimap.Radius" /> tiles round us in the floor's own diamond, walls in the original's light stone and each
/// floor tile in its own colour shrunk to a pixel, and on it the exits, NPCs, monsters, party and bot and others as dots,
/// with us as one tile's diamond and a tick the way we face. Pressing it opens the full 길 찾기 map.
/// </summary>
/// <remarks>
/// Light to draw: the floor is baked once per map into a picture of one pixel per tile, and each redraw only lays that
/// picture down through the diamond transform (<see cref="Minimap.FromGrid" />) and the few dots over it. It redraws when
/// we step and a few times a second for the others, never the whole grid again. A map with no floor picture shows the
/// walls only; a map with no layout at all says so.
/// </remarks>
public sealed partial class MinimapView : Button
{
    private readonly WorldView _world;
    private readonly WorldClient? _server;
    private readonly MapGuide _guide;

    private ImageTexture? _grid;
    private (int Map, bool Laid) _baked = (-2, false);
    private (Tile Where, Direction Facing, int Map) _drawnAt;
    private IReadOnlyList<TabMarker> _markers = [];
    private Tile? _botAt;

    /// <summary>The bot (0x5E) gets a colour of its own — the heal green, since healing is what it is for.</summary>
    public static readonly Color BotPaint = new("#72e07e");
    private double _since;

    private static readonly Color Wall = new("#8a8a7e");
    private static readonly Color Bare = new("#2a2a30");
    private static readonly Color Ground = new(0, 0, 0, 0.55f);
    private static readonly Color Edge = new("#636357");

    // --minimap 을 서버 없이(--screen game) 주면 노비스마을 (37,29)(새 캐릭터가 서는 곳)에 선 셈 치고 그린다 — 사진·배치 검사용.
    private const int PretendMap = 20373;
    private static readonly Tile PretendAt = new(37, 29);
    private readonly MapLayout? _pretend;

    private MapLayout? Layout => _pretend ?? _world.Layout;

    private int MapId => _pretend is not null ? PretendMap : _world.MapId;

    private Tile Standing => _pretend is not null ? PretendAt : _world.Standing;

    private Direction Looking => _pretend is not null ? Direction.South : _world.Looking;

    private string PlaceName => _pretend is not null ? "노비스마을" : _world.PlaceName;

    public MinimapView(WorldView world, WorldClient? server, MapGuide guide)
    {
        _world = world;
        _server = server;
        _guide = guide;

        const string pretendPath = "res://assets/world/map20373.txt";

        if (server is null && Main.CheckingMinimap && Godot.FileAccess.FileExists(pretendPath))
        {
            _pretend = MapLayout.Read(Godot.FileAccess.GetFileAsString(pretendPath));
        }

        Name = "Minimap";
        Flat = true;
        FocusMode = FocusModeEnum.None;
        ClipContents = true;
        TextureFilter = TextureFilterEnum.Nearest;
        TooltipText = "길 찾기";
        // 세로는 위 줄 첫 줄의 남는 폭을 다 쓴다(GameScreen) — 최소 폭만 정한다. 높이는 내 판(세 줄)과 비슷하게.
        CustomMinimumSize = Main.Portrait ? new Vector2(96, 76) : new Vector2(128, 64);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            return;
        }

        // 걸으면 바로, 다른 이들은 0.25초마다.
        bool moved = _drawnAt != (Standing, Looking, MapId);

        if (moved || (_since += delta) >= 0.25)
        {
            _since = 0;
            _drawnAt = (Standing, Looking, MapId);
            Refresh();
        }
    }

    /// <summary>What it shows, for <c>--minimap</c>: the map, where we stand, and how many dots of each kind are in sight.</summary>
    public string Describe()
    {
        (int columns, int rows) = Layout is { } layout ? (layout.Columns, layout.Rows) : _world.MapSize;
        TabMapProjection frame = Minimap.Frame(Standing, columns, rows, Size.X, Size.Y);
        IReadOnlyList<TabMarker> seen = Minimap.InSight(frame, Size.X, Size.Y, _markers);
        string kinds = string.Join(" ", seen.GroupBy(one => one.Kind).Select(group => $"{group.Key}={group.Count()}"));

        return $"맵 {MapId} 나 {Standing.X},{Standing.Y} 칸 {frame.HalfWidth * 2:0.0}px 바닥 {(_grid is null ? "없음" : "있음")} 점 [{kinds}]";
    }

    private void Refresh()
    {
        if (_baked != (MapId, Layout is not null))
        {
            _baked = (MapId, Layout is not null);
            _grid = Bake(Layout, MapId);
        }

        _markers = TabMap.Markers(
            Standing,
            _server?.Others ?? [],
            _server?.Creatures ?? [],
            _server?.Roster.Members.Select(member => member.Name) ?? [],
            _guide.ExitsOn(MapId),
            _guide.SignsOn(MapId));

        if (_pretend is not null)
        {
            // 서버 없는 사진용 — 괴물 둘 · 파티원 하나 · 사람 하나 · 봇 하나를 지어 둔다(색 구분을 보이려고).
            _markers =
            [
                .. _markers,
                new TabMarker(new Tile(41, 31), TabMarkerKind.Monster, string.Empty, []),
                new TabMarker(new Tile(33, 33), TabMarkerKind.Monster, string.Empty, []),
                new TabMarker(new Tile(36, 26), TabMarkerKind.Party, "파티원", []),
                new TabMarker(new Tile(42, 27), TabMarkerKind.Person, string.Empty, []),
            ];
            _botAt = new Tile(38, 30);
            QueueRedraw();
            return;
        }

        _botAt = _server?.Companion is { } bot && _server.Others.FirstOrDefault(other => other.Serial == bot.Serial) is { } seen
            ? seen.Where
            : null;

        QueueRedraw();
    }

    /// <summary>
    /// One pixel per tile: a wall is light stone, a floor tile is that tile's own colour (the middle of its picture on
    /// the floor sheet) darkened so the dots stand out, a cell with nothing laid is left clear. Once per map.
    /// </summary>
    private static ImageTexture? Bake(MapLayout? layout, int mapId)
    {
        if (layout is null || layout.Columns == 0)
        {
            return null;
        }

        Image? sheet = null;
        string floorPath = $"res://assets/world/map{mapId}-floor.png";

        if (ResourceLoader.Exists(floorPath) && GD.Load<Texture2D>(floorPath)?.GetImage() is { } image)
        {
            if (image.IsCompressed())
            {
                image.Decompress();
            }

            sheet = image;
        }

        Dictionary<int, Color> colours = [];
        Image grid = Image.CreateEmpty(layout.Columns, layout.Rows, false, Image.Format.Rgba8);

        for (int row = 0; row < layout.Rows; row++)
        {
            for (int column = 0; column < layout.Columns; column++)
            {
                Color paint;

                if (layout.Blocks(new Tile(column, row)))
                {
                    paint = Wall;
                }
                else if (sheet is null)
                {
                    paint = Bare;
                }
                else
                {
                    int tile = layout.Floor(column, row);

                    if (!colours.TryGetValue(tile, out paint))
                    {
                        paint = layout.Tiles.TryGetValue(tile, out (int X, int Y) at)
                            ? Sample(sheet, at.X + (IsometricFloor.TileWidth / 2), at.Y + (IsometricFloor.TileHeight / 2))
                            : new Color(0, 0, 0, 0);
                        colours[tile] = paint;
                    }
                }

                grid.SetPixel(column, row, paint);
            }
        }

        return ImageTexture.CreateFromImage(grid);
    }

    /// <summary>A few pixels round a tile's middle, averaged and darkened — one colour for the whole tile.</summary>
    private static Color Sample(Image sheet, int x, int y)
    {
        Color sum = new(0, 0, 0, 0);
        int count = 0;

        foreach ((int dx, int dy) in new[] { (0, 0), (-8, 0), (8, 0), (0, -4), (0, 4) })
        {
            int px = Math.Clamp(x + dx, 0, sheet.GetWidth() - 1);
            int py = Math.Clamp(y + dy, 0, sheet.GetHeight() - 1);
            Color one = sheet.GetPixel(px, py);
            sum = new Color(sum.R + one.R, sum.G + one.G, sum.B + one.B, sum.A + one.A);
            count++;
        }

        Color mean = new(sum.R / count, sum.G / count, sum.B / count, 1);

        return mean.Darkened(0.35f);
    }

    public override void _Draw()
    {
        Vector2 box = Size;
        DrawRect(new Rect2(Vector2.Zero, box), Ground);

        (int columns, int rows) = Layout is { } layout ? (layout.Columns, layout.Rows) : _world.MapSize;

        if (columns == 0)
        {
            DrawString(GetThemeDefaultFont(), new Vector2(6, (box.Y / 2) + 4), "지도 없음", HorizontalAlignment.Left, -1, 11, Greybox.Muted);
            DrawBorder(box);
            return;
        }

        TabMapProjection frame = Minimap.Frame(Standing, columns, rows, box.X, box.Y);

        if (_grid is not null)
        {
            // 한 칸 = 한 픽셀인 그림을 마름모로 눕혀 깐다(Minimap.FromGrid 와 같은 식).
            DrawSetTransformMatrix(new Transform2D(
                new Vector2(frame.HalfWidth, frame.HalfHeight),
                new Vector2(-frame.HalfWidth, frame.HalfHeight),
                new Vector2(frame.OriginX, frame.OriginY)));
            DrawTexture(_grid, Vector2.Zero);
            DrawSetTransformMatrix(Transform2D.Identity);
        }

        // 걸어갈 길 — 점으로.
        foreach (Tile step in _world.Route)
        {
            if (Minimap.Sees(frame, box.X, box.Y, step))
            {
                DrawCircle(At(frame, step), 1.5f, Greybox.Accent);
            }
        }

        float dot = Math.Clamp(frame.HalfHeight * 0.8f, 2f, 3.5f);

        foreach (TabMarker marker in Minimap.InSight(frame, box.X, box.Y, _markers))
        {
            Color paint = TabMapPanel.Paint(marker.Kind);

            if (marker.Kind == TabMarkerKind.Exit)
            {
                foreach (Tile tile in marker.Goals.Where(tile => Minimap.Sees(frame, box.X, box.Y, tile)))
                {
                    Diamond(frame, tile, paint, 1f);
                }

                continue;
            }

            Vector2 at = At(frame, marker.Where);
            DrawCircle(at, dot + 1, Colors.Black);
            DrawCircle(at, dot, paint);
        }

        if (_botAt is { } botAt && Minimap.Sees(frame, box.X, box.Y, botAt))
        {
            Vector2 at = At(frame, botAt);
            DrawCircle(at, dot + 1, Colors.Black);
            DrawCircle(at, dot, BotPaint);
        }

        // 나 — 한 칸 크기의 마름모, 검은 테두리, 보는 쪽으로 짧은 줄.
        Diamond(frame, Standing, Colors.Black, 1.35f);
        Diamond(frame, Standing, Colors.White, 1f);
        (float fx, float fy) = frame.Toward(Looking);
        Vector2 me = At(frame, Standing);
        DrawLine(me, me + (new Vector2(fx, fy) * frame.HalfWidth * 1.8f), Colors.White, 2, true);

        // 지금 곳 — 아래 왼쪽에 작게.
        if (PlaceName.Length > 0)
        {
            Font font = GetThemeDefaultFont();
            Vector2 where = new(4, box.Y - 4);
            DrawStringOutline(font, where, PlaceName, HorizontalAlignment.Left, box.X - 8, 10, 3, Colors.Black);
            DrawString(font, where, PlaceName, HorizontalAlignment.Left, box.X - 8, 10, Greybox.Title);
        }

        DrawBorder(box);
    }

    private void DrawBorder(Vector2 box) =>
        DrawRect(new Rect2(new Vector2(0.5f, 0.5f), box - Vector2.One), ButtonPressed || IsPressed() ? Greybox.Title : Edge, false, 1);

    private void Diamond(TabMapProjection frame, Tile tile, Color paint, float grow)
    {
        Vector2 at = At(frame, tile);
        float w = frame.HalfWidth * grow, h = frame.HalfHeight * grow;
        DrawColoredPolygon([at + new Vector2(0, -h), at + new Vector2(w, 0), at + new Vector2(0, h), at + new Vector2(-w, 0)], paint);
    }

    private static Vector2 At(TabMapProjection frame, Tile tile)
    {
        (float x, float y) = frame.Centre(tile.X, tile.Y);

        return new Vector2(x, y);
    }
}
