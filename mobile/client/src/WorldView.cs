using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// The floor and everyone standing on it, seen through a window that follows the player. Every picture here
/// was drawn out of this repository's own archives by scripts/build-client-assets.ps1.
/// </summary>
public sealed partial class WorldView : Control
{
    private const string FloorPath = "res://assets/world/safehouse.png";

    /// <summary>How long one tile takes to walk, and how many frames that walk is drawn in.</summary>
    private const double StepSeconds = 0.28;

    // Y sorting is what makes someone standing in front actually draw in front, which an isometric floor
    // needs: screen height is depth here.
    private readonly Node2D _camera = new() { Name = "Camera", YSortEnabled = true };
    private readonly Sprite2D _floor = new() { Name = "Floor", Centered = false };

    private Actor _player = null!;
    private Vector2 _floorSize;

    private Queue<Direction> _rehearsal = new();

    private Vector2 _from;
    private Vector2 _to;
    private double _walked = -1;
    private int _drawnFrame = -1;

    public WorldView()
    {
        Name = "World";
        ClipContents = true;
    }

    public override void _Ready()
    {
        _floor.Texture = GD.Load<Texture2D>(FloorPath);
        _floorSize = _floor.Texture.GetSize();

        AddChild(_camera);
        _camera.AddChild(_floor);

        // Standing on the dark rug in the middle of the safe house, which is where lod1 starts you.
        _player = Add(new Actor("수련생", Actor.Sheet.Walk("res://assets/actor/hero-walk.png")), new Vector2(700, 470));
        Add(new Actor("주모", Actor.Sheet.Walk("res://assets/actor/npc-walk.png")), new Vector2(790, 430)).Face(Direction.South);
        Add(new Actor("말벌", Actor.Sheet.Creature("res://assets/actor/wasp.png", 59)), new Vector2(640, 420));

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

        (int x, int y) = Facing.Step(direction);

        _from = _player.Position;
        _to = new Vector2(
            Mathf.Clamp(_from.X + x, 40, _floorSize.X - 40),
            Mathf.Clamp(_from.Y + y, 60, _floorSize.Y - 10));

        _walked = 0;
        _drawnFrame = -1;
    }

    public override void _Process(double delta)
    {
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
