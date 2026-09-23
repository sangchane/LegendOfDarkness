using Godot;

namespace LodClient;

/// <summary>
/// 설정 창. 지금은 자동 포션의 줄 둘뿐이다 — 체력·마력이 몇 % 이하일 때 마시나를 돌림판으로 고른다.
/// 무엇을 마실지와 켜고 끄기는 게임 화면의 포션 단추에서 한다(<see cref="PotionChip"/>).
/// </summary>
public sealed partial class SettingsPanel : PanelContainer
{
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
        wheels.AddThemeConstantOverride("separation", Main.Gutter * 3);

        PercentWheel health = new(Main.HealthPotion.Percent);
        health.Changed += percent => Main.SetPotions(Main.HealthPotion with { Percent = percent }, Main.ManaPotion);
        wheels.AddChild(Titled("체력 포션", health));

        PercentWheel mana = new(Main.ManaPotion.Percent);
        mana.Changed += percent => Main.SetPotions(Main.HealthPotion, Main.ManaPotion with { Percent = percent });
        wheels.AddChild(Titled("마력 포션", mana));

        inside.AddChild(head);
        inside.AddChild(new Label { Text = "이하가 되면 저절로 마신다", HorizontalAlignment = HorizontalAlignment.Center });
        inside.AddChild(wheels);

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
