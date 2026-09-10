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

    // Worn by anyone the server has not described — somebody we have only ever seen take a step.
    private const string OtherSheet = "res://assets/actor/npc-walk.png";

    // One drawing per wardrobe piece, all cut on the same cell, so they stack without arithmetic.
    private const string PartsFolder = "res://assets/actor/parts/";

    /// <summary>How long one tile takes to walk, and how many frames that walk is drawn in.</summary>
    private const double StepSeconds = 0.28;

    // Y sorting is what makes someone standing in front actually draw in front, which an isometric floor
    // needs: screen height is depth here.
    private readonly Node2D _camera = new() { Name = "Camera", YSortEnabled = true };
    private readonly Sprite2D _floor = new() { Name = "Floor", Centered = false };

    private readonly CancellationTokenSource _leaving = new();

    private Actor _player = null!;

    // Our own figure starts in borrowed clothes because the server has not spoken yet.
    private bool _wearingOwn;
    private Vector2 _floorSize;

    // The tile we believe we are on. A walk moves it straight away, because the server answers an allowed
    // step with silence; when it does speak, it wins.
    private Tile _tile;
    private int _heard = -1;
    private int _rows = 31;

    // Everyone the server has shown us, by the serial it calls them.
    private readonly Dictionary<uint, Actor> _crowd = [];

    // Whoever is picked out, and the mark that says so. Zero is nobody.
    private readonly TargetMark _mark = new() { Name = "Target", Visible = false };
    private uint _target;
    private bool _rehearsedPick;
    private int _settling;

    private Queue<Direction> _rehearsal = new();

    private Vector2 _from;
    private Vector2 _to;
    private double _walked = -1;
    private int _drawnFrame = -1;

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

            return one is null ? string.Empty : one.Name.Length > 0 ? one.Name : one.Serial.ToString();
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
        _player = Add(new Actor("수련생", Actor.Sheet.Walk(HeroSheet)), Ground(_tile));

        if (server is null)
        {
            // Nobody to ask, so stand a couple of figures up to look at.
            Add(new Actor("주모", Actor.Sheet.Walk(OtherSheet)), Ground(new Tile(6, 4))).Face(Direction.South);
            Add(new Actor("말벌", Actor.Sheet.Creature("res://assets/actor/wasp.png", 59)), Ground(new Tile(3, 6)));
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
    private void Choose(Vector2 where)
    {
        const float reach = 34;
        const float waist = 32;

        float nearest = reach * reach;

        _target = 0;

        foreach ((uint serial, Actor actor) in _crowd)
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

    /// <summary>Puts the mark on whoever is picked out, or takes it away.</summary>
    private void Mark()
    {
        if (_target != 0 && _crowd.TryGetValue(_target, out Actor? actor))
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
        if (_walked >= 0)
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

            if (!_crowd.TryGetValue(one.Serial, out Actor? actor))
            {
                // The server gives a name with the appearance; a serial is only for somebody we have
                // only ever seen take a step.
                string called = one.Name.Length > 0 ? one.Name : one.Serial.ToString();

                // Somebody who changes clothes is not redressed until they leave and come back; the
                // server does say so, and this is where to listen when there is anything to wear.
                actor = Add(new Actor(called, Dress(one)), Ground(one.Where));
                _crowd[one.Serial] = actor;
            }

            actor.Position = Ground(one.Where);
            actor.Face(one.Facing);
        }

        foreach (uint serial in _crowd.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _crowd[serial].QueueFree();
            _crowd.Remove(serial);
        }

        Mark();
    }

    /// <summary>
    /// The pieces somebody is drawn from. A piece we have no picture for is left out rather than left
    /// blank — the wardrobe in this repository only holds what the world can currently hand out.
    /// </summary>
    private static Actor.Sheet Dress(Character one)
    {
        if (one.Wearing is null)
        {
            return Actor.Sheet.Walk(OtherSheet);
        }

        List<string> paths = [];
        List<int> colours = [];

        foreach (Piece piece in Wardrobe.Pieces(one.Wearing))
        {
            string path = $"{PartsFolder}{piece.Name}.png";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            paths.Add(path);
            colours.Add(piece.Colour);
        }

        return paths.Count > 0 ? Actor.Sheet.Walk(paths, colours) : Actor.Sheet.Walk(OtherSheet);
    }

    /// <summary>
    /// Puts our own figure into what the server says we are wearing. It describes us like anybody else, but
    /// only after we are already standing there, so the figure has to be built again once it does.
    /// </summary>
    private void Wear()
    {
        if (_wearingOwn || server?.Self is not { Wearing: not null } mine)
        {
            return;
        }

        _wearingOwn = true;

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
        if (_rehearsedPick || !Main.Picking || _crowd.Count == 0 || _heard < 0 || _settling++ < 60)
        {
            return;
        }

        _rehearsedPick = true;

        Actor somebody = _crowd.Values.First();

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

    /// <summary>Keeps the player in the middle without showing anything past the edge of the floor.</summary>
    private void Look()
    {
        Vector2 window = Size;

        float x = Mathf.Clamp(_player.Position.X - (window.X / 2), 0, Mathf.Max(0, _floorSize.X - window.X));
        float y = Mathf.Clamp(_player.Position.Y - (window.Y / 2) - 20, 0, Mathf.Max(0, _floorSize.Y - window.Y));

        _camera.Position = new Vector2(-Mathf.Round(x), -Mathf.Round(y));
    }
}
