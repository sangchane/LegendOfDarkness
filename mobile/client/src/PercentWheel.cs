using System.Collections.Generic;
using Godot;

namespace LodClient;

/// <summary>
/// A wheel of percentages in steps of five: slide it and the row that stops in the middle band is the value.
/// Three rows show at once, the chosen one in the middle. It settles onto a row by itself once the finger lets go.
/// </summary>
public partial class PercentWheel : ScrollContainer
{
    private const int Row = 40;
    private const int Step = 5;
    private const double SettleSeconds = 0.12;

    private readonly List<Label> _rows = [];
    private int _value;
    private int _lastScroll = -1;
    private double _still;
    private bool _placed;

    public PercentWheel(int value)
    {
        _value = value;
        CustomMinimumSize = new Vector2(96, Row * 3);
        HorizontalScrollMode = ScrollMode.Disabled;
        VerticalScrollMode = ScrollMode.ShowNever;

        VBoxContainer list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 0);

        // 위아래 빈 줄 하나씩 — 첫 값과 끝 값도 가운데 띠에 올 수 있게.
        list.AddChild(new Control { CustomMinimumSize = new Vector2(0, Row), MouseFilter = MouseFilterEnum.Pass });

        for (int percent = Step; percent < 100; percent += Step)
        {
            Label row = new()
            {
                Text = $"{percent}%",
                CustomMinimumSize = new Vector2(0, Row),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Pass,
            };

            _rows.Add(row);
            list.AddChild(row);
        }

        list.AddChild(new Control { CustomMinimumSize = new Vector2(0, Row), MouseFilter = MouseFilterEnum.Pass });
        AddChild(list);
    }

    /// <summary>A new value has settled in the middle band.</summary>
    public event System.Action<int>? Changed;

    public override void _Process(double delta)
    {
        // 목록 높이가 잡히기 전에는 굴려 놓아도 0 으로 되돌아간다 — 잡힌 뒤 한 번 제자리에 놓는다.
        if (!_placed)
        {
            if (GetVScrollBar().MaxValue < Row * (_rows.Count + 1))
            {
                return;
            }

            _placed = true;
            ScrollVertical = IndexOf(_value) * Row;
        }

        if (ScrollVertical != _lastScroll)
        {
            _lastScroll = ScrollVertical;
            _still = 0;
            Light(Mathf.Clamp(Mathf.RoundToInt(ScrollVertical / (float)Row), 0, _rows.Count - 1));
            return;
        }

        if ((_still += delta) < SettleSeconds)
        {
            return;
        }

        int index = Mathf.Clamp(Mathf.RoundToInt(ScrollVertical / (float)Row), 0, _rows.Count - 1);

        if (ScrollVertical != index * Row)
        {
            ScrollVertical = index * Row;
        }

        int value = (index + 1) * Step;

        if (value != _value)
        {
            _value = value;
            Changed?.Invoke(value);
        }
    }

    public override void _Draw() =>
        DrawRect(new Rect2(0, Row, Size.X, Row), Greybox.Accent with { A = 0.25f });

    private static int IndexOf(int value) => Mathf.Clamp(value / Step - 1, 0, 100 / Step - 2);

    private void Light(int index)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            _rows[i].AddThemeColorOverride("font_color", i == index ? Greybox.Text : Greybox.Muted);
        }
    }
}
