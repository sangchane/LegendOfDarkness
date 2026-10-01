using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 장비창 — 위 메뉴 [장비] 로 연다(사용자, 2026-10-01: 소지품 창의 탭으로는 세로가 모자라 따로 뺐다). 창은 원작 장비 그림
/// 그 자체이고 틀·제목 줄·여백이 없다(<see cref="GearGrid" />). 그림 위 한 줄에 그림에 칸이 없는 다섯 자리(이름 달린)와 닫기 X.
/// </summary>
/// <remarks>
/// 칸을 누르면 그 위에 이름·한 줄·[벗기]가 뜨고, 빠르게 두 번 누르면 바로 벗는다 — 소지품 창의 동작 줄과 같은 규칙.
/// </remarks>
public sealed partial class GearPanel : PanelContainer
{
    private readonly GearGrid _gear = new();

    private readonly PanelContainer _action = new() { Name = "GearAction", TopLevel = true, Visible = false, ZIndex = 5 };
    private readonly Label _actionName = new();
    private readonly Label _actionLine = new();
    private readonly Button _off = WindowFrame.IconButton(GlyphKind.TakeOff, "벗기", width: 56);
    private readonly DoubleTap _taps = new();

    // 고른 자리(서버 번호), 없으면 0. 무엇을 그렸는지 — 같으면 다시 그리지 않는다.
    private int _chosen;
    private string? _showing;

    public GearPanel()
    {
        Name = "GearWindow";
        Visible = false;

        // 그림 밖은 위 한 줄뿐 — 그 뒤만 불투명 어둠으로 채우고 여백은 두지 않는다.
        StyleBoxFlat sheet = Greybox.Sheet();
        sheet.SetContentMarginAll(0);
        sheet.ContentMarginTop = Main.Gutter / 2;
        sheet.SetBorderWidthAll(0);
        AddThemeStyleboxOverride("panel", sheet);

        Close = WindowFrame.CloseButton();
        Close.CustomMinimumSize = new Vector2(GearGrid.SpareSide, GearGrid.SpareSide);
        _gear.Append(Close);
        _gear.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        _gear.Chosen += slot =>
        {
            if (_taps.Tap(slot, Time.GetTicksMsec() / 1000.0))
            {
                _action.Visible = false;
                TakenOff?.Invoke(slot);
                return;
            }

            _chosen = slot;
            _showing = null;
        };

        _off.Pressed += () =>
        {
            if (_chosen > 0)
            {
                TakenOff?.Invoke(_chosen);
                _action.Visible = false;
            }
        };

        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color("#0f0f0f");
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(10);
        plate.SetContentMarginAll(6);
        _action.AddThemeStyleboxOverride("panel", plate);
        _actionName.AddThemeColorOverride("font_color", Greybox.Text);
        _actionName.AddThemeFontSizeOverride("font_size", 13);
        _actionLine.AddThemeColorOverride("font_color", Greybox.Muted);
        _actionLine.AddThemeFontSizeOverride("font_size", 11);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(_actionName);
        column.AddChild(_actionLine);
        column.AddChild(_off);
        _action.AddChild(column);

        AddChild(_gear);
        Control actionLayer = new() { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(actionLayer);
        actionLayer.AddChild(_action);
    }

    /// <summary>The X above the picture, so whoever opened the window can decide what shutting it means.</summary>
    public Button Close { get; }

    /// <summary>Somebody asked to take off what is in one worn place. The number is the server's own.</summary>
    public event System.Action<int>? TakenOff;

    /// <summary>Shows what is worn and our fighting figures; the squares are redrawn only when what is worn or picked changes.</summary>
    public void Show(IReadOnlyList<WornItem> worn, Vitals? mine, Vector2 room)
    {
        _gear.ShowFigures(mine);
        _gear.Lay(_gear.ScaleThatFits(room));

        string wanted = $"{_chosen}|" + string.Join(";", worn.Select(gear => $"{gear.Slot}:{gear.Icon}"));

        if (wanted == _showing)
        {
            return;
        }

        _showing = wanted;
        _gear.Show(worn, -_chosen);

        WornItem? picked = worn.FirstOrDefault(one => one.Slot == _chosen);
        _action.Visible = picked is not null;

        if (picked is not null)
        {
            // 걸친 것은 바로 버릴 수 없다 — 벗어서 소지품에 든 다음에야.
            _actionName.Text = picked.Called;
            _actionLine.Text = ItemActions.Line(picked);
            _action.ResetSize();
        }
    }

    /// <summary>Picks the first worn thing and presses 벗기 — only for a run with no hand on it, through the same events.</summary>
    public bool PressFirst()
    {
        if (_gear.Cells.FirstOrDefault() is not Button cell)
        {
            return false;
        }

        cell.EmitSignal(BaseButton.SignalName.Pressed);
        _off.EmitSignal(BaseButton.SignalName.Pressed);

        return true;
    }

    public override void _Process(double delta)
    {
        if (!_action.Visible)
        {
            return;
        }

        if (_gear.FindChild($"Slot{_chosen}", true, false) is not Control cell || !cell.IsVisibleInTree())
        {
            _action.Visible = false;
            return;
        }

        // 고른 칸 바로 위에, 자리가 없으면 아래에 — 창 좌우 안에서.
        Rect2 window = GetGlobalRect();
        Rect2 at = cell.GetGlobalRect();
        Vector2 size = _action.GetCombinedMinimumSize();
        float x = Mathf.Clamp(at.GetCenter().X - (size.X / 2), window.Position.X, Mathf.Max(window.Position.X, window.End.X - size.X));
        float above = at.Position.Y - size.Y - 4;

        _action.Size = size;
        _action.GlobalPosition = new Vector2(x, above >= window.Position.Y ? above : at.End.Y + 4);
    }
}
