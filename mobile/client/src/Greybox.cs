using Godot;

namespace LodClient;

/// <summary>
/// The one place the screens get their look. It began as a deliberately grey placeholder for judging layout;
/// it now carries the original 4.51 theme, worked out and written down in <c>data/ui-vault/</c>.
/// </summary>
/// <remarks>
/// The rule is 안A — <b>borrow the colours, not the stone</b>. Everything is flat, generously spaced and
/// rounded; the original's palette is what carries the feel. 안C was built first, with the original stone
/// under the frames and buttons, and put on a device: the texture reads as clutter at phone size, so the
/// plainer one won (사용자, 2026-09-18).
///
/// Nothing here moves anything. Only the material changes, so the screens that call this were not touched.
/// </remarks>
public static class Greybox
{
    // ── 색. data/ui-vault/색/ 의 값 그대로다. ────────────────────────────────

    /// <summary>Faint text — a label beside a number, a page count.</summary>
    public static readonly Color Muted = new("#97978b");

    /// <summary>A title on dark stone. Never engraved: engraving only works on the light stone.</summary>
    public static readonly Color Title = new("#abab9f");

    /// <summary>Engraved text, for the light stone only, with a hairline of white under it.</summary>
    public static readonly Color Engrave = new("#100f0b");

    /// <summary>Health and mana. The windows carry no colour of their own, so these are the only accents.</summary>
    public static readonly Color Health = new("#c8783c");

    public static readonly Color Mana = new("#5a6fa8");

    /// <summary>A number that has fallen far enough to act on — nearly dead, nearly out of mana.</summary>
    public static readonly Color Gone = new("#a33f36");

    /// <summary>Ordinary text on a dark inside.</summary>
    public static readonly Color Text = new("#d6d6cc");

    private static readonly Color Inner = new("#0f0f0f");
    private static readonly Color Cell = new("#1f1f24");
    private static readonly Color CellEdge = new("#303036");
    private static readonly Color Deep = new("#636357");

    /// <summary>
    /// The one strong colour, borrowed from the original's health bead. Everything that finishes a job wears it
    /// — the attack button, the button that commits, the line under the open tab.
    /// </summary>
    public static readonly Color Accent = new("#c8783c");

    /// <summary>Letters on the accent. Dark, because dark on that orange is what stays readable.</summary>
    public static readonly Color OnAccent = new("#1a1208");

    /// <summary>A window's own frame — one shade above its inside.</summary>
    private static readonly Color Frame_ = new("#17171b");

    // ── 치수. data/ui-vault/치수/치수.md ─────────────────────────────────────

    /// <summary>The stone frame's thickness.</summary>
    public const int Frame = 5;

    /// <summary>Padding inside a window, and the gap between its parts.</summary>
    public const int Pad = 12;

    public const int Gap = 8;

    /// <summary>Rounding. Only on the inside — a stone frame has to stay square to read as the original's.</summary>
    public const int Round = 12;


    /// <summary>
    /// A cell in a grid, a row in a list, an input box. Flat and dark — the vault forbids stone here, because a
    /// pattern under small text is the first thing to fail on a phone.
    /// </summary>
    public static StyleBoxFlat Surface() => new()
    {
        BgColor = Cell,
        BorderColor = CellEdge,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1
    };

    /// <summary>
    /// A panel that has to stay readable over the map. The greybox surface was made for a flat grey background;
    /// over gold floor tiles its text disappears, so this one is nearly opaque — 96%, the value the vault settles
    /// on as the only one that survives that floor.
    /// </summary>
    public static StyleBoxFlat Plate() => new()
    {
        BgColor = Inner with { A = 0.96f },
        BorderColor = CellEdge,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
        ContentMarginTop = 4,
        ContentMarginBottom = 4
    };

    /// <summary>
    /// A window laid over the screen — the pack, an NPC's talk. Fully opaque: since the map runs under the
    /// controls in portrait too, the log and the buttons behind a see-through plate read through the window.
    /// </summary>
    public static StyleBoxFlat Sheet()
    {
        StyleBoxFlat sheet = Plate();
        sheet.BgColor = Inner;

        return sheet;
    }

    /// <summary>
    /// A window's frame. Flat and dark, a shade above what is inside it, so the window reads as one thing
    /// without a pattern doing the work.
    /// </summary>
    public static StyleBoxFlat Stone()
    {
        StyleBoxFlat frame = new() { BgColor = Frame_, BorderColor = CellEdge };
        frame.SetBorderWidthAll(1);
        frame.SetCornerRadiusAll(Round + 4);
        frame.SetContentMarginAll(1);

        return frame;
    }

    /// <summary>The colour that means "this is the one" — the button that commits, the tab that is open.</summary>
    public static StyleBoxFlat Lit()
    {
        StyleBoxFlat filled = new() { BgColor = Accent };
        filled.SetCornerRadiusAll(Round);

        return filled;
    }

    /// <summary>
    /// The one button that commits — 입기, 삽니다, 보내기. Filled in the accent with dark letters on it, which
    /// is the plainest way to say which button finishes the job. Only one per window.
    /// </summary>
    public static void Commit(Button button)
    {
        StyleBoxFlat pressed = Lit();
        pressed.BgColor = Accent.Darkened(0.18f);

        button.AddThemeStyleboxOverride("normal", Lit());
        button.AddThemeStyleboxOverride("hover", Lit());
        button.AddThemeStyleboxOverride("focus", Lit());
        button.AddThemeStyleboxOverride("pressed", pressed);

        StyleBoxFlat off = Surface();
        off.SetCornerRadiusAll(Round);
        button.AddThemeStyleboxOverride("disabled", off);

        foreach (string colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(colour, OnAccent);
        }

        button.AddThemeColorOverride("font_disabled_color", Muted);
    }

    /// <summary>
    /// A tab. The open one is filled and its letters brighten; the others are bare. Filling rather than
    /// underlining means it can be told apart without reading, and without a second colour.
    /// </summary>
    public static void Tab(Button button)
    {
        StyleBoxFlat quiet = new() { BgColor = new Color(0, 0, 0, 0) };
        quiet.SetCornerRadiusAll(Round);

        StyleBoxFlat chosen = new() { BgColor = Cell };
        chosen.SetCornerRadiusAll(Round);
        chosen.BorderColor = Accent;
        chosen.SetBorderWidthAll(0);
        chosen.BorderWidthBottom = 2;

        button.AddThemeStyleboxOverride("normal", quiet);
        button.AddThemeStyleboxOverride("hover", quiet);
        button.AddThemeStyleboxOverride("focus", quiet);
        button.AddThemeStyleboxOverride("pressed", chosen);
        button.AddThemeColorOverride("font_color", Muted);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Text);
        button.AddThemeColorOverride("font_focus_color", Muted);
    }

    /// <summary>
    /// The strip a window's title sits on. No band of its own — a line under it is enough, which is what keeps
    /// the window feeling light.
    /// </summary>
    public static Control Header(Control inside)
    {
        StyleBoxFlat strip = new() { BgColor = new Color(0, 0, 0, 0), BorderColor = CellEdge };
        strip.BorderWidthBottom = 1;
        strip.ContentMarginLeft = Pad / 2;
        strip.ContentMarginRight = Pad / 2;
        strip.ContentMarginTop = Pad / 2;
        strip.ContentMarginBottom = Pad / 2;

        PanelContainer head = new();
        head.AddThemeStyleboxOverride("panel", strip);
        head.AddChild(inside);

        return head;
    }

    /// <summary>
    /// Rounds a thumb button — the movement pad and the fan round the attack — into a disc. The plates stay as
    /// opaque as every other button's, because the glyphs on them sit over gold floor tiles too.
    /// </summary>
    public static void Disc(Button button)
    {
        foreach ((string state, StyleBoxFlat box) in new[]
                 {
                     ("normal", Plate()), ("hover", Plate()), ("pressed", Surface()), ("focus", Plate()), ("disabled", Surface())
                 })
        {
            // 반지름이 한 변의 절반 이상이면 왼쪽·오른쪽 조각이 가운데서 겹친다. 불투명할 때는 안 보이지만 흐리게 하면
            // (빈 칸 · 걷는 중의 방향판) 겹친 줄이 짙게 드러났다 — 절반보다 1 작게. 크기를 먼저 정하고 부른다.
            box.SetCornerRadiusAll(Mathf.RoundToInt(button.CustomMinimumSize.X / 2) - 1);

            // 지름 48 의 원 안에 드는 네모가 34 다 — 원작 아이콘(35)이 둥근 칸 밖으로 삐져나오지 않게 7 씩 들인다.
            box.SetContentMarginAll(7);
            button.AddThemeStyleboxOverride(state, box);
        }
    }

    /// <summary>Darkens what is under it, so a number written over a picture can be read.</summary>
    public static StyleBoxFlat Shade() => new() { BgColor = new Color(0, 0, 0, 0.55f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23 };

    /// <summary>The play area behind the HUD.</summary>
    public static StyleBoxFlat World() => new() { BgColor = Inner };

    /// <summary>Filled portion of a bar. Always paired with numbers, never read by shade alone.</summary>
    public static StyleBoxFlat Fill() => new() { BgColor = Health };

    /// <summary>
    /// Marks a zone the layout has to respect. A guide, not part of the game: it goes away once the rule it
    /// shows has been checked on a device.
    /// </summary>
    public static StyleBoxFlat Outline() => new()
    {
        BgColor = new Color(0, 0, 0, 0),
        BorderColor = Deep with { A = 0.55f },
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1
    };
}
