using Godot;

namespace LodClient;

/// <summary>
/// A 10~90%, 10-step gauge bar for the auto-potion threshold — replaces the 1~99 select box for the health and
/// mana potions (사용자 요청, 2026-09-26: "포션 먹는 거 게이지? 바라고 하나? 10단위에서 인식 잘 되는 방식"). The
/// heal-skill percent stays a <see cref="PercentSelect"/> (1~99, exact) — only the two potions read at this
/// coarser, easier-to-drag step. The fill colour is the same one the HUD's own bar uses
/// (<see cref="Greybox.Health"/>/<see cref="Greybox.Mana"/>), and the marks under it name every step, 10 to 90.
/// </summary>
/// <remarks>
/// If the saved value is not a multiple of ten (an old 1~99 pick, e.g. 65), the handle and the big number beside
/// it show the nearest ten without changing what is saved — only an actual drag calls <see cref="Changed"/> and
/// moves the saved value to a step of ten.
/// </remarks>
public sealed partial class PotionGauge : HBoxContainer
{
    private const int Minimum = 10;
    private const int Maximum = 90;
    private const int Step = 10;

    private readonly HSlider _slider = new();
    private readonly Label _value;

    public PotionGauge(int stored, Color fill)
    {
        AddThemeConstantOverride("separation", Main.Gutter);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        int shown = Snap(stored);

        _slider.MinValue = Minimum;
        _slider.MaxValue = Maximum;
        _slider.Step = Step;
        _slider.Value = shown;
        _slider.TickCount = (Maximum - Minimum) / Step + 1;
        _slider.TicksOnBorders = true;
        _slider.CustomMinimumSize = new Vector2(120, Main.TouchMinimum);
        _slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // 테마의 기본 홈은 어두운 속에 묻혀 채워진 쪽만 보였다 — 홈 전체를 칸 색으로, 채운 쪽을 이 포션의 색으로
        // (SettingsPanel.SliderRow 와 같은 결).
        StyleBoxFlat groove = Greybox.Surface();
        groove.ContentMarginTop = 3;
        groove.ContentMarginBottom = 3;
        StyleBoxFlat filled = Greybox.Fill(fill);
        filled.ContentMarginTop = 3;
        filled.ContentMarginBottom = 3;
        _slider.AddThemeStyleboxOverride("slider", groove);
        _slider.AddThemeStyleboxOverride("grabber_area", filled);
        _slider.AddThemeStyleboxOverride("grabber_area_highlight", filled);

        HBoxContainer ticks = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (int percent = Minimum; percent <= Maximum; percent += Step)
        {
            Label tick = new()
            {
                Text = percent.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            tick.AddThemeFontSizeOverride("font_size", 9);
            tick.AddThemeColorOverride("font_color", Greybox.Muted);
            ticks.AddChild(tick);
        }

        VBoxContainer column = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(_slider);
        column.AddChild(ticks);

        _value = new Label
        {
            Text = $"{shown}%",
            CustomMinimumSize = new Vector2(56, Main.TouchMinimum),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _value.AddThemeFontSizeOverride("font_size", 20);
        _value.AddThemeColorOverride("font_color", Greybox.Text);

        _slider.ValueChanged += now =>
        {
            int picked = (int)now;
            _value.Text = $"{picked}%";
            Changed?.Invoke(picked);
        };

        AddChild(column);
        AddChild(_value);
    }

    /// <summary>The bar was dragged to a new step-of-ten value.</summary>
    public event System.Action<int>? Changed;

    private static int Snap(int raw) => Mathf.Clamp(Mathf.RoundToInt(raw / (float)Step) * Step, Minimum, Maximum);
}
