using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// A figure standing on the floor. Its origin is where its feet touch, so the world can place it by tile
/// and the mirror for west and south turns it about itself rather than sliding it sideways.
/// </summary>
public sealed partial class Actor : Node2D
{
    /// <summary>
    /// The sheets a figure is drawn from: frames laid left to right, furthest back first, and where the
    /// drawing sits inside its cell. Wardrobe pieces are cut on one shared cell, so several sheets laid
    /// over each other line up without any further arithmetic.
    /// </summary>
    /// <param name="FeetX">
    /// Where in the cell the figure stands. A wardrobe cell has room for a weapon held out to one side and
    /// for the tallest pose, so the figure is nowhere near the middle of it and the cell cannot say where
    /// the feet are. Both numbers come from scripts/build-client-assets.ps1, which cuts every sheet.
    /// </param>
    public sealed record Sheet(
        IReadOnlyList<string> Paths,
        int CellWidth,
        int CellHeight,
        float FeetX,
        float FeetY)
    {
        public static Sheet Walk(params string[] paths) => new(paths, 80, 88, 31.5f, 83f);

        public static Sheet Creature(string path, int size) => new([path], size, size, size / 2f, size);
    }

    private readonly List<Sprite2D> _sprites = [];
    private readonly Sheet _sheet;

    private Direction _direction = Direction.South;
    private int _step;

    public string DisplayName { get; }

    /// <summary>Which way the figure is turned, so a replacement can be stood the same way.</summary>
    public Direction Looking => _direction;

    public Actor(string displayName, Sheet sheet)
    {
        DisplayName = displayName;
        _sheet = sheet;
        Name = displayName;
    }

    public override void _Ready()
    {
        foreach (string path in _sheet.Paths)
        {
            Sprite2D piece = new()
            {
                Centered = false,
                Texture = GD.Load<Texture2D>(path),
                RegionEnabled = true,

                // Drawn up and to the left of the origin, so the origin is between the feet.
                Offset = new Vector2(-_sheet.FeetX, -_sheet.FeetY)
            };

            _sprites.Add(piece);
            AddChild(piece);
        }

        Face(_direction);
    }

    public void Face(Direction direction)
    {
        _direction = direction;

        Facing facing = Facing.Of(direction);

        // Mirroring about this node's origin keeps the feet where they were.
        Scale = new Vector2(facing.Mirror ? -1 : 1, 1);

        ShowFrame(WalkMotion.Stand(facing.Side));
    }

    /// <summary>Advances the walk by one frame in the direction already faced.</summary>
    public void Stride()
    {
        ShowFrame(WalkMotion.Walk(Facing.Of(_direction).Side, _step++));
    }

    public void Rest()
    {
        _step = 0;
        ShowFrame(WalkMotion.Stand(Facing.Of(_direction).Side));
    }

    private void ShowFrame(int frame)
    {
        Rect2 cell = new(frame * _sheet.CellWidth, 0, _sheet.CellWidth, _sheet.CellHeight);

        foreach (Sprite2D piece in _sprites)
        {
            piece.RegionRect = cell;
        }
    }
}
