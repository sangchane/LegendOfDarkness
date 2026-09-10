using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// Main game greybox, built to section 5 of the wireframes. The world fills the whole screen while every
/// label and control stays inside the safe area, and the two thumb clusters sit where a thumb can reach.
/// </summary>
public partial class GameScreen : Control
{
    private const int AuxFontSize = 14;
    private const int AttackSize = 64;
    private const int StatusBarWidth = 120;
    private const int PortraitStatusBarWidth = 96;

    /// <summary>Rows the chat and combat log keeps in portrait. Landscape has no room for it at all.</summary>
    private const int LogHeight = 76;

    private WorldView _world = null!;
    private Label _place = null!;
    private Control _topRow = null!;
    private Control _controlRow = null!;

    private readonly WorldClient? _server;

    public GameScreen(WorldClient? server = null)
    {
        _server = server;
        Name = "GameScreen";
        AnchorRight = 1;
        AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    public override void _Ready()
    {
        MarginContainer hud = Main.SafeAreaContainer();

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);

        _topRow = BuildTopRow();

        if (Main.Portrait)
        {
            // Portrait has the height to give the world a row of its own, so nothing the player is aiming
            // at sits under a thumb. That is the whole reason to hold the phone this way.
            BuildWorld(fullBleed: false);
            _controlRow = BuildControlRow();

            AddChild(hud);
            hud.AddChild(rows);
            rows.AddChild(_topRow);
            rows.AddChild(_world);
            rows.AddChild(BuildLog());
            rows.AddChild(_controlRow);
        }
        else
        {
            // Landscape has no such room: the world fills the screen and the HUD floats over its corners.
            BuildWorld(fullBleed: true);
            _controlRow = BuildControlRow();

            AddChild(_world);
            AddChild(hud);
            hud.AddChild(rows);
            rows.AddChild(_topRow);
            rows.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
            rows.AddChild(_controlRow);
        }
    }

    /// <summary>
    /// The world itself. In landscape it fills the screen and the HUD floats over it; in portrait it takes a
    /// row of its own so nothing the player is aiming at sits under a thumb.
    /// </summary>
    private Control BuildWorld(bool fullBleed)
    {
        _world = new WorldView(_server);

        if (fullBleed)
        {
            _world.AnchorRight = 1;
            _world.AnchorBottom = 1;
            _world.GrowHorizontal = GrowDirection.Both;
            _world.GrowVertical = GrowDirection.Both;
        }
        else
        {
            _world.SizeFlagsVertical = SizeFlags.ExpandFill;
        }

        return _world;
    }

    /// <summary>
    /// What the original kept at the bottom of its screen. Portrait has the height to keep it, and it is
    /// what replaces the passing notice landscape has to make do with.
    /// </summary>
    private static Control BuildLog()
    {
        Panel frame = new() { CustomMinimumSize = new Vector2(0, LogHeight) };
        frame.AddThemeStyleboxOverride("panel", Greybox.Surface());

        MarginContainer inset = new()
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };
        inset.AddThemeConstantOverride("margin_left", Main.Gutter);
        inset.AddThemeConstantOverride("margin_right", Main.Gutter);
        inset.AddThemeConstantOverride("margin_bottom", Main.Gutter / 2);
        frame.AddChild(inset);

        VBoxContainer lines = new() { Alignment = BoxContainer.AlignmentMode.End };
        lines.AddThemeConstantOverride("separation", 2);

        foreach (string line in new[] { "\uc548\uc804 \uac00\uc625\uc5d0 \ub4e4\uc5b4\uc654\uc2b5\ub2c8\ub2e4.", "\uc8fc\ubaa8: \uc5b4\uc11c \uc624\uc2dc\uac8c.", "\uac70\ubbf8\ub97c \uaca8\ub215\ub2c8\ub2e4." })
        {
            lines.AddChild(Aux(line));
        }

        inset.AddChild(lines);

        return frame;
    }

    /// <summary>Name and health on the left, world state and inventory on the right, on a plate that keeps
    /// them readable over the floor.</summary>
    private Control BuildTopRow()
    {
        PanelContainer plate = new();
        plate.AddThemeStyleboxOverride("panel", Greybox.Plate());

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        row.AddChild(Aux("수련생"));
        row.AddChild(BuildHealth());
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // Where the server says we are. Offline it stays empty rather than claiming something untrue.
        _place = Aux(string.Empty);

        // 360 across cannot hold this as well, so in portrait the log carries it instead.
        if (!Main.Portrait)
        {
            row.AddChild(_place);
        }

        row.AddChild(new Button
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        });

        plate.AddChild(row);

        return plate;
    }

    /// <summary>
    /// Keeps the place name in step with the server. Nothing else on this screen changes yet, so this is
    /// the one thing worth watching each frame.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_world.PlaceName.Length > 0)
        {
            _place.Text = $"{_world.PlaceName} · {_world.Standing.X},{_world.Standing.Y}";
        }
    }

    /// <summary>Bar and numbers together: health must never be readable by colour alone.</summary>
    private static Control BuildHealth()
    {
        HBoxContainer health = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        health.AddThemeConstantOverride("separation", Main.Gutter / 2);

        ProgressBar bar = new()
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? PortraitStatusBarWidth : StatusBarWidth, 12),
            MaxValue = 250,
            Value = 180,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        bar.AddThemeStyleboxOverride("background", Greybox.Surface());
        bar.AddThemeStyleboxOverride("fill", Greybox.Fill());

        health.AddChild(bar);
        health.AddChild(Aux(Main.Portrait ? "180 / 250" : "HP 180 / 250"));

        return health;
    }

    /// <summary>
    /// Movement on the left, attack on the right, status between them. Centred inside a capped width so the
    /// clusters never drift further apart than a thumb can travel on a very wide screen.
    /// </summary>
    private Control BuildControlRow()
    {
        CenterContainer center = new();

        HBoxContainer row = new()
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? 0 : Main.ThumbSpanMaximum, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label status = new()
        {
            Text = "그쪽으로는 갈 수 없습니다.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        status.AddThemeColorOverride("font_color", Greybox.Muted);

        // The notice floats over the floor in landscape, so it gets a plate of its own rather than an
        // outline: a line of text on gold tiles is unreadable either way without one.
        PanelContainer notice = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };
        notice.AddThemeStyleboxOverride("panel", Greybox.Plate());
        notice.AddChild(status);

        row.AddChild(BuildMovementPad());
        row.AddChild(Main.Portrait
            ? new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }
            : notice);
        row.AddChild(new Button
        {
            Text = "공격",
            CustomMinimumSize = new Vector2(AttackSize, AttackSize),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        });

        center.AddChild(row);

        return center;
    }

    /// <summary>
    /// Four directions, no diagonals: one tap is one tile, which is what this game is about. The floor is
    /// laid in diamonds, so each of them moves diagonally on screen.
    /// </summary>
    private Control BuildMovementPad()
    {
        GridContainer pad = new() { Columns = 3, SizeFlagsVertical = SizeFlags.ShrinkEnd };
        pad.AddThemeConstantOverride("h_separation", Main.Gutter / 2);
        pad.AddThemeConstantOverride("v_separation", Main.Gutter / 2);

        (string Glyph, Direction Where)?[] layout =
        [
            null, ("↑", Direction.North), null,
            ("←", Direction.West), null, ("→", Direction.East),
            null, ("↓", Direction.South), null
        ];

        foreach ((string Glyph, Direction Where)? key in layout)
        {
            if (key is null)
            {
                pad.AddChild(new Control { CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) });
                continue;
            }

            Direction where = key.Value.Where;

            Button button = new()
            {
                Text = key.Value.Glyph,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
            };

            button.Pressed += () => _world.Walk(where);

            pad.AddChild(button);
        }

        return pad;
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }
}
