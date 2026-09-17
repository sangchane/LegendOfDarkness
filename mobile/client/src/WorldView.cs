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

    /// <summary>How long one tile takes to walk, and how many frames that walk is drawn in.</summary>
    private const double StepSeconds = 0.28;

    // Y sorting is what makes someone standing in front actually draw in front, which an isometric floor
    // needs: screen height is depth here.
    private readonly Node2D _camera = new() { Name = "Camera", YSortEnabled = true };
    private readonly Sprite2D _floor = new() { Name = "Floor", Centered = false };

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

    // 그림이 없는 NPC(팩의 스크립트 NPC)가 선 자리의 표식. 번호로 골라 말을 건다.
    private readonly Dictionary<uint, NpcMark> _signs = [];
    private readonly List<AudioStreamPlayer> _voices = [];

    /// <summary>How high above the feet a flash on somebody bursts.</summary>
    private const float BodyMiddle = 36;

    // Whoever is picked out, and the mark that says so. Zero is nobody.
    private readonly TargetMark _mark = new() { Name = "Target", Visible = false };
    private uint _target;
    private bool _rehearsedPick;
    private int _settling;
    private int _rehearsedStrike = -1;

    private Queue<Direction> _rehearsal = new();

    /// <summary>Frames since the hunt started. Everything it does is paced off this rather than a timer.</summary>
    private int _hunted;

    /// <summary>Which way it walks while nothing is in sight.</summary>
    private Direction _heading = Direction.North;

    /// <summary>Where the last roaming step started, so a step that did not land can be noticed.</summary>
    private Tile _roamedFrom;

    /// <summary>한 번 휘두르는 간격(프레임). 60프레임이 1초다.</summary>
    private const int SwingFrames = 40;

    /// <summary>점수를 한 점 쓰는 간격(프레임).</summary>
    private const int SpendFrames = 30;

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
        _camera.AddChild(_mark);

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

    /// <summary>Whether a step is under way — the movement pad fades while it is.</summary>
    public bool Walking => _walked >= 0;

    /// <summary>Starts a step. Ignored while one is still running, so a tile is never half walked.</summary>
    public void Walk(Direction direction)
    {
        if (Frozen || _walked >= 0)
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
        // 리허설은 1초에 한 번씩 휘두른다. 한 번만 휘두르면 0.28초짜리 동작을 사진으로 잡기가 어렵다.
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
        if (!Main.Hunting || server is null || Frozen || _walked >= 0)
        {
            return;
        }

        _hunted++;

        SpendAPoint();

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

        // 벽 너머의 괴물은 보이기는 해도 갈 수 없다. 가장 가까운 한 마리만 보고 걸으면 그 벽에 대고
        // 영원히 걷게 된다 — 실제로 이것 때문에 (14,50) 에서 멈춰 있었다. 그래서 자리가 한참 그대로면
        // 그 한 마리를 잊고 방향을 튼다.
        _stuck = standing == _chasedFrom ? _stuck + 1 : 0;
        _chasedFrom = standing;

        if (_stuck > StuckTicks)
        {
            _stuck = 0;
            _heading = (Direction)((((int)_heading) + 1) % 4);
            Roam();
            return;
        }

        Walk(Toward(dx, dy));
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

    /// <summary>The nearest monster's tile, or nothing when none is in sight.</summary>
    private Tile? Nearest() =>
        server?.Creatures
            .Where(beast => beast.Kind == CreatureKind.Hostile)
            .OrderBy(beast => Math.Abs(beast.Where.X - _tile.X) + Math.Abs(beast.Where.Y - _tile.Y))
            .Select(beast => (Tile?)beast.Where)
            .FirstOrDefault();

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
        if (_hunted % SpendFrames != 0 || server?.Vitals is not { Unspent: > 0 } mine)
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
        if (Frozen)
        {
            return;
        }

        // Drawn straight away rather than waiting to be told: the server does not answer an allowed blow,
        // the same as a step, and a swing that lags a third of a second reads as a broken button.
        _player.Strike();

        _ = server?.AttackAsync(_leaving.Token);
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

        _ = server?.UseSkillAsync(slot, _leaving.Token);
    }

    /// <summary>Casts a learned spell at a chosen serial, or at self when target is zero.</summary>
    public void UseSpell(int slot, uint target)
    {
        if (Frozen)
        {
            return;
        }

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
                Show(effect.TargetAnimation, target.Position, effect.Speed);
            }

            if (effect.SourceAnimation > 0 && Someone(world, effect.Source) is { } source)
            {
                Show(effect.SourceAnimation, source.Position, effect.Speed);
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

    private void Show(int number, Vector2 feet, int speed)
    {
        if (number <= 0 || Flash.Make(number, speed) is not { } flash)
        {
            return;
        }

        // 발밑이 아니라 몸 한가운데쯤에 터진다.
        flash.Position = feet + new Vector2(0, -BodyMiddle);
        _camera.AddChild(flash);
    }

    /// <summary>Plays the sounds the server asked for (0x19). The number is the file's name.</summary>
    private void Sounds()
    {
        while (server is { } world && world.TakeSound(out int number))
        {
            string path = $"res://assets/sound/{number}.mp3";

            if (!ResourceLoader.Exists(path))
            {
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
        _tile = state.Where;
        _walked = -1;
        _player.Position = Ground(_tile);
        _player.Rest();
    }

    public override void _Process(double delta)
    {
        Listen();
        Wear();
        Crowd();
        Herd();
        Swings();
        Flashes();
        Sounds();
        RehearseAPick();
        HuntOnItsOwn();

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
    /// Keeps the player in the middle of the view, wherever they stand. The view used to stop at the edge
    /// of the floor so that nothing past it showed, but the starting tile is a corner one and the screen is
    /// bigger than the original's, so that rule left the character parked in a corner of the screen.
    /// Showing a little emptiness past the edge is the smaller cost.
    /// </summary>
    private void Look()
    {
        Vector2 window = Size;

        // The 20 keeps the character a little below the exact middle, where the original put it.
        float x = _player.Position.X - (window.X / 2);
        float y = _player.Position.Y - (FocusY ?? (window.Y / 2)) - 20;

        _camera.Position = new Vector2(-Mathf.Round(x), -Mathf.Round(y));
    }
}
