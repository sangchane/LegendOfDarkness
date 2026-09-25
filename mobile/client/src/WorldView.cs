using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The floor and everyone standing on it, seen through a window that follows the player. Every picture here
/// was drawn out of this repository's own archives by scripts/build-client-assets.ps1.
/// </summary>
public sealed partial class WorldView(WorldClient? server = null) : Control
{
    private const string FloorPath = "res://assets/world/safehouse.png";

    /// <summary>Where the per-map floor pictures live, named <c>map&lt;번호&gt;.png</c>.</summary>
    private const string FloorFolder = "res://assets/world/";
    private const string HeroSheet = "res://assets/actor/hero-walk.png";
    private const string HeroStrikeSheet = "res://assets/actor/hero-attack.png";

    // Worn by anyone the server has not described — somebody we have only ever seen take a step.
    private const string OtherSheet = "res://assets/actor/npc-walk.png";

    // One drawing per wardrobe piece, all cut on the same cell, so they stack without arithmetic.
    private const string PartsFolder = "res://assets/actor/parts/";

    // Monsters, named by the number the server calls them. It counts from 0x4000; the archive counts from 1.
    private const string CreatureFolder = "res://assets/actor/creature/";
    private const int CreatureNumbering = 0x4000;

    /// <summary>How long one tile takes to walk, and how many frames that walk is drawn in — 30% slower than the
    /// original 0.28 so the walk can actually be seen on a phone (사용자, 2026-09-24).</summary>
    private const double StepSeconds = 0.4;

    // Y sorting is what makes someone standing in front actually draw in front, which an isometric floor
    // needs: screen height is depth here.
    private readonly Node2D _camera = new() { Name = "Camera", YSortEnabled = true };
    private readonly Sprite2D _floor = new() { Name = "Floor", Centered = false };

    // 맞은 만큼·채운 만큼 떠오르는 숫자(0x5D) — 한 노드가 전부 그린다.
    private readonly FigureLayer _figures = new();

    // 맵을 타일로 맞춰 까는 바닥(map<번호>.txt 가 있을 때). 한 번 그려 두면 Godot 가 명령을 들고 있다가 다시 쓴다.
    private readonly Node2D _tiledFloor = new() { Name = "TiledFloor" };
    private Texture2D? _floorSheet;

    private readonly CancellationTokenSource _leaving = new();

    private Actor _player = null!;

    // Our own figure starts in borrowed clothes because the server has not spoken yet.
    // 우리 자신이 마지막으로 그려졌을 때 입고 있던 것. 아직 아무것도 못 들었으면 null 이 아니라
    // 없음이라, 사전과 달리 값 하나로 둔다.
    private Appearance? _wearingOwn;
    private bool _dressedOnce;
    private Vector2 _floorSize;

    /// <summary>Which map's picture is down, so it is only swapped when the map actually changes.</summary>
    private int _floored = -1;

    /// <summary>The walls and what stands on this map, when its layout was drawn out (<see cref="StandObjects" />).</summary>
    private MapLayout? _layout;

    // 건물·나무 하나하나. 맵이 바뀌면 통째로 치운다.
    private readonly List<Sprite2D> _objects = [];

    // sotp.dat 에 투명 표시가 붙은 그림(샘물 반짝임 …)은 가리지 않고 빛을 더한다 — 맵 편집기가 그렇게 그린다.
    private readonly CanvasItemMaterial _glow = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    // 바닥 색 돌림(물결). 원작 Legend.exe 는 100ms 마다 mpt%04d.tbl 의 색 구간(처음 끝 빠르기)을 한 칸씩 돌린다 —
    // 끝 칸 색이 처음으로, 나머지는 한 칸 위로. 빠르기는 몇 번 만에 한 번 돌리나(0x5df450). 돌 점은 -floor-cycle.png 가
    // (빨강 = 팔레트 칸, 초록 = 줄) 짚고, -floor-cycle-colours.png 가 줄마다 원래 256색과 그 아래 구간(처음·길이·빠르기)을 든다.
    // 돌 점이 없는 맵에는 이 재료를 붙이지 않는다. 알맹이 없는 계산이라 시험은 tools/tests 의 PaletteCyclesTests 가 한다.
    private static readonly Shader FloorCycle = new()
    {
        Code = """
            shader_type canvas_item;
            uniform sampler2D marks : filter_nearest;
            uniform sampler2D colours : filter_nearest;

            void fragment() {
                vec4 mark = texture(marks, UV);
                if (mark.a > 0.5) {
                    int index = int(round(mark.r * 255.0));
                    int row = int(round(mark.g * 255.0)) * 2;
                    ivec3 run = ivec3(round(texelFetch(colours, ivec2(index, row + 1), 0).rgb * 255.0));
                    int turns = (int(floor(TIME * 10.0)) / max(run.z, 1)) % run.y;
                    int shown = run.x + (index - run.x - turns + run.y) % run.y;
                    COLOR.rgb = texelFetch(colours, ivec2(shown, row), 0).rgb;
                }
            }
            """
    };

    // The tile we believe we are on. A walk moves it straight away, because the server answers an allowed
    // step with silence; when it does speak, it wins.
    private Tile _tile;
    private int _heard = -1;
    private int _rows = 31;

    // Everyone the server has shown us, by the serial it calls them.
    private readonly Dictionary<uint, Actor> _crowd = [];

    // 마지막으로 그렸을 때 무엇을 입고 있었는지. 서버가 갈아입은 사람을 다시 알려 주면 이것과
    // 달라지고, 그때만 그림을 새로 짓는다 — 매 프레임 다시 지으면 걸음이 끊긴다.
    private readonly Dictionary<uint, Appearance?> _worn = [];

    // Everything else standing on the floor — monsters, merchants — by the same kind of serial.
    private readonly Dictionary<uint, Actor> _herd = [];

    // And what is lying on it. Marked rather than drawn: nothing has been cut out of the icon archive yet.
    private readonly Dictionary<uint, GroundMark> _dropped = [];

    // A floor item stays in the server snapshot until its pickup answer arrives.  Keep that wait out of
    // the frame loop, or a full bag would send dozens of identical requests per second.
    private readonly AutoLootGate _autoLoot = new();

    // The server has no cooldown on using an item either; this waits for each drink to be answered.
    private readonly AutoPotion _potion = new();

    // 그림이 없는 NPC(팩의 스크립트 NPC)가 선 자리의 표식. 번호로 골라 말을 건다.
    private readonly Dictionary<uint, NpcMark> _signs = [];
    private readonly List<AudioStreamPlayer> _voices = [];

    // Whoever is picked out, and the mark that says so. Zero is nobody.
    private readonly TargetMark _mark = new() { Name = "Target", Visible = false };
    private uint _target;
    private bool _rehearsedPick;
    private int _settling;
    private int _rehearsedStrike = -1;

    private Queue<Direction> _rehearsal = new();

    /// <summary>
    /// 내 평타를 어떤 몸 동작으로 그리나. 서버가 한 번 말해 주면 그 뒤로는 기다리지 않고 이것으로 그린다.
    /// 아직 못 들었으면 일반 휘두르기다. 공격 단추에 대한 대답만 듣는다 — 주문 자세를 평타로 배우지 않게.
    /// </summary>
    private readonly OwnBlow _ownBlow = new();

    /// <summary>Frames since the hunt started. Everything it does is paced off this rather than a timer.</summary>
    private int _hunted;

    /// <summary>Which way it walks while nothing is in sight.</summary>
    private Direction _heading = Direction.North;

    /// <summary>Where the last roaming step started, so a step that did not land can be noticed.</summary>
    private Tile _roamedFrom;

    /// <summary>한 번 휘두르는 간격(프레임). 60프레임이 1초다.</summary>
    private const int SwingFrames = 40;

    /// <summary>점수를 한 점 쓰는 간격(프레임).</summary>

    /// <summary>쫓는 중에 자리가 이만큼 그대로면 그 괴물은 갈 수 없는 곳에 있다고 본다.</summary>
    private const int StuckTicks = 40;

    /// <summary>쫓기 시작한 자리. 서버가 말한 칸이다 — 이 클라이언트가 믿는 칸이 아니다.</summary>
    private Tile _chasedFrom;

    private int _stuck;

    private Vector2 _from;
    private Vector2 _to;
    private double _walked = -1;
    private int _drawnFrame = -1;

    /// <summary>
    /// While something is laid over the world — the pack, say — the floor takes neither taps nor steps.
    /// The world keeps running underneath: other people still walk about.
    /// </summary>
    public bool Frozen { get; set; }

    /// <summary>The tile the player is on, as this client believes it — which is what a player wants shown.</summary>
    public Tile Standing => _tile;

    /// <summary>Which way we face — the arrow on the 길 찾기 map.</summary>
    public Direction Looking => _player.Looking;

    /// <summary>This map's walls, when its layout was drawn out. The 길 찾기 map draws these.</summary>
    public MapLayout? Layout => _layout;

    /// <summary>Which map we are on, by the server's number, or -1 before it has said.</summary>
    public int MapId => server?.State?.Map.Id ?? -1;

    /// <summary>How big the server says this map is, for when no layout was drawn out.</summary>
    public (int Columns, int Rows) MapSize => server?.State?.Map is { } map ? (map.Columns, map.Rows) : (0, 0);

    // 길 찾기: 가려는 곳과 거기까지 남은 길. 한 걸음마다 다시 잰다 — 서버가 걸음을 되돌리거나 누가 길을 막아도 따라간다.
    private TabGoal? _guide;
    private int _guideMap = -1;
    private int _guideSteps;
    private int _guideBudget;
    private IReadOnlyList<Tile> _route = [];
    // 바닥(y 0)보다 한 줄 아래에 세워 두어야 높이로 줄 세울 때 바닥 뒤로 숨지 않는다. 그리는 쪽이 그 한 줄을 되돌린다.
    private readonly Node2D _trail = new() { Name = "Trail", Position = new Vector2(0, 1) };

    /// <summary>Where we are being walked to, by its name — empty for a bare tile — or null when we are not.</summary>
    public string? Guiding => _guide?.Label;

    /// <summary>The tiles still to walk, the goal last. Empty when not guiding.</summary>
    public IReadOnlyList<Tile> Route => _route;

    /// <summary>
    /// Walks us to one of these tiles, round the walls, a step at a time — what a tap on the 길 찾기 map asks for.
    /// False when there is no way there from here.
    /// </summary>
    public bool Guide(TabGoal goal)
    {
        if (TabMap.WayToAny(_tile, goal.Goals, Walled) is not { } way)
        {
            return false;
        }

        _guide = goal;
        _guideMap = MapId;
        _guideSteps = 0;
        _guideBudget = (3 * way.Count) + 20;
        _route = way;
        _trail.QueueRedraw();

        return true;
    }

    /// <summary>Stops walking to the goal — the pad was pressed, we got there, or the way closed.</summary>
    public void StopGuiding()
    {
        if (_guide is null)
        {
            return;
        }

        _guide = null;
        _route = [];
        _trail.QueueRedraw();
    }

    /// <summary>Whether a tile cannot be stood on, as the pathing sees it.</summary>
    public bool Blocked(Tile tile) => Walled(tile);

    /// <summary>What the server calls this map, once it has said.</summary>
    public string PlaceName => server?.State?.Map.Name ?? string.Empty;

    /// <summary>
    /// Whoever is picked out, by the name the server gave them, or nothing if nobody is. A serial stands in
    /// when we have only ever seen them take a step.
    /// </summary>
    public string TargetName
    {
        get
        {
            if (_target == 0)
            {
                return string.Empty;
            }

            Character? one = server?.Others.FirstOrDefault(other => other.Serial == _target);

            if (one is not null)
            {
                return one.Name.Length > 0 ? one.Name : one.Serial.ToString();
            }

            Creature? beast = server?.Creatures.FirstOrDefault(other => other.Serial == _target);

            // 이식한 NPC 는 이름에 자리가 붙어 온다(카르마@노비스마을식당#3,10). 이름만 보인다.
            return beast is null ? string.Empty
                : beast.Name.Length > 0 ? beast.Name.Split('@')[0]
                : $"괴물 {beast.Sprite - CreatureNumbering}";
        }
    }

    public override void _ExitTree() => _leaving.Cancel();

    public override void _Ready()
    {
        Name = "World";
        ClipContents = true;

        _floor.Texture = GD.Load<Texture2D>(FloorPath);
        _floorSize = _floor.Texture.GetSize();

        AddChild(_camera);

        // 한꺼번에 네 소리까지. 기술 하나가 맞는 소리와 휘두르는 소리를 겹쳐 낸다.
        for (int voice = 0; voice < 4; voice++)
        {
            AudioStreamPlayer player = new() { VolumeDb = -6 };
            _voices.Add(player);
            AddChild(player);
        }

        _camera.AddChild(_floor);
        _camera.AddChild(_tiledFloor);
        _tiledFloor.Draw += LayTiles;
        _camera.AddChild(_trail);
        _trail.Draw += DrawTrail;
        _camera.AddChild(_mark);
        _camera.AddChild(_figures);

        _tile = new Tile(4, 4);
        _player = Add(
            new Actor("수련생", Actor.Sheet.Walk([HeroSheet], [0], [HeroStrikeSheet])),
            Ground(_tile));

        if (server is null)
        {
            // Nobody to ask, so stand a couple of figures up to look at.
            Add(new Actor("주모", Actor.Sheet.Walk(OtherSheet)), Ground(new Tile(6, 4))).Face(Direction.South);
            Add(new Actor("말벌", CreatureSheet($"{CreatureFolder}mns001.png")), Ground(new Tile(3, 6)));
        }
        else
        {
            _ = server.PumpAsync(_leaving.Token);
        }

        _rehearsal = new Queue<Direction>(Main.Rehearse
            .Select(letter => letter switch
            {
                'N' or 'n' => Direction.North,
                'E' or 'e' => Direction.East,
                'S' or 's' => Direction.South,
                _ => Direction.West
            }));

        Look();
    }

    /// <summary>How far one tile is on the drawn floor — a figure further than that is not walking but arriving.</summary>
    private float TileStep => Ground(new Tile(1, 0)).DistanceTo(Ground(new Tile(0, 0)));

    /// <summary>Where a tile puts a pair of feet on the drawn floor.</summary>
    private Vector2 Ground(Tile tile)
    {
        (int x, int y) = IsometricFloor.Stand(tile.X, tile.Y, _rows);

        return new Vector2(x, y);
    }

    private Actor Add(Actor actor, Vector2 where)
    {
        actor.Position = where;
        _camera.AddChild(actor);

        return actor;
    }

    /// <summary>
    /// A tap picks out whoever was tapped, and picks nobody when it lands on empty floor. Picking a monster or a
    /// person is all it does — attacking is a separate ask, so a mistaken tap costs nothing — but an NPC is talked to,
    /// as the original client talks to one it is clicked on.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (Frozen)
        {
            return;
        }

        Vector2 at;

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                at = click.Position;
                break;

            case InputEventScreenTouch { Pressed: true } touch:
                at = touch.Position;
                break;

            default:
                return;
        }

        // Already in this view's own coordinates; the camera says how far the world has been slid under it.
        Choose(at - _camera.Position);
        AcceptEvent();
    }

    /// <summary>
    /// Whoever stands nearest the tap, if anybody stands near enough. Measured against the middle of a
    /// figure rather than its feet, because that is the part of it a thumb aims at.
    /// </summary>
    /// <remarks>
    /// Things lying on the floor are looked at first. They are small and they sit under everyone's feet,
    /// so a tap that lands on both is far likelier to have meant the thing than the figure standing over it.
    /// </remarks>
    private void Choose(Vector2 where)
    {
        if (Lift(where))
        {
            return;
        }

        const float reach = 34;
        const float figureWaist = 32;

        float nearest = reach * reach;

        _target = 0;

        // 사람·괴물은 허리를, 표식은 떠 있는 높이(NpcMark.Waist)를 겨눈다.
        IEnumerable<(uint Serial, Vector2 Aim)> standing = _crowd.Concat(_herd)
            .Select(one => (one.Key, one.Value.Position - new Vector2(0, figureWaist)))
            .Concat(_signs.Select(sign => (sign.Key, sign.Value.Position - new Vector2(0, NpcMark.Waist))));

        foreach ((uint serial, Vector2 aim) in standing)
        {
            float distance = aim.DistanceSquaredTo(where);

            if (distance < nearest)
            {
                nearest = distance;
                _target = serial;
            }
        }

        Mark();

        // 서버가 창을 보내 오면 화면이 연다(GameScreen). 여기서는 누른 것만 알린다.
        if (server is { } world && world.Creatures.FirstOrDefault(one => one.Serial == _target) is { Kind: CreatureKind.Merchant })
        {
            _ = world.ClickAsync(_target, System.Threading.CancellationToken.None);
        }
    }

    /// <summary>
    /// One creature's sheet and the numbering that goes with it. The frames lie side by side in square
    /// cells, so a cell is as tall as the sheet and as wide as it is tall — that is the whole reason the
    /// extractor squares them. The numbering comes out of the archive too, in a text file beside the
    /// picture: every creature walks and swings on frames of its own choosing, and a creature played with
    /// the person's numbering asks for frames that are not there (docs/original-sprite-animation.md 4절).
    /// </summary>
    private static Actor.Sheet CreatureSheet(string path)
    {
        int size = GD.Load<Texture2D>(path).GetHeight();
        string beside = System.IO.Path.ChangeExtension(path, ".txt");

        CreatureMotion? motion = Godot.FileAccess.FileExists(beside)
            ? CreatureMotion.Read(Godot.FileAccess.GetFileAsString(beside))
            : null;

        return Actor.Sheet.Creature(path, size, motion);
    }

    /// <summary>
    /// Asks for whatever lies nearest the tap, and says whether there was anything to ask for. **The
    /// original has no automatic looting** — walking over a thing leaves it lying there, and only asking
    /// takes it — so this happens on a tap and at no other time.
    /// </summary>
    /// <remarks>
    /// Nothing is drawn or removed here. The server decides whether we are close enough (its
    /// <c>ClickLootDistance</c> is ten tiles) and answers by no longer showing the thing, which is what
    /// makes it disappear. A tap on bare floor sends nothing.
    /// </remarks>
    private bool Lift(Vector2 where)
    {
        if (server is null)
        {
            return false;
        }

        // 한 칸 남짓. 사람을 고르는 34 보다 좁은 것은, 빗나간 탭이 엉뚱한 것을 줍는 편이
        // 아무도 고르지 못하는 것보다 나쁘기 때문이다.
        const float reach = 24;

        float nearest = reach * reach;
        GroundMark? asked = null;

        // 그려진 것을 그대로 재려고 server.Creatures 가 아니라 표식을 돈다. 같은 칸이라도 그림 크기에
        // 따라 눌러야 할 자리가 달라진다.
        foreach (GroundMark mark in _dropped.Values)
        {
            float distance = (mark.Position + mark.Middle).DistanceSquaredTo(where);

            if (distance < nearest)
            {
                nearest = distance;
                asked = mark;
            }
        }

        if (asked is null)
        {
            return false;
        }

        _ = server.PickUpAsync(asked.Where, _leaving.Token);

        // 실제로 보낼 때만 말한다. 리허설 쪽에서 말하면 화면이 얼어 탭이 무시돼도 찍혀서,
        // 되는 줄 알고 한참 헤맸다(2026-09-11).
        GD.Print($"GREYBOX_LIFTED {asked.Where.X},{asked.Where.Y} 그림 {asked.Sprite}");

        return true;
    }

    /// <summary>Puts the mark on whoever is picked out, or takes it away.</summary>
    private void Mark()
    {
        if (_target != 0 && (_crowd.TryGetValue(_target, out Actor? actor) || _herd.TryGetValue(_target, out actor)))
        {
            // A hair above the figure so the ring sorts behind its feet rather than over them.
            _mark.Position = actor.Position - new Vector2(0, 1);
            _mark.Visible = true;

            return;
        }

        if (_target != 0 && _signs.TryGetValue(_target, out NpcMark? sign))
        {
            _mark.Position = sign.Position - new Vector2(0, 1);
            _mark.Visible = true;

            return;
        }

        _target = 0;
        _mark.Visible = false;
    }

    /// <summary>
    /// How far down this view the character stands, when not in the middle. In portrait the map runs under the controls,
    /// and the character belongs in the middle of the part nothing covers.
    /// </summary>
    public float? FocusY { get; set; }

    /// <summary>Where across the view the player stands — the middle unless a window covers one side (the 길 찾기 map in landscape).</summary>
    public float? FocusX { get; set; }

    /// <summary>
    /// Whether we are in a coma (badge 89 on us, 0x3A). The server refuses every step and blow then
    /// (<c>CanMoveDuringReap</c> false), so taking one here would only walk us off and snap us back.
    /// </summary>
    private bool Comatose => _player.Comatose;

    /// <summary>Whether a step is under way — the movement pad fades while it is.</summary>
    public bool Walking => _walked >= 0;

    /// <summary>Starts a step. Ignored while one is still running, so a tile is never half walked.</summary>
    public void Walk(Direction direction)
    {
        if (Frozen || _walked >= 0 || Comatose)
        {
            return;
        }

        bool turned = _player.Looking != direction;
        _player.Face(direction);

        (int column, int row) = Facing.TileStep(direction);
        Tile next = new(_tile.X + column, _tile.Y + row);

        // 벽이나 맵 밖으로는 내딛지 않고 돌아서기만 한다. 서버는 어차피 거절하는데, 먼저 걸어 들어갔다가
        // 되돌려지면 맵 밖을 돌아다니는 것처럼 보였다.
        if (_layout?.Blocks(next) == true)
        {
            if (turned)
            {
                _ = server?.TurnAsync(direction, _leaving.Token);
            }

            return;
        }

        // Telling the server is enough — it only answers when it disagrees.
        _ = server?.WalkAsync(direction, _leaving.Token);

        _tile = next;
        _from = _player.Position;
        _to = Ground(next);

        _walked = 0;
        _drawnFrame = -1;
    }

    /// <summary>
    /// Draws everyone the server has shown us, and stops drawing the ones it has taken away.
    /// </summary>
    private void Crowd()
    {
        if (server is null)
        {
            return;
        }

        HashSet<uint> present = [];

        foreach (Character one in server.Others)
        {
            present.Add(one.Serial);

            // 갈아입었으면 그리던 것을 버리고 다시 짓는다.
            if (_crowd.TryGetValue(one.Serial, out Actor? standing)
                && _worn.TryGetValue(one.Serial, out Appearance? before)
                && before != one.Wearing)
            {
                standing.QueueFree();
                _crowd.Remove(one.Serial);
            }

            if (!_crowd.TryGetValue(one.Serial, out Actor? actor))
            {
                // The server gives a name with the appearance; a serial is only for somebody we have
                // only ever seen take a step.
                string called = one.Name.Length > 0 ? one.Name : one.Serial.ToString();

                // Somebody who changes clothes is not redressed until they leave and come back; the
                // server does say so, and this is where to listen when there is anything to wear.
                actor = Add(new Actor(called, Dress(one)), Ground(one.Where));
                _crowd[one.Serial] = actor;
                _worn[one.Serial] = one.Wearing;
            }

            actor.GoTo(Ground(one.Where), TileStep, StepSeconds);

            // 매 프레임 돌려세우면 서 있는 그림으로 되돌아가 걷는 동작이 지워진다. 바뀔 때만.
            if (actor.Looking != one.Facing)
            {
                actor.Face(one.Facing);
            }
        }

        foreach (uint serial in _crowd.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _crowd[serial].QueueFree();
            _crowd.Remove(serial);
            _worn.Remove(serial);
        }

        Mark();
    }

    /// <summary>
    /// The pieces somebody is drawn from. A piece we have no picture for is left out rather than left
    /// blank — the wardrobe in this repository only holds what the world can currently hand out.
    /// </summary>
    /// <summary>
    /// The sheets one person is drawn from. The equipment panel draws the same figure in its middle, so
    /// this is shared rather than written twice — one wardrobe, one set of rules about layer order.
    /// </summary>
    internal static Actor.Sheet Dress(Character one)
    {
        if (one.Wearing is null)
        {
            return Actor.Sheet.Walk(OtherSheet);
        }

        List<string> paths = [];
        List<int> colours = [];
        List<string> striking = [];
        List<char> parts = [];

        foreach (Piece piece in Wardrobe.Pieces(one.Wearing))
        {
            string path = $"{PartsFolder}{piece.Name}.png";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            paths.Add(path);
            colours.Add(piece.Colour);
            parts.Add(piece.Name[1]);

            // A piece with no swing of its own is left out while the rest of the figure swings (Actor.Play).
            string swung = $"{PartsFolder}{piece.Name}02.png";
            striking.Add(ResourceLoader.Exists(swung) ? swung : path);
        }

        return paths.Count > 0 ? Actor.Sheet.Walk(paths, colours, striking, parts) : Actor.Sheet.Walk(OtherSheet);
    }

    /// <summary>
    /// Puts our own figure into what the server says we are wearing. It describes us like anybody else, but
    /// only after we are already standing there, so the figure has to be built again once it does.
    /// </summary>
    private void Wear()
    {
        if (server?.Self is not { Wearing: not null } mine)
        {
            return;
        }

        if (_dressedOnce && _wearingOwn == mine.Wearing)
        {
            return;
        }

        _wearingOwn = mine.Wearing;
        _dressedOnce = true;

        Direction looking = _player.Looking;

        Actor dressed = new(mine.Name.Length > 0 ? mine.Name : _player.DisplayName, Dress(mine))
        {
            Position = _player.Position
        };

        _camera.AddChild(dressed);
        _player.QueueFree();

        _player = dressed;
        _player.Face(looking);
    }

    /// <summary>
    /// Taps the first person the server shows us, once, when asked to on the command line. It goes through
    /// the same event the screen sends, so a run without a hand on it still checks the arithmetic that
    /// turns a place on the screen into a person.
    /// </summary>
    private void RehearseAPick()
    {
        // Not the moment somebody appears: the view is still sliding to where the server put us, and a tap
        // aimed before it settles lands on empty floor.
        // 리허설은 1초에 한 번씩 휘두른다. 한 번만 휘두르면 0.4초짜리 동작을 사진으로 잡기가 어렵다.
        // 화면의 버튼은 여전히 한 번 누르면 한 번이다(시안 AC-009).
        if (_rehearsedStrike >= 0 && _rehearsedStrike++ % 60 == 30)
        {
            Strike();
        }

        // 한참 뒤에 결과를 본다 — 서버가 답하는 데 한 프레임보다 오래 걸린다.
        if (_rehearsedStrike == 121 && server is not null)
        {
            foreach (Creature beast in server.Creatures)
            {
                GD.Print($"GREYBOX_STRUCK {beast.Serial} 체력 {server.Health(beast.Serial)?.ToString() ?? "모름"}");
            }

            GD.Print($"GREYBOX_SAID {server.Said}");
        }

        if (_rehearsedPick
            || !(Main.Picking || Main.Striking || Main.Lifting || Main.Saying.Length > 0)
            || _heard < 0
            || (Main.Picking && _crowd.Count + _herd.Count == 0)
            || (Main.Lifting && _dropped.Count == 0)
            // 버린 다음에 주우려면 버릴 때까지 기다려야 한다. 소지품은 90 프레임에 열린다.
            || _settling++ < (Main.Throwing && Main.Lifting ? 150 : 60))
        {
            return;
        }

        _rehearsedPick = true;

        if (Main.Striking)
        {
            _rehearsedStrike = 0;
        }

        if (Main.Saying.Length > 0)
        {
            _ = server?.SayAsync(Main.Saying, _leaving.Token);
        }

        if (Main.Lifting)
        {
            // 가장 가까운 것 하나. 그림 한가운데를 누른다 — 눈이 겨냥하는 자리와 같아야 Lift 의 셈까지
            // 확인된다. 표식이 스스로 어디가 가운데인지 말하므로 여기서 다시 세지 않는다.
            GroundMark? thing = _dropped.Values
                .OrderBy(mark => mark.Position.DistanceSquaredTo(_player.Position))
                .FirstOrDefault();

            // 무엇이 보이는지 먼저 말한다 — 안 주워질 때 자리와 그림 번호가 없으면 물어볼 것이 없다.
            foreach (GroundMark seen in _dropped.Values)
            {
                GD.Print($"GREYBOX_ONFLOOR {seen.Where.X},{seen.Where.Y} 그림 {seen.Sprite}");
            }

            if (thing is not null)
            {
                InputEventMouseButton onIt = new()
                {
                    ButtonIndex = MouseButton.Left,
                    Pressed = true,
                    Position = thing.Position + thing.Middle + _camera.Position
                };

                GetViewport().PushInput(onIt, true);
            }
        }

        if (!Main.Picking)
        {
            return;
        }

        // 괴물도 고를 수 있어야 전투가 확인된다 — 사람만 보면 아무도 없는 방에서 멈춘다. 가장 가까운
        // 쪽을 고르는 것은 사람이 하는 것과 같고, 멀리 헤매다 화면 밖으로 나간 것을 누르지 않게 해 준다.
        Actor? somebody = _herd.Values.Concat(_crowd.Values)
            .OrderBy(actor => actor.Position.DistanceSquaredTo(_player.Position))
            .FirstOrDefault();

        if (somebody is null)
        {
            return;
        }

        InputEventMouseButton tap = new()
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = somebody.Position + _camera.Position - new Vector2(0, 32)
        };

        GetViewport().PushInput(tap, true);


        // Said out loud so a run with nobody watching can be checked afterwards.
        GD.Print($"GREYBOX_PICKED {TargetName}");
    }

    /// <summary>
    /// Draws the monsters and merchants the server has shown us, and stops drawing the ones it has taken
    /// away. Same shape as the crowd, but each of these brings its own drawing rather than a wardrobe.
    /// </summary>
    private void Herd()
    {
        if (server is null)
        {
            return;
        }

        HashSet<uint> present = [];

        foreach (Creature one in server.Creatures)
        {
            present.Add(one.Serial);

            if (one.Kind == CreatureKind.Passable)
            {
                if (!_dropped.TryGetValue(one.Serial, out GroundMark? mark))
                {
                    mark = new GroundMark
                    {
                        Name = $"Dropped{one.Serial}",
                        Picture = ItemIcons.For(one.Sprite)
                    };

                    _camera.AddChild(mark);
                    _dropped[one.Serial] = mark;
                }

                mark.Where = one.Where;
                mark.Sprite = one.Sprite;
                mark.Position = Ground(one.Where);

                continue;
            }

            if (!_herd.TryGetValue(one.Serial, out Actor? actor))
            {
                string path = $"{CreatureFolder}mns{one.Sprite - CreatureNumbering:000}.png";

                if (!ResourceLoader.Exists(path))
                {
                    // A merchant with no picture is one of the pack's script NPCs: it gets a sign so it can be found and
                    // tapped (NpcMark). Anything else with nothing cut yet stays off the floor — better an empty tile
                    // than a wrong picture.
                    if (one.Kind == CreatureKind.Merchant)
                    {
                        if (!_signs.TryGetValue(one.Serial, out NpcMark? sign))
                        {
                            sign = new NpcMark { Name = $"Sign{one.Serial}" };
                            _camera.AddChild(sign);
                            _signs[one.Serial] = sign;
                        }

                        sign.Position = Ground(one.Where);
                    }

                    continue;
                }

                string called = one.Name.Length > 0 ? one.Name : one.Serial.ToString();

                actor = Add(new Actor(called, CreatureSheet(path)), Ground(one.Where));
                _herd[one.Serial] = actor;
            }

            actor.GoTo(Ground(one.Where), TileStep, StepSeconds);

            // 매 프레임 돌려세우면 서 있는 그림으로 되돌아가 걷는 동작이 지워진다. 바뀔 때만.
            if (actor.Looking != one.Facing)
            {
                actor.Face(one.Facing);
            }
        }

        foreach (uint serial in _herd.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _herd[serial].QueueFree();
            _herd.Remove(serial);
        }

        foreach (uint serial in _dropped.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _dropped[serial].QueueFree();
            _dropped.Remove(serial);
        }

        foreach (uint serial in _signs.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _signs[serial].QueueFree();
            _signs.Remove(serial);
        }
    }

    /// <summary>Whoever is picked out, by serial, or zero for nobody.</summary>
    public uint Target => _target;

    /// <summary>
    /// Only when checking without a hand (<c>--invite</c>): taps the person of that name where a thumb would — the
    /// middle of the figure — so the picking itself is what gets checked. False while they are not on screen.
    /// </summary>
    public bool TapPerson(string name)
    {
        uint serial = server?.Others.FirstOrDefault(other => other.Name == name)?.Serial ?? 0;

        if (serial == 0 || !_crowd.TryGetValue(serial, out Actor? actor))
        {
            return false;
        }

        GetViewport().PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = actor.Position + _camera.Position - new Vector2(0, 32)
        }, true);

        return true;
    }

    /// <summary>
    /// Plays the character by itself: walks to the nearest monster, swings at it, and spends the points a
    /// level hands out. Only runs when the build was told to (<c>--hunt</c>, or a shipped `hunt.cfg`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it lives in the client and not outside it.</b> Skills, motion, sound and the flash a blow
    /// makes are all drawn here. A character driven from another connection leaves this screen showing
    /// somebody standing still, and then the only thing a run can check is the server's arithmetic.
    /// </para>
    /// <para>
    /// It moves at the pace a thumb would. The server rejects steps sent faster than
    /// <c>WalkingSpeedLimitFactor</c> and puts the character back where it was, which on screen reads as
    /// teleporting, and a swing sent before the last one finished is refused in words.
    /// </para>
    /// <para>
    /// 우드랜드1-1 은 60×60 — 3,600칸에 괴물이 50마리다. 화면에 보이는 것은 열두 칸 안이라, 서 있으면
    /// 아무것도 지나가지 않는다. 그래서 보이지 않을 때는 걸어서 찾고, 벽에 막히면 방향을 튼다.
    /// </para>
    /// </remarks>
    private void HuntOnItsOwn()
    {
        if (!Main.Hunting || server is null || Frozen || _walked >= 0 || _guide is not null)
        {
            return;
        }

        _hunted++;

        Tile standing = server.State?.Where ?? _tile;

        if (Nearest() is not { } prey)
        {
            _stuck = 0;
            Roam();
            return;
        }

        int dx = prey.X - standing.X, dy = prey.Y - standing.Y;

        if (Math.Abs(dx) + Math.Abs(dy) <= 1)
        {
            _stuck = 0;
            Face(dx, dy);

            // 한 번 휘두르고 서버가 허락하는 간격만큼 쉰다. GlobalBaseSkillDelay 가 500ms 다.
            if (_hunted % SwingFrames == 0)
            {
                Strike();
            }

            return;
        }

        // 자리가 한참 그대로면 그 한 마리를 잊는다. 길찾기가 벽은 돌아가 주지만, 사람이나 괴물이 길목에
        // 서 있는 것까지는 모른다 — 그럴 때 빠져나오는 마지막 장치다.
        _stuck = standing == _chasedFrom ? _stuck + 1 : 0;
        _chasedFrom = standing;

        if (_stuck > StuckTicks)
        {
            _stuck = 0;
            _heading = (Direction)((((int)_heading) + 1) % 4);
            Roam();
            return;
        }

        // 벽을 뚫고 가려 하지 않는다 — 돌아가는 길의 첫 걸음을 딛는다. 길이 아예 없으면 다른 데를 본다.
        if (Pathing.StepTowards(standing, prey, Walled, ChaseReach) is not { } step)
        {
            _stuck = 0;
            Roam();
            return;
        }

        Walk(step);
    }

    // 자동 사냥 — 판단은 알맹이(AutoHunt), 여기는 그 결정을 걸음·평타·기술로 옮기기만 한다.
    private readonly AutoHunt _autoHunt = new();

    /// <summary>다른 사람이 이만큼 안에 친 괴물은 "남이 치는 것"으로 본다.</summary>
    private static readonly System.TimeSpan ContestedFor = System.TimeSpan.FromSeconds(5);

    /// <summary>자동 사냥이 켜져 있나.</summary>
    public bool AutoHunting => _autoHunt.On;

    /// <summary>손이 잠시 조작 중이라 자동 사냥이 쉬고 있나.</summary>
    public bool AutoHuntPaused => _autoHunt.On && _autoHunt.Paused(Now);

    /// <summary>기술 부채꼴에 놓인 기술 — GameScreen 이 AbilityBar 에서 이어 준다.</summary>
    public System.Func<IReadOnlyList<LearnedSkill>>? BarSkills { get; set; }

    /// <summary>자동 사냥이 스스로 멈췄다 — 한 줄 알림.</summary>
    public event System.Action<string>? AutoHuntStopped;

    private static System.TimeSpan Now => System.TimeSpan.FromMilliseconds(Time.GetTicksMsec());

    /// <summary>켜고 끈다. 켠 자리가 사냥 반경의 중심이다.</summary>
    public void SetAutoHunt(bool on)
    {
        if (on && !_autoHunt.On)
        {
            _autoHunt.Start(_tile, MapId);
        }
        else if (!on && _autoHunt.On)
        {
            _autoHunt.Stop();
        }
    }

    /// <summary>사람이 방향판을 눌렀다 — 잠시 손에 맡기고, 선 자리를 새 중심으로.</summary>
    public void SteeredByHand()
    {
        if (_autoHunt.On)
        {
            _autoHunt.Steered(_tile, Now);
        }
    }

    /// <summary>사람이 공격·기술 단추를 눌렀다 — 잠시 손에 맡긴다.</summary>
    public void FoughtByHand()
    {
        if (_autoHunt.On)
        {
            _autoHunt.Pause(Now);
        }
    }

    private void AutoHuntTick()
    {
        if (!_autoHunt.On || server is not { } world || Frozen || _walked >= 0 || _guide is not null)
        {
            return;
        }

        uint me = world.Serial;
        HuntSight sight = new()
        {
            Standing = _tile,
            Facing = _player.Looking,
            MapId = MapId,
            Vitals = world.Vitals,
            Comatose = Comatose,
            Creatures = world.Creatures,
            HealthOf = world.Health,
            FoughtByOthers = serial => world.StruckByOthers(serial, ContestedFor),
            Skills = BarSkills?.Invoke() ?? [],
            Spells = world.Spells,
            Cooling = world.CoolingFor,
            PotionReady = Main.HealthPotion.Enabled && AutoPotion.Count(world.Pack, Main.HealthPotion.Potion) > 0,
            AutoLoot = Main.AutoLoot,
            Blocked = Walled,
            People = [.. world.Others.Where(one => one.Serial != me).Select(one => one.Where)],
            Now = Now,
        };

        HuntStep step = _autoHunt.Next(sight, Main.AutoHuntSettings);

        if (step.Target != 0 && step.Target != _target)
        {
            _target = step.Target;
            Mark();
        }

        switch (step.Act)
        {
            case HuntAct.Stop:
                GD.Print($"GREYBOX_AUTOHUNT 멈춤 {step.Why}");
                AutoHuntStopped?.Invoke(step.Why);
                break;
            case HuntAct.Heal:
                UseSpell(step.Slot, 0);
                break;
            case HuntAct.Walk:
                Walk(step.Toward);
                break;
            case HuntAct.Face:
                _player.Face(step.Toward);
                _ = world.TurnAsync(step.Toward, _leaving.Token);
                break;
            case HuntAct.Strike:
                Strike();
                break;
            case HuntAct.Skill:
                UseSkill(step.Slot);
                break;
        }
    }

    /// <summary>Faces the neighbouring tile without stepping onto it, so a swing lands the right way.</summary>
    private void Face(int dx, int dy)
    {
        _player.Face(Toward(dx, dy));
        _ = server?.TurnAsync(Toward(dx, dy), _leaving.Token);
    }

    private static Direction Toward(int dx, int dy) =>
        Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Direction.East : Direction.West)
            : (dy >= 0 ? Direction.South : Direction.North);

    /// <summary>
    /// The nearest monster's tile — nearest by <b>walking</b>, not by how the crow flies. One three tiles off
    /// behind a wall is further away than one six tiles off down an open lane, and picking by the straight line
    /// is what left a figure pressed against that wall for ever.
    /// </summary>
    private Tile? Nearest()
    {
        if (server is null)
        {
            return null;
        }

        Tile? best = null;
        int shortest = int.MaxValue;

        foreach (Creature beast in server.Creatures.Where(one => one.Kind == CreatureKind.Hostile))
        {
            // 멀리 있는 것부터 길을 재면 프레임이 녹는다 — 직선으로도 멀면 아예 보지 않는다.
            if (Math.Abs(beast.Where.X - _tile.X) + Math.Abs(beast.Where.Y - _tile.Y) > ChaseReach)
            {
                continue;
            }

            if (Pathing.Steps(_tile, beast.Where, Walled, ChaseReach) is not { } steps || steps >= shortest)
            {
                continue;
            }

            shortest = steps;
            best = beast.Where;
        }

        return best;
    }

    /// <summary>How far away a monster may be and still be worth walking to.</summary>
    private const int ChaseReach = 20;

    /// <summary>Whether a tile cannot be walked on — the walls this map was drawn with, and everything off it.</summary>
    private bool Walled(Tile tile) =>
        tile.X < 0 || tile.Y < 0
        || (_layout is { } map && (tile.X >= map.Columns || tile.Y >= map.Rows || map.Blocks(tile)));

    /// <summary>Walks on looking for something to fight, turning when the last step did not land.</summary>
    /// <remarks>
    /// Whether the step landed is asked of the <b>server</b>, not of what this client believes. A step is
    /// drawn the moment it is asked for — that is what keeps walking from lagging a third of a second — so
    /// the client's own tile moves even into a wall, and comparing against it says "we moved" every time.
    /// The character then walks into the same wall for ever, which is exactly what it did.
    /// </remarks>
    private void Roam()
    {
        // 걸음 간격은 이미 _walked 가 잰다 — 한 걸음이 끝나기 전에는 여기 오지도 않는다. 그 위에 프레임
        // 제동을 하나 더 걸었더니 막힌 자리에서 한 걸음에 13초가 걸렸다.
        Tile standing = server?.State?.Where ?? _tile;

        if (standing == _roamedFrom)
        {
            _heading = (Direction)((((int)_heading) + 1) % 4);
        }

        _roamedFrom = standing;
        Walk(_heading);
    }

    /// <summary>
    /// Puts one unspent point on whichever attribute is furthest from the Monk build. One point at a time,
    /// because the server takes one per packet.
    /// </summary>
    private void SpendAPoint()
    {
        // 레벨업이 준 점수를 스스로 찍는다. 예전에는 자동 사냥 중에만 돌아, 사람이 놀면 점수가 쌓이기만 했다
        // (2026-09-18 조사). 서버는 한 번에 한 점씩 받으므로 프레임마다 한 점.
        if (server?.Vitals is not { Unspent: > 0 } mine)
        {
            return;
        }

        (Stat Which, int Want, int Have)[] build =
        [
            (Stat.Con, 65, mine.Con), (Stat.Str, 77, mine.Str),
            (Stat.Int, 43, mine.Int), (Stat.Wis, 36, mine.Wis)
        ];

        Stat? next = build
            .Where(want => want.Want > want.Have)
            .OrderByDescending(want => want.Want - want.Have)
            .Select(want => (Stat?)want.Which)
            .FirstOrDefault();

        if (next is { } which)
        {
            _ = server.RaiseAsync(which, _leaving.Token);
        }
    }

    /// <summary>
    /// Swings at whatever stands in front of us. Nothing is assumed about the result — the server knows
    /// where everyone is and how recently we last swung, and answers in words when it refuses.
    /// </summary>
    public void Strike()
    {
        if (Frozen || Comatose)
        {
            return;
        }

        // Drawn straight away rather than waiting to be told: the server does not answer an allowed blow,
        // the same as a step, and a swing that lags a third of a second reads as a broken button.
        //
        // 무엇을 그릴지는 **서버가 지난번에 말해 준 것**을 쓴다. 평타 동작은 입은 것이 정하는데(무기의
        // 공격모션, 없으면 갑옷의 것 — 도복은 주먹 132) 클라이언트는 그 칸을 모른다. 그래서 늘 일반
        // 휘두르기만 그렸고, 무도가가 주먹을 안 쥐었다(사용자, 2026-09-18).
        // 직업 동작은 그 동작을 받는 옷(skill.tbl ST)을 입었을 때만 — 아니면 원작처럼 일반 휘두르기다.
        // 서버가 아직 한 번도 답하지 않았으면 미리 그리지 않는다 — 공통 휘두르기를 짐작해 그리면 무도가가 로그인 뒤
        // 첫 평타를 휘둘렀다(2026-09-25). 그 한 번은 답이 오면 Swings 가 그린다.
        if (_ownBlow.Swung(System.TimeSpan.FromMilliseconds(Time.GetTicksMsec())))
        {
            DrawOwnBlow(_ownBlow.Number ?? 1, _ownBlow.Speed);
        }

        _ = server?.AttackAsync(_leaving.Token);
    }

    /// <summary>Our own blow in the motion given — a class motion only in clothes skill.tbl lists for it, else the plain swing.</summary>
    private void DrawOwnBlow(int number, int speed)
    {
        BodyMotion blow = BodyMotion.Of(number) is { } known
            && server is { } world
            && BodyMotion.Fits(number, ArmourOf(world, world.Serial))
            ? known
            : BodyMotion.Blow;
        _player.Play(blow, blow.SecondsPerFrame(speed));
    }

    /// <summary>
    /// Uses a learned skill. Unlike the plain blow nothing is drawn yet: which motion a skill makes (a kick, a
    /// stab, a cast) only the server says, and it says so to us as well (0x1A) — <see cref="Swings" /> draws it.
    /// </summary>
    public void UseSkill(int slot)
    {
        if (Frozen)
        {
            return;
        }

        _ownBlow.Other();
        _ = server?.UseSkillAsync(slot, _leaving.Token);
    }

    /// <summary>Casts a learned spell at a chosen serial, or at self when target is zero.</summary>
    public void UseSpell(int slot, uint target)
    {
        if (Frozen)
        {
            return;
        }

        _ownBlow.Other();
        _ = server?.UseSpellAsync(slot, target, _leaving.Token);
    }

    /// <summary>
    /// Draws the flashes the server asked for (0x29). On somebody the first picture goes over whoever it lands
    /// on and the second over whoever made it; on the ground it goes on the tile.
    /// </summary>
    private void Flashes()
    {
        while (server is { } world && world.TakeEffect(out Effect? effect))
        {
            if (effect.At is Tile at)
            {
                Show(effect.TargetAnimation, Ground(at), effect.Speed);
                continue;
            }

            if (effect.TargetAnimation > 0 && Someone(world, effect.Target) is { } target)
            {
                Show(effect.TargetAnimation, target.Position, effect.Speed, target);
            }

            if (effect.SourceAnimation > 0 && Someone(world, effect.Source) is { } source)
            {
                Show(effect.SourceAnimation, source.Position, effect.Speed, source);
            }
        }
    }

    /// <summary>The armour number somebody is shown wearing, 0 when bare or not described.</summary>
    private static int ArmourOf(WorldClient world, uint serial) =>
        (serial == world.Serial ? world.Self : world.Others.FirstOrDefault(other => other.Serial == serial))?.Wearing?.Armor ?? 0;

    private Actor? Someone(WorldClient world, uint serial) =>
        serial == world.Serial ? _player
        : _crowd.TryGetValue(serial, out Actor? person) ? person
        : _herd.TryGetValue(serial, out Actor? beast) ? beast
        : null;

    // 무엇이 없어서 안 보였는지 한 번씩만 적는다. 같은 번호로 계속 적으면 기록이 그것만 남는다(2026-09-18 조사).
    private readonly HashSet<string> _toldAbout = [];

    private void Told(string what)
    {
        if (_toldAbout.Add(what))
        {
            GD.Print($"GREYBOX_MISSING {what}");
        }
    }

    private void Show(int number, Vector2 feet, int speed, Actor? on = null)
    {
        if (number <= 0)
        {
            return;
        }

        if (Flash.Make(number, speed) is not { } flash)
        {
            Told($"이펙트 {number} 그림이 없습니다");
            return;
        }

        // 혼수인 동안은 혼수가 머리 칸을 차지한다 — 다른 머리 이펙트(Miss·일음지 …)는 그리지 않는다.
        if (flash.OnHead && on is { Comatose: true } && number != Overhead.ComaEffect)
        {
            flash.Free();
            return;
        }

        // 그림이 제 기준점을 지니므로 발밑(칸)에 놓는다 — 몸 가운데는 그림이 정한다. 머리 이펙트만은 맞은
        // 이의 그려진 머리 바로 위 칸으로 옮긴다(Overhead.Shift) — 원작 칸 그대로면 키 큰 사람의 얼굴을 덮었다.
        flash.Land(feet, on?.HeadTop);
        _camera.AddChild(flash);

        if (flash.OnHead)
        {
            GD.Print($"GREYBOX_HEAD_FLASH {number} frame {Engine.GetProcessFrames()} on {on?.DisplayName}");
        }
    }

    // 배경음악은 효과음과 따로 한 대에서 돈다 — 맵을 옮기면 갈아 끼우고, 같은 곡이면 이어서 튼다.
    private AudioStreamPlayer? _band;
    private int _playing = -1;

    /// <summary>
    /// Plays the map's music (0x19 with a number of 128 or more). One song at a time, looping, and the same song is
    /// left alone when the next map asks for it again.
    /// </summary>
    private void Band()
    {
        while (server is { } world && world.TakeMusic(out int song))
        {
            if (song == _playing)
            {
                continue;
            }

            if (song == Music.Silence)
            {
                _band?.Stop();
                _playing = -1;
                continue;
            }

            string path = $"res://assets/music/{song}.ogg";

            if (!ResourceLoader.Exists(path))
            {
                Told($"곡 {song} 파일이 없습니다");
                continue;
            }

            if (_band is null)
            {
                _band = new AudioStreamPlayer { Name = "Band" };
                AddChild(_band);
            }

            if (GD.Load<AudioStream>(path) is AudioStreamOggVorbis stream)
            {
                stream.Loop = true;
                _band.Stream = stream;
                _band.Play();
                _playing = song;
            }
        }
    }

    /// <summary>Plays the sounds the server asked for (0x19). The number is the file's name.</summary>
    /// <summary>
    /// Picks up whatever we are standing on. The only way to lift something was to tap it, and on a floor with
    /// thirty monsters on it nobody finds a 20-pixel bundle to tap — a character fought all night and came home
    /// with an empty bag (사용자, 2026-09-18). Stepping on it is what players expect now.
    /// </summary>
    /// <remarks>
    /// The server decides whether it may be carried (weight, a full bag) and says so in words; this only asks.
    /// Asking again while the answer is on its way would ask many times for the one bundle, so it waits.
    /// </remarks>
    private void Gather()
    {
        if (server is null || Frozen)
        {
            return;
        }

        // Allowed walks are silent on this protocol.  State.Where is therefore often the tile where we
        // logged in, while _tile is the prediction we just told the server to make.  Pickup must name the
        // latter or the server quite correctly finds no item at the old coordinate.
        if (_autoLoot.Next(Main.AutoLoot, _tile, server.Creatures) is { } where)
        {
            _ = server.PickUpAsync(where, _leaving.Token);
        }
    }

    /// <summary>Drinks a carried potion when health or mana has fallen to the line chosen on the game screen.</summary>
    private void Drink()
    {
        if (server is null || Frozen || server.Vitals is not { } vitals)
        {
            return;
        }

        System.TimeSpan now = System.TimeSpan.FromMilliseconds(Time.GetTicksMsec());

        if (_potion.Next(vitals, server.Pack, Main.HealthPotion, Main.ManaPotion, now) is { } slot)
        {
            _ = server.UseAsync(slot, _leaving.Token);
        }
    }

    /// <summary>
    /// Puts what somebody said over their head (0x0D) — a person, a monster, or a merchant's sign. Somebody the screen
    /// is not drawing has nowhere to put it; the log still has it.
    /// </summary>
    public void Speak(uint serial, string words)
    {
        if (server is { } world && serial == world.Serial)
        {
            _player.Say(words);
        }
        else if (_crowd.TryGetValue(serial, out Actor? person))
        {
            person.Say(words);
        }
        else if (_herd.TryGetValue(serial, out Actor? beast))
        {
            beast.Say(words);
        }
        else if (_signs.TryGetValue(serial, out NpcMark? sign))
        {
            if (sign.GetNodeOrNull<SpeechBubble>("Speech") is not { } bubble)
            {
                bubble = new SpeechBubble { Name = "Speech", Position = new Vector2(0, -NpcMark.Waist * 2 - 4) };
                sign.AddChild(bubble);
            }

            bubble.Say(words);
        }
    }

    /// <summary>
    /// Floats how much a blow took or a heal gave over whoever it was (0x5D). Read after <see cref="Wounds" /> so a
    /// blow's bar is already up and the number starts above it.
    /// </summary>
    private void Figures()
    {
        while (server is { } world && world.TakeFigure(out Figure? figure))
        {
            Actor? on = figure.Target == world.Serial ? _player
                : _herd.TryGetValue(figure.Target, out Actor? beast) ? beast
                : _crowd.TryGetValue(figure.Target, out Actor? person) ? person
                : null;

            if (on is not null && figure.Amount > 0)
            {
                _figures.Add(on, on.FigureStart, figure, world.Serial);
                GD.Print($"GREYBOX_FIGURE {FloatingFigure.Tone(figure, world.Serial)} {FloatingFigure.Text(figure)} on {on.DisplayName}");
            }
        }
    }

    /// <summary>
    /// Puts a bar over the head of whoever was just struck. The server tells us about every blow (0x13) with
    /// what is left as a percentage, and until now the screen only listened for the sound in it — so a fight
    /// showed no sign of how it was going, on a monster or on a person.
    /// </summary>
    private void Wounds()
    {
        while (server is { } world && world.TakeHurt(out uint serial, out int left))
        {
            // 번호 0 은 허공을 친 것이다 — 아무의 체력도 아니다.
            if (serial == 0)
            {
                continue;
            }

            if (serial == world.Serial)
            {
                _player.Struck(left);
            }
            else if (_herd.TryGetValue(serial, out Actor? beast))
            {
                beast.Struck(left);
            }
            else if (_crowd.TryGetValue(serial, out Actor? person))
            {
                person.Struck(left);
            }
        }

        // 내 것은 0x3A 로(원작 그대로), 남의 것은 우리 서버가 둘레에 알리는 0x5C 로 온다.
        if (server is { } mine)
        {
            _player.Ailing(mine.Ailments);

            // 사람은 체력바 아래 배지, 괴물은 배지 없이 몸을 그 마법 그림의 색으로 물들인다 — 해로운 것 중
            // 가장 오래 남는 것의 색으로(사용자 결정 2026-09-18).
            foreach ((uint serial, Actor person) in _crowd)
            {
                person.Ailing(mine.AilmentsOf(serial).Select(one => one.Badge));
            }

            foreach ((uint serial, Actor beast) in _herd)
            {
                SeenAilment? worst = mine.AilmentsOf(serial)
                    .Where(one => one.Harmful && one.Effect > 0)
                    .OrderByDescending(one => one.Left)
                    .FirstOrDefault();

                beast.Tint(worst is null ? Colors.White : Flash.Tint(worst.Effect));
            }
        }
    }

    private void Sounds()
    {
        while (server is { } world && world.TakeSound(out int number))
        {
            string path = $"res://assets/sound/{number}.mp3";

            if (!ResourceLoader.Exists(path))
            {
                Told($"소리 {number} 파일이 없습니다");
                continue;
            }

            AudioStreamPlayer? free = _voices.Find(voice => !voice.Playing);

            if (free is null)
            {
                continue;
            }

            free.Stream = GD.Load<AudioStream>(path);
            free.Play();
        }
    }

    /// <summary>
    /// Draws the body motions the server names — anybody's, ours included, since a skill's motion is only known
    /// from here. Our own plain blow is drawn as it is asked for, so that one coming back is left alone. An emote
    /// shows over a person's head (<see cref="Emote" />); a motion with no drawing moves nobody; a creature swings its
    /// own blow for any. A skill motion is drawn only in clothes skill.tbl lists for it — the original client does
    /// nothing otherwise (<see cref="BodyMotion.Fits" />).
    /// </summary>
    private void Swings()
    {
        while (server is { } world && world.TakeMotion(out Motion? motion))
        {
            // 미리 그리지 않은 평타(로그인 뒤 첫 번)는 답이 왔을 때 그린다.
            if (motion.Serial == world.Serial
                && _ownBlow.Heard(motion.Number, motion.Speed, System.TimeSpan.FromMilliseconds(Time.GetTicksMsec())))
            {
                DrawOwnBlow(motion.Number, motion.Speed);
                continue;
            }

            if ((motion.Serial == world.Serial && motion.Number == 1) || Someone(world, motion.Serial) is not { } actor)
            {
                continue;
            }

            if (BodyMotion.Of(motion.Number) is { } body
                && (_herd.ContainsKey(motion.Serial) || BodyMotion.Fits(motion.Number, ArmourOf(world, motion.Serial))))
            {
                actor.Play(body, body.SecondsPerFrame(motion.Speed));
            }
            else if (_herd.ContainsKey(motion.Serial))
            {
                actor.Strike();
            }
            else if (Emote.Of(motion.Number) is { } emote)
            {
                actor.Show(emote);
            }
            else
            {
                Told($"몸동작 {motion.Number} 을 모릅니다");
            }
        }
    }

    /// <summary>
    /// Puts the map we are standing on under our feet. One picture per map, drawn ahead of time from the
    /// map file and the original tileset, and named by the map's own number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The floor used to be one fixed picture — the safe house — whatever map the server said we were on.
    /// On a 60×60 hunting zone that picture covers a corner and everything past it is black, which is what
    /// made 우드랜드 look like the room a new character wakes in with a different name on it.
    /// </para>
    /// <para>
    /// The pictures are drawn by <c>tools/dat-extract map &lt;seo.dat&gt; &lt;맵파일&gt; &lt;가로&gt;
    /// &lt;세로&gt; &lt;출력&gt;</c>, which lays tiles out with exactly the arithmetic
    /// <see cref="IsometricFloor" /> uses, so a tile in the picture sits where a figure standing on that
    /// tile is drawn. A map with no picture shows no floor: keeping the last map's floor drew people and NPCs
    /// standing off its edge, since their tiles belong to a different map.
    /// </para>
    /// </remarks>
    private void LayTheFloor(MapInfo map)
    {
        if (map.Id == _floored)
        {
            return;
        }

        _floored = map.Id;
        StandObjects(map);

        if (_floorSheet is not null)
        {
            _floor.Texture = null;
            return;
        }

        string path = $"{FloorFolder}map{map.Id}.png";

        if (!ResourceLoader.Exists(path))
        {
            GD.Print($"바닥 그림이 없습니다: {path} ({map.Name})");
            _floor.Texture = null;
            return;
        }

        _floor.Texture = GD.Load<Texture2D>(path);
        _floorSize = _floor.Texture.GetSize();
    }

    /// <summary>
    /// Stands up the map's buildings, trees and lamps, each picture on its own so a figure behind one is drawn under
    /// it and a figure in front over it — the camera sorts everything by height. Also takes the map's walls, so a
    /// step into one is not taken, and its floor tiles (<see cref="LayTiles" />). All of it comes from
    /// <c>map&lt;번호&gt;.txt</c>, <c>-floor.png</c> and <c>-objects.png</c>, which <c>scripts/build-client-maps.py</c>
    /// draws out of the same .map file and sotp.dat the server reads.
    /// </summary>
    private void StandObjects(MapInfo map)
    {
        foreach (Sprite2D standing in _objects)
        {
            standing.QueueFree();
        }

        _objects.Clear();
        _layout = null;
        _floorSheet = null;
        _tiledFloor.QueueRedraw();

        string layoutPath = $"{FloorFolder}map{map.Id}.txt";
        string sheetPath = $"{FloorFolder}map{map.Id}-objects.png";

        if (!Godot.FileAccess.FileExists(layoutPath))
        {
            return;
        }

        _layout = MapLayout.Read(Godot.FileAccess.GetFileAsString(layoutPath));

        string floorPath = $"{FloorFolder}map{map.Id}-floor.png";
        _floorSheet = _layout.Tiles.Count > 0 && ResourceLoader.Exists(floorPath) ? GD.Load<Texture2D>(floorPath) : null;
        _tiledFloor.Material = _floorSheet is not null ? CyclingFloor(map.Id) : null;

        if (!ResourceLoader.Exists(sheetPath))
        {
            return;
        }

        Texture2D sheet = GD.Load<Texture2D>(sheetPath);
        Dictionary<int, AtlasTexture> cut = [];

        foreach (MapObject standing in _layout.Objects)
        {
            if (!_layout.Pictures.TryGetValue(standing.Picture, out MapPicture picture))
            {
                continue;
            }

            if (!cut.TryGetValue(standing.Picture, out AtlasTexture? texture))
            {
                texture = new AtlasTexture { Atlas = sheet, Region = new Rect2(picture.X, picture.Y, MapPicture.Width, picture.Height) };
                cut[standing.Picture] = texture;
            }

            (int x, int y) = IsometricFloor.ObjectFoot(standing.Column, standing.Row, _layout.Rows, standing.Right);
            Sprite2D sprite = new()
            {
                Texture = texture,
                Centered = false,
                Offset = new Vector2(0, -picture.Height),
                Position = new Vector2(x, y),
                Material = picture.Glows ? _glow : null
            };

            _camera.AddChild(sprite);
            _objects.Add(sprite);
        }
    }

    /// <summary>The floor's colour-turning material, or nothing when no pixel of this map's floor turns (<see cref="FloorCycle" />).</summary>
    private static ShaderMaterial? CyclingFloor(int mapId)
    {
        string marks = $"{FloorFolder}map{mapId}-floor-cycle.png";
        string colours = $"{FloorFolder}map{mapId}-floor-cycle-colours.png";

        if (!ResourceLoader.Exists(marks) || !ResourceLoader.Exists(colours))
        {
            return null;
        }

        ShaderMaterial material = new() { Shader = FloorCycle };
        material.SetShaderParameter("marks", GD.Load<Texture2D>(marks));
        material.SetShaderParameter("colours", GD.Load<Texture2D>(colours));
        return material;
    }

    /// <summary>
    /// Lays the floor tile by tile, in the same order tools/dat-extract draws a floor picture (row by row), so a
    /// tile's overlap onto its neighbour comes out the same.
    /// </summary>
    private void LayTiles()
    {
        if (_layout is not { } layout || _floorSheet is null)
        {
            return;
        }

        Vector2 size = new(IsometricFloor.TileWidth, IsometricFloor.TileHeight);

        for (int row = 0; row < layout.Rows; row++)
        {
            for (int column = 0; column < layout.Columns; column++)
            {
                if (!layout.Tiles.TryGetValue(layout.Floor(column, row), out (int X, int Y) at))
                {
                    continue;
                }

                (int x, int y) = IsometricFloor.Corner(column, row, layout.Rows);
                _tiledFloor.DrawTextureRectRegion(_floorSheet, new Rect2(new Vector2(x, y), size), new Rect2(new Vector2(at.X, at.Y), size));
            }
        }
    }

    /// <summary>Takes the server's word for where we are, whenever it gives one.</summary>
    private void Listen()
    {
        if (server?.State is not { } state || server.PositionReports == _heard)
        {
            return;
        }

        _heard = server.PositionReports;
        _rows = state.Map.Rows;
        LayTheFloor(state.Map);

        if (state.Where == _tile)
        {
            return;
        }

        // Put back: a step it would not allow, or one it never saw.
        Tile walkedTo = _tile;
        _tile = state.Where;
        _walked = -1;
        _player.Position = Ground(_tile);
        _player.Rest();
        StandAsPut(walkedTo);
    }

    /// <summary>
    /// Turns us the way the server stood us when it put us on a tile we did not walk to — 이형환위 lands past the
    /// target and faces back at it (MonkStrike.Step). Only then: at any other time what the server last said about
    /// our facing is a step behind our walking (<see cref="OwnFacing" />), and following it draws a walk west as north.
    /// </summary>
    private void StandAsPut(Tile walkedTo)
    {
        if (OwnFacing.PutBy(server?.Self, _tile, walkedTo) is { } facing && facing != _player.Looking)
        {
            _player.Face(facing);
        }
    }

    public override void _Process(double delta)
    {
        Listen();
        SpendAPoint();
        Wear();
        Crowd();
        Herd();
        Swings();
        Wounds();
        Figures();
        Gather();
        Drink();
        Flashes();
        Sounds();
        Band();
        RehearseAPick();
        RehearseOverhead(delta);
        HuntOnItsOwn();
        AutoHuntTick();
        FollowGuide();

        if (_walked < 0 && _rehearsal.Count > 0)
        {
            Walk(_rehearsal.Dequeue());
        }

        if (_walked >= 0)
        {
            _walked += delta;

            double progress = Mathf.Min(1.0, _walked / StepSeconds);
            _player.Position = _from.Lerp(_to, (float)progress);

            int frame = (int)(progress * WalkMotion.WalkFrames);

            if (frame != _drawnFrame && progress < 1.0)
            {
                _drawnFrame = frame;
                _player.Stride();
            }

            if (progress >= 1.0)
            {
                _walked = -1;
                _player.Rest();
            }
        }

        Look();
    }

    /// <summary>
    /// Takes the next step to the goal once the last one has finished. The way is measured again every step from the
    /// tile we are on, so a step the server put back, or somebody standing in the lane, is walked round rather than
    /// into. Stops on arriving, when the map changes under us (we went through the exit), or when the way is gone.
    /// </summary>
    private void FollowGuide()
    {
        if (_guide is not { } goal || _walked >= 0 || Frozen || Comatose)
        {
            return;
        }

        if (MapId != _guideMap)
        {
            StopGuiding();
            return;
        }

        // 사람이 길목에 서 있으면 서버가 걸음마다 되돌린다. 처음 길의 세 배 넘게 걸었으면 그만둔다.
        IReadOnlyList<Tile>? way = TabMap.WayToAny(_tile, goal.Goals, Walled);

        if (way is not { Count: > 0 } || ++_guideSteps > _guideBudget)
        {
            StopGuiding();
            return;
        }

        _route = way;
        _trail.QueueRedraw();
        Walk(TabMap.StepOf(_tile, way[0]));
    }

    /// <summary>Dots on the floor along the way still to walk, and a ring on where it ends — so the way can be seen.</summary>
    private void DrawTrail()
    {
        if (_route.Count == 0)
        {
            return;
        }

        Color dot = Greybox.Accent with { A = 0.9f };

        for (int at = 0; at < _route.Count - 1; at++)
        {
            Vector2 middle = Ground(_route[at]) - new Vector2(0, 9);
            _trail.DrawCircle(middle, 6, Colors.Black with { A = 0.55f });
            _trail.DrawCircle(middle, 4.5f, dot);
        }

        Vector2 end = Ground(_route[^1]) - new Vector2(0, 9);
        _trail.DrawArc(end, 12, 0, Mathf.Tau, 32, Greybox.Accent, 3);
    }

    private double _overheadAt = -1;

    /// <summary>
    /// With no server, stands what <c>--overhead</c> asks for over the heads, so the stack can be photographed: badges
    /// on everyone (seven on 주모 with the bar up, six on us with it down), 일음지 42 and Miss 33/115 over 주모 and the
    /// wasp every second — or, for <c>coma</c>, us in a coma with its effect 24 and a Miss that must not show.
    /// </summary>
    private void RehearseOverhead(double delta)
    {
        if (server is not null || Main.Overhead.Length == 0)
        {
            return;
        }

        int was = (int)_overheadAt;
        _overheadAt += delta;

        if ((int)_overheadAt == was && _overheadAt > 0)
        {
            return;
        }

        // 쿠로토 — 서버가 보내는 그대로(쓴 쪽 그림 4, 속도 117)를 매초 내게 그린다. 링이 몸에 겹치는지 찍어 본다.
        if (Main.Overhead == "kuroto")
        {
            Show(4, _player.Position, 117, _player);
            return;
        }

        Ailment[] many = [new(3, 6), new(12, 1), new(27, 4), new(40, 2), new(55, 5), new(82, 3), new(101, 6)];
        List<Actor> others = [.. _camera.GetChildren().OfType<Actor>().Where(actor => actor != _player)];
        Actor? person = others.Find(actor => actor.DisplayName == "주모");
        Actor? beast = others.Find(actor => actor.DisplayName == "말벌");
        bool coma = Main.Overhead == "coma";
        int beat = (int)_overheadAt;

        _player.Ailing(coma ? [new(Lod.Mobile.Core.Art.Overhead.ComaIcon, 2), .. many[..3]] : many[..6]);
        person?.Ailing(many);
        person?.Struck(55);
        beast?.Struck(30);

        if (coma)
        {
            Show(Lod.Mobile.Core.Art.Overhead.ComaEffect, _player.Position, 100, _player);
            Show(33, _player.Position, 100, _player);
            GD.Print($"GREYBOX_OVERHEAD coma tile {_tile} at {_player.Position} comatose {Comatose}");
            return;
        }

        foreach (Actor? one in new[] { person, beast })
        {
            if (one is not null)
            {
                Show(beat % 2 == 0 ? 42 : one == person ? 33 : 115, one.Position, 100, one);
            }
        }

        // 떠오르는 숫자 넷 — 내가 준 것(말벌) · 남의 싸움(주모) · 내가 받은 것 · 회복, 나를 1번으로 친다.
        const uint self = 1;

        if (beast is not null)
        {
            _figures.Add(beast, beast.FigureStart, new Figure(2, self, 37 + beat, FigureKind.Damage), self);
        }

        if (person is not null)
        {
            _figures.Add(person, person.FigureStart, new Figure(3, 9, 12, FigureKind.Damage), self);
        }

        _figures.Add(_player, _player.FigureStart,
            beat % 2 == 0 ? new Figure(self, 2, 8, FigureKind.Damage) : new Figure(self, self, 120, FigureKind.Heal), self);
    }

    /// <summary>
    /// Keeps the player in the middle of the view, wherever they stand. The view used to stop at the edge
    /// of the floor so that nothing past it showed, but the starting tile is a corner one and the screen is
    /// bigger than the original's, so that rule left the character parked in a corner of the screen.
    /// Showing a little emptiness past the edge is the smaller cost.
    /// </summary>
    private void Look()
    {
        Vector2 window = Size;

        // The 20 keeps the character a little below the exact middle, where the original put it.
        float x = _player.Position.X - (FocusX ?? (window.X / 2));
        float y = _player.Position.Y - (FocusY ?? (window.Y / 2)) - 20;

        _camera.Position = new Vector2(-Mathf.Round(x), -Mathf.Round(y));
    }
}
