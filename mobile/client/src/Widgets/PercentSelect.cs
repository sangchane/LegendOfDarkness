using System.Linq;
using Godot;

namespace LodClient;

/// <summary>
/// A select box — a percent 1~99 by default, or any list of names (<see cref="Of"/> — 봇 저주 고르기, 2026-10-03) — press it and a scrollable list drops down to pick from
/// (사용자 요청, 2026-09-26: 돌림판 대신 눌러서 뜨는 목록으로, 5~95 5칸 대신 1~99 전부).
/// </summary>
/// <remarks>
/// Same shape as the skill-slot picker (<see cref="AbilityBar"/>) — a <see cref="PopupPanel"/> holding a
/// scrolling list of <see cref="Greybox.Plain"/> rows, no stone (docs/original-ui-451.md: "돌을 안 쓰는 곳 — 목록").
/// <see cref="TouchInput"/> already lets a drag started on a row scroll the list underneath it, for any
/// <see cref="ScrollContainer"/> in the tree, so this needs no touch handling of its own.
/// </remarks>
public sealed partial class PercentSelect : Button
{
    private const int MinVisibleRows = 3;
    private const int MaxVisibleRows = 6;

    private readonly PopupPanel _picker = new();
    private readonly ScrollContainer _scroll = new();
    private readonly Button[] _rows;
    private readonly string[] _labels;
    private readonly int _first;
    private readonly int _width;
    private readonly Control _bounds;

    private int _value;

    /// <summary>
    /// <paramref name="bounds"/> is the window this select box lives in (the settings panel) — the dropped-down
    /// list is kept inside its edges rather than the whole screen's (사용자 신고, 2026-09-26: 목록이 설정 창 밖까지
    /// 덮었다).
    /// </summary>
    public PercentSelect(int value, Control bounds)
        : this([.. System.Linq.Enumerable.Range(1, 99).Select(percent => $"{percent}%")], first: 1, value, bounds, width: 96)
    {
    }

    /// <summary>이름 목록에서 하나 — 값은 고른 줄의 번호(0부터).</summary>
    public static PercentSelect Of(string[] labels, int index, Control bounds, int width) => new(labels, 0, index, bounds, width);

    private PercentSelect(string[] labels, int first, int value, Control bounds, int width)
    {
        _labels = labels;
        _first = first;
        _width = width;
        _bounds = bounds;
        _rows = new Button[labels.Length];
        _value = Mathf.Clamp(value, first, first + labels.Length - 1);
        Text = _labels[_value - _first];
        CustomMinimumSize = new Vector2(width, Main.TouchMinimum);
        Greybox.Plain(this);

        VBoxContainer list = new();
        list.AddThemeConstantOverride("separation", Main.Gutter / 2);

        for (int at = 0; at < labels.Length; at++)
        {
            int picked = first + at;
            Button row = new()
            {
                Text = labels[at],
                Alignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(width, Main.TouchMinimum)
            };
            Greybox.Plain(row);
            row.Pressed += () => Pick(picked);

            _rows[at] = row;
            list.AddChild(row);
        }

        // 실제 높이는 열 때마다(Open) 창 안 남은 자리를 보고 정한다.
        _scroll.AddChild(list);
        _picker.AddChild(_scroll);
        AddChild(_picker);

        Pressed += Open;
    }

    /// <summary>A new value was picked from the list.</summary>
    public event System.Action<int>? Changed;

    /// <summary>
    /// Opens the list — also called by <c>--percent-open</c> to check it without a hand. Kept inside
    /// <see cref="_bounds"/> (the settings window), not the screen: opens upward when there is not enough room
    /// below, and its height fits whatever room is left in that direction (at least <see cref="MinVisibleRows"/>
    /// rows).
    /// </summary>
    public async void Open()
    {
        Rect2 at = GetGlobalRect();
        Rect2 bounds = _bounds.GetGlobalRect();

        int rowStride = Main.TouchMinimum + Main.Gutter / 2;
        int spaceBelow = (int)(bounds.End.Y - at.End.Y) - Main.Gutter;
        int spaceAbove = (int)(at.Position.Y - bounds.Position.Y) - Main.Gutter;

        bool below = spaceBelow >= rowStride * MinVisibleRows || spaceBelow >= spaceAbove;
        int rows = Mathf.Clamp((below ? spaceBelow : spaceAbove) / rowStride, MinVisibleRows, MaxVisibleRows);
        _scroll.CustomMinimumSize = new Vector2(_width, Mathf.Min(rows, _labels.Length) * rowStride);

        _picker.Popup(new Rect2I(0, 0, 0, 0));

        Vector2I size = _picker.Size;

        int x = Mathf.Clamp((int)at.Position.X,
            (int)bounds.Position.X + Main.Gutter,
            Mathf.Max((int)bounds.Position.X + Main.Gutter, (int)bounds.End.X - size.X - Main.Gutter));

        int y = below
            ? Mathf.Min((int)at.End.Y + Main.Gutter / 2, (int)bounds.End.Y - size.Y - Main.Gutter)
            : Mathf.Max((int)bounds.Position.Y + Main.Gutter, (int)at.Position.Y - Main.Gutter / 2 - size.Y);

        _picker.Position = new Vector2I(x, y);

        // 목록의 줄 하나하나가 제자리를 잡는 데(VBoxContainer 정렬) 한 프레임 걸린다 — 그 뒤에야
        // 지금 값의 줄이 정확한 자리에 있어, 열자마자 굴려도 엉뚱한 데로 간다(--percent-open 로 확인, 2026-09-26).
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (IsInstanceValid(this) && _picker.Visible)
        {
            _scroll.EnsureControlVisible(_rows[_value - _first]);
        }
    }

    private void Pick(int value)
    {
        _value = value;
        Text = _labels[value - _first];
        _picker.Hide();
        Changed?.Invoke(value);
    }
}
