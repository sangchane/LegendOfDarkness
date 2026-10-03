using System.Linq;
using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Automation;

namespace LodClient;

/// <summary>
/// 설정 창 — 메뉴가 늘어 세 탭으로 나눴다(2026-09-26). 위쪽 작은 아이콘 탭, 오른쪽 위 X(<see cref="WindowFrame"/>).
/// <list type="bullet">
/// <item><b>자동</b> — 자동 포션 줄 둘(체력·마력이 몇 % 이하일 때 마시나, 게이지 바 <see cref="PotionGauge"/> 10~90%), 자동 사냥의
/// 반경 슬라이더·회복 기술 셀렉트 박스(<see cref="PercentSelect"/>, 1~99). 무엇을 마실지와 켜고 끄기는 게임 화면의 포션 단추에서
/// 한다(<see cref="PotionChip"/>).</item>
/// <item><b>봇</b> — [봇 부르기]/[봇 보내기], 「마법사」 저주·나르콜리 켜고 끄기.</item>
/// </list>
/// [로그아웃] 은 어느 탭에서나 보이는 제목 줄에 있다(누르면 [로그아웃]·[게임 종료]·[취소] 판, <see cref="ExitChoice"/>).
/// 계정 탭(자동 로그인 끄기)은 뺐다(사용자, 2026-09-30) — 로그아웃한 로그인 화면에서 「자동 로그인」을 끄면 저장된 계정이 지워진다.
/// </summary>
public sealed partial class SettingsPanel : PanelContainer
{
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

        // 밟은 것을 알아서 주울지 — 원작에 없던 것이라 끌 수 있어야 한다(2026-09-19). 소지품 창에서 옮겨 왔다(사용자 2026-10-01).
        auto.AddChild(Row("아이템 자동 줍기", Switch(Main.AutoLoot, Main.SetAutoLoot)));

        // ── 봇 ───────────────────────────────────────────────
        // 봇(성직자 동료) — 부르면 서버가 봇을 내 곁으로 데려와 파티에 넣는다. 결과는 서버 알림으로 온다(우리 확장 0xF1·0x5E).
        VBoxContainer bot = Page();
        bot.AddThemeConstantOverride("separation", Main.Gutter / 2);

        // 부르기·보내기는 켬/끔 한 줄(사용자, 2026-10-03: 공간 적게). 누르면 무엇을 할지는 게임 화면이 정한다.
        Companion = Switch(false, _ => { });
        bot.AddChild(BotRow("봇", Companion));

        // 마법사·성직자 — 셀렉트는 마법 이름 그대로(고른 적 없으면 봇이 배운 가장 센 것을 보인다, ShowBotLevel), 켬은 두 칸씩.
        bot.AddChild(Heading("마법사"));
        _curse = PercentSelect.Of([.. Main.CurseChoices.Select(one => one.Name)], Main.BotCurse, this, SelectWidth, SelectHeight);
        _curse.Changed += row => Order(curse: row);
        bot.AddChild(Pair(BotRow("저주", _curse), BotRow("나르콜리", Switch(Main.BotSleep, on => Order(sleep: on)))));

        bot.AddChild(Heading("성직자"));
        _heal = PercentSelect.Of([.. Main.HealChoices.Select(one => one.Name)], Main.BotHeal, this, SelectWidth, SelectHeight);
        _heal.Changed += row => Order(heal: row);
        _groupHeal = PercentSelect.Of([.. Main.GroupHealChoices.Select(one => one.Name)], Main.BotGroupHeal, this, SelectWidth, SelectHeight);
        _groupHeal.Changed += row => Order(groupHeal: row);
        bot.AddChild(Pair(BotRow("회복", _heal), BotRow("파티", _groupHeal)));

        Control PriestSwitch(string name, CompanionSpells.Priest bit) =>
            BotRow(name, Switch((Main.BotPriest & bit) != 0, on => Order(priest: on ? Main.BotPriest | bit : Main.BotPriest & ~bit)));
        bot.AddChild(Pair(PriestSwitch("디나르콜리", CompanionSpells.Priest.Dinarcoli), PriestSwitch("디소루마", CompanionSpells.Priest.Disoruma)));
        bot.AddChild(Pair(PriestSwitch("호르라마", CompanionSpells.Priest.Horrama), PriestSwitch("에나르마", CompanionSpells.Priest.Enarma)));
        ShowBotLevel(0);

        // [로그아웃] 은 탭이 아니라 제목 줄에 — 어느 탭에서나 한 번에 닿는다(사용자, 2026-09-26: 종료가 너무 깊고 로그아웃이 안 보인다).
        Exit = new Button { Text = "로그아웃", CustomMinimumSize = new Vector2(76, Main.TouchMinimum), FocusMode = FocusModeEnum.None };
        Greybox.Plain(Exit);

        Button autoTab = WindowFrame.IconButton(GlyphKind.Auto, "자동", tab: true, width: 52);
        Button botTab = WindowFrame.IconButton(GlyphKind.Bot, "봇", tab: true, width: 52);
        _pages["자동"] = (autoTab, auto);
        _pages["봇"] = (botTab, bot);

        foreach ((string name, (Button tab, Control _)) in _pages)
        {
            tab.Pressed += () => ShowTab(name);
        }

        VBoxContainer pages = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pages.AddChild(auto);
        pages.AddChild(bot);

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        inside.AddChild(WindowFrame.Head(WindowFrame.Tabs(autoTab, botTab), Close, Exit));

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
            // 세로도 봇 탭(마법사·성직자)이 길어 화면을 넘는다 — 남는 높이까지만 보이고 그 안을 굴린다(_Ready 에서 잰다).
            _portraitScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _portraitScroll.AddChild(pages);
            inside.AddChild(_portraitScroll);
            _portraitPages = pages;
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

        // 돌 테두리(Stone)는 속을 안 그린다 — 지도 위에서 글자가 비쳐 안 읽혔다(사용자, 2026-09-30). 상점·기록 창처럼 불투명 판을 한 겹.
        PanelContainer within = new();
        within.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        within.AddChild(margin);
        AddChild(within);

        ShowTab(Main.SettingsTab is { Length: > 0 } asked && _pages.ContainsKey(asked) ? asked : "자동");
    }

    private ScrollContainer? _portraitScroll;
    private Control? _portraitPages;

    /// <summary>세로 화면에서 굴림 칸의 높이 — 속 높이와 화면 아래 위 줄·제목 줄을 뺀 남는 높이 중 작은 것.</summary>
    private void FitPortrait()
    {
        if (_portraitScroll is null || _portraitPages is null || !IsInsideTree())
        {
            return;
        }

        // 굴림 칸이 선 자리부터 화면 아래(여백·창 테두리 몫을 남기고)까지. 자리를 잡기 전이면 위 줄·제목 줄 몫(실측 약 290)을 뺀다.
        float top = _portraitScroll.GlobalPosition.Y > 0 ? _portraitScroll.GlobalPosition.Y : 290;
        float room = GetViewportRect().Size.Y - top - Main.Gutter * 3;
        Vector2 inner = _portraitPages.GetCombinedMinimumSize();
        _portraitScroll.CustomMinimumSize = new Vector2(inner.X, Mathf.Min(inner.Y, Mathf.Max(200, room)));
    }

    public override void _Ready()
    {
        FitPortrait();
        VisibilityChanged += () => Callable.From(FitPortrait).CallDeferred();
        Resized += () => Callable.From(FitPortrait).CallDeferred();
    }

    /// <summary>탭 하나를 보인다 — 자동 · 봇.</summary>
    public void ShowTab(string name)
    {
        foreach ((string each, (Button tab, Control page)) in _pages)
        {
            page.Visible = each == name;
            tab.SetPressedNoSignal(each == name);
            tab.EmitSignal(BaseButton.SignalName.Toggled, each == name);
        }

        FitPortrait();
    }

    /// <summary>제목 줄의 [로그아웃] — 누르면 게임 화면이 [로그아웃]·[게임 종료]·[취소] 판(<see cref="ExitChoice"/>)을 연다.</summary>
    public Button Exit { get; }

    private static VBoxContainer Page()
    {
        VBoxContainer page = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", Main.Gutter);

        return page;
    }

    /// <summary>켜고 끄는 단추 — 테마의 체크 그림만(판 없이).</summary>
    private static CheckButton Switch(bool on, System.Action<bool> toggled)
    {
        CheckButton check = new() { ButtonPressed = on, CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        check.Toggled += on => toggled(on);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
        {
            check.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        return check;
    }

    /// <summary>봇 탭에서 하나를 바꾸고 나머지는 그대로 둔 채 남기고 알린다.</summary>
    private void Order(int? curse = null, bool? sleep = null, CompanionSpells.Priest? priest = null, int? heal = null, int? groupHeal = null)
    {
        Main.SetBotOrders(curse ?? Main.BotCurse, sleep ?? Main.BotSleep, priest ?? Main.BotPriest, heal ?? Main.BotHeal, groupHeal ?? Main.BotGroupHeal);
        BotMagicChanged?.Invoke();
    }

    /// <summary>봇 탭에서 고른 것을 바꿨다 — 값은 <see cref="Main.BotOrdersFor"/>.</summary>
    public event System.Action? BotMagicChanged;

    private static Label Caption(string text)
    {
        Label caption = new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(200, 0) };
        caption.AddThemeColorOverride("font_color", Greybox.Muted);
        caption.AddThemeFontSizeOverride("font_size", 13);

        return caption;
    }

    public Button Close { get; }

    /// <summary>봇 켬/끔 — 끈 채 누르면 부르기, 켠 채 누르면 보내기. 누르면 무엇을 할지는 게임 화면이 정한다.</summary>
    public CheckButton Companion { get; }

    private bool? _present;

    /// <summary>동료가 있나(0x5E)가 바뀔 때만 켬/끔을 맞춘다 — 누른 뒤 서버 답을 기다리는 동안 되돌아가지 않게.</summary>
    public void ShowCompanion(bool present)
    {
        if (_present != present)
        {
            _present = present;
            Companion.SetPressedNoSignal(present);
        }
    }

    private const int SelectWidth = 108;
    private const int SelectHeight = 36;
    private readonly PercentSelect _curse;
    private readonly PercentSelect _heal;
    private readonly PercentSelect _groupHeal;

    /// <summary>고른 적 없는 셀렉트에 지금 레벨에서 봇이 배운 가장 센 마법 이름을 보인다(게임 화면이 틱마다).</summary>
    public void ShowBotLevel(int ownerLevel)
    {
        if (Main.BotCurse < 0)
        {
            _curse.Display(Main.AutoRow([.. Main.CurseChoices.Select(one => one.Level)], ownerLevel));
        }

        if (Main.BotHeal < 0)
        {
            _heal.Display(Main.AutoRow([.. Main.HealChoices.Select(one => one.Level)], ownerLevel));
        }

        if (Main.BotGroupHeal < 0)
        {
            _groupHeal.Display(Main.AutoRow([.. Main.GroupHealChoices.Select(one => one.Level)], ownerLevel));
        }
    }

    private static Label Heading(string text)
    {
        Label heading = new() { Text = text };
        heading.AddThemeColorOverride("font_color", Greybox.Accent);
        heading.AddThemeFontSizeOverride("font_size", 13);

        return heading;
    }

    /// <summary>봇 탭 한 칸 — 이름 왼쪽, 단추 오른쪽, 창 가장자리에서 띄운다.</summary>
    private static Control BotRow(string title, Control control)
    {
        HBoxContainer row = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Label name = new() { Text = title, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeColorOverride("font_color", Greybox.Muted);
        name.AddThemeFontSizeOverride("font_size", 14);
        control.CustomMinimumSize = new Vector2(control.CustomMinimumSize.X, SelectHeight);
        row.AddChild(name);
        row.AddChild(control);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(Main.Gutter, 0) });

        return row;
    }

    /// <summary>두 칸을 한 줄에.</summary>
    private static Control Pair(Control left, Control right)
    {
        HBoxContainer pair = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pair.AddThemeConstantOverride("separation", Main.Gutter * 2);
        pair.AddChild(left);
        pair.AddChild(right);

        return pair;
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
