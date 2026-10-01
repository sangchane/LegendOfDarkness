using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core;
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

    // 워프 칸 위 이름표 — 어디로 가는 출구인지(사용자 2026-10-02). 맵이 바뀌면 다시 세운다.
    private readonly List<Control> _exitTags = [];

    /// <summary>출구 이름을 아는 길잡이(<c>guide.txt</c>). 길 찾기 창·미니맵과 같은 것.</summary>
    public MapGuide Exits { get; set; } = MapGuide.Empty;

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

    /// <summary>
    /// While frozen, whether a tap may still press on a person — the gear window is open, and pressing somebody shows
    /// theirs in its place at once (사용자 2026-10-01). Nothing else on the floor answers.
    /// </summary>
    public bool PeopleOnly { get; set; }

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
            Main.Fire(server.PumpAsync(_leaving.Token));
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

    /// <summary>돌아서기만 한다 — 방향키를 짧게 누르면(원작처럼) 칸을 옮기지 않고 그쪽을 본다(사용자 2026-10-02).</summary>
    /// <returns>돌았거나 이미 그쪽을 보면 true, 걸음 중이라 아직 못 돌았으면 false.</returns>
    public bool Turn(Direction direction)
    {
        if (_player.Looking == direction)
        {
            return true;
        }

        if (Frozen || _walked >= 0 || Comatose)
        {
            return false;
        }

        _player.Face(direction);
        Main.Fire(server?.TurnAsync(direction, _leaving.Token));
        return true;
    }

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
                Main.Fire(server?.TurnAsync(direction, _leaving.Token));
            }

            return;
        }

        // Telling the server is enough — it only answers when it disagrees.
        Main.Fire(server?.WalkAsync(direction, _leaving.Token));

        _tile = next;
        _from = _player.Position;
        _to = Ground(next);

        _walked = 0;
        _drawnFrame = -1;
    }

    /// <summary>Whoever is picked out, by serial, or zero for nobody.</summary>
    public uint Target => _target;

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

            double progress = Mathf.Min(1.0, _walked / Tuning.StepSeconds);
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
