using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// One automatic-potion switch on the game screen: the chosen potion's picture with the line written small on it.
/// Tap turns it on or off; press and hold opens a row of the potions of its kind to pick another. The line itself
/// is moved in the settings window (<see cref="SettingsPanel"/>) — 사용자, 2026-09-23.
/// </summary>
public partial class PotionChip : Button
{
    private const ulong HoldMilliseconds = 400;

    private readonly Potion[] _choices;
    private readonly System.Func<PotionRule> _read;
    private readonly System.Action<PotionRule> _write;
    private readonly System.Func<IReadOnlyList<InventoryItem>> _pack;
    private readonly Label _line = new();
    private readonly PopupPanel _picker = new();

    private ulong _downAt;
    private bool _down;
    private bool _held;
    private (PotionRule Rule, int Count)? _shown;

    public PotionChip(
        Potion[] choices,
        System.Func<PotionRule> read,
        System.Action<PotionRule> write,
        System.Func<IReadOnlyList<InventoryItem>> pack)
    {
        _choices = choices;
        _read = read;
        _write = write;
        _pack = pack;
        CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum);
        ExpandIcon = true;
        IconAlignment = HorizontalAlignment.Center;
        Greybox.Plain(this);

        // 줄은 작게, 그림 아래 구석에 — 그림이 무엇을 마시는지 말하고 숫자는 거든다.
        _line.AddThemeFontSizeOverride("font_size", 11);
        _line.AddThemeColorOverride("font_outline_color", Colors.Black);
        _line.AddThemeConstantOverride("outline_size", 4);
        _line.HorizontalAlignment = HorizontalAlignment.Right;
        _line.VerticalAlignment = VerticalAlignment.Bottom;
        _line.MouseFilter = MouseFilterEnum.Ignore;
        _line.SetAnchorsPreset(LayoutPreset.FullRect);
        _line.OffsetRight = -3;
        _line.OffsetBottom = -1;
        AddChild(_line);
        AddChild(_picker);
    }

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
                PotionRule now = _read();
                _write(now with { Enabled = !now.Enabled });
            }
        }

        AcceptEvent();
    }

    private int _rehearsed;

    public override void _Process(double delta)
    {
        // --pick-potion: 월드가 자리를 잡은 뒤 한 번 길게 누른 셈 친다(체력 단추만).
        if (Main.PickingPotion && _choices == AutoPotion.Healing && ++_rehearsed == 240)
        {
            Pick();
        }

        if (_down && !_held && Time.GetTicksMsec() - _downAt >= HoldMilliseconds)
        {
            _held = true;
            Pick();
        }

        PotionRule rule = _read();
        int count = AutoPotion.Count(_pack(), rule.Potion);

        if (_shown != (rule, count))
        {
            _shown = (rule, count);
            Show(rule, count);
        }
    }

    /// <summary>Off, or none left: the picture greys. The word or the number says which — colour is not the only sign.</summary>
    private void Show(PotionRule rule, int count)
    {
        Icon = ItemIcons.For(IconOf(rule.Potion));
        Modulate = rule.Enabled && count > 0 ? Colors.White : new Color(1, 1, 1, 0.45f);
        _line.Text = rule.Enabled ? $"{rule.Percent}%" : "끔";
        TooltipText = $"{rule.Potion} {count}개";
    }

    /// <summary>A row of every potion of this kind, each with how many are carried. Picking one closes it.</summary>
    private void Pick()
    {
        foreach (Node old in _picker.GetChildren())
        {
            _picker.RemoveChild(old);
            old.QueueFree();
        }

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        foreach (Potion potion in _choices)
        {
            int count = AutoPotion.Count(_pack(), potion.Name);

            Button choice = new()
            {
                Icon = ItemIcons.For(potion.Icon),
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
                Text = count.ToString(),
                TooltipText = potion.Name,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum + 16),
                Modulate = count > 0 ? Colors.White : new Color(1, 1, 1, 0.45f),
            };

            Greybox.Plain(choice);
            choice.AddThemeFontSizeOverride("font_size", 11);

            if (potion.Name == _read().Potion)
            {
                choice.AddThemeStyleboxOverride("normal", Greybox.Lit());
            }

            string name = potion.Name;
            choice.Pressed += () =>
            {
                _write(_read() with { Potion = name });
                _picker.Hide();
            };

            row.AddChild(choice);
        }

        _picker.AddChild(row);
        Rect2 at = GetGlobalRect();
        _picker.Popup(new Rect2I((int)at.Position.X, (int)at.End.Y + Main.Gutter / 2, 0, 0));

        // 단추가 오른쪽 아래 부채꼴 맨 위로 옮겨 가(GameScreen) 단추 왼쪽 끝에서 펴면 줄이 화면 오른쪽 밖으로 나갔다 —
        // 화면 안으로 당긴다. 아래로 넘치면 단추 위로 편다.
        Vector2 screen = GetViewportRect().Size;
        Vector2I size = _picker.Size;
        int x = Mathf.Clamp((int)at.Position.X, Main.Gutter, Mathf.Max(Main.Gutter, (int)screen.X - size.X - Main.Gutter));
        int y = (int)at.End.Y + Main.Gutter / 2;

        if (y + size.Y > screen.Y - Main.Gutter)
        {
            y = (int)at.Position.Y - size.Y - Main.Gutter / 2;
        }

        _picker.Position = new Vector2I(x, y);
    }

    private int IconOf(string name)
    {
        foreach (Potion potion in _choices)
        {
            if (potion.Name == name)
            {
                return potion.Icon;
            }
        }

        return _choices[0].Icon;
    }
}
