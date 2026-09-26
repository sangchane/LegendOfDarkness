using System;
using System.Linq;
using Godot;

namespace LodClient;

/// <summary>What a <see cref="Glyph" /> draws. Drawn with lines, not pictures — the original 4.51 has no icons for these.</summary>
public enum GlyphKind
{
    Close,
    Bag,
    Armor,
    Sort,
    Loot,
    Use,
    Drop,
    TakeOff,
    Auto,
    Bot,
    Account,
    Zoom,
    Stop,
    Town,
    Field
}

/// <summary>
/// A small line-drawn icon — the X in a window's corner, the icon tabs, the buttons of an item's action row. Like the
/// mock-up's <c>.pip</c> beads (docs/ui/mockups-451) it is drawn, not loaded, so it stays crisp at any size and takes its
/// colour from the 4.51 palette (<see cref="Greybox" />).
/// </summary>
public sealed partial class Glyph : Control
{
    private GlyphKind _kind;
    private Color _paint = Greybox.Title;

    public Glyph(GlyphKind kind, float size = 18)
    {
        _kind = kind;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public Color Paint
    {
        get => _paint;
        set
        {
            _paint = value;
            QueueRedraw();
        }
    }

    public GlyphKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float s = Math.Min(Size.X, Size.Y);
        Vector2 o = (Size - new Vector2(s, s)) / 2;
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        float w = Math.Max(1.5f, s / 10);
        Color c = _paint;

        switch (_kind)
        {
            case GlyphKind.Close:
                DrawLine(P(0.2f, 0.2f), P(0.8f, 0.8f), c, w + 0.5f, true);
                DrawLine(P(0.8f, 0.2f), P(0.2f, 0.8f), c, w + 0.5f, true);
                break;

            case GlyphKind.Bag:
                DrawRect(new Rect2(P(0.18f, 0.36f), new Vector2(0.64f * s, 0.52f * s)), c, false, w);
                DrawArc(P(0.5f, 0.36f), 0.17f * s, Mathf.Pi, Mathf.Tau, 12, c, w, true);
                DrawLine(P(0.18f, 0.55f), P(0.82f, 0.55f), c, w * 0.8f, true);
                break;

            case GlyphKind.Armor:
                DrawPolyline([P(0.5f, 0.1f), P(0.85f, 0.24f), P(0.8f, 0.6f), P(0.5f, 0.9f), P(0.2f, 0.6f), P(0.15f, 0.24f), P(0.5f, 0.1f)], c, w, true);
                DrawLine(P(0.5f, 0.28f), P(0.5f, 0.72f), c, w * 0.8f, true);
                break;

            case GlyphKind.Sort:
                DrawLine(P(0.15f, 0.25f), P(0.85f, 0.25f), c, w, true);
                DrawLine(P(0.15f, 0.5f), P(0.65f, 0.5f), c, w, true);
                DrawLine(P(0.15f, 0.75f), P(0.45f, 0.75f), c, w, true);
                break;

            case GlyphKind.Loot:
                DrawLine(P(0.5f, 0.1f), P(0.5f, 0.6f), c, w, true);
                DrawPolyline([P(0.3f, 0.42f), P(0.5f, 0.62f), P(0.7f, 0.42f)], c, w, true);
                DrawPolyline([P(0.15f, 0.6f), P(0.15f, 0.88f), P(0.85f, 0.88f), P(0.85f, 0.6f)], c, w, true);
                break;

            case GlyphKind.TakeOff:
                DrawLine(P(0.5f, 0.62f), P(0.5f, 0.12f), c, w, true);
                DrawPolyline([P(0.3f, 0.32f), P(0.5f, 0.12f), P(0.7f, 0.32f)], c, w, true);
                DrawPolyline([P(0.15f, 0.6f), P(0.15f, 0.88f), P(0.85f, 0.88f), P(0.85f, 0.6f)], c, w, true);
                break;

            case GlyphKind.Use:
                DrawPolyline([P(0.15f, 0.52f), P(0.4f, 0.78f), P(0.86f, 0.22f)], c, w + 0.5f, true);
                break;

            case GlyphKind.Drop:
                DrawLine(P(0.15f, 0.25f), P(0.85f, 0.25f), c, w, true);
                DrawLine(P(0.4f, 0.12f), P(0.6f, 0.12f), c, w, true);
                DrawPolyline([P(0.24f, 0.25f), P(0.3f, 0.9f), P(0.7f, 0.9f), P(0.76f, 0.25f)], c, w, true);
                DrawLine(P(0.43f, 0.4f), P(0.44f, 0.76f), c, w * 0.7f, true);
                DrawLine(P(0.57f, 0.4f), P(0.56f, 0.76f), c, w * 0.7f, true);
                break;

            case GlyphKind.Auto:
                DrawArc(P(0.5f, 0.5f), 0.32f * s, -0.4f, Mathf.Pi * 1.55f, 20, c, w, true);
                DrawColoredPolygon([P(0.8f, 0.2f), P(0.86f, 0.46f), P(0.6f, 0.4f)], c);
                break;

            case GlyphKind.Bot:
                DrawRect(new Rect2(P(0.42f, 0.14f), new Vector2(0.16f * s, 0.72f * s)), c);
                DrawRect(new Rect2(P(0.2f, 0.34f), new Vector2(0.6f * s, 0.16f * s)), c);
                break;

            case GlyphKind.Account:
                DrawArc(P(0.5f, 0.33f), 0.16f * s, 0, Mathf.Tau, 16, c, w, true);
                DrawArc(P(0.5f, 0.95f), 0.33f * s, Mathf.Pi * 1.08f, Mathf.Pi * 1.92f, 16, c, w, true);
                break;

            case GlyphKind.Zoom:
                DrawArc(P(0.42f, 0.42f), 0.26f * s, 0, Mathf.Tau, 18, c, w, true);
                DrawLine(P(0.62f, 0.62f), P(0.88f, 0.88f), c, w + 0.5f, true);
                break;

            case GlyphKind.Stop:
                DrawRect(new Rect2(P(0.25f, 0.25f), new Vector2(0.5f * s, 0.5f * s)), c);
                break;

            case GlyphKind.Town:
                // 지붕 있는 집.
                DrawPolyline([P(0.12f, 0.48f), P(0.5f, 0.15f), P(0.88f, 0.48f)], c, w, true);
                DrawPolyline([P(0.22f, 0.42f), P(0.22f, 0.86f), P(0.78f, 0.86f), P(0.78f, 0.42f)], c, w, true);
                DrawRect(new Rect2(P(0.42f, 0.6f), new Vector2(0.16f * s, 0.26f * s)), c);
                break;

            case GlyphKind.Field:
                // 엇갈린 두 칼 — 싸우는 곳.
                DrawLine(P(0.18f, 0.18f), P(0.78f, 0.78f), c, w, true);
                DrawLine(P(0.82f, 0.18f), P(0.22f, 0.78f), c, w, true);
                DrawLine(P(0.62f, 0.86f), P(0.86f, 0.62f), c, w, true);
                DrawLine(P(0.14f, 0.62f), P(0.38f, 0.86f), c, w, true);
                break;
        }
    }
}

/// <summary>
/// The one frame every big window wears (인벤토리 · 설정 · 봇 장비 · 월드맵 · 길 찾기, 2026-09-26): a title or a row of small
/// icon tabs on the left, the window's own tools beside them, and an X in the top-right corner — where every phone game
/// keeps it. The X is drawn small but the place a thumb presses is the full 44 (<see cref="Main.TouchMinimum" />).
/// </summary>
public static class WindowFrame
{
    /// <summary>How tall the icon's label is. Small — the icon carries it; the word only settles doubt.</summary>
    private const int LabelSize = 10;

    /// <summary>The X. Bare until pressed; its glyph is a third of the pressable square.</summary>
    public static Button CloseButton()
    {
        Button close = new()
        {
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "닫기"
        };
        Quiet(close);

        Glyph x = new(GlyphKind.Close, 16) { Paint = Greybox.Title };
        close.AddChild(x);
        Centre(x, 16);

        return close;
    }

    /// <summary>
    /// A small icon button with its word under it. A tab when <paramref name="tab" /> (the open one is filled and lit);
    /// otherwise an action with a thin edge, so it still reads as a button over the dark inside.
    /// </summary>
    public static Button IconButton(GlyphKind kind, string label, bool tab = false, int width = 0)
    {
        Button button = new()
        {
            CustomMinimumSize = new Vector2(Math.Max(width, Main.TouchMinimum), Main.TouchMinimum),
            ToggleMode = tab,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = label
        };

        if (tab)
        {
            Greybox.Tab(button);
        }
        else
        {
            Greybox.Plain(button);
        }

        VBoxContainer stack = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", 0);
        stack.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        Glyph glyph = new(kind, label.Length > 0 ? 18 : 20);
        stack.AddChild(glyph);

        Label word = new()
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = label.Length > 0
        };
        word.AddThemeFontSizeOverride("font_size", LabelSize);
        stack.AddChild(word);

        button.AddChild(stack);
        button.SetMeta("glyph", glyph);
        button.SetMeta("word", word);

        void Tint(bool lit)
        {
            Color paint = !tab || lit ? Greybox.Text : Greybox.Muted;
            glyph.Paint = paint;
            word.AddThemeColorOverride("font_color", paint);
        }

        Tint(button.ButtonPressed);
        button.Toggled += Tint;

        return button;
    }

    /// <summary>Rewrites an icon button's word (확대 ↔ 전체, 줍기 켬 ↔ 끔).</summary>
    public static void Relabel(Button button, string label)
    {
        if (button.GetMeta("word").As<Label>() is { } word)
        {
            word.Text = label;
        }

        button.TooltipText = label;
    }

    /// <summary>A window title on the dark frame: light letters, a little larger than the body.</summary>
    public static Label Title(string text)
    {
        Label title = new() { Text = text, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeColorOverride("font_color", Greybox.Title);
        title.AddThemeFontSizeOverride("font_size", 15);
        title.ClipText = true;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

        return title;
    }

    /// <summary>
    /// The head of a window: <paramref name="left" /> (a title, or the icon tabs) takes the room, then the tools, then the X
    /// at the far right — on the strip under which the window's body begins.
    /// </summary>
    public static Control Head(Control left, Button close, params Control[] tools)
    {
        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter / 2);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(left);

        foreach (Control tool in tools)
        {
            head.AddChild(tool);
        }

        head.AddChild(close);

        return Greybox.Header(head);
    }

    /// <summary>A row of icon tabs that act as one: pressing one lights it and puts out the others.</summary>
    public static HBoxContainer Tabs(params Button[] tabs)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);
        ButtonGroup group = new() { AllowUnpress = false };

        foreach (Button tab in tabs)
        {
            tab.ButtonGroup = group;
            row.AddChild(tab);
        }

        return row;
    }

    /// <summary>Pins a child in the middle of a button (a Button lays out no children of its own).</summary>
    public static void Centre(Control child, float size)
    {
        child.AnchorLeft = child.AnchorRight = child.AnchorTop = child.AnchorBottom = 0.5f;
        child.OffsetLeft = child.OffsetTop = -size / 2;
        child.OffsetRight = child.OffsetBottom = size / 2;
    }

    /// <summary>A button with no plate at all until pressed — the X.</summary>
    private static void Quiet(Button button)
    {
        StyleBoxEmpty none = new();
        StyleBoxFlat down = new() { BgColor = new Color(1, 1, 1, 0.08f) };
        down.SetCornerRadiusAll(Greybox.Round);

        button.AddThemeStyleboxOverride("normal", none);
        button.AddThemeStyleboxOverride("hover", none);
        button.AddThemeStyleboxOverride("focus", none);
        button.AddThemeStyleboxOverride("pressed", down);
    }
}

/// <summary>
/// The 월드맵 button, shaped like the map it opens (사용자, 2026-09-26: 마름모 모양 테두리로, 맨 왼쪽에) — a diamond edge in the
/// 4.51 palette round its word, the same diamond the minimap and the 길 찾기 map draw the floor in. The pressable square
/// stays the full rectangle, so a thumb need not hit the diamond.
/// </summary>
public sealed partial class DiamondButton : Button
{
    private readonly string _word;

    public DiamondButton(string word)
    {
        _word = word;
        Flat = true;
        FocusMode = FocusModeEnum.None;
        TooltipText = word;
        CustomMinimumSize = new Vector2(72, Main.TouchMinimum + 4);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public override void _Draw()
    {
        Vector2 c = Size / 2;
        float w = (Size.X / 2) - 1, h = (Size.Y / 2) - 1;
        Vector2[] edge = [c + new Vector2(0, -h), c + new Vector2(w, 0), c + new Vector2(0, h), c + new Vector2(-w, 0)];
        bool down = IsPressed() || Disabled;

        DrawColoredPolygon(edge, new Color("#0f0f0f") with { A = 0.92f });
        DrawPolyline([.. edge, edge[0]], down ? Greybox.Title : new Color("#636357"), 1.5f, true);

        // 안쪽 한 겹 더 — 원작 틀의 두 겹 돌처럼.
        Vector2[] inner = [.. edge.Select(point => c + ((point - c) * 0.82f))];
        DrawPolyline([.. inner, inner[0]], new Color("#303036"), 1f, true);

        Font font = GetThemeDefaultFont();
        const int size = 13;
        Vector2 text = font.GetStringSize(_word, HorizontalAlignment.Left, -1, size);
        DrawString(font, new Vector2(c.X - (text.X / 2), c.Y + (size / 2f) - 2), _word, HorizontalAlignment.Left, -1, size,
            Disabled ? Greybox.Muted : Greybox.Title);
    }
}
