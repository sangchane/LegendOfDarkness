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

    private Control _world = null!;
    private Control _topRow = null!;
    private Control _controlRow = null!;
    private Panel _focusBand = null!;

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
        _world = BuildWorld();
        AddChild(_world);

        MarginContainer hud = Main.SafeAreaContainer();
        AddChild(hud);

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);
        hud.AddChild(rows);

        _topRow = BuildTopRow();
        _controlRow = BuildControlRow();

        rows.AddChild(_topRow);
        rows.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        rows.AddChild(_controlRow);

        _ = PlaceWorldOnceMeasured();
    }

    /// <summary>
    /// Everything the player must see or tap goes in the band between the two HUD rows. Below it is where a
    /// thumb rests, and a monster or a dropped item there is hidden by the player's own hand.
    /// </summary>
    private async System.Threading.Tasks.Task PlaceWorldOnceMeasured()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Rect2 band = FocusBand();

        _focusBand.Position = band.Position;
        _focusBand.Size = band.Size;

        // Laid out around the band's centre rather than the screen's, which is what a camera has to do too.
        Vector2 centre = band.Position + (band.Size / 2);

        // One row: the band is only tall enough for a single object plus its caption, which is itself worth
        // seeing. A three row movement pad takes a large share of a 360 unit high screen.
        float row = centre.Y - (Main.TouchMinimum / 2);

        Place("Npc", new Vector2(centre.X - 230, row));
        Place("GroundItem", new Vector2(centre.X - 115, row));
        Place("Player", new Vector2(centre.X - (Main.TouchMinimum / 2), row));
        Place("Monster", new Vector2(centre.X + 120, row));
    }

    private Rect2 FocusBand()
    {
        float top = _topRow.GlobalPosition.Y + _topRow.Size.Y + Main.Gutter;
        float bottom = _controlRow.GlobalPosition.Y - Main.Gutter;
        float left = Main.SafeInsets.Left;
        float right = Size.X - Main.SafeInsets.Right;

        return new Rect2(left, top, right - left, Mathf.Max(bottom - top, Main.TouchMinimum));
    }

    private void Place(string name, Vector2 position) =>
        _world.GetNode<Control>(name).Position = position;

    /// <summary>
    /// Full-bleed world. Greybox stands the objects in for sprites, which still live inside the .dat
    /// archives, so their tap sizes can be judged before any art exists.
    /// </summary>
    private Control BuildWorld()
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

        _focusBand = new Panel { Name = "FocusBand" };
        _focusBand.AddThemeStyleboxOverride("panel", Greybox.Outline());
        world.AddChild(_focusBand);

        // Node names stay plain because a caption may carry characters a node path cannot.
        world.AddChild(WorldObject("Npc", "NPC"));
        world.AddChild(WorldObject("Monster", "거미 3/10"));
        world.AddChild(WorldObject("Player", "플레이어"));
        world.AddChild(WorldObject("GroundItem", "지면 아이템"));

        return world;
    }

    /// <summary>A tappable world object. Sized to the touch minimum because taps land on it directly.</summary>
    private static Control WorldObject(string name, string caption)
    {
        VBoxContainer group = new() { Name = name };
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
        row.AddChild(new Button
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        });

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

        HBoxContainer row = new() { CustomMinimumSize = new Vector2(Main.ThumbSpanMaximum, 0) };
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

        row.AddChild(BuildMovementPad());
        row.AddChild(status);
        row.AddChild(new Button
        {
            Text = "공격",
            CustomMinimumSize = new Vector2(AttackSize, AttackSize),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        });

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
            pad.AddChild(direction is null
                ? new Control { CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) }
                : new Button
                {
                    Text = direction,
                    CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
                });
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
