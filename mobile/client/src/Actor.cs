using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

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
    /// <param name="Parts">
    /// The part letter of each layer (b body, w weapon, s shield …), one for one with <paramref name="Paths" />, so
    /// the layers can be stacked in the order the original client stacks them for the way the figure faces
    /// (<see cref="Wardrobe.Rank" />). Empty for a sheet drawn as one piece.
    /// </param>
    public sealed record Sheet(
        IReadOnlyList<string> Paths,
        IReadOnlyList<int> Colours,
        int CellWidth,
        int CellHeight,
        float FeetX,
        float FeetY,
        IReadOnlyList<string> StrikePaths,
        CreatureMotion? Motion = null,
        IReadOnlyList<char>? Parts = null)
    {
        public static Sheet Walk(params string[] paths) => Walk(paths, new int[paths.Length], []);

        public static Sheet Walk(
            IReadOnlyList<string> paths,
            IReadOnlyList<int> colours,
            IReadOnlyList<string> striking,
            IReadOnlyList<char>? parts = null) =>
            new(paths, colours, 120, 96, 31.5f, 83f, striking, Parts: parts);

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
    private readonly Sheet _sheet;

    // The motion under way, how long each drawing is held, and which pieces have no drawing for it.
    private BodyMotion _playing = BodyMotion.Blow;
    private double _perFrame = SecondsPerStrikeFrame;
    private readonly List<bool> _still = [];

    /// <summary>How long one drawing of a swing is held. Two of them make a blow.</summary>
    private const double SecondsPerStrikeFrame = 0.14;

    private Direction _direction = Direction.South;
    private int _step;

    // Where we are in a motion, in seconds, or below zero when not moving.
    private double _struck = -1;

    // A step between two tiles: from where, to where, how long it takes and how far in we are (below zero when
    // standing). The server only ever says which tile somebody is on now, so the walk in between is ours.
    private Vector2 _stepFrom;
    private Vector2 _stepTo;
    private double _stepSeconds;
    private double _stepped = -1;
    private int _strideDrawn = -1;
    private bool _placed;

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
            _still.Add(false);

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

        if (_sheet.Parts is { } parts && parts.Count == _sprites.Count)
        {
            // Bottom first, as Legend.exe stacks a figure for this side — facing us the weapon goes under the body.
            int place = 0;

            foreach (int layer in Enumerable.Range(0, _sprites.Count).OrderBy(layer => Wardrobe.Rank(parts[layer], facing.Side)))
            {
                MoveChild(_sprites[layer], place++);
            }
        }

        ShowFrame(_sheet.Motion?.Stand(facing.Side) ?? WalkMotion.Stand(facing.Side));
    }

    /// <summary>
    /// Goes to where the server now says this figure stands. A tile away, it walks there — sliding across the gap
    /// and striding as it goes, the way our own figure does. Further than that (just arrived, pulled back) it is
    /// simply there. Asking again for the place it is already going changes nothing, so this can be called on
    /// every frame.
    /// </summary>
    public void GoTo(Vector2 there, float tile, double seconds)
    {
        if (_placed && there == (_stepped >= 0 ? _stepTo : Position))
        {
            return;
        }

        if (!_placed || Position.DistanceTo(there) > tile * 1.5f)
        {
            _placed = true;
            _stepped = -1;
            Position = there;
            return;
        }

        _stepFrom = Position;
        _stepTo = there;
        _stepSeconds = seconds;
        _stepped = 0;
        _strideDrawn = -1;
    }

    /// <summary>Advances the walk by one frame in the direction already faced.</summary>
    public void Stride()
    {
        Facing facing = Facing.Of(_direction);

        ShowFrame(_sheet.Motion?.Walk(facing.Side, _step++) ?? WalkMotion.Walk(facing.Side, _step++));
    }

    public void Rest()
    {
        _step = 0;
        ShowFrame(_sheet.Motion?.Stand(Facing.Of(_direction).Side) ?? WalkMotion.Stand(Facing.Of(_direction).Side));
    }

    /// <summary>
    /// Swings once. Nothing follows from it here — whether it hit is the server's to say — and a swing
    /// already under way is left to finish rather than restarted.
    /// </summary>
    public void Strike()
    {
        if (_struck < 0)
        {
            Play(BodyMotion.Blow, SecondsPerStrikeFrame);
        }
    }

    /// <summary>
    /// Plays a body motion the server named, towards the way the figure already faces — a spell does not turn
    /// anybody round (user decision). Each piece plays it from its own file for that motion; a piece with no
    /// such file is not drawn until the motion ends. A creature has one blow of its own and plays that whatever
    /// the motion.
    /// </summary>
    /// <remarks>
    /// A new motion takes over from one under way. The server sends several at once when one press sets off
    /// more than one thing — Hades runs every learned skill of the blow kind with the plain blow (1, then 131,
    /// then 133) — and the last is what the figure ends up doing.
    /// <para>
    /// A class motion is only drawn whole in that class's clothes: skill.tbl's ST column lists the clothes each
    /// motion is for, and ordinary clothes have no file for it (trousers have none for a rogue, a shield none for
    /// any skill), so the figure shows through. The original does the same (user).
    /// </para>
    /// </remarks>
    public void Play(BodyMotion motion, double secondsPerFrame)
    {
        if (_sheet.Motion is null)
        {
            List<Texture2D?> sheets = [];

            for (int layer = 0; layer < _sprites.Count; layer++)
            {
                sheets.Add(MotionSheet(layer, motion));
            }

            // 어느 부위에도 그 동작 그림이 없으면 그릴 것이 없다 — 가만히 둔다.
            if (sheets.TrueForAll(sheet => sheet is null))
            {
                return;
            }

            for (int layer = 0; layer < _sprites.Count; layer++)
            {
                _still[layer] = sheets[layer] is null;
                _sprites[layer].Texture = sheets[layer] ?? _standing[layer];
                _sprites[layer].Visible = sheets[layer] is not null;
            }
        }

        _playing = motion;
        _perFrame = secondsPerFrame;
        _struck = 0;
        ShowPlaying(0);
    }

    public override void _Process(double delta)
    {
        if (_stepped >= 0)
        {
            _stepped += delta;

            double progress = Mathf.Min(1.0, _stepped / _stepSeconds);
            Position = _stepFrom.Lerp(_stepTo, (float)progress);

            if (progress >= 1.0)
            {
                _stepped = -1;

                if (_struck < 0)
                {
                    Rest();
                }
            }
            else if (_struck < 0 && (int)(progress * WalkMotion.WalkFrames) is int stride && stride != _strideDrawn)
            {
                _strideDrawn = stride;
                Stride();
            }
        }

        if (_struck < 0)
        {
            return;
        }

        _struck += delta;

        int frame = (int)(_struck / _perFrame);
        int frames = _sheet.Motion?.AttackCount ?? _playing.Count;

        if (frame >= frames)
        {
            _struck = -1;
            Wear(_standing);
            Rest();

            return;
        }

        ShowPlaying(frame);
    }

    /// <summary>
    /// The sheet one piece draws this motion from, or nothing when it has none. The blow is the separate file
    /// the dresser found (ending 02); a skill is the same piece's file with the class letter on the end.
    /// </summary>
    private Texture2D? MotionSheet(int layer, BodyMotion motion)
    {
        string walk = _sheet.Paths[layer];
        string? path = motion.File == BodyMotion.Blow.File
            ? layer < _sheet.StrikePaths.Count && _sheet.StrikePaths[layer] != walk ? _sheet.StrikePaths[layer] : null
            : walk.EndsWith(".png") ? $"{walk[..^4]}{motion.File}.png" : null;

        return path is not null && ResourceLoader.Exists(path) ? Palettes.Load(path, _sheet.Colours[layer]) : null;
    }

    private void ShowPlaying(int step)
    {
        Facing facing = Facing.Of(_direction);

        if (_sheet.Motion is { } creature)
        {
            ShowFrame(creature.Strike(facing.Side, step));
            return;
        }

        for (int layer = 0; layer < _sprites.Count; layer++)
        {
            if (!_still[layer])
            {
                int frame = _playing.Frame(facing.Side, step);
                _sprites[layer].RegionRect = new Rect2(frame * _sheet.CellWidth, 0, _sheet.CellWidth, _sheet.CellHeight);
            }
        }
    }

    /// <summary>Puts every layer back on the sheets it stands in, pieces a motion left out included.</summary>
    private void Wear(List<Texture2D> sheets)
    {
        for (int layer = 0; layer < _sprites.Count && layer < sheets.Count; layer++)
        {
            _sprites[layer].Texture = sheets[layer];
            _sprites[layer].Visible = true;
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
