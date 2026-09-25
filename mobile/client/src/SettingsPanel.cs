using Godot;

namespace LodClient;

/// <summary>
/// 설정 창. 자동 포션 줄 둘 — 체력·마력이 몇 % 이하일 때 마시나를 돌림판으로 고른다 — 그리고
/// 자동 로그인 끄기 단추 하나. 무엇을 마실지와 켜고 끄기는 게임 화면의 포션 단추에서 한다(<see cref="PotionChip"/>).
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

    private static Control Titled(string title, Control below)
    {
        VBoxContainer column = new();
        column.AddChild(new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center });
        column.AddChild(below);

        return column;
    }
}
