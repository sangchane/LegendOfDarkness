using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 길 찾기 창 — 원작에서 Tab 을 누르면 뜨던 지형 지도(<c>Legend.exe</c> <c>MapViewPane</c>, 2005 판 <c>setoa.dat</c> 의
/// <c>TabMap</c> 단추)를 엄지로 쓰게 다시 만든 것. 위 줄 [지도](월드맵 — 다른 곳으로 가기)와 다르다: 이것은 지금 선 맵 안의 길이다.
/// </summary>
/// <remarks>
/// 벽 · 걸을 수 있는 곳 · 출구(간 곳 이름) · NPC(이름) · 괴물 · 파티 · 다른 사람 · 나(보는 쪽)를 그린다. 출구나 NPC 를 누르면
/// 그리로 걷기 시작하고 길이 지도와 바닥 둘 다에 그려진다. 창은 열린 채로 두어도 걸음은 계속된다(월드를 멈추지 않는다) —
/// [닫기] 한 번으로 닫고, 방향판을 누르면 길 안내가 멈춘다. 계산(투영·분류·길)은 알맹이 <see cref="TabMap" /> 에 있다.
/// </remarks>
public sealed partial class TabMapPanel : PanelContainer
{
    private readonly WorldView _world;
    private readonly WorldClient? _server;
    private readonly MapGuide _guide;
    private readonly Label _title = new() { Text = "길 찾기" };
    private readonly Canvas _canvas;
    private double _since;

    // 거절 문구("갈 수 없습니다")를 제목 자리에 잠깐 띄운다.
    private string _said = string.Empty;
    private double _saidFor;

    /// <summary>
    /// How much larger than "the whole map" it is drawn, round us. A 70×70 town fitted into a phone box leaves each tile
    /// a few pixels across — enough to see the shape of the place, too small to aim at a door.
    /// </summary>
    private const float Near = 2.5f;

    public TabMapPanel(WorldView world, WorldClient? server, MapGuide guide)
    {
        _world = world;
        _server = server;
        _guide = guide;
        _canvas = new Canvas(this);

        Name = "TabMap";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Sheet());

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter / 2);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // 긴 제목(곳 이름 + 안내)이 창을 밀어 세로 화면에서 닫기가 화면 밖으로 나갔다 — 두 줄까지 접고, 넘치면 줄임표로 자른다.
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _title.MaxLinesVisible = 2;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _title.CustomMinimumSize = new Vector2(Main.TouchMinimum, 0);
        _title.AddThemeFontSizeOverride("font_size", 14);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.AddThemeColorOverride("font_color", Greybox.Title);
        head.AddChild(_title);

        Zoom = new Button { Text = "확대", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        Greybox.Plain(Zoom);
        Zoom.Pressed += () =>
        {
            _canvas.Zoom = _canvas.Zoom > 1 ? 1 : Near;
            Zoom.Text = _canvas.Zoom > 1 ? "전체" : "확대";
            _canvas.Refresh();
        };
        head.AddChild(Zoom);

        Stop = new Button { Text = "멈춤", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum), Visible = false };
        Greybox.Plain(Stop);
        Stop.Pressed += () => _world.StopGuiding();
        head.AddChild(Stop);

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        Greybox.Plain(Close);
        head.AddChild(Close);

        _canvas.SizeFlagsVertical = SizeFlags.ExpandFill;
        _canvas.CustomMinimumSize = new Vector2(0, Main.Portrait ? 320 : 160);

        inside.AddChild(Greybox.Header(head));
        inside.AddChild(_canvas);
        inside.AddChild(Legend());

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", Main.Gutter);
        margin.AddThemeConstantOverride("margin_right", Main.Gutter);
        margin.AddThemeConstantOverride("margin_top", Main.Gutter / 2);
        margin.AddThemeConstantOverride("margin_bottom", Main.Gutter);
        margin.AddChild(inside);

        AddChild(margin);
    }

    public Button Close { get; }

    public Button Stop { get; }

    public Button Zoom { get; }

    /// <summary>What each kind of dot looks like. The only strong colours are the original's two beads and the red of "gone".</summary>
    private static Color Paint(TabMarkerKind kind) => kind switch
    {
        TabMarkerKind.Monster => Greybox.Gone,
        TabMarkerKind.Person => Greybox.Muted,
        TabMarkerKind.Npc => new Color("#e8e2c8"),
        TabMarkerKind.Party => new Color("#7f95d0"),
        TabMarkerKind.Exit => Greybox.Accent,
        _ => Colors.White
    };

    /// <summary>Opens on the map we stand on.</summary>
    public void Open()
    {
        Visible = true;
        _since = 1;
    }

    /// <summary>
    /// A tap at a point on the drawn map, as a finger gives it. Also what a hands-free run calls, so it goes through
    /// the very same arithmetic.
    /// </summary>
    public void TapAt(Vector2 at)
    {
        TabMapProjection map = _canvas.Projection;

        if (TabMap.Pick(map, at.X, at.Y, _canvas.Markers, _world.Blocked) is not { } goal)
        {
            Say("그곳으로는 갈 수 없습니다");
            return;
        }

        if (!_world.Guide(goal))
        {
            Say(goal.Label.Length > 0 ? $"{goal.Label} — 가는 길이 없습니다" : "그곳으로 가는 길이 없습니다");
            return;
        }

        GD.Print($"GREYBOX_TABMAP_GO {(goal.Label.Length > 0 ? goal.Label : "바닥")} {_world.Route.Count}걸음 → {_world.Route[^1]}");
        _canvas.QueueRedraw();
    }

    /// <summary>Where on the drawn map the marker with this label stands, for a hands-free tap. Null when none does.</summary>
    public Vector2? PointOf(string label)
    {
        TabMarker? marker = _canvas.Markers.FirstOrDefault(one => one.Label.Contains(label, StringComparison.Ordinal)
            && one.Kind is TabMarkerKind.Exit or TabMarkerKind.Npc);

        if (marker is null)
        {
            return null;
        }

        (float x, float y) = _canvas.Projection.Centre(marker.Where.X, marker.Where.Y);

        return new Vector2(x, y);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        // 제목 한 줄이 곧 안내다: 평소에는 곳 이름과 "누르면 걷는다", 걷는 동안은 간 곳과 남은 걸음, 거절되면 그 까닭.
        string place = _world.PlaceName;
        Stop.Visible = _world.Guiding is not null;
        _title.AddThemeColorOverride("font_color", Greybox.Title);

        if ((_saidFor -= delta) > 0)
        {
            _title.Text = _said;
            _title.AddThemeColorOverride("font_color", Greybox.Gone);
        }
        else if (_world.Guiding is { } going && _world.Route.Count > 0)
        {
            _title.Text = $"→ {(going.Length > 0 ? going : "고른 자리")}\n{_world.Route.Count}걸음 남음";
            _title.AddThemeColorOverride("font_color", Greybox.Accent);
        }
        else
        {
            _title.Text = place.Length > 0 ? $"{place}\n누르면 그리로 걷습니다" : "길 찾기";
        }

        // 사람·괴물은 움직이니 자주 다시 그린다. 매 프레임은 필요 없다.
        if ((_since += delta) >= 0.2)
        {
            _since = 0;
            _canvas.Refresh();
        }
    }

    private void Say(string words)
    {
        _said = words;
        _saidFor = 2.5;
    }

    private Control Legend()
    {
        HFlowContainer row = new();
        row.AddThemeConstantOverride("h_separation", Main.Gutter + 2);

        foreach ((TabMarkerKind kind, string words) in new[]
                 {
                     (TabMarkerKind.Me, "나"), (TabMarkerKind.Exit, "출구"), (TabMarkerKind.Npc, "NPC"),
                     (TabMarkerKind.Monster, "괴물"), (TabMarkerKind.Party, "파티"), (TabMarkerKind.Person, "사람")
                 })
        {
            Label key = new() { Text = $"● {words}" };
            key.AddThemeColorOverride("font_color", Paint(kind));
            key.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(key);
        }

        return row;
    }

    /// <summary>The drawn map itself: the floor baked once per map and size, the dots and the way drawn over it.</summary>
    private sealed partial class Canvas(TabMapPanel owner) : Control
    {
        private ImageTexture? _floor;
        private (int Map, TabMapProjection Projection, bool Drawn) _baked = (-2, default, false);

        public TabMapProjection Projection { get; private set; }

        /// <summary>1 is the whole map; more is that much larger, round us.</summary>
        public float Zoom { get; set; } = 1;

        public IReadOnlyList<TabMarker> Markers { get; private set; } = [];

        public override void _Ready()
        {
            ClipContents = true;
            MouseFilter = MouseFilterEnum.Stop;
            Resized += Refresh;
        }

        public override void _GuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                    owner.TapAt(click.Position);
                    AcceptEvent();
                    break;

                case InputEventScreenTouch { Pressed: true } touch:
                    owner.TapAt(touch.Position);
                    AcceptEvent();
                    break;
            }
        }

        public void Refresh()
        {
            WorldView world = owner._world;
            (int columns, int rows) = world.Layout is { } layout ? (layout.Columns, layout.Rows) : world.MapSize;

            if (columns == 0 || Size.X < 1 || Size.Y < 1)
            {
                Markers = [];
                QueueRedraw();
                return;
            }

            Projection = TabMapProjection.Fit(columns, rows, Size.X, Size.Y);

            if (Zoom > 1)
            {
                Projection = Projection.Zoomed(Zoom, world.Standing.X, world.Standing.Y, Size.X, Size.Y);
            }

            if (_baked != (world.MapId, Projection, world.Layout is not null))
            {
                _baked = (world.MapId, Projection, world.Layout is not null);
                _floor = Bake(world, Projection, Size);
            }

            WorldClient? server = owner._server;
            Markers = TabMap.Markers(
                world.Standing,
                server?.Others ?? [],
                server?.Creatures ?? [],
                server?.Roster.Members.Select(member => member.Name) ?? [],
                owner._guide.ExitsOn(world.MapId),
                owner._guide.SignsOn(world.MapId));

            QueueRedraw();
        }

        /// <summary>
        /// One picture of the floor: walls as the original's light stone, floor a shade above the window, nothing off
        /// the map. Every pixel asks which tile it is on — a few hundred thousand questions, once per map.
        /// </summary>
        private static ImageTexture Bake(WorldView world, TabMapProjection map, Vector2 size)
        {
            int width = Math.Max(1, (int)Math.Ceiling(size.X));
            int height = Math.Max(1, (int)Math.Ceiling(size.Y));
            byte[] pixels = new byte[width * height * 4];
            byte[] wall = [0x8a, 0x8a, 0x7e, 0xff];
            byte[] floor = [0x2a, 0x2a, 0x30, 0xff];
            byte[] unknown = [0x1c, 0x1c, 0x20, 0xff];
            MapLayout? layout = world.Layout;

            // 칸마다 Godot 에 한 번씩 묻지 않고 바이트로 채워 한 번에 넘긴다 — 확대하면 걸을 때마다 다시 굽는다.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map.TileAt(x + 0.5f, y + 0.5f) is not (int column, int row))
                    {
                        continue;
                    }

                    byte[] paint = layout is null ? unknown : layout.Blocks(new Tile(column, row)) ? wall : floor;
                    Buffer.BlockCopy(paint, 0, pixels, ((y * width) + x) * 4, 4);
                }
            }

            return ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels));
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("#0f0f0f"));

            if (Markers.Count == 0)
            {
                DrawString(ThemeDB.FallbackFont, new Vector2(12, 24), "이 맵은 아직 지도가 없습니다.", HorizontalAlignment.Left, -1, 14, Greybox.Muted);
                return;
            }

            if (_floor is not null)
            {
                DrawTexture(_floor, Vector2.Zero);
            }

            TabMapProjection map = Projection;
            float dot = Math.Clamp(map.HalfWidth * 0.9f, 2.5f, 5f);
            WorldView world = owner._world;

            // 걸어갈 길 — 선과 끝 고리.
            if (world.Route.Count > 0)
            {
                List<Vector2> line = [At(map, world.Standing)];
                line.AddRange(world.Route.Select(tile => At(map, tile)));
                DrawPolyline([.. line], Greybox.Accent, 3, true);
                DrawArc(line[^1], dot + 5, 0, Mathf.Tau, 24, Greybox.Accent, 2, true);
            }

            Font font = ThemeDB.FallbackFont;

            foreach (TabMarker marker in Markers)
            {
                Vector2 at = At(map, marker.Where);
                Color paint = Paint(marker.Kind);

                switch (marker.Kind)
                {
                    case TabMarkerKind.Exit:
                        foreach (Tile tile in marker.Goals)
                        {
                            Diamond(map, tile, paint);
                        }

                        break;

                    case TabMarkerKind.Me:
                        // 나는 이름들 위에 맨 나중에 그린다 — 출구 이름이 화살표를 덮었다.
                        break;

                    case TabMarkerKind.Npc:
                        DrawCircle(at, dot + 1, Colors.Black);
                        DrawCircle(at, dot, paint);
                        DrawArc(at, dot + 3, 0, Mathf.Tau, 16, paint, 1, true);
                        break;

                    default:
                        DrawCircle(at, dot * 0.8f + 1, Colors.Black);
                        DrawCircle(at, dot * 0.8f, paint);
                        break;
                }
            }

            // 이름은 점을 다 그린 뒤 — 점이 글자를 가리지 않게. 출구가 먼저 자리를 잡고, 겹치는 이름은 건너뛴다(점은 남는다).
            List<Rect2> taken = [];

            foreach (TabMarker marker in Markers.Where(one => one.Label.Length > 0).OrderByDescending(one => one.Kind == TabMarkerKind.Exit))
            {
                Vector2 at = At(map, marker.Where);
                int fontSize = marker.Kind == TabMarkerKind.Exit ? 13 : 12;
                Vector2 size = font.GetStringSize(marker.Label, HorizontalAlignment.Left, -1, fontSize);
                float x = Math.Clamp(at.X - (size.X / 2), 2, Math.Max(2, Size.X - size.X - 2));
                float y = Math.Clamp(at.Y - dot - 5, size.Y, Size.Y - 2);
                Vector2 where = new(x, y);
                Rect2 box = new(new Vector2(x - 2, y - size.Y + 2), size + new Vector2(4, 0));

                if (taken.Any(other => other.Intersects(box)))
                {
                    continue;
                }

                taken.Add(box);
                DrawStringOutline(font, where, marker.Label, HorizontalAlignment.Left, -1, fontSize, 4, Colors.Black);
                DrawString(font, where, marker.Label, HorizontalAlignment.Left, -1, fontSize, Paint(marker.Kind));
            }

            // 나 — 보는 쪽을 가리키는 화살표. 검은 테두리 안에 흰 속.
            Vector2 me = At(map, world.Standing);
            (float fx, float fy) = map.Toward(world.Looking);
            Vector2 ahead = new(fx, fy);
            Vector2 side = new(-fy, fx);
            float reach = dot + 5;
            Vector2[] arrow = [me + (ahead * reach * 1.4f), me - (ahead * reach * 0.7f) + (side * reach * 0.8f), me - (ahead * reach * 0.7f) - (side * reach * 0.8f)];
            DrawColoredPolygon(arrow, Colors.Black);
            DrawColoredPolygon([.. arrow.Select(point => me + ((point - me) * 0.7f))], Paint(TabMarkerKind.Me));
        }

        private void Diamond(TabMapProjection map, Tile tile, Color paint)
        {
            Vector2 at = At(map, tile);
            float w = Math.Max(map.HalfWidth, 3), h = Math.Max(map.HalfHeight, 3);
            DrawColoredPolygon([at + new Vector2(0, -h), at + new Vector2(w, 0), at + new Vector2(0, h), at + new Vector2(-w, 0)], paint);
        }

        private static Vector2 At(TabMapProjection map, Tile tile)
        {
            (float x, float y) = map.Centre(tile.X, tile.Y);

            return new Vector2(x, y);
        }
    }
}
