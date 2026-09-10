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
    private const int PortraitStatusBarWidth = 96;

    /// <summary>Rows the chat and combat log keeps in portrait. Landscape has no room for it at all.</summary>
    private const int LogHeight = 76;

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
        MarginContainer hud = Main.SafeAreaContainer();

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);

        _topRow = BuildTopRow();
        _controlRow = BuildControlRow();

        if (Main.Portrait)
        {
            // Portrait has the height to give the world a row of its own, so nothing the player is aiming
            // at sits under a thumb. That is the whole reason to hold the phone this way.
            _world = BuildWorld(fullBleed: false);

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
            _world = BuildWorld(fullBleed: true);

            AddChild(_world);
            AddChild(hud);
            hud.AddChild(rows);
            rows.AddChild(_topRow);
            rows.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
            rows.AddChild(_controlRow);
        }

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

        if (Main.Portrait)
        {
            // Only 360 across, so the objects stand on two rows rather than shrinking below the tap minimum.
            float upper = centre.Y - Main.TouchMinimum - 24;
            float lower = centre.Y + 24;

            Place("Npc", new Vector2(centre.X - 110, upper));
            Place("Monster", new Vector2(centre.X + 30, upper));
            Place("GroundItem", new Vector2(centre.X - 145, lower));
            Place("Player", new Vector2(centre.X - (Main.TouchMinimum / 2), lower));

            return;
        }

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
        // In portrait the world already is the band, so its own rect answers the question.
        if (Main.Portrait)
        {
            return new Rect2(Vector2.Zero, _world.Size);
        }

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
    private Control BuildWorld(bool fullBleed)
    {
        Panel world = new() { Name = "World" };

        if (fullBleed)
        {
            world.AnchorRight = 1;
            world.AnchorBottom = 1;
            world.GrowHorizontal = GrowDirection.Both;
            world.GrowVertical = GrowDirection.Both;
        }
        else
        {
            world.SizeFlagsVertical = SizeFlags.ExpandFill;
        }

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

    /// <summary>Name and health on the left, world state and inventory on the right.</summary>
    private static Control BuildTopRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        row.AddChild(Aux("수련생"));
        row.AddChild(BuildHealth());
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // 360 across cannot hold the map name and the connection state as well; the log carries them instead.
        if (!Main.Portrait)
        {
            row.AddChild(Aux("안전 가옥"));
            row.AddChild(Aux("연결됨"));
        }

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
    private static Control BuildControlRow()
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
            VerticalAlignment = VerticalAlignment.Bottom,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        status.AddThemeColorOverride("font_color", Greybox.Muted);

        row.AddChild(BuildMovementPad());
        row.AddChild(Main.Portrait
            ? new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }
            : status);
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
