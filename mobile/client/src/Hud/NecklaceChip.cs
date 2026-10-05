using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 방향판 위 속성 목걸이 칸 하나(사용자, 2026-10-05) — 포션 칸(<see cref="PotionChip" />)과 같은 결. 짧게 누르면 고른 목걸이를
/// 끼고(가방 물건 사용 0x1C), 길게 누르면 그 속성 목걸이 중에서 고른다. 묶음 칸(세로의 수→토→풍→화, 암흑↔생명)은 누를 때마다
/// 다음 차례를 낀다(<see cref="NecklaceSwap.Next(IReadOnlyList{Element}, IReadOnlyList{WornItem}, System.Func{Element, bool})" />).
/// 지금 끼고 있는 것은 자동 공격처럼 둘레를 도는 빛(<see cref="AutoHuntRing" />) — 회색 칸·금테는 배경에서 튀었다(사용자 2026-10-05).
/// 묶음 칸은 묶음 안의 것을 끼고 있으면 그것을 보여 준다.
/// </summary>
public partial class NecklaceChip : Button
{
    private const ulong HoldMilliseconds = 400;

    private readonly IReadOnlyList<Element> _group;
    private readonly System.Func<IReadOnlyList<InventoryItem>> _pack;
    private readonly System.Func<IReadOnlyList<WornItem>> _worn;
    private readonly System.Action<int> _use;
    private readonly System.Action<string> _say;
    private readonly Label _letter = new();
    private readonly AutoHuntRing _ring = new();
    private readonly PopupPanel _picker = new();

    private ulong _downAt;
    private bool _down;
    private bool _held;
    private (string? Name, Element Kind, int? Slot, bool On)? _shown;

    /// <param name="group">속성 하나면 그 속성 칸, 여럿이면 차례로 도는 칸(수·토·풍·화 / 암흑·생명).</param>
    public NecklaceChip(
        IReadOnlyList<Element> group,
        System.Func<IReadOnlyList<InventoryItem>> pack,
        System.Func<IReadOnlyList<WornItem>> worn,
        System.Action<int> use,
        System.Action<string> say)
    {
        _group = group;
        _pack = pack;
        _worn = worn;
        _use = use;
        _say = say;
        CustomMinimumSize = new Vector2(AbilityFan.PotionSide, AbilityFan.PotionSide);
        ExpandIcon = true;
        IconAlignment = HorizontalAlignment.Center;
        FocusMode = FocusModeEnum.None;
        // 포션 칸처럼 어두운 반투명 원 — 바탕 회색 칸은 길바닥 위에서 튀었다.
        StyleBoxFlat circle = new() { BgColor = new Color(0, 0, 0, 0.35f) };
        circle.SetCornerRadiusAll(AbilityFan.PotionSide / 2);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            AddThemeStyleboxOverride(state, circle);
        }

        _letter.AddThemeFontSizeOverride("font_size", 10);
        _letter.AddThemeColorOverride("font_outline_color", Colors.Black);
        _letter.AddThemeConstantOverride("outline_size", 4);
        _letter.MouseFilter = MouseFilterEnum.Ignore;
        _letter.SetAnchorsPreset(LayoutPreset.FullRect);
        _letter.OffsetLeft = 2;
        _letter.OffsetTop = -2;
        AddChild(_letter);

        _ring.SetAnchorsPreset(LayoutPreset.FullRect);
        _ring.Show(false, false);
        AddChild(_ring);
        AddChild(_picker);
    }

    private bool Single => _group.Count == 1;

    /// <summary>누르면 낄 속성 — 묶음 칸은 지금 낀 것 다음 차례 중 가방에 있는 것.</summary>
    private Element Kind => Single ? _group[0] : NecklaceSwap.Next(_group, _worn(), element => NecklaceSwap.SlotOf(_pack(), TargetOf(element)) is not null);

    private string? TargetOf(Element element) => NecklaceSwap.Chosen(Main.Necklace(element), _pack(), _worn(), element);

    private string? Target => TargetOf(Kind);

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            return;
        }

        if (button.Pressed)
        {
            _down = true;
            _held = false;
            _downAt = Time.GetTicksMsec();
        }
        else if (_down)
        {
            _down = false;

            if (!_held)
            {
                Wear();
            }
        }

        AcceptEvent();
    }

    public override void _Process(double delta)
    {
        if (_down && !_held && Time.GetTicksMsec() - _downAt >= HoldMilliseconds)
        {
            _held = true;
            Pick();
        }

        WornItem? on = NecklaceSwap.Worn(_worn());
        Element current = NecklaceSwap.Current(_worn());
        // 묶음 칸은 묶음 안의 것을 끼고 있으면 그것을, 아니면 누르면 낄 것을 보여 준다.
        bool wearingGroup = !Single && _group.Contains(current);
        Element kind = wearingGroup ? current : Kind;
        string? name = wearingGroup ? on!.Name : TargetOf(kind);
        bool lit = wearingGroup || (on is not null && on.Name == name);
        (string?, Element, int?, bool) now = (name, kind, NecklaceSwap.SlotOf(_pack(), name), lit);

        if (_shown != now)
        {
            _shown = now;
            Show(now);
        }
    }

    /// <summary>고른 것이 가방에도 몸에도 없으면 흐리게, 지금 끼고 있으면 밝은 테.</summary>
    private void Show((string? Name, Element Kind, int? Slot, bool On) look)
    {
        int icon = NecklaceSwap.Choices(_pack(), _worn(), [look.Kind]).FirstOrDefault(one => one.Name == look.Name).Icon;
        Icon = icon > 0 ? ItemIcons.For(icon) : null;
        _letter.Text = NecklaceSwap.Letter(look.Kind);
        Modulate = look.Slot is not null || look.On ? Colors.White : new Color(1, 1, 1, 0.45f);
        _ring.Show(look.On, false);
        TooltipText = look.Name ?? $"{NecklaceSwap.Letter(look.Kind)} 목걸이 없음";
    }

    private void Wear()
    {
        string? name = Target;
        WornItem? on = NecklaceSwap.Worn(_worn());

        if (name is null)
        {
            _say($"{NecklaceSwap.Letter(Kind)} 속성 목걸이가 없습니다.");
        }
        else if (on?.Name == name)
        {
            _say($"이미 {name}을(를) 끼고 있습니다.");
        }
        else if (NecklaceSwap.SlotOf(_pack(), name) is { } slot)
        {
            _use(slot);
        }
        else
        {
            _say($"가방에 {name}이(가) 없습니다.");
        }
    }

    /// <summary>그 속성(전환 칸은 암흑·생명 둘)의 목걸이를 한 줄로 — 고르면 닫힌다. 포션 칸의 고르기와 같은 모양.</summary>
    private void Pick()
    {
        foreach (Node old in _picker.GetChildren())
        {
            _picker.RemoveChild(old);
            old.QueueFree();
        }

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);
        IReadOnlyList<Element> kinds = _group;

        foreach ((string name, int icon, Element element) in NecklaceSwap.Choices(_pack(), _worn(), kinds))
        {
            Button choice = new()
            {
                Icon = ItemIcons.For(icon),
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
                Text = NecklaceSwap.Letter(element),
                TooltipText = name,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum + 16),
            };

            Greybox.Plain(choice);
            choice.AddThemeFontSizeOverride("font_size", 11);

            if (name == NecklaceSwap.Chosen(Main.Necklace(element), _pack(), _worn(), element))
            {
                choice.AddThemeStyleboxOverride("normal", Greybox.Lit());
                choice.AddThemeColorOverride("font_color", Greybox.Engrave);
            }

            choice.Pressed += () =>
            {
                Main.SetNecklace(element, name);
                _shown = null;
                _picker.Hide();
            };

            row.AddChild(choice);
        }

        if (row.GetChildCount() == 0)
        {
            row.AddChild(new Label { Text = "가진 목걸이가 없습니다" });
        }

        _picker.AddChild(row);
        Rect2 at = GetGlobalRect();
        _picker.Popup(new Rect2I((int)at.Position.X, (int)at.Position.Y, 0, 0));

        // 방향판 위라 칸 위로 편다. 화면 안으로 당긴다.
        Vector2 screen = GetViewportRect().Size;
        Vector2I size = _picker.Size;
        int x = Mathf.Clamp((int)at.Position.X, Main.Gutter, Mathf.Max(Main.Gutter, (int)screen.X - size.X - Main.Gutter));
        int y = Mathf.Max(Main.Gutter, (int)at.Position.Y - size.Y - Main.Gutter / 2);
        _picker.Position = new Vector2I(x, y);
    }
}
