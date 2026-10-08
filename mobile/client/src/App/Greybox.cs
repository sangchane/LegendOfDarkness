using Godot;

namespace LodClient;

/// <summary>
/// The one place the screens get their look. It began as a deliberately grey placeholder for judging layout;
/// it now carries the original 4.51 theme, worked out and written down in <c>data/ui-vault/</c>.
/// </summary>
/// <remarks>
/// Original 4.51 stone on frames, title strips and primary actions; flat, dark interiors for small text.
/// The extracted tiles repeat at their native size, so a phone never stretches a desktop dialog into a skin.
/// Material changes stay here; touch targets, safe areas and responsive layouts stay with the screens.
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

    private static readonly Texture2D LightStone = GD.Load<Texture2D>("res://assets/ui/stone.png");

    // ── 치수. data/ui-vault/치수/치수.md ─────────────────────────────────────

    /// <summary>The stone frame's thickness.</summary>
    public const int Frame = 5;

    /// <summary>Padding inside a window, and the gap between its parts.</summary>
    public const int Pad = 12;

    public const int Gap = 8;

    /// <summary>
    /// Rounding of everything pressed, picked or filled in — buttons, fields, list rows, item cells. Only on the inside: a stone
    /// frame, and a sheet laid inside one, stay square to read as the original's. One value for all of them (사용자 2026-10-08
    /// 「아이템 이미지는 각지게 테두리하고 어떤 버튼은 라운드로하고 통일성이 없는거 같아 … 전반적으로」) — it was 0 · 3 · 8 · 12 by screen.
    /// </summary>
    public const int Round = 8;

    /// <summary>Rounding of a plate floating over a window — an info box, an ask, a card. A step rounder than what it holds.</summary>
    public const int RoundPlate = 12;

    /// <summary>
    /// The smallest words anywhere — dev:ui Godot mobile 「보조 26px 이상(기준 폭 720)」 is 13 on our 360 design width; smaller does
    /// not read on a phone (UI 리뷰 2026-10-09, there were 8 · 9 · 10 · 11 · 12 by screen).
    /// </summary>
    public const int SmallText = 13;

    /// <summary>Words in a box that cannot grow — under an icon button (「버리는 중」 has to fit 48) and in a party tile.</summary>
    public const int TightText = 12;

    /// <summary>Room between a box's edge and the picture, words or number inside it — nothing touches an edge.</summary>
    public const int Inset = 8;

    /// <summary>Height of a slider's groove — a gauge a thumb can see (6 read as a hairline, 사용자 2026-10-08 「게이지도 높이 조금만 키워」).</summary>
    public const int GrooveHeight = 10;

    /// <summary>
    /// A slider's groove: the whole track in the cell colour, the filled part in <paramref name="fill" />, both <see cref="GrooveHeight" />
    /// tall with round ends — the potion gauges and the settings sliders share it.
    /// </summary>
    public static void Groove(Slider slider, Color fill)
    {
        StyleBoxFlat groove = Rounded(Surface(), GrooveHeight / 2);
        StyleBoxFlat filled = Rounded(Fill(fill), GrooveHeight / 2);
        foreach (StyleBoxFlat box in new[] { groove, filled })
        {
            box.ContentMarginTop = box.ContentMarginBottom = GrooveHeight / 2;
        }

        slider.AddThemeStyleboxOverride("slider", groove);
        slider.AddThemeStyleboxOverride("grabber_area", filled);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", filled);
    }

    /// <summary>
    /// A switch's picture, 52×28 — a pill with a knob, lit when on. The engine's own was a small thing beside the big buttons
    /// (사용자 2026-10-08 「라디오박스 … 가로 좀 넓히고」). Drawn with soft edges so it stays smooth at any scale.
    /// </summary>
    public static Texture2D SwitchIcon(bool on, bool disabled = false)
    {
        const int wide = 52, high = 28;
        const float r = high / 2f;
        Color track = on ? Accent : Cell, edge = on ? Accent : Muted, knob = on ? Text : Muted;
        float fade = disabled ? 0.4f : 1f;
        float knobX = on ? wide - r : r;
        Image image = Image.CreateEmpty(wide, high, false, Image.Format.Rgba8);

        for (int y = 0; y < high; y++)
        {
            for (int x = 0; x < wide; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = new Vector2(px, py).DistanceTo(new Vector2(Mathf.Clamp(px, r, wide - r), r)) - (r - 0.5f);
                float cover = Mathf.Clamp(0.5f - d, 0, 1);
                if (cover <= 0)
                {
                    image.SetPixel(x, y, Colors.Transparent);
                    continue;
                }

                Color paint = d > -1.5f ? edge : track;
                float knobCover = Mathf.Clamp(0.5f - (new Vector2(px, py).DistanceTo(new Vector2(knobX, r)) - (r - 4)), 0, 1);
                paint = paint.Lerp(knob, knobCover);
                image.SetPixel(x, y, paint with { A = cover * fade });
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>The same box with <see cref="Round" /> corners (or <paramref name="radius" />).</summary>
    public static StyleBoxFlat Rounded(StyleBoxFlat box, int radius = Round)
    {
        box.SetCornerRadiusAll(radius);
        return box;
    }


    /// <summary>Login backdrop, shared with its panel and input surfaces.</summary>
    public static ColorRect EntryBackground()
    {
        ColorRect background = new() { Color = Inner, MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return background;
    }

    /// <summary>Game title cutout with real alpha, contained at any screen ratio.</summary>
    public static TextureRect EntryTitle(Vector2 minimum) => new()
    {
        Texture = new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>("res://assets/ui/title-cutout.png"),
            Region = new Rect2(130, 61, 1451, 828)
        },
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        CustomMinimumSize = minimum,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public static Texture2D CheckIcon(bool selected)
    {
        Image image = Image.CreateEmpty(18, 18, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        for (int x = 1; x < 17; x++)
        {
            image.SetPixel(x, 1, Muted);
            image.SetPixel(x, 16, Muted);
            image.SetPixel(1, x, Muted);
            image.SetPixel(16, x, Muted);
        }
        if (selected)
            for (int x = 4; x < 14; x++)
            {
                int y = x < 7 ? x + 4 : 18 - x;
                image.SetPixel(x, y, Accent);
                image.SetPixel(x, y + 1, Accent);
            }
        return ImageTexture.CreateFromImage(image);
    }

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

    /// <summary>A narrow original stone rim. Preserve container geometry while drawing a visible edge.</summary>
    public static StyleBoxTexture Stone()
    {
        StyleBoxTexture frame = Tile(LightStone, 1);
        frame.DrawCenter = false;
        frame.SetTextureMarginAll(4);
        frame.SetExpandMarginAll(2);
        return frame;
    }

    /// <summary>Original bright stone for an action or the selected tab.</summary>
    public static StyleBoxTexture Lit() => Tile(LightStone, 2);

    private static StyleBoxTexture Tile(Texture2D texture, int padding)
    {
        StyleBoxTexture tile = new()
        {
            Texture = texture,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile
        };
        tile.SetContentMarginAll(padding);
        return tile;
    }

    /// <summary>
    /// The button that commits — 입기, 삽니다, 보내기. Same as every button on the login and create screens: opaque
    /// dark plate, light letters, a thin edge — one per window, told apart by its brighter edge. No grey fill: dark
    /// letters on grey read poorly, and stone under letters clashes (사용자, 2026-09-30).
    /// </summary>
    public static void Commit(Button button)
    {
        foreach (string state in new[] { "normal", "hover", "focus", "pressed", "hover_pressed", "disabled" })
        {
            StyleBoxFlat plate = Sheet();
            plate.SetCornerRadiusAll(Round);
            plate.SetContentMarginAll(4);
            if (state is not ("pressed" or "hover_pressed" or "disabled")) plate.BorderColor = Title;
            if (state is "pressed" or "hover_pressed") plate.BgColor = Cell;
            button.AddThemeStyleboxOverride(state, plate);
        }

        foreach (string colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
        {
            button.AddThemeColorOverride(colour, Text);
        }

        button.AddThemeColorOverride("font_disabled_color", Muted);
    }

    /// <summary>
    /// A tab, as on the create screen: every tab keeps the dark plate; the open one gets a thick bright underline
    /// and bright letters, the others a thin rule and muted letters.
    /// </summary>
    public static void Tab(Button button)
    {
        foreach (string state in new[] { "normal", "hover", "focus", "pressed", "hover_pressed" })
        {
            bool chosen = state is "pressed" or "hover_pressed";
            StyleBoxFlat plate = Sheet();
            plate.SetBorderWidthAll(0);
            plate.BorderWidthBottom = chosen ? 3 : 1;
            if (chosen) plate.BorderColor = Title;
            plate.SetContentMarginAll(4);
            button.AddThemeStyleboxOverride(state, plate);
        }

        button.AddThemeColorOverride("font_color", Muted);
        button.AddThemeColorOverride("font_hover_color", Muted);
        button.AddThemeColorOverride("font_focus_color", Muted);
        button.AddThemeColorOverride("font_pressed_color", Text);
        button.AddThemeColorOverride("font_hover_pressed_color", Text);
    }

    /// <summary>
    /// A window's title row: a flat dark plate (no stone under letters, work order 0절) with the original dragon
    /// emblem on its left — the one mark that keeps every window reading as Dark Ages once the stone strip is gone.
    /// The emblem is the option01 dragon cut out (`assets/ui/dragon-cutout.png`), shown at half size, never enlarged (R9).
    /// A head already full to the edge (the shop's three filters and the NPC's name) passes <paramref name="emblem" /> false.
    /// </summary>
    public static Control Header(Control inside, bool emblem = true)
    {
        StyleBoxFlat strip = new() { BgColor = Cell, BorderColor = CellEdge, BorderWidthBottom = 1 };
        strip.SetContentMarginAll(Pad / 2);

        TextureRect dragon = new()
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/dragon-cutout.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(27, 24),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Pad / 2);
        if (emblem) row.AddChild(dragon);
        inside.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(inside);

        PanelContainer head = new();
        head.AddThemeStyleboxOverride("panel", strip);
        head.AddChild(row);

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

    /// <summary>
    /// An ordinary button — 대화, 닫기, 정렬. Dark and rounded, with a visible edge: these sit straight on the
    /// map, and without an edge a dark button on a dark tile stops looking like a button at all.
    /// </summary>
    public static void Plain(Button button)
    {
        foreach (string state in new[] { "normal", "hover", "focus" })
        {
            StyleBoxFlat box = new() { BgColor = Inner with { A = 0.92f }, BorderColor = Deep };
            box.SetBorderWidthAll(1);
            box.SetCornerRadiusAll(Round);
            button.AddThemeStyleboxOverride(state, box);
        }

        StyleBoxFlat down = new() { BgColor = Cell, BorderColor = Muted };
        down.SetBorderWidthAll(1);
        down.SetCornerRadiusAll(Round);
        button.AddThemeStyleboxOverride("pressed", down);

        button.AddThemeColorOverride("font_color", Title);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Text);
    }

    /// <summary>Darkens what is under it, so a number written over a picture can be read.</summary>
    public static StyleBoxFlat Shade() => new() { BgColor = new Color(0, 0, 0, 0.55f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23 };

    /// <summary>The play area behind the HUD.</summary>
    public static StyleBoxFlat World() => new() { BgColor = Inner };

    /// <summary>Filled portion of a bar, in the health colour. Always paired with numbers, never read by shade alone.</summary>
    public static StyleBoxFlat Fill() => Fill(Health);

    /// <summary>Filled portion of a bar in a given colour — health's orange, mana's blue, still paired with numbers.</summary>
    public static StyleBoxFlat Fill(Color paint) => new() { BgColor = paint };

    /// <summary>
    /// The numbers written on a bar (2026-09-27 — 막대 옆 따로 적던 숫자를 막대 안에 얹어 판을 좁힌다). Light letters with a dark
    /// outline, so they read the same over the filled colour and over the dark empty part. Centred across the whole bar.
    /// </summary>
    public static Label OnBar(ProgressBar bar, int fontSize)
    {
        Label text = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        text.AddThemeFontSizeOverride("font_size", fontSize);
        text.AddThemeColorOverride("font_color", Text);
        text.AddThemeColorOverride("font_outline_color", new Color("#030303"));
        // 작은 글자는 외곽선이 두꺼우면 획이 뭉개진다.
        text.AddThemeConstantOverride("outline_size", fontSize <= 9 ? 2 : 3);
        text.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // 막대보다 글자가 조금 높다 — 위아래로 넘쳐도 가운데에 선다.
        text.OffsetTop = -4;
        text.OffsetBottom = 4;
        bar.AddChild(text);

        return text;
    }

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
