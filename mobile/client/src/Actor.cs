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
    /// <param name="Colours">
    /// What to dye each sheet, one per path. A piece with nothing to dye ignores it.
    /// </param>
    /// <param name="StrikePaths">
    /// The same pieces drawn swinging, one for one with <paramref name="Paths" />. A blow is drawn in a
    /// file of its own rather than further along the walk, so this is a second set of sheets and not a
    /// range of frames. Empty for anything with no blow of its own to draw.
    /// </param>
    public sealed record Sheet(
        IReadOnlyList<string> Paths,
        IReadOnlyList<int> Colours,
        int CellWidth,
        int CellHeight,
        float FeetX,
        float FeetY,
        IReadOnlyList<string> StrikePaths,
        CreatureMotion? Motion = null)
    {
        public static Sheet Walk(params string[] paths) => Walk(paths, new int[paths.Length], []);

        public static Sheet Walk(
            IReadOnlyList<string> paths,
            IReadOnlyList<int> colours,
            IReadOnlyList<string> striking) =>
            new(paths, colours, 80, 88, 31.5f, 83f, striking);

        /// <summary>
        /// A creature draws from one sheet and numbers its own frames. Its <paramref name="motion" /> comes
        /// out of the archive beside the picture — without it the person's numbering would be used, which
        /// asks for frames a creature's sheet does not have.
        /// </summary>
        public static Sheet Creature(string path, int size, CreatureMotion? motion = null) =>
            new([path], [0], size, size, size / 2f, size, [], motion);
    }

    private readonly List<Sprite2D> _sprites = [];
    private readonly List<Texture2D> _standing = [];
    private readonly List<Texture2D> _swinging = [];
    private readonly Sheet _sheet;

    /// <summary>How long one drawing of a swing is held. Two of them make a blow.</summary>
    private const double SecondsPerStrikeFrame = 0.14;

    private Direction _direction = Direction.South;
    private int _step;

    // Where we are in a swing, in seconds, or below zero when not swinging.
    private double _struck = -1;

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
        for (int layer = 0; layer < _sheet.Paths.Count; layer++)
        {
            Texture2D worn = Palettes.Load(_sheet.Paths[layer], _sheet.Colours[layer]);

            _standing.Add(worn);
            _swinging.Add(layer < _sheet.StrikePaths.Count
                ? Palettes.Load(_sheet.StrikePaths[layer], _sheet.Colours[layer])
                : worn);

            Sprite2D piece = new()
            {
                Centered = false,
                Texture = worn,
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

        ShowFrame(_sheet.Motion?.Stand() ?? WalkMotion.Stand(facing.Side));
    }

    /// <summary>Advances the walk by one frame in the direction already faced.</summary>
    public void Stride()
    {
        ShowFrame(_sheet.Motion is { } motion
            ? motion.Walk(_step++)
            : WalkMotion.Walk(Facing.Of(_direction).Side, _step++));
    }

    public void Rest()
    {
        _step = 0;
        ShowFrame(_sheet.Motion?.Stand() ?? WalkMotion.Stand(Facing.Of(_direction).Side));
    }

    /// <summary>
    /// Swings once. Nothing follows from it here — whether it hit is the server's to say — and a swing
    /// already under way is left to finish rather than restarted.
    /// </summary>
    public void Strike()
    {
        // 사람은 평타를 다른 파일에 들고 있고, 괴물은 같은 시트 안에 들고 있다. 둘 중 아무것도 없으면
        // 휘두르는 그림이 없다는 뜻이므로 가만히 둔다.
        if (_struck >= 0 || (_sheet.StrikePaths.Count == 0 && _sheet.Motion is null))
        {
            return;
        }

        _struck = 0;
        Wear(_swinging);
        ShowFrame(_sheet.Motion?.Strike(0) ?? WalkMotion.Strike(Facing.Of(_direction).Side, 0));
    }

    public override void _Process(double delta)
    {
        if (_struck < 0)
        {
            return;
        }

        _struck += delta;

        int frame = (int)(_struck / SecondsPerStrikeFrame);
        int swings = _sheet.Motion?.AttackCount ?? WalkMotion.StrikeFrames;

        if (frame >= swings)
        {
            _struck = -1;
            Wear(_standing);
            Rest();

            return;
        }

        ShowFrame(_sheet.Motion?.Strike(frame) ?? WalkMotion.Strike(Facing.Of(_direction).Side, frame));
    }

    /// <summary>Swaps every layer between the sheets it stands in and the ones it swings in.</summary>
    private void Wear(List<Texture2D> sheets)
    {
        for (int layer = 0; layer < _sprites.Count && layer < sheets.Count; layer++)
        {
            _sprites[layer].Texture = sheets[layer];
        }
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
