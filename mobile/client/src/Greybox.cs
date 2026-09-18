using Godot;

namespace LodClient;

/// <summary>
/// The one place the screens get their look. It began as a deliberately grey placeholder for judging layout;
/// it now carries the original 4.51 theme, worked out and written down in <c>data/ui-vault/</c>.
/// </summary>
/// <remarks>
/// The rule the vault settles on is 안C — <b>the frame, the title strip and the one button that commits are
/// stone; everything you read or choose from is flat</b>. There are two stones and which goes where matters:
/// wide faces take the dark one, and only small buttons take the light one. Laying the light stone widely was
/// tried first and the titles stopped being readable.
///
/// Nothing here moves anything. The vault is explicit about that — only the material changes, so the screens
/// that call this were not touched.
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
    /// The light stone's own colour, for the places a texture cannot go. A round button is one: Godot's textured
    /// box has no corners to round, and a square attack button among round ones reads as a mistake.
    /// </summary>
    public static readonly Color StoneLit = new("#7d776c");

    // ── 치수. data/ui-vault/치수/치수.md ─────────────────────────────────────

    /// <summary>The stone frame's thickness.</summary>
    public const int Frame = 5;

    /// <summary>Padding inside a window, and the gap between its parts.</summary>
    public const int Pad = 12;

    public const int Gap = 8;

    /// <summary>Rounding. Only on the inside — a stone frame has to stay square to read as the original's.</summary>
    public const int Round = 12;

    private const string DarkStone = "res://assets/ui/stone-dark.png";
    private const string LitStone = "res://assets/ui/stone-lit.png";

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
    /// The stone a window is framed in, and the strip its title sits on. Dark, because this is a wide face.
    /// Square on purpose — the rounding belongs to whatever is inside it.
    /// </summary>
    public static StyleBoxTexture Stone() => Rock(DarkStone, Frame);

    /// <summary>
    /// The lighter stone, for the one button that commits and for the attack button. Only ever small faces: the
    /// pattern is what carries the original's feel, and that same pattern under a paragraph destroys it.
    /// </summary>
    public static StyleBoxTexture Lit() => Rock(LitStone, 3);

    private static StyleBoxTexture Rock(string path, int edge)
    {
        StyleBoxTexture rock = new()
        {
            Texture = GD.Load<Texture2D>(path),

            // 64x64 무늬를 늘리지 않고 되풀이해 깐다 — 늘리면 돌결이 뭉개진다.
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile
        };

        rock.SetContentMarginAll(edge);

        return rock;
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

    /// <summary>
    /// The one button that commits — 입기, 삽니다, 보내기, 들어가기. Light stone with the words engraved into it,
    /// which is how the original's own buttons are made. Only one per window: if everything is stone, nothing is.
    /// </summary>
    public static void Commit(Button button)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            button.AddThemeStyleboxOverride(state, Lit());
        }

        button.AddThemeStyleboxOverride("disabled", Surface());
        button.AddThemeColorOverride("font_color", Engrave);
        button.AddThemeColorOverride("font_hover_color", Engrave);
        button.AddThemeColorOverride("font_pressed_color", Engrave);
        button.AddThemeColorOverride("font_focus_color", Engrave);
        button.AddThemeColorOverride("font_disabled_color", Muted);
    }

    /// <summary>
    /// A tab. The chosen one is light stone and engraved; the others stay flat, so which one is open can be told
    /// without reading the words.
    /// </summary>
    public static void Tab(Button button)
    {
        StyleBoxFlat quiet = Surface();
        quiet.SetCornerRadiusAll(10);

        button.AddThemeStyleboxOverride("normal", quiet);
        button.AddThemeStyleboxOverride("hover", quiet);
        button.AddThemeStyleboxOverride("focus", quiet);
        button.AddThemeStyleboxOverride("pressed", Lit());
        button.AddThemeColorOverride("font_color", Muted);
        button.AddThemeColorOverride("font_hover_color", Title);
        button.AddThemeColorOverride("font_pressed_color", Engrave);
        button.AddThemeColorOverride("font_focus_color", Muted);
    }

    /// <summary>
    /// The strip a window's title sits on — dark stone across the full width, with the title in light letters.
    /// Never engraved: engraving needs the light stone under it or the words vanish.
    /// </summary>
    public static Control Header(Control inside)
    {
        PanelContainer strip = new();
        strip.AddThemeStyleboxOverride("panel", Stone());
        strip.AddChild(inside);

        return strip;
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
