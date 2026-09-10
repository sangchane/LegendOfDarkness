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
    private Label _who = null!;
    private Label _place = null!;
    private Label _target = null!;
    private PackPanel _pack = null!;
    private Label _notice = null!;
    private ProgressBar _targetHealth = null!;

    // 서버가 말한 횟수. 같은 말을 다시 하는 것과 새로 하는 것을 가르려고 센다.
    private int _heard = -1;
    private Control _packRow = null!;

    // 손 없이 확인할 때 스스로 열어 보기 위한 것. 월드가 자리를 잡을 때까지 센다.
    private int _settling;

    // 리허설로 한 번만 입어 본다.
    private bool _worn;
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

    /// <summary>
    /// The pieces a layout check walks: everything that has to stay on the screen and out of each other's
    /// way. Named in Korean because the names are printed for a person to read.
    /// </summary>
    public IReadOnlyList<(string Name, Control Part)> Parts =>
        [("위 줄", _topRow), ("조작 줄", _controlRow), ("인벤토리", _pack), ("월드", _world)];

    public override void _Ready()
    {
        MarginContainer hud = Main.SafeAreaContainer();

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);

        // In landscape the HUD lies over the whole world, and a container that eats taps would stop anyone
        // ever touching a figure. The plates and buttons inside it still take their own.
        hud.MouseFilter = MouseFilterEnum.Ignore;
        rows.MouseFilter = MouseFilterEnum.Ignore;

        _topRow = BuildTopRow();
        _pack = new PackPanel();
        _pack.Close.Pressed += () => Carrying(false);
        _pack.Used += slot => _ = _server?.UseAsync(slot, System.Threading.CancellationToken.None);

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
            rows.AddChild(_packRow = BuildPackRow());
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
            rows.AddChild(_packRow = BuildPackRow());
            rows.AddChild(_controlRow);

            // 가로에서는 위 줄과 조작 줄 사이에 남는 높이가 아이콘 한 칸도 안 된다. 시안대로
            // 모달이니 조작 줄 위로 덮게 두고(열려 있는 동안 이동·공격은 어차피 막힌다),
            // 줄 자체는 빈자리로 남겨 조작 줄이 위로 올라오지 않게 한다.
            // MarginContainer 는 자식을 꽉 채우므로 닻이 먹지 않는다. 그냥 Control 을 한 겹 두면
            // 그 안에서는 닻이 그대로 듣는다.
            Control over = new() { MouseFilter = MouseFilterEnum.Ignore };
            hud.AddChild(over);
            over.AddChild(_pack);

            _pack.SetAnchorsPreset(LayoutPreset.FullRect);
            _pack.AnchorLeft = 0.6f;
            _pack.OffsetLeft = 0;
            _pack.OffsetTop = Main.TouchMinimum + (Main.Gutter * 3);
            _pack.OffsetRight = 0;
            _pack.OffsetBottom = 0;
        }
    }

    /// <summary>
    /// Where the pack sits when it is open: against the right edge, over rather than beside the world, and
    /// taking a bit over a third of the width. The rest of the row lets taps through to the floor.
    /// </summary>
    private Control BuildPackRow()
    {
        HBoxContainer row = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };

        // 높이는 남는 만큼만 — 최소 높이를 박으면 위·아래 줄이 화면 밖으로 밀린다(한 번 그렇게 됐다).
        _pack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pack.SizeFlagsVertical = SizeFlags.ExpandFill;

        if (!Main.Portrait)
        {
            // 가로에서는 이 줄이 위 줄과 조작 줄 사이의 빈 자리이기도 하다. 닫혀 있어도 남아 있어야
            // 조작 줄이 위로 올라오지 않는다. 패널은 오른쪽 3분의 1 남짓만 덮는다.
            row.AddChild(new Control
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 62,
                MouseFilter = MouseFilterEnum.Ignore
            });

            _pack.SizeFlagsStretchRatio = 38;

            // 가로에서는 패널을 이 줄이 아니라 HUD 위에 덮어 놓는다. 줄은 빈자리로만 남는다.
            return row;
        }

        row.AddChild(_pack);

        // 세로에서는 시안대로 월드 아래 전폭이고, 닫혀 있으면 줄째로 사라져 월드에 자리를 돌려준다.
        row.Visible = !Main.Portrait;

        return row;
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

        // Empty until the server names us, in step with the place name below: a made-up name on the
        // HUD is worse than none, because there is no way to tell it from a real one.
        _who = Aux(string.Empty);

        row.AddChild(_who);
        row.AddChild(BuildHealth());
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // Whoever is picked out, in the middle where the original kept it. Empty until somebody is.
        _target = Aux(string.Empty);

        _targetHealth = new ProgressBar
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? 48 : 72, 10),
            MaxValue = 100,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Visible = false
        };
        _targetHealth.AddThemeStyleboxOverride("background", Greybox.Surface());
        _targetHealth.AddThemeStyleboxOverride("fill", Greybox.Fill());

        HBoxContainer picked = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        picked.AddThemeConstantOverride("separation", Main.Gutter / 2);
        picked.AddChild(_target);
        picked.AddChild(_targetHealth);

        row.AddChild(picked);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // Where the server says we are. Offline it stays empty rather than claiming something untrue.
        _place = Aux(string.Empty);

        // 360 across cannot hold this as well, so in portrait the log carries it instead.
        if (!Main.Portrait)
        {
            row.AddChild(_place);
        }

        Button pack = new()
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        pack.Pressed += () => Carrying(true);
        row.AddChild(pack);

        plate.AddChild(row);

        return plate;
    }

    /// <summary>Keeps the place name and whoever is picked out in step with the world below.</summary>
    public override void _Process(double delta)
    {
        if (_world.PlaceName.Length > 0)
        {
            _place.Text = $"{_world.PlaceName} · {_world.Standing.X},{_world.Standing.Y}";
        }

        // The server names us in 0x33; nothing else on this screen knows who we are.
        if (_server?.Self?.Name is { Length: > 0 } called)
        {
            _who.Text = called;
        }

        ShowTarget();

        if (_server is { } server && server.SaidCount != _heard)
        {
            _heard = server.SaidCount;
            _notice.Text = server.Said;
        }

        if (Main.OpeningPack && !_pack.Visible && _settling++ == 90)
        {
            Carrying(true);
        }

        if (_pack.Visible)
        {
            _pack.Show(_server?.Pack ?? [], _server?.Worn ?? []);

            // 손 없이 확인할 때만. 목록이 채워진 다음 프레임에 첫 줄을 한 번 누른다.
            if (Main.Wearing && !_worn && _pack.PressFirst())
            {
                _worn = true;
                GD.Print("GREYBOX_WORE 첫 줄을 눌렀다");
            }
        }
    }

    /// <summary>
    /// Whoever is picked out, and how hurt they are. The bar is never alone — the number is beside it,
    /// because health must not be readable by colour or length alone.
    /// </summary>
    private void ShowTarget()
    {
        int? left = _world.Target == 0 ? null : _server?.Health(_world.Target);

        _target.Text = left is { } percent
            ? $"{_world.TargetName} {percent}%"
            : _world.TargetName;

        _targetHealth.Visible = left is not null;

        if (left is { } value)
        {
            _targetHealth.Value = value;
        }
    }

    /// <summary>
    /// Opens or shuts the pack. While it is open the world takes no taps and no steps — the panel lies over
    /// it, and a thumb aimed at the list must not walk the character.
    /// </summary>
    private void Carrying(bool open)
    {
        _pack.Visible = open;
        _world.Frozen = open;

        if (Main.Portrait)
        {
            _packRow.Visible = open;
        }

        if (open)
        {
            _pack.Show(_server?.Pack ?? [], _server?.Worn ?? []);
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
    /// Movement on the left, attack on the right, status between them, inside a width capped so the two
    /// clusters never drift further apart than a thumb can travel on a very wide screen.
    /// </summary>
    private Control BuildControlRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        _notice = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _notice.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _notice.AddThemeColorOverride("font_color", Greybox.Muted);

        // The notice floats over the floor in landscape, so it gets a plate of its own rather than an
        // outline: a line of text on gold tiles is unreadable either way without one.
        PanelContainer notice = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };
        notice.AddThemeStyleboxOverride("panel", Greybox.Plate());
        notice.AddChild(_notice);

        row.AddChild(BuildMovementPad());
        row.AddChild(Main.Portrait
            ? new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }
            : notice);
        Button strike = new()
        {
            Text = "공격",
            CustomMinimumSize = new Vector2(AttackSize, AttackSize),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };

        // One tap is one blow. It does not chase and it does not repeat — the server decides whether it
        // landed, and says so in words we show below rather than guessing at damage here.
        strike.Pressed += () => _world.Strike();
        row.AddChild(strike);

        return Main.Capped(row, Main.ThumbSpanMaximum);
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
