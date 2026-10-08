using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>소지품 창 — 고른 것 옆의 정보 상자(이름·한 줄·수치·입기/사용·분해·X)와 위 줄 [버리기], 버릴 개수·분해 묻기.</summary>
public sealed partial class PackPanel : PanelContainer
{
    private bool _dropping;

    // 묻는 판이 분해를 묻는 중이면 그때의 칸·이름(아니면 버릴 개수) — 판이 떠 있는 동안 다른 칸을 누르거나 정렬해도 물은 것만 분해한다.
    private (int Slot, string Name)? _breaking;

    // 지금 고른 것 — 위 줄 [버리기]가 버린다.
    private InventoryItem? _held;

    /// <summary>
    /// The row beside a picked thing: its name, a line under it, and the icon buttons that apply. One button does both
    /// carried things — the server's use (0x1C) puts gear on and drinks a potion — so only its word changes.
    /// </summary>
    private void BuildAction()
    {
        BuildActionRow();
        BuildDropToggle();
        BuildDropAsk();
    }

    /// <summary>정보 상자: 판, 그림·이름·한 줄, 오른쪽 위 주 동작과 X, 그 아래 수치.</summary>
    private void BuildActionRow()
    {
        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color("#0f0f0f");
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(Greybox.RoundPlate);
        plate.SetContentMarginAll(6);
        _action.AddThemeStyleboxOverride("panel", plate);

        _actionName.AddThemeColorOverride("font_color", Greybox.Title);
        _actionName.AddThemeFontSizeOverride("font_size", 15);
        _actionName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _actionName.ClipText = true;
        _actionName.CustomMinimumSize = new Vector2(120, 0);
        _actionLine.AddThemeColorOverride("font_color", Greybox.Muted);
        _actionLine.AddThemeFontSizeOverride("font_size", Greybox.SmallText);
        _actionStats.AddThemeColorOverride("font_color", Greybox.Text);
        _actionStats.AddThemeFontSizeOverride("font_size", Greybox.SmallText);

        // 주 동작은 강조색 아이콘 — 창마다 확정은 하나(Greybox.Commit 과 같은 뜻).
        if (_use.GetMeta("glyph").As<Glyph>() is { } lit)
        {
            lit.Paint = Greybox.Accent;
        }

        // 정보 상자 오른쪽 위 X — 장비창과 같다(사용자 2026-10-02).
        _shut.Pressed += () =>
        {
            _chosen = 0;
            _showing = null;
            _action.Visible = false;
        };

        _use.Pressed += () =>
        {
            if (_chosen > 0)
            {
                Used?.Invoke(_chosen);
            }
        };

        // 분해(사용자 2026-10-08 「아이템 버리기와 별개로 와우처럼 분해」) — 장비일 때만, 한 번 묻는다. 버린 것은 주워 오지만 분해한 것은 돌아오지 않는다.
        _break.Pressed += () =>
        {
            if (_held is { } held)
            {
                Break(held);
            }
        };

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        // 누르면 뜨는 정보 상자 — 그림을 크게, 이름 아래에 내구·개수(사용자 2026-10-01, 다른 게임의 말풍선처럼).
        VBoxContainer words = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddChild(_actionName);
        words.AddChild(_actionLine);
        HBoxContainer top = new();
        // 장착·분해·X 셋이 들어도 세로 창(360) 안에 서게 간격은 반(2026-10-08 분해를 더하며).
        top.AddThemeConstantOverride("separation", Main.Gutter / 2);
        top.AddChild(_actionIcon);
        top.AddChild(words);
        // 교체·장착·사용은 오른쪽 위(장비창의 [장착 해제]와 같은 자리) — 아래 단추 줄 몫의 빈 곳이 없어진다(사용자 2026-10-01).
        top.AddChild(_use);
        top.AddChild(_break);
        top.AddChild(_shut);
        column.AddChild(top);
        // 서버가 보낸 수치 — 공격력·방어·능력치·요구 레벨·직업·무게(우리 확장 0x0F 꼬리).
        column.AddChild(_actionTable);
        column.AddChild(_actionStats);
        _action.AddChild(column);
    }

    /// <summary>위 줄 [버리기] — 켜고 끄는 단추. 켜진 동안은 빨갛고, 창이 닫히면 꺼진다.</summary>
    private void BuildDropToggle()
    {
        // 버리기는 위 줄에 켜고 끄는 단추(사용자 2026-10-01) — 켜 둔 동안 누르는 것마다 버린다. 창을 닫으면 꺼진다.
        // 켜진 버리기는 빨갛게 — 판·테두리·아이콘·글자까지, 글자도 「버리는 중」(사용자 2026-10-01: 잘못 버리지 않게).
        Color danger = new("#e05a5a");
        StyleBoxFlat armed = Greybox.Sheet();
        armed.BgColor = new Color("#3a1414");
        armed.BorderColor = danger;
        armed.SetBorderWidthAll(2);
        armed.SetCornerRadiusAll(Greybox.Round);
        armed.SetContentMarginAll(4);
        _drop.AddThemeStyleboxOverride("pressed", armed);
        _drop.AddThemeStyleboxOverride("hover_pressed", armed);
        _drop.Toggled += on =>
        {
            WindowFrame.Relabel(_drop, on ? "버리는 중" : "버리기");

            if (on)
            {
                _drop.GetMeta("glyph").As<Glyph>().Paint = danger;
                _drop.GetMeta("word").As<Label>().AddThemeColorOverride("font_color", danger);
            }

            _dropping = on;
            _chosen = 0;
            _showing = null;
            _action.Visible = false;
            _ask.Visible = false;
        };
        VisibilityChanged += () =>
        {
            if (!Visible)
            {
                _drop.ButtonPressed = false;
            }
        };
    }

    /// <summary>묶음을 버릴 때 몇 개인지 묻는 판(기본은 전부) — 분해할 때는 개수 없이 「분해할까요?」만.</summary>
    private void BuildDropAsk()
    {
        _dropCount.GetLineEdit().VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number;

        _askDrop.Pressed += () =>
        {
            if (_breaking is { } asked)
            {
                if (_lastCarried.Any(item => item.Slot == asked.Slot && item.Name == asked.Name))
                {
                    Disassembled?.Invoke(asked.Slot);
                }
            }
            else if (_held is { } held)
            {
                Dropped?.Invoke(held.Slot, (int)_dropCount.Value);
            }

            _ask.Visible = false;
        };
        _askCancel.Pressed += () => _ask.Visible = false;
        Greybox.Commit(_askDrop);
        Greybox.Plain(_askCancel);

        StyleBoxFlat askPlate = Greybox.Plate();
        askPlate.BgColor = new Color("#0f0f0f");
        askPlate.BorderColor = Greybox.Muted;
        askPlate.SetCornerRadiusAll(Greybox.RoundPlate);
        askPlate.SetContentMarginAll(Main.Gutter);
        _ask.AddThemeStyleboxOverride("panel", askPlate);
        _askName.AddThemeColorOverride("font_color", Greybox.Title);
        _askName.HorizontalAlignment = HorizontalAlignment.Center;
        VBoxContainer asking = new();
        asking.AddThemeConstantOverride("separation", Main.Gutter);
        asking.AddChild(_askName);
        // 숫자 칸은 작게 가운데, 숫자도 가운데.
        _dropCount.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _dropCount.CustomMinimumSize = new Vector2(120, 40);
        _dropCount.Alignment = HorizontalAlignment.Center;
        asking.AddChild(_dropCount);
        HBoxContainer answers = new() { Alignment = BoxContainer.AlignmentMode.Center };
        answers.AddThemeConstantOverride("separation", Main.Gutter);
        answers.AddChild(_askDrop);
        answers.AddChild(_askCancel);
        asking.AddChild(answers);
        _ask.AddChild(asking);
    }

    /// <summary>
    /// Fills the action row for whatever is picked — name, one line, and the buttons that apply — or hides it when
    /// nothing is. Where it stands is worked out every frame (<see cref="PlaceAction" />), once the grid has settled.
    /// </summary>
    private void ShowChosen(IReadOnlyList<InventoryItem> carried)
    {
        InventoryItem? held = carried.FirstOrDefault(item => item.Slot == _chosen);

        if (held is not null)
        {
            _actionName.Text = held.Name;
            _actionIcon.Texture = ItemIcons.For(held.Icon);
            _actionLine.Text = ItemActions.Line(held);
            _actionLine.Visible = _actionLine.Text.Length > 0;
            WornItem? instead = ItemActions.WornInstead(held, Worn);
            WindowFrame.ShowStats(_actionTable, _actionStats, ItemActions.Stats(held, instead?.Stats));

            // 같은 자리에 걸친 것이 있으면 그것과 견준다(▲ 나음 · ▼ 못함).
            if (instead is not null)
            {
                _actionLine.Text = _actionLine.Text.Length > 0 ? $"{_actionLine.Text} · {instead.Called} 착용 중" : $"{instead.Called} 착용 중";
                _actionLine.Visible = true;
            }
            WindowFrame.Relabel(_use, ItemActions.Primary(held, Worn));
            _use.Visible = true;
            _break.Visible = ItemActions.IsGear(held);
            _held = held;
            _action.Visible = !_ask.Visible;
            _action.ResetSize();

            return;
        }

        _action.Visible = false;

        if (!_dropping)
        {
            _held = null;
            _ask.Visible = false;
        }
    }

    /// <summary>Throws away what was pressed while 버리기 is on: one at once, a bundle after asking how many (all, to begin with).</summary>
    private void Throw(InventoryItem item)
    {
        _held = item;

        if (item.Stacks <= 1)
        {
            Dropped?.Invoke(item.Slot, 1);
            return;
        }

        _askName.Text = $"{item.Name} — 몇 개 버릴까요?";
        _askDrop.Text = "버리기";
        _breaking = null;
        _dropCount.Visible = true;
        _dropCount.MaxValue = item.Stacks;
        _dropCount.Value = item.Stacks;
        Ask();
    }

    /// <summary>정보 상자의 [분해] — 「분해할까요?」를 한 번 묻는다(숫자 칸 없이).</summary>
    private void Break(InventoryItem item)
    {
        _askName.Text = $"{item.Name} — 분해할까요?";
        _askDrop.Text = "분해";
        _breaking = (item.Slot, item.Name);
        _dropCount.Visible = false;
        _action.Visible = false;
        Ask();
    }

    /// <summary>묻는 판을 띄운다 — 숫자 칸을 숨기거나 보이면 판 크기가 바뀌므로 가운데에 다시 세운다.</summary>
    private void Ask()
    {
        _ask.Visible = true;
        _ask.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
    }

    /// <summary>
    /// Stands the action row just above the picked cell — below it when there is no room above inside the window — and
    /// keeps it inside the window left and right.
    /// </summary>
    private void PlaceAction()
    {
        if (!_action.Visible)
        {
            return;
        }

        Control? cell = _cellsBySlot.GetValueOrDefault(_chosen);

        if (cell is null || !cell.IsVisibleInTree())
        {
            _action.Visible = false;
            return;
        }

        Rect2 window = GetGlobalRect();
        Rect2 at = cell.GetGlobalRect();
        Vector2 size = _action.GetCombinedMinimumSize();
        float x = Mathf.Clamp(at.GetCenter().X - (size.X / 2), window.Position.X + 4, Mathf.Max(window.Position.X + 4, window.End.X - size.X - 4));
        float above = at.Position.Y - size.Y - 4;
        float y = above >= window.Position.Y + 4 ? above : at.End.Y + 4;

        _action.Size = size;
        float bottom = Mathf.Min(window.End.Y, GetViewportRect().Size.Y - TouchInput.Covered);
        _action.GlobalPosition = new Vector2(x, Mathf.Clamp(y, window.Position.Y + 4, Mathf.Max(window.Position.Y + 4, bottom - size.Y - 4)));
    }
}
