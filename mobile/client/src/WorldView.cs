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

    private readonly CancellationTokenSource _leaving = new();

    private Actor _player = null!;

    // Our own figure starts in borrowed clothes because the server has not spoken yet.
    // 우리 자신이 마지막으로 그려졌을 때 입고 있던 것. 아직 아무것도 못 들었으면 null 이 아니라
    // 없음이라, 사전과 달리 값 하나로 둔다.
    private Appearance? _wearingOwn;
    private bool _dressedOnce;
    private Vector2 _floorSize;

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

    // Whoever is picked out, and the mark that says so. Zero is nobody.
    private readonly TargetMark _mark = new() { Name = "Target", Visible = false };
    private uint _target;
    private bool _rehearsedPick;
    private int _settling;
    private int _rehearsedStrike = -1;

    private Queue<Direction> _rehearsal = new();

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

            return beast is null ? string.Empty
                : beast.Name.Length > 0 ? beast.Name
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
        _camera.AddChild(_floor);
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
    /// A tap picks out whoever was tapped, and picks nobody when it lands on empty floor. Nothing follows
    /// from that on its own — attacking and talking are separate asks — so a mistaken tap costs nothing.
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
        const float waist = 32;

        float nearest = reach * reach;

        _target = 0;

        foreach ((uint serial, Actor actor) in _crowd.Concat(_herd))
        {
            float distance = (actor.Position - new Vector2(0, waist)).DistanceSquaredTo(where);

            if (distance < nearest)
            {
                nearest = distance;
                _target = serial;
            }
        }

        Mark();
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

        _target = 0;
        _mark.Visible = false;
    }

    /// <summary>Starts a step. Ignored while one is still running, so a tile is never half walked.</summary>
    public void Walk(Direction direction)
    {
        if (Frozen || _walked >= 0)
        {
            return;
        }

        _player.Face(direction);

        (int column, int row) = Facing.TileStep(direction);
        Tile next = new(_tile.X + column, _tile.Y + row);

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

            actor.Position = Ground(one.Where);
            actor.Face(one.Facing);
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

        foreach (Piece piece in Wardrobe.Pieces(one.Wearing))
        {
            string path = $"{PartsFolder}{piece.Name}.png";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            paths.Add(path);
            colours.Add(piece.Colour);

            // A piece with no swing of its own keeps standing while the rest of the figure moves. That is
            // how the original looks too — a hat does not swing.
            string swung = $"{PartsFolder}{piece.Name}02.png";
            striking.Add(ResourceLoader.Exists(swung) ? swung : path);
        }

        return paths.Count > 0 ? Actor.Sheet.Walk(paths, colours, striking) : Actor.Sheet.Walk(OtherSheet);
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
                    // Nothing cut for this one yet. Better an empty tile than a wrong picture.
                    continue;
                }

                string called = one.Name.Length > 0 ? one.Name : one.Serial.ToString();

                actor = Add(new Actor(called, CreatureSheet(path)), Ground(one.Where));
                _herd[one.Serial] = actor;
            }

            actor.Position = Ground(one.Where);
            actor.Face(one.Facing);
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
    }

    /// <summary>Whoever is picked out, by serial, or zero for nobody.</summary>
    public uint Target => _target;

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
    /// Draws whoever the server says has swung. Our own blow is drawn as it is asked for rather than here,
    /// so a swing of ours that comes back is left alone.
    /// </summary>
    private void Swings()
    {
        while (server is { } world && world.TakeMotion(out uint serial))
        {
            if (serial == world.Serial)
            {
                continue;
            }

            if (_crowd.TryGetValue(serial, out Actor? actor) || _herd.TryGetValue(serial, out actor))
            {
                actor.Strike();
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
        RehearseAPick();

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
        float y = _player.Position.Y - (window.Y / 2) - 20;

        _camera.Position = new Vector2(-Mathf.Round(x), -Mathf.Round(y));
    }
}
