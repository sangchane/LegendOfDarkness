using Godot;

namespace LodClient;

/// <summary>
/// A select box for a percent, 1~99 in steps of one — press it and a scrollable list drops down to pick from
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
    private const int Minimum = 1;
    private const int Maximum = 99;
    private const int MaxVisibleRows = 6;

    private readonly PopupPanel _picker = new();
    private readonly ScrollContainer _scroll = new();
    private readonly Button[] _rows = new Button[Maximum - Minimum + 1];

    private int _value;

    public PercentSelect(int value)
    {
        _value = Mathf.Clamp(value, Minimum, Maximum);
        Text = $"{_value}%";
        CustomMinimumSize = new Vector2(96, Main.TouchMinimum);
        Greybox.Plain(this);

        VBoxContainer list = new();
        list.AddThemeConstantOverride("separation", Main.Gutter / 2);

        for (int percent = Minimum; percent <= Maximum; percent++)
        {
            int picked = percent;
            Button row = new()
            {
                Text = $"{percent}%",
                Alignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(96, Main.TouchMinimum)
            };
            Greybox.Plain(row);
            row.Pressed += () => Pick(picked);

            _rows[percent - Minimum] = row;
            list.AddChild(row);
        }

        _scroll.CustomMinimumSize = new Vector2(96, MaxVisibleRows * (Main.TouchMinimum + Main.Gutter / 2));
        _scroll.AddChild(list);
        _picker.AddChild(_scroll);
        AddChild(_picker);

        Pressed += Open;
    }

    /// <summary>A new value was picked from the list.</summary>
    public event System.Action<int>? Changed;

    /// <summary>Opens the list — also called by <c>--percent-open</c> to check it without a hand.</summary>
    public async void Open()
    {
        _picker.Popup(new Rect2I(0, 0, 0, 0));

        // 화면 밖으로 넘치지 않게, 단추 바로 아래에.
        Rect2 at = GetGlobalRect();
        Vector2 screen = GetViewportRect().Size;
        Vector2I size = _picker.Size;

        int x = Mathf.Clamp((int)at.Position.X, Main.Gutter, Mathf.Max(Main.Gutter, (int)screen.X - size.X - Main.Gutter));
        int y = Mathf.Clamp((int)at.End.Y + Main.Gutter / 2, Main.Gutter, Mathf.Max(Main.Gutter, (int)screen.Y - size.Y - Main.Gutter));

        _picker.Position = new Vector2I(x, y);

        // 목록의 줄 하나하나가 제자리를 잡는 데(VBoxContainer 정렬) 한 프레임 걸린다 — 그 뒤에야
        // 지금 값의 줄이 정확한 자리에 있어, 열자마자 굴려도 엉뚱한 데로 간다(--percent-open 로 확인, 2026-09-26).
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (IsInstanceValid(this) && _picker.Visible)
        {
            _scroll.EnsureControlVisible(_rows[_value - Minimum]);
        }
    }

    private void Pick(int value)
    {
        _value = value;
        Text = $"{value}%";
        _picker.Hide();
        Changed?.Invoke(value);
    }
}
