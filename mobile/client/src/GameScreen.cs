using Godot;

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

    public GameScreen()
    {
        Name = "GameScreen";
        AnchorRight = 1;
        AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    public override void _Ready()
    {
        AddChild(BuildWorld());

        MarginContainer hud = Main.SafeAreaContainer();
        AddChild(hud);

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);
        hud.AddChild(rows);

        rows.AddChild(BuildTopRow());
        rows.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        rows.AddChild(BuildControlRow());
    }

    /// <summary>
    /// Full-bleed world. Greybox stands the objects in for sprites, which still live inside the .dat
    /// archives, so their tap sizes can be judged before any art exists.
    /// </summary>
    private static Control BuildWorld()
    {
        Panel world = new()
        {
            Name = "World",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };
        world.AddThemeStyleboxOverride("panel", Greybox.World());

        // Kept inside the focus band: above the thumb clusters and the status line, below the top row.
        world.AddChild(WorldObject("NPC", new Vector2(255, 70)));
        world.AddChild(WorldObject("거미 3/10", new Vector2(465, 70)));
        world.AddChild(WorldObject("플레이어", new Vector2(360, 120)));
        world.AddChild(WorldObject("지면 아이템", new Vector2(255, 165)));

        return world;
    }

    /// <summary>A tappable world object. Sized to the touch minimum because taps land on it directly.</summary>
    private static Control WorldObject(string caption, Vector2 position)
    {
        VBoxContainer group = new() { Position = position };
        group.AddThemeConstantOverride("separation", 2);

        Panel body = new() { CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        body.AddThemeStyleboxOverride("panel", Greybox.Surface());

        Label label = new()
        {
            Text = caption,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        group.AddChild(body);
        group.AddChild(label);

        return group;
    }

    /// <summary>Name and health on the left, world state and inventory on the right.</summary>
    private static Control BuildTopRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        row.AddChild(Aux("수련생"));
        row.AddChild(BuildHealth());
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(Aux("안전 가옥"));
        row.AddChild(Aux("연결됨"));

        Button inventory = new()
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };
        row.AddChild(inventory);

        return row;
    }

    /// <summary>Bar and numbers together: health must never be readable by colour alone.</summary>
    private static Control BuildHealth()
    {
        HBoxContainer health = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        health.AddThemeConstantOverride("separation", Main.Gutter / 2);

        ProgressBar bar = new()
        {
            CustomMinimumSize = new Vector2(StatusBarWidth, 12),
            MaxValue = 250,
            Value = 180,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        bar.AddThemeStyleboxOverride("background", Greybox.Surface());
        bar.AddThemeStyleboxOverride("fill", Greybox.Fill());

        health.AddChild(bar);
        health.AddChild(Aux("HP 180 / 250"));

        return health;
    }

    /// <summary>
    /// Movement on the left, attack on the right, status between them. Centred inside a capped width so the
    /// clusters never drift further apart than a thumb can travel on a very wide screen.
    /// </summary>
    private static Control BuildControlRow()
    {
        CenterContainer center = new();

        HBoxContainer row = new()
        {
            CustomMinimumSize = new Vector2(Main.ThumbSpanMaximum, 0)
        };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label status = new()
        {
            Text = "그쪽으로는 갈 수 없습니다.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        status.AddThemeColorOverride("font_color", Greybox.Muted);

        Button attack = new()
        {
            Text = "공격",
            CustomMinimumSize = new Vector2(AttackSize, AttackSize),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };

        row.AddChild(BuildMovementPad());
        row.AddChild(status);
        row.AddChild(attack);

        center.AddChild(row);

        return center;
    }

    /// <summary>Four directions, no diagonals: one tap is one tile, which is what this game is about.</summary>
    private static Control BuildMovementPad()
    {
        GridContainer pad = new() { Columns = 3, SizeFlagsVertical = SizeFlags.ShrinkEnd };
        pad.AddThemeConstantOverride("h_separation", Main.Gutter / 2);
        pad.AddThemeConstantOverride("v_separation", Main.Gutter / 2);

        string?[] layout = [null, "↑", null, "←", null, "→", null, "↓", null];

        foreach (string? direction in layout)
        {
            pad.AddChild(direction is null ? PadGap() : PadButton(direction));
        }

        return pad;
    }

    private static Control PadGap() =>
        new() { CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };

    private static Button PadButton(string direction) => new()
    {
        Text = direction,
        CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
    };

    private static Label Aux(string text)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }
}
