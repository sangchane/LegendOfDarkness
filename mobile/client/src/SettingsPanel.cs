using Godot;

namespace LodClient;

/// <summary>
/// 설정 창. 자동 포션 줄 둘 — 체력·마력이 몇 % 이하일 때 마시나를 돌림판으로 고른다 — 자동 사냥의 반경·회복 줄
/// 슬라이더 둘, 그리고 자동 로그인 끄기 단추 하나. 무엇을 마실지와 켜고 끄기는 게임 화면의 포션 단추에서 한다(<see cref="PotionChip"/>).
/// 자동 로그인을 다시 켜는 것은 로그인 화면에서만 한다(계정·비밀번호가 그 화면에만 있다).
/// </summary>
public sealed partial class SettingsPanel : PanelContainer
{
    private Button _autoLoginOff = null!;

    public SettingsPanel()
    {
        Name = "Settings";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);
        head.AddChild(new Label { Text = "설정", SizeFlagsHorizontal = SizeFlags.ExpandFill });

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        Greybox.Plain(Close);
        head.AddChild(Close);

        HBoxContainer wheels = new() { Alignment = BoxContainer.AlignmentMode.Center };
        // 가로는 방향판과 부채꼴 사이 가운데에 서므로(GameScreen.Cover) 좁게 — 640 폭에서도 부채꼴에 닿지 않는다.
        wheels.AddThemeConstantOverride("separation", Main.Portrait ? Main.Gutter * 3 : Main.Gutter);

        PercentWheel health = new(Main.HealthPotion.Percent);
        health.Changed += percent => Main.SetPotions(Main.HealthPotion with { Percent = percent }, Main.ManaPotion);
        wheels.AddChild(Titled("체력 포션", health));

        PercentWheel mana = new(Main.ManaPotion.Percent);
        mana.Changed += percent => Main.SetPotions(Main.HealthPotion, Main.ManaPotion with { Percent = percent });
        wheels.AddChild(Titled("마력 포션", mana));

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

        inside.AddChild(head);
        inside.AddChild(new Label { Text = "이하가 되면 저절로 마신다", HorizontalAlignment = HorizontalAlignment.Center });
        inside.AddChild(wheels);

        // 가로는 높이가 모자라 자동 사냥 두 줄을 돌림판 옆에 세운다. 세로는 아래에.
        if (Main.Portrait)
        {
            inside.AddChild(BuildAutoHunt());
        }
        else
        {
            wheels.AddChild(BuildAutoHunt());
        }

        inside.AddChild(_autoLoginOff);

        MarginContainer margin = new();

        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, Main.Gutter);
        }

        margin.AddChild(inside);
        AddChild(margin);
    }

    public Button Close { get; }

    /// <summary>
    /// 자동 사냥 두 줄 — 켠 자리에서 몇 칸까지 쫓나(4~20, 기본 12), 체력 몇 % 이하에서 회복 기술을 쓰나(10~90, 기본 50).
    /// 켜고 끄기는 게임 화면의 [자동] 단추에서 한다.
    /// </summary>
    private static Control BuildAutoHunt()
    {
        VBoxContainer rows = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", Main.Gutter);

        rows.AddChild(SliderRow("사냥 반경", 4, 20, 1, Main.AutoHuntSettings.Radius, value => $"{value}칸",
            value => Main.SetAutoHuntSettings(Main.AutoHuntSettings with { Radius = value })));
        rows.AddChild(SliderRow("회복 기술", 10, 90, 5, Main.AutoHuntSettings.HealPercent, value => $"{value}%",
            value => Main.SetAutoHuntSettings(Main.AutoHuntSettings with { HealPercent = value })));

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

    private static Control Titled(string title, Control below)
    {
        VBoxContainer column = new();
        column.AddChild(new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center });
        column.AddChild(below);

        return column;
    }
}
