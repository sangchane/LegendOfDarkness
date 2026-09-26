using System.Collections.Generic;
using Godot;

namespace LodClient;

/// <summary>
/// 설정 창 — 메뉴가 늘어 세 탭으로 나눴다(2026-09-26). 위쪽 작은 아이콘 탭, 오른쪽 위 X(<see cref="WindowFrame"/>).
/// <list type="bullet">
/// <item><b>자동</b> — 자동 포션 줄 둘(체력·마력이 몇 % 이하일 때 마시나, 게이지 바 <see cref="PotionGauge"/> 10~90%), 자동 사냥의
/// 반경 슬라이더·회복 기술 셀렉트 박스(<see cref="PercentSelect"/>, 1~99). 무엇을 마실지와 켜고 끄기는 게임 화면의 포션 단추에서
/// 한다(<see cref="PotionChip"/>).</item>
/// <item><b>봇</b> — [봇 부르기]/[봇 보내기].</item>
/// <item><b>계정</b> — 자동 로그인 끄기, [종료](위 줄에 있던 것 — 누르면 [로그아웃]·[게임 종료]·[취소] 판, <see cref="ExitChoice"/>).
/// 자동 로그인을 다시 켜는 것은 로그인 화면에서만 한다(계정·비밀번호가 그 화면에만 있다).</item>
/// </list>
/// </summary>
public sealed partial class SettingsPanel : PanelContainer
{
    private Button _autoLoginOff = null!;
    private readonly Dictionary<string, PercentSelect> _percentSelects = new();
    private int _rehearsedOpen; // --percent-open: 손 없이 확인할 때 몇 프레임 기다렸다 목록을 연다.
    private readonly Dictionary<string, (Button Tab, Control Page)> _pages = new();

    public SettingsPanel()
    {
        Name = "Settings";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        Close = WindowFrame.CloseButton();

        // ── 자동 ─────────────────────────────────────────────
        VBoxContainer auto = Page();
        PotionGauge health = new(Main.HealthPotion.Percent, Greybox.Health);
        health.Changed += percent => Main.SetPotions(Main.HealthPotion with { Percent = percent }, Main.ManaPotion);
        auto.AddChild(Caption("자동 포션 — 이하가 되면 저절로 마신다"));
        auto.AddChild(Row("체력 포션", health));

        PotionGauge mana = new(Main.ManaPotion.Percent, Greybox.Mana);
        mana.Changed += percent => Main.SetPotions(Main.HealthPotion, Main.ManaPotion with { Percent = percent });
        auto.AddChild(Row("마력 포션", mana));
        auto.AddChild(BuildAutoHunt());

        // ── 봇 ───────────────────────────────────────────────
        // 봇(성직자 동료) — 부르면 서버가 봇을 내 곁으로 데려와 파티에 넣는다. 결과는 서버 알림으로 온다(우리 확장 0xF1·0x5E).
        VBoxContainer bot = Page();
        bot.AddChild(Caption("성직자 봇이 따라다니며 회복·버프를 겁니다. 왼쪽 봇 칸을 누르면 봇 장비창이 열립니다."));
        Companion = new Button { Text = CallText, CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        Greybox.Plain(Companion);
        bot.AddChild(Companion);

        // ── 계정 ─────────────────────────────────────────────
        VBoxContainer account = Page();
        _autoLoginOff = new Button
        {
            Text = "자동 로그인 끄기",
            Disabled = Main.SavedLogin is null,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
        };
        Greybox.Plain(_autoLoginOff);
        _autoLoginOff.Pressed += () =>
        {
            Main.SetSavedLogin(null);
            _autoLoginOff.Disabled = true;
        };

        account.AddChild(Caption(Main.SavedLogin is null ? "자동 로그인이 꺼져 있습니다." : "이 기기에 계정이 저장되어 있습니다."));
        account.AddChild(_autoLoginOff);

        Exit = new Button { Text = "종료", CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        Greybox.Plain(Exit);
        account.AddChild(Exit);

        Button autoTab = WindowFrame.IconButton(GlyphKind.Auto, "자동", tab: true, width: 52);
        Button botTab = WindowFrame.IconButton(GlyphKind.Bot, "봇", tab: true, width: 52);
        Button accountTab = WindowFrame.IconButton(GlyphKind.Account, "계정", tab: true, width: 52);
        _pages["자동"] = (autoTab, auto);
        _pages["봇"] = (botTab, bot);
        _pages["계정"] = (accountTab, account);

        foreach ((string name, (Button tab, Control _)) in _pages)
        {
            tab.Pressed += () => ShowTab(name);
        }

        VBoxContainer pages = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pages.AddChild(auto);
        pages.AddChild(bot);
        pages.AddChild(account);

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        inside.AddChild(WindowFrame.Head(WindowFrame.Tabs(autoTab, botTab, accountTab), Close));

        MarginContainer margin = new();

        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, Main.Gutter);
        }

        margin.AddThemeConstantOverride("margin_top", Main.Gutter / 2);

        // 가로는 화면이 낮아(360짜리) 자동 탭(포션 게이지 둘 + 자동 사냥 둘)이 다 안 들어간다 — 탭 안을 굴린다. 탭 줄과 X 는
        // 굴리지 않는다. 세로는 다 보인다.
        if (Main.Portrait)
        {
            inside.AddChild(pages);
        }
        else
        {
            // ScrollContainer 는 속의 너비를 제 최소 크기로 올려 보내지 않는다 — 안 주면 가로 폭이 0 이 돼 창이 통째로 사라진다
            // (실측, 2026-09-26). 세로와 같은 내용 너비를 그대로 준다.
            ScrollContainer scroll = new() { CustomMinimumSize = new Vector2(340, 200), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            scroll.AddChild(pages);
            inside.AddChild(scroll);
        }

        margin.AddChild(inside);
        AddChild(margin);

        ShowTab(Main.SettingsTab is { Length: > 0 } asked && _pages.ContainsKey(asked) ? asked : "자동");
    }

    /// <summary>탭 하나를 보인다 — 자동 · 봇 · 계정.</summary>
    public void ShowTab(string name)
    {
        foreach ((string each, (Button tab, Control page)) in _pages)
        {
            page.Visible = each == name;
            tab.SetPressedNoSignal(each == name);
            tab.EmitSignal(BaseButton.SignalName.Toggled, each == name);
        }
    }

    /// <summary>계정 탭의 [종료] — 누르면 게임 화면이 [로그아웃]·[게임 종료]·[취소] 판(<see cref="ExitChoice"/>)을 연다.</summary>
    public Button Exit { get; }

    private static VBoxContainer Page()
    {
        VBoxContainer page = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", Main.Gutter);

        return page;
    }

    private static Label Caption(string text)
    {
        Label caption = new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(200, 0) };
        caption.AddThemeColorOverride("font_color", Greybox.Muted);
        caption.AddThemeFontSizeOverride("font_size", 13);

        return caption;
    }

    public Button Close { get; }

    private const string CallText = "봇 부르기";
    private const string DismissText = "봇 보내기";

    /// <summary>[봇 부르기] — 봇이 있으면 [봇 보내기]. 누르면 무엇을 할지는 게임 화면이 정한다.</summary>
    public Button Companion { get; }

    /// <summary>동료가 있나(0x5E)에 따라 단추 글자를 바꾼다.</summary>
    public void ShowCompanion(bool present)
    {
        string text = present ? DismissText : CallText;

        if (Companion.Text != text)
        {
            Companion.Text = text;
        }
    }

    /// <summary>
    /// 자동 사냥 두 줄 — 켠 자리에서 몇 칸까지 쫓나(4~20, 기본 12), 체력 몇 % 이하에서 회복 기술을 쓰나(1~99, 기본 50).
    /// 켜고 끄기는 공격 단추를 0.5초 길게 눌러서 한다(<see cref="AbilityBar"/>).
    /// </summary>
    private Control BuildAutoHunt()
    {
        VBoxContainer rows = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", Main.Gutter);

        rows.AddChild(SliderRow("사냥 반경", 4, 20, 1, Main.AutoHuntSettings.Radius, value => $"{value}칸",
            value => Main.SetAutoHuntSettings(Main.AutoHuntSettings with { Radius = value })));

        PercentSelect heal = new(Main.AutoHuntSettings.HealPercent, this);
        heal.Changed += value => Main.SetAutoHuntSettings(Main.AutoHuntSettings with { HealPercent = value });
        rows.AddChild(Row("회복 기술", heal));
        _percentSelects["heal"] = heal;

        VBoxContainer block = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        block.AddThemeConstantOverride("separation", Main.Gutter / 2);
        block.AddChild(new Label { Text = "자동 사냥", HorizontalAlignment = HorizontalAlignment.Center });
        block.AddChild(rows);

        return block;
    }

    private static Control SliderRow(string title, int from, int to, int step, int value,
        System.Func<int, string> shown, System.Action<int> changed)
    {
        HBoxContainer row = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label name = new() { Text = title, VerticalAlignment = VerticalAlignment.Center };
        name.AddThemeColorOverride("font_color", Greybox.Muted);

        Label figure = new()
        {
            Text = shown(value),
            CustomMinimumSize = new Vector2(44, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        HSlider slider = new()
        {
            MinValue = from,
            MaxValue = to,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(96, Main.TouchMinimum),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        // 테마의 기본 홈은 어두운 속에 묻혀 채워진 쪽만 보였다 — 홈 전체를 칸 색으로, 채운 쪽을 강조색으로.
        StyleBoxFlat groove = Greybox.Surface();
        groove.ContentMarginTop = 3;
        groove.ContentMarginBottom = 3;
        StyleBoxFlat filled = Greybox.Fill(Greybox.Accent);
        filled.ContentMarginTop = 3;
        filled.ContentMarginBottom = 3;
        slider.AddThemeStyleboxOverride("slider", groove);
        slider.AddThemeStyleboxOverride("grabber_area", filled);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", filled);

        slider.ValueChanged += now =>
        {
            figure.Text = shown((int)now);
            changed((int)now);
        };

        row.AddChild(name);
        row.AddChild(slider);
        row.AddChild(figure);

        return row;
    }

    /// <summary>A label on the left, some control on the right — the heal-skill select and, since it already
    /// fills the row itself (<see cref="PotionGauge"/>), the two potion gauges too.</summary>
    private static Control Row(string title, Control control)
    {
        HBoxContainer row = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label name = new()
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = control is PotionGauge ? SizeFlags.ShrinkBegin : SizeFlags.ExpandFill
        };
        name.AddThemeColorOverride("font_color", Greybox.Muted);

        row.AddChild(name);
        row.AddChild(control);

        return row;
    }

    /// <summary>
    /// --percent-open heal: 손 없이 확인할 때, 창이 자리를 잡으면 그 셀렉트 박스를 스스로 눌러 목록을 열어 본다
    /// (<see cref="AbilityBar"/>의 --slot-hold 와 같은 결). 체력·마력은 이제 게이지 바라(<see cref="PotionGauge"/>,
    /// 2026-09-26) 열 목록이 없다 — 이 값은 받아도 조용히 아무 일 하지 않는다.
    /// </summary>
    public override void _Process(double delta)
    {
        if (Main.PercentOpen.Length == 0 || _rehearsedOpen < 0)
        {
            return;
        }

        if (Visible && ++_rehearsedOpen >= 30 && _percentSelects.TryGetValue(Main.PercentOpen, out PercentSelect? select))
        {
            select.Open();
            _rehearsedOpen = -1;
        }
    }
}
