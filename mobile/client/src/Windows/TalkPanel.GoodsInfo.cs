using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 상점 물건 정보(사용자 2026-10-05) — 줄 끝 [정보]를 누르면 그 줄 아래에 수치가 펼쳐지고, 같은 자리에 입은 것이 있으면
/// 소지품 정보 상자처럼 ▲▼ 로 견준다. 수치는 서버가 상점 목록 뒤에 붙여 보낸 것(옛 서버면 [정보]가 없다).
/// </summary>
public sealed partial class TalkPanel
{
    /// <summary>지금 입은 것 — 상점 물건과 견줄 때 읽는다(GameScreen 이 넣는다).</summary>
    public System.Func<IReadOnlyList<WornItem>> WornNow { get; set; } = () => [];

    private void AddGoodsInfo(Control row, DialogueGoods goods)
    {
        if (goods.Stats is null)
        {
            return;
        }

        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color("#0f0f0f");
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(Greybox.Round);
        plate.SetContentMarginAll(6);
        PanelContainer box = new() { Visible = false };
        box.AddThemeStyleboxOverride("panel", plate);
        Label line = new();
        line.AddThemeColorOverride("font_color", Greybox.Muted);
        line.AddThemeFontSizeOverride("font_size", Greybox.SmallText);
        GridContainer table = new();
        Label notes = new();
        notes.AddThemeColorOverride("font_color", Greybox.Text);
        notes.AddThemeFontSizeOverride("font_size", Greybox.SmallText);
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(line);
        column.AddChild(table);
        column.AddChild(notes);
        box.AddChild(column);

        Button info = new() { Text = "정보", CustomMinimumSize = new Vector2(52, Main.TouchMinimum), FocusMode = FocusModeEnum.None };
        Greybox.Plain(info);
        info.AddThemeFontSizeOverride("font_size", 13);
        info.Pressed += () =>
        {
            if (box.Visible)
            {
                box.Visible = false;
                return;
            }

            // 펼칠 때마다 다시 견준다 — 그새 갈아입었을 수 있다.
            WornItem? instead = ItemActions.WornInstead(goods.Stats, WornNow());
            line.Text = instead is not null ? $"{instead.Called} 착용 중 — ▲ 나음 · ▼ 못함" : string.Empty;
            line.Visible = line.Text.Length > 0;
            WindowFrame.ShowStats(table, notes, ItemActions.Stats(goods.Stats, instead?.Stats));
            box.Visible = true;
        };
        info.SetMeta("goods", goods.Name);
        row.AddChild(info);
        // 줄 바로 아래에 — 거르개가 줄을 숨기면 같이 숨는다.
        row.VisibilityChanged += () => box.Visible &= row.Visible;
        _offers.AddChild(box);
        _offers.MoveChild(box, row.GetIndex() + 1);
    }

    /// <summary>손 없이 — 그 물건 줄의 [정보]를 누른다(<c>--shop-info</c>, 사진용).</summary>
    public void PressInfo(string name)
    {
        foreach (Node found in _offers.FindChildren("*", "Button", true, false))
        {
            if (found is Button info && info.HasMeta("goods") && info.GetMeta("goods").AsString() == name)
            {
                info.EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }
        }
    }
}
