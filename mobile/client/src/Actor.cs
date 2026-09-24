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

    // The emote over the head, how long it has been up (below zero when none) and the sprite it is drawn on.
    private const string EmoteSheet = "res://assets/actor/emote.png";
    private Emote? _emote;
    private double _emoted = -1;
    private Sprite2D? _balloon;

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

    /// <summary>The bar over the head, made when the actor is put on the floor.</summary>
    private HealthBar? _hurt;

    /// <summary>Says this one was struck and this much of them is left, as a percentage the server sends (0x13).</summary>
    public void Struck(int left) => _hurt?.Struck(left);

    /// <summary>What this one said last, over the bar. Made the first time they speak.</summary>
    private SpeechBubble? _speech;

    /// <summary>Where over the head the words sit — just above the bar.</summary>
    private float _speechAt = -64;

    /// <summary>Shows words over the head for a few seconds (0x0D).</summary>
    public void Say(string words)
    {
        if (_speech is null)
        {
            _speech = new SpeechBubble { Name = "Speech", Position = new Vector2(0, _speechAt) };
            AddChild(_speech);
        }

        _speech.Say(words);
    }

    /// <summary>The badges under the bar — what is on this one now.</summary>
    private StatusRow? _ailing;

    /// <summary>
    /// Says what is on this one now (0x3A for us, 0x5C for others). An empty list clears the badges; a coma hides
    /// them and keeps the head slot for the coma (<see cref="Overhead" />).
    /// </summary>
    public void Ailing(IEnumerable<Ailment> ailments)
    {
        List<Ailment> on = [.. ailments];

        Comatose = Overhead.InComa(on);
        _ailing?.Show(on);
    }

    /// <summary>Whether this one is in a coma now — the coma effect then owns the head slot.</summary>
    public bool Comatose { get; private set; }

    /// <summary>The top of the drawn head, from the feet (negative) — where the head slot starts.</summary>
    public float HeadTop { get; private set; } = -60;

    /// <summary>Where a floating number starts now, from the feet: just above the bar and badges that are up.</summary>
    public float FigureStart => FloatingFigure.Start(
        HeadTop, _hurt?.Visible ?? false, _ailing?.Visible ?? false, HealthBar.Thickness, StatusRow.Height);

    /// <summary>Puts the badge row over the bar while it shows, and in its place when it does not.</summary>
    private void Stack()
    {
        if (_ailing is null || _hurt is null)
        {
            return;
        }

        float badges = Overhead.Place(HeadTop, _hurt.Visible, HealthBar.Thickness, StatusRow.Height).Badges;

        if (_ailing.Position.Y != badges)
        {
            _ailing.Position = new Vector2(0, badges);
        }
    }

    /// <summary>The colour a monster's body is tinted with while a spell is on it; white is none.</summary>
    private Color _tint = Colors.White;

    /// <summary>
    /// Tints the body — only the drawn figure, not the bar or badges over it — in a spell's colour for as long as
    /// it lasts (0x5C, <see cref="Flash.Tint" />). White takes it off.
    /// </summary>
    public void Tint(Color colour)
    {
        if (colour == _tint)
        {
            return;
        }

        _tint = colour;

        foreach (Sprite2D piece in _sprites)
        {
            piece.SelfModulate = colour;
        }
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

            piece.SelfModulate = _tint;
            _sprites.Add(piece);
            AddChild(piece);
        }

        // 머리 위는 아래에서부터 머리 이펙트 칸 · 체력바 · 배지 한 줄이다(Overhead). 그림 칸이 아니라 **그려진 머리**
        // 위에 둔다 — 칸은 옆으로 뻗은 무기까지 담느라 사람 기준 120x96 에 발이 83 이라, 칸 꼭대기에 붙이면
        // 머리 위로 서른 칸쯤 떠 버린다.
        HeadTop = -_sheet.FeetY + DrawnTop(_standing[0], _sheet);

        (float bar, float badges) = Overhead.Place(HeadTop, true, HealthBar.Thickness, StatusRow.Height);

        _hurt = new HealthBar { Name = "Health", Visible = false, Position = new Vector2(0, bar) };
        AddChild(_hurt);

        // 말은 가장 높이 선 배지 줄 위에 — 막대·배지가 떠 있어도 겹치지 않는다.
        _speechAt = badges - 3;

        _ailing = new StatusRow { Name = "Status", Visible = false };
        AddChild(_ailing);
        Stack();

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

        StackBalloon();

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
    public void Strike() => Play(BodyMotion.Blow, SecondsPerStrikeFrame);

    /// <summary>
    /// Plays a body motion the server named, towards the way the figure already faces — a spell does not turn
    /// anybody round (user decision). Each piece plays it from its own file for that motion; a piece with no
    /// such file is not drawn until the motion ends. A creature has one blow of its own and plays that whatever
    /// the motion.
    /// </summary>
    /// <remarks>
    /// A motion that arrives while another is under way is ignored, as the original client does (Legend.exe 2005
    /// 0x4e1130: a busy figure keeps what it is doing). Hades sends several at once when one press sets off more than
    /// one thing — every learned skill of the blow kind runs with the plain blow (1, then 131, then 133) — so only
    /// the first of those is seen, the same as it would be in the original.
    /// <para>
    /// A class motion is only drawn whole in that class's clothes: skill.tbl's ST column lists the clothes each
    /// motion is for, and ordinary clothes have no file for it (trousers have none for a rogue, a shield none for
    /// any skill), so the figure shows through. The original does the same (user).
    /// </para>
    /// </remarks>
    public void Play(BodyMotion motion, double secondsPerFrame)
    {
        if (_struck >= 0 || _emoted >= 0)
        {
            return;
        }

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

    /// <summary>
    /// Shows an emote over the head — a face drawn on the head, or a balloon above it with the face — while the body
    /// stands.
    /// </summary>
    /// <remarks>
    /// As the original does (Legend.exe 2005 0x4e1104): only a figure facing us shows one, and only when it is doing
    /// nothing else; while it is up the figure is busy, so a motion that arrives meanwhile is ignored. It is drawn just
    /// under the hair (0x4e7ca7, before the first H), so hair and a helmet cover the top of the bare head.
    /// </remarks>
    public void Show(Emote emote)
    {
        if (_sheet.Motion is not null || _struck >= 0 || _emoted >= 0 || Facing.Of(_direction).Side != Lod.Mobile.Core.Art.Side.Front)
        {
            return;
        }

        if (_balloon is null)
        {
            // The emote sheet is cut to the wardrobe's cells, so it stands on the same feet as the pieces.
            _balloon = new Sprite2D
            {
                Centered = false,
                Texture = GD.Load<Texture2D>(EmoteSheet),
                RegionEnabled = true,
                Offset = new Vector2(-_sheet.FeetX, -_sheet.FeetY)
            };

            AddChild(_balloon);
        }

        _emote = emote;
        _emoted = 0;
        _balloon.Visible = true;
        StackBalloon();
        ShowEmote();
    }

    public override void _Process(double delta)
    {
        Stack();

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

        if (_emoted >= 0)
        {
            _emoted += delta;

            if (_emoted >= _emote!.Seconds)
            {
                _emoted = -1;
                _balloon!.Visible = false;
            }
            else
            {
                ShowEmote();
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
    /// Hands up, the kiss and the wave (ending 03) are drawn without the shield, the weapon or its front piece —
    /// the original leaves those out of that file's motions (Legend.exe 2005 0x4e78f4 → 0x4e84e0).
    /// </summary>
    private Texture2D? MotionSheet(int layer, BodyMotion motion)
    {
        if (motion.File == "03" && _sheet.Parts is { } parts && layer < parts.Count && parts[layer] is 's' or 'w' or 'p')
        {
            return null;
        }

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

    private void ShowEmote() =>
        _balloon!.RegionRect = new Rect2(_emote!.FrameAt(_emoted) * _sheet.CellWidth, 0, _sheet.CellWidth, _sheet.CellHeight);

    /// <summary>Puts the emote just under the hair — above everything below it in the order this side is stacked in.</summary>
    private void StackBalloon()
    {
        if (_balloon is null || _sheet.Parts is not { } parts || parts.Count != _sprites.Count)
        {
            return;
        }

        var side = Facing.Of(_direction).Side;
        MoveChild(_balloon, parts.Count(part => Wardrobe.Rank(part, side) < Wardrobe.Rank('h', side)));
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

    /// <summary>
    /// How far down the cell the drawing actually starts — the first row with anything painted on it. A cell is
    /// cut wide and tall enough for a weapon held out and for the tallest pose, so its top is nowhere near the
    /// head; hanging a bar off the cell leaves it floating in the air.
    /// </summary>
    /// <remarks>Measured once per sheet, because reading a picture back is slow and thirty monsters share a few.</remarks>
    private static float DrawnTop(Texture2D sheet, Sheet cut)
    {
        string key = $"{sheet.ResourcePath}|{cut.CellWidth}x{cut.CellHeight}";

        if (_heads.TryGetValue(key, out float known))
        {
            return known;
        }

        float top = 0;
        Image drawn = sheet.GetImage();

        if (drawn is not null)
        {
            int wide = Mathf.Min(cut.CellWidth, drawn.GetWidth());
            int tall = Mathf.Min(cut.CellHeight, drawn.GetHeight());

            for (int row = 0; row < tall && top == 0; row++)
            {
                for (int column = 0; column < wide; column++)
                {
                    if (drawn.GetPixel(column, row).A > 0.1f)
                    {
                        top = row;
                        break;
                    }
                }
            }
        }

        _heads[key] = top;
        return top;
    }

    /// <summary>Where the drawing starts in each sheet we have measured.</summary>
    private static readonly Dictionary<string, float> _heads = [];

    private void ShowFrame(int frame)
    {
        Rect2 cell = new(frame * _sheet.CellWidth, 0, _sheet.CellWidth, _sheet.CellHeight);

        foreach (Sprite2D piece in _sprites)
        {
            piece.RegionRect = cell;
        }
    }
}
