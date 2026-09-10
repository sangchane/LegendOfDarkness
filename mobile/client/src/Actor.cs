using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// A figure standing on the floor. Its origin is where its feet touch, so the world can place it by tile
/// and the mirror for west and south turns it about itself rather than sliding it sideways.
/// </summary>
public sealed partial class Actor : Node2D
{
    /// <summary>One sheet: frames laid left to right, and where the drawing sits inside its cell.</summary>
    public sealed record Sheet(string Path, int CellWidth, int CellHeight, int ArtOffset)
    {
        /// <summary>
        /// Stacked wardrobe pieces do not land in the middle of their cell — the clothes decide where the
        /// figure sits — so the offset says how far right of centre it actually is.
        /// </summary>
        public static Sheet Walk(string path) => new(path, 47, 83, 8);

        public static Sheet Creature(string path, int size) => new(path, size, size, 0);
    }

    private readonly Sprite2D _sprite = new() { Centered = false };
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
        _sprite.Texture = GD.Load<Texture2D>(_sheet.Path);
        _sprite.RegionEnabled = true;

        // Drawn up and to the left of the origin, so the origin is between the feet.
        _sprite.Offset = new Vector2(-(_sheet.CellWidth / 2f) - _sheet.ArtOffset, -_sheet.CellHeight);

        AddChild(_sprite);

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

    private void ShowFrame(int frame) =>
        _sprite.RegionRect = new Rect2(frame * _sheet.CellWidth, 0, _sheet.CellWidth, _sheet.CellHeight);
}
