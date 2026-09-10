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

    /// <summary>How long one tile takes to walk, and how many frames that walk is drawn in.</summary>
    private const double StepSeconds = 0.28;

    // Y sorting is what makes someone standing in front actually draw in front, which an isometric floor
    // needs: screen height is depth here.
    private readonly Node2D _camera = new() { Name = "Camera", YSortEnabled = true };
    private readonly Sprite2D _floor = new() { Name = "Floor", Centered = false };

    private readonly CancellationTokenSource _leaving = new();

    private Actor _player = null!;
    private Vector2 _floorSize;

    // The tile we believe we are on. A walk moves it straight away, because the server answers an allowed
    // step with silence; when it does speak, it wins.
    private Tile _tile;
    private int _heard = -1;
    private int _rows = 31;

    private Queue<Direction> _rehearsal = new();

    private Vector2 _from;
    private Vector2 _to;
    private double _walked = -1;
    private int _drawnFrame = -1;

    /// <summary>The tile the player is on, as this client believes it — which is what a player wants shown.</summary>
    public Tile Standing => _tile;

    /// <summary>What the server calls this map, once it has said.</summary>
    public string PlaceName => server?.State?.Map.Name ?? string.Empty;

    public override void _ExitTree() => _leaving.Cancel();

    public override void _Ready()
    {
        Name = "World";
        ClipContents = true;

        _floor.Texture = GD.Load<Texture2D>(FloorPath);
        _floorSize = _floor.Texture.GetSize();

        AddChild(_camera);
        _camera.AddChild(_floor);

        // Offline the figures stand on the dark rug in the middle of the safe house; connected, the server
        // says where the player is and this moves them there as soon as it does.
        _tile = new Tile(4, 4);

        _player = Add(new Actor("수련생", Actor.Sheet.Walk("res://assets/actor/hero-walk.png")), Ground(_tile));
        Add(new Actor("주모", Actor.Sheet.Walk("res://assets/actor/npc-walk.png")), Ground(new Tile(6, 4))).Face(Direction.South);
        Add(new Actor("말벌", Actor.Sheet.Creature("res://assets/actor/wasp.png", 59)), Ground(new Tile(3, 6)));

        if (server is not null)
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
