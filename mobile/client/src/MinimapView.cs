using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The minimap at the left end of the top row — a rectangle again, as large as the row lets it be (사용자, 2026-09-26: 둥근
/// 것은 너무 작아서 안 보인다 — 사각형으로, 크게, [+]·[−] 로 확대·축소). Inside is the 길 찾기 map's own drawing, centred on
/// us: the floor's flat diamond, walls in its light stone and floor in its dark (<see cref="TabMapPanel.WallPaint" />).
/// Everyone is one dot of two pixels in the 길 찾기 map's colours — monsters red, NPCs pale, party blue, the bot green,
/// others grey — exits small diamonds, and we are a white dot a little larger with a thin black rim. [+]·[−] in the right
/// corners step how many tiles show (<see cref="Minimap.Steps" />, 6~24, kept on the device); pressing the rest of it
/// opens the full 길 찾기 map.
/// </summary>
/// <remarks>
/// Light to draw: the floor is baked once per map into a picture of one pixel per tile, and each redraw lays that
/// picture down through the diamond transform (<see cref="Minimap.FromGrid" />) and the few dots over it — when we step and
/// four times a second for the others. (The round look's <see cref="Minimap.SeesRound" /> stays in the core, unused.)
/// </remarks>
public sealed partial class MinimapView : Button
{
    private readonly WorldView _world;
    private readonly WorldClient? _server;
    private readonly MapGuide _guide;
    private readonly Face _face;
    private readonly Rim _rim = new();

    private ImageTexture? _grid;
    private (int Map, bool Laid) _baked = (-2, false);
    private (Tile Where, Direction Facing, int Map) _drawnAt;
    private IReadOnlyList<TabMarker> _markers = [];
    private Tile? _botAt;

    /// <summary>The bot (0x5E) gets a colour of its own — the heal green, since healing is what it is for.</summary>
    public static readonly Color BotPaint = new("#72e07e");
    private double _since;

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
        TooltipText = "길 찾기";

        // 사각형 — 세로는 첫 줄의 남는 폭을 다 쓰고(최소 120) 높이 96, 가로는 176×76(가로 360 에서 위 줄이 80 을 넘으면 조작 줄을
        // 밀어낸다 — 그 안의 가장 큰 높이).
        CustomMinimumSize = Main.Portrait ? new Vector2(120, 96) : new Vector2(176, 76);
        SizeFlagsHorizontal = Main.Portrait ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        ClipContents = true;
        _face = new Face(this) { MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest };
        _face.SetAnchorsPreset(LayoutPreset.FullRect);
        _rim.MouseFilter = MouseFilterEnum.Ignore;
        _rim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_face);
        AddChild(_rim);

        // [+]·[−] — 오른쪽 위·아래 구석. 누르는 곳은 44 폭(높이는 판의 반까지), 그림은 작게. 이것을 누르면 길 창은 열리지 않는다
        // (아이 단추가 먼저 받는다).
        ZoomIn = Corner(GlyphKind.Plus, top: true);
        ZoomOut = Corner(GlyphKind.Minus, top: false);
        ZoomIn.Pressed += () => Zoom(Minimap.ZoomIn(Main.MinimapRadius));
        ZoomOut.Pressed += () => Zoom(Minimap.ZoomOut(Main.MinimapRadius));
    }

    /// <summary>[+] — 칸을 줄여 크게.</summary>
    public Button ZoomIn { get; }

    /// <summary>[−] — 칸을 늘려 작게.</summary>
    public Button ZoomOut { get; }

    private void Zoom(int radius)
    {
        Main.SetMinimapRadius(radius);
        GD.Print($"GREYBOX_MINIMAP_ZOOM {Main.MinimapRadius}칸");
        _face.QueueRedraw();
    }

    private Button Corner(GlyphKind kind, bool top)
    {
        float tall = Mathf.Min(Main.TouchMinimum, CustomMinimumSize.Y / 2);
        Button button = new() { Flat = true, FocusMode = FocusModeEnum.None, TooltipText = kind == GlyphKind.Plus ? "확대" : "축소" };
        StyleBoxEmpty none = new();

        foreach (string state in new[] { "normal", "hover", "focus", "pressed" })
        {
            button.AddThemeStyleboxOverride(state, none);
        }

        button.AnchorLeft = button.AnchorRight = 1;
        button.AnchorTop = button.AnchorBottom = top ? 0 : 1;
        button.OffsetLeft = -Main.TouchMinimum;
        button.OffsetRight = 0;
        button.OffsetTop = top ? 0 : -tall;
        button.OffsetBottom = top ? tall : 0;

        // 작은 둥근 판 위에 + / − — 지도 위에서도 보이게 어두운 판.
        Control chip = new ZoomChip(kind) { MouseFilter = MouseFilterEnum.Ignore };
        chip.AnchorLeft = chip.AnchorRight = 1;
        chip.AnchorTop = chip.AnchorBottom = top ? 0 : 1;
        chip.OffsetLeft = -20;
        chip.OffsetRight = -2;
        chip.OffsetTop = top ? 2 : -20;
        chip.OffsetBottom = top ? 20 : -2;
        button.AddChild(chip);
        AddChild(button);

        return button;
    }

    /// <summary>The little dark disc with + or − on it.</summary>
    private sealed partial class ZoomChip(GlyphKind kind) : Control
    {
        public override void _Draw()
        {
            Vector2 c = Size / 2;
            DrawCircle(c, (Size.X / 2) + 0.5f, new Color("#636357"));
            DrawCircle(c, (Size.X / 2) - 0.5f, new Color("#0f0f0f") with { A = 0.9f });
            Color paint = Greybox.Title;
            float r = Size.X * 0.28f;
            DrawLine(c - new Vector2(r, 0), c + new Vector2(r, 0), paint, 2, true);

            if (kind == GlyphKind.Plus)
            {
                DrawLine(c - new Vector2(0, r), c + new Vector2(0, r), paint, 2, true);
            }
        }
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
        TabMapProjection frame = Minimap.Frame(Standing, columns, rows, Size.X, Size.Y, Main.MinimapRadius);
        IReadOnlyList<TabMarker> seen = Minimap.InSight(frame, Size.X, Size.Y, _markers);
        string kinds = string.Join(" ", seen.GroupBy(one => one.Kind).Select(group => $"{group.Key}={group.Count()}"));

        return $"맵 {MapId} 나 {Standing.X},{Standing.Y} 반경 {Main.MinimapRadius} 칸 {frame.HalfWidth * 2:0.0}px 바닥 {(_grid is null ? "없음" : "있음")} 점 [{kinds}]";
    }

    private void Refresh()
    {
        if (_baked != (MapId, Layout is not null))
        {
            _baked = (MapId, Layout is not null);
            _grid = Bake(Layout);
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
            _face.QueueRedraw();
            return;
        }

        _botAt = _server?.Companion is { } bot && _server.Others.FirstOrDefault(other => other.Serial == bot.Serial) is { } seen
            ? seen.Where
            : null;

        _face.QueueRedraw();
    }

    /// <summary>
    /// One pixel per tile in the 길 찾기 map's colours: a wall its light stone, a floor its dark, off the laid floor clear.
    /// Once per map.
    /// </summary>
    private static ImageTexture? Bake(MapLayout? layout)
    {
        if (layout is null || layout.Columns == 0)
        {
            return null;
        }

        Image grid = Image.CreateEmpty(layout.Columns, layout.Rows, false, Image.Format.Rgba8);

        for (int row = 0; row < layout.Rows; row++)
        {
            for (int column = 0; column < layout.Columns; column++)
            {
                grid.SetPixel(column, row, layout.Blocks(new Tile(column, row)) ? TabMapPanel.WallPaint : TabMapPanel.FloorPaint);
            }
        }

        return ImageTexture.CreateFromImage(grid);
    }

    /// <summary>The map itself.</summary>
    private sealed partial class Face(MinimapView owner) : Control
    {
        public override void _Draw()
        {
            float wide = Size.X, high = Size.Y;
            DrawRect(new Rect2(Vector2.Zero, Size), TabMapPanel.Backdrop with { A = 0.9f });

            (int columns, int rows) = owner.Layout is { } layout ? (layout.Columns, layout.Rows) : owner._world.MapSize;

            if (columns == 0)
            {
                Font font = GetThemeDefaultFont();
                DrawString(font, new Vector2(0, (high / 2) + 4), "지도 없음", HorizontalAlignment.Center, wide, 11, Greybox.Muted);
                return;
            }

            TabMapProjection frame = Minimap.Frame(owner.Standing, columns, rows, wide, high, Main.MinimapRadius);

            if (owner._grid is not null)
            {
                // 한 칸 = 한 픽셀인 그림을 마름모로 눕혀 깐다(Minimap.FromGrid 와 같은 식).
                DrawSetTransformMatrix(new Transform2D(
                    new Vector2(frame.HalfWidth, frame.HalfHeight),
                    new Vector2(-frame.HalfWidth, frame.HalfHeight),
                    new Vector2(frame.OriginX, frame.OriginY)));
                DrawTexture(owner._grid, Vector2.Zero);
                DrawSetTransformMatrix(Transform2D.Identity);
            }

            // 걸어갈 길 — 한 픽셀 점.
            foreach (Tile step in owner._world.Route)
            {
                if (Minimap.Sees(frame, wide, high, step))
                {
                    DrawRect(new Rect2(At(frame, step) - new Vector2(0.5f, 0.5f), Vector2.One), Greybox.Accent);
                }
            }

            foreach (TabMarker marker in Minimap.InSight(frame, wide, high, owner._markers))
            {
                Color paint = TabMapPanel.Paint(marker.Kind);

                if (marker.Kind == TabMarkerKind.Exit)
                {
                    // 출구는 작은 마름모(반 칸) — 문이 어디인지는 보여야 한다.
                    foreach (Tile tile in marker.Goals.Where(tile => Minimap.Sees(frame, wide, high, tile)))
                    {
                        Vector2 at = At(frame, tile);
                        float w = frame.HalfWidth * 0.6f, h = frame.HalfHeight * 0.6f;
                        DrawColoredPolygon([at + new Vector2(0, -h), at + new Vector2(w, 0), at + new Vector2(0, h), at + new Vector2(-w, 0)], paint);
                    }

                    continue;
                }

                Dot(At(frame, marker.Where), paint);
            }

            if (owner._botAt is { } bot && Minimap.Sees(frame, wide, high, bot))
            {
                Dot(At(frame, bot), BotPaint);
            }

            // 나 — 흰 점, 조금 크게(3픽셀) + 검은 테 1픽셀. 다른 점과 한눈에 갈린다.
            Vector2 me = At(frame, owner.Standing);
            DrawRect(new Rect2(me - new Vector2(2.5f, 2.5f), new Vector2(5, 5)), Colors.Black);
            DrawRect(new Rect2(me - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), Colors.White);

            // 지금 곳 — 아래 왼쪽에 작게(위 줄의 곳 이름 판을 대신한다). 오른쪽 구석은 [+]·[−] 자리.
            if (owner.PlaceName.Length > 0)
            {
                Font font = GetThemeDefaultFont();
                Vector2 where = new(4, high - 4);
                DrawStringOutline(font, where, owner.PlaceName, HorizontalAlignment.Left, wide - 28, 10, 3, Colors.Black);
                DrawString(font, where, owner.PlaceName, HorizontalAlignment.Left, wide - 28, 10, Greybox.Title);
            }
        }

        /// <summary>Everyone else: a two-pixel dot.</summary>
        private void Dot(Vector2 at, Color paint) => DrawRect(new Rect2(at - Vector2.One, new Vector2(2, 2)), paint);

        private static Vector2 At(TabMapProjection frame, Tile tile)
        {
            (float x, float y) = frame.Centre(tile.X, tile.Y);

            return new Vector2(Mathf.Round(x), Mathf.Round(y));
        }
    }

    /// <summary>The edge, drawn over the map; brighter while pressed.</summary>
    private sealed partial class Rim : Control
    {
        public override void _Draw()
        {
            bool down = GetParent() is MinimapView view && view.IsPressed();
            DrawRect(new Rect2(new Vector2(0.5f, 0.5f), Size - Vector2.One), down ? Greybox.Title : Edge, false, 1);
        }

        public override void _Process(double delta)
        {
            if (GetParent() is MinimapView view && view.IsPressed() != _down)
            {
                _down = view.IsPressed();
                QueueRedraw();
            }
        }

        private bool _down;
    }
}
