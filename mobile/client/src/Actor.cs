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
    public sealed record Sheet(IReadOnlyList<string> Paths, int CellWidth, int CellHeight, int ArtOffset)
    {
        /// <summary>
        /// Stacked wardrobe pieces do not land in the middle of their cell — the clothes decide where the
        /// figure sits — so the offset says how far right of centre it actually is.
        /// </summary>
        public static Sheet Walk(params string[] paths) => new(paths, 47, 83, 8);

        public static Sheet Creature(string path, int size) => new([path], size, size, 0);
    }

    private readonly List<Sprite2D> _sprites = [];
    private readonly Sheet _sheet;

    private Direction _direction = Direction.South;
    private int _step;

    public string DisplayName { get; }

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
                Offset = new Vector2(-(_sheet.CellWidth / 2f) - _sheet.ArtOffset, -_sheet.CellHeight)
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
