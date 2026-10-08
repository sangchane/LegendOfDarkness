using System;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// The one frame every big window wears (인벤토리 · 설정 · 봇 장비 · 월드맵 · 길 찾기, 2026-09-26): a title or a row of small
/// icon tabs on the left, the window's own tools beside them, and an X in the top-right corner — where every phone game
/// keeps it. The X is drawn small but the place a thumb presses is the full 44 (<see cref="Main.TouchMinimum" />).
/// </summary>
public static class WindowFrame
{
    /// <summary>How tall the icon's label is. Small — the icon carries it; the word only settles doubt.</summary>
    private const int LabelSize = Greybox.TightText;

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
    /// A small icon button with its word under it. A tab when <paramref name="tab" /> (the open one is underlined and lit);
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
            Color paint = tab && lit ? Greybox.Text : !tab ? Greybox.Text : Greybox.Muted;
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

    // ▲ 나음 · ▼ 못함 색.
    private static readonly Color Better = new("#7fd17f");
    private static readonly Color Worse = new("#e07070");

    /// <summary>
    /// The info box's numbers (소지품 · 장비창): two to a row in columns — name, value, how it compares — so they line up
    /// (사용자 2026-10-01), and the rest (elements, needs, weight) under them in <paramref name="notes" />.
    /// </summary>
    public static void ShowStats(GridContainer table, Label notes, IReadOnlyList<StatLine> lines)
    {
        foreach (Node old in table.GetChildren())
        {
            table.RemoveChild(old);
            old.QueueFree();
        }

        table.Columns = 6;
        table.AddThemeConstantOverride("h_separation", 6);
        table.AddThemeConstantOverride("v_separation", 0);

        foreach (StatLine line in lines.Where(line => line.Numeric))
        {
            table.AddChild(Small(line.Name, Greybox.Muted));
            Label value = Small(line.Value, Greybox.Text);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.CustomMinimumSize = new Vector2(44, 0);
            table.AddChild(value);
            Label change = Small(line.Change == 0 ? string.Empty : $"{(line.Change > 0 ? "▲" : "▼")}{System.Math.Abs(line.Change)}",
                line.Change > 0 ? Better : Worse);
            change.CustomMinimumSize = new Vector2(34, 0);
            table.AddChild(change);
        }

        table.Visible = table.GetChildCount() > 0;
        notes.Text = Packed(notes, [.. lines.Where(line => !line.Numeric).Select(line => line.Text)]);
        notes.Visible = notes.Text.Length > 0;
    }

    private static Label Small(string text, Color colour)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", Greybox.SmallText);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    /// <summary>
    /// The numbers several to a line, broken only between two of them — the engine's own wrapping breaks Korean between
    /// any two letters ("요구 레 / 벨 41").
    /// </summary>
    public static string Packed(Label label, IReadOnlyList<string> numbers)
    {
        const float wide = 220;
        const string gap = "   ";
        Font font = label.GetThemeFont("font");
        int size = label.GetThemeFontSize("font_size");
        List<string> lines = [];

        foreach (string number in numbers)
        {
            string joined = lines.Count > 0 ? lines[^1] + gap + number : number;

            if (lines.Count > 0 && font.GetStringSize(joined, fontSize: size).X <= wide)
            {
                lines[^1] = joined;
            }
            else
            {
                lines.Add(number);
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>A window title on the dark frame: light letters, a little larger than the body.</summary>
    public static Label Title(string text)
    {
        Label title = new() { Text = text, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeColorOverride("font_color", Greybox.Title);
        title.AddThemeFontSizeOverride("font_size", 15);
        title.ClipText = true;

        // 잘리면 글자를 지우고 왼쪽 용 문양만 남긴다(사용자 2026-10-01: 잘리면 아이콘으로만) — 「소지…」처럼 반만 보이지 않게.
        title.Resized += () => title.Text = title.GetThemeFont("font").GetStringSize(text, fontSize: 15).X <= title.Size.X ? text : string.Empty;

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
