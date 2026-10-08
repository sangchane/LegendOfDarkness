using System;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace LodClient;

/// <summary>경매장 창의 줄 — 찾기(눌러 고름) · 올리기(가방, 눌러 고름) · 내 경매([취소]) · 받을 것([받기]).</summary>
public sealed partial class AuctionPanel
{
    /// <summary>받을 것의 금화 줄 그림 — 위 줄 「경매장」 단추와 같은 금화 무더기.</summary>
    private const int GoldIcon = 32910;

    private AuctionRow? _pick;
    private InventoryItem? _item;
    private string _bagSeen = string.Empty;
    private double _bagIn;

    /// <summary>줄을 모두 치운다. 눌러 고르는 줄의 묶음도 새로 만든다.</summary>
    private void Clear()
    {
        foreach (Node old in _rows.GetChildren())
        {
            _rows.RemoveChild(old);
            old.QueueFree();
        }

        _acts.Clear();
        Names.Clear();
        RowCount = null;
        _group = new ButtonGroup { AllowUnpress = true };
    }

    /// <summary>줄 자리에 한 줄 안내("…" · 빈 상태).</summary>
    private void Fill(string text)
    {
        Clear();
        Label label = Words(text, Greybox.Muted);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(0, Main.TouchMinimum);
        _rows.AddChild(label);
    }

    /// <summary>줄을 다 늘어놓은 뒤 — 하나도 없으면 빈 상태 문구.</summary>
    private void Filled()
    {
        int count = Names.Count;
        if (count == 0)
        {
            Fill(Empties[_tab]);
        }

        RowCount = count;
        Lock();
    }

    /// <summary>찾기 줄 — 눌러 고르면 아래(가로는 옆)에 입찰 줄이 선다.</summary>
    private Control Found(AuctionRow row)
    {
        Button button = Selectable(Look(row.Image, Title(row.Name, row.Stacks), Detail(row)));
        button.Pressed += () => PickRow(button.ButtonPressed ? row : null);
        return button;
    }

    /// <summary>내 경매 줄 — 내가 올린 것에는 [취소]. 입찰이 있으면 현재가의 5% 수수료가 든다.</summary>
    private Control Mine(AuctionRow row)
    {
        HBoxContainer line = Look(row.Image, Title(row.Name, row.Stacks), Detail(row));
        if ((row.Flags & 1) != 0)
        {
            _ownShown = row;
            line.AddChild(RowButton("취소", () => AskCancel(row)));
        }

        return Plate(line);
    }

    /// <summary>받을 것 줄 — 물건은 그림·이름, 금화는 「금화 N전」. 둘째 줄은 까닭.</summary>
    private Control ClaimRow(AuctionClaim claim)
    {
        bool gold = claim.Kind == 1;
        HBoxContainer line = Look(
            gold ? GoldIcon : claim.Image,
            gold ? $"금화 {claim.Gold:N0}전" : Title(claim.Name, claim.Stacks),
            Reasons[Math.Min((int)claim.Reason, Reasons.Length - 1)]);
        line.AddChild(RowButton("받기", () => Act(server => server.AuctionTakeAsync(claim.Id, CancellationToken.None))));

        return Plate(line);
    }

    /// <summary>올리기 탭 — 가방을 늘어놓는다. 고른 물건이 아직 그대로 있으면 고름을 이어 간다.</summary>
    private void ShowBag()
    {
        InventoryItem[] bag = [.. Bag?.Invoke() ?? []];
        InventoryItem? kept = _item;
        _item = null;
        _bagSeen = Signature(bag);
        Clear();

        foreach (InventoryItem item in bag)
        {
            Names.Add(item.Name);
            Button button = Selectable(Look(item.Icon, Title(item.Name, item.Stacks), string.Empty));
            button.Pressed += () => PickItem(button.ButtonPressed ? item : null);
            _rows.AddChild(button);

            if (kept is not null && item.Slot == kept.Slot && item.Name == kept.Name && item.Stacks == kept.Stacks)
            {
                button.SetPressedNoSignal(true);
                _item = item;
            }
        }

        Filled();
        Side();
    }

    /// <summary>올리기 탭이 보이는 동안 0.5초마다 가방이 바뀌었나 본다 — 자동 줍기·포션으로 칸이 바뀌면 고른 칸이 엉뚱한 물건을 가리키지 않게.</summary>
    private void PollBag(double delta)
    {
        if (_tab != 1 || !Visible || Bag is null || (_bagIn -= delta) > 0)
        {
            return;
        }

        _bagIn = 0.5;
        if (Signature(Bag()) != _bagSeen)
        {
            ShowBag();
        }
    }

    private static string Signature(System.Collections.Generic.IEnumerable<InventoryItem> bag) =>
        string.Join('|', bag.Select(item => $"{item.Slot}:{item.Name}:{item.Stacks}"));

    private static string Title(string name, int stacks) => stacks > 1 ? $"{name} ×{stacks}" : name;

    private static string Detail(AuctionRow row)
    {
        string mark = (row.Flags & 1) != 0 ? (row.Flags & 4) != 0 ? " · 내 물건 · 입찰 있음" : " · 내 물건"
            : (row.Flags & 2) != 0 ? " · 최고 입찰" : string.Empty;
        string buyout = row.Buyout > 0 ? $"{row.Buyout:N0}" : "—";

        return $"{Bands[Math.Min((int)row.Band, Bands.Length - 1)]} · 현 {row.Price:N0} · 즉 {buyout}{mark}";
    }

    /// <summary>줄의 모양 — 그림 · 이름 · 둘째 줄(작고 흐리게). 손을 받지 않는다(받는 쪽은 줄을 감싼 단추).</summary>
    private static HBoxContainer Look(int image, string title, string detail)
    {
        HBoxContainer line = new() { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        line.AddThemeConstantOverride("separation", Main.Gutter);
        line.AddChild(new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = ItemIcons.For(image),
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TextureFilter = TextureFilterEnum.Nearest
        });

        VBoxContainer words = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        words.AddThemeConstantOverride("separation", 0);
        Label name = Words(title, Greybox.Text);
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        words.AddChild(name);

        if (detail.Length > 0)
        {
            Label more = Words(detail, Greybox.Muted);
            more.AddThemeFontSizeOverride("font_size", 13);
            more.ClipText = true;
            more.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            words.AddChild(more);
        }

        line.AddChild(words);
        return line;
    }

    /// <summary>손을 받는 줄이 없는 줄(내 경매 · 받을 것)의 어두운 판.</summary>
    private static Control Plate(Control line)
    {
        PanelContainer plate = new() { CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        plate.AddThemeStyleboxOverride("panel", Greybox.Rounded(Greybox.Surface()));
        plate.AddChild(line);

        return plate;
    }

    /// <summary>눌러 고르는 줄 — 하나만 고르고, 고른 줄은 테두리가 밝다. 한 번 더 누르면 고름을 푼다.</summary>
    private Button Selectable(Control look)
    {
        Button button = new()
        {
            ToggleMode = true,
            ButtonGroup = _group,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        StyleBoxFlat lit = Greybox.Rounded(Greybox.Surface());
        lit.BorderColor = Greybox.Muted;
        foreach (string state in new[] { "normal", "hover", "focus" })
        {
            button.AddThemeStyleboxOverride(state, Greybox.Rounded(Greybox.Surface()));
        }

        foreach (string state in new[] { "pressed", "hover_pressed" })
        {
            button.AddThemeStyleboxOverride(state, lit);
        }

        // 단추는 속을 배치해 주지 않는다 — 줄 모양을 단추 크기에 맞춰 편다.
        look.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        look.OffsetLeft = Main.Gutter / 2;
        look.OffsetRight = -Main.Gutter / 2;
        button.AddChild(look);

        return button;
    }

    /// <summary>줄 끝의 동작 단추 — 답을 기다리는 동안 잠긴다.</summary>
    private Button RowButton(string text, Action press)
    {
        Button button = Small(text);
        button.CustomMinimumSize = new Vector2(64, 40);
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.Disabled = _busy || _asking;
        button.Pressed += press;
        _acts.Add(button);

        return button;
    }

    private readonly CenterContainer _confirm = new() { Visible = false, MouseFilter = MouseFilterEnum.Stop };
    private readonly Label _confirmText = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        CustomMinimumSize = new Vector2(240, 0)
    };

    private uint _cancelling;
    private AuctionRow? _ownShown;

    /// <summary>손 없이 확인할 때(<c>--auction-confirm</c>) — 늘어선 내 경매 하나의 [취소]를 누른 셈. 내 것이 없으면 false.</summary>
    public bool RehearseCancel()
    {
        if (_ownShown is not { } row)
        {
            return false;
        }

        AskCancel(row);
        return true;
    }

    /// <summary>
    /// [취소] 를 누르면 먼저 묻는 판(사용자 2026-10-07) — 입찰이 있으면 현재가의 5% 수수료가 들고, 없어도 보증금은 돌아오지 않는다.
    /// 툴팁은 터치에서 뜨지 않아 판으로 묻는다. 모양은 소지품 「버리기」 판과 같다.
    /// </summary>
    private void BuildConfirm()
    {
        Button yes = Small("취소하기");
        Greybox.Commit(yes);
        Button no = Small("그만두기");
        yes.CustomMinimumSize = no.CustomMinimumSize = new Vector2(96, Tall);
        yes.Pressed += () =>
        {
            _confirm.Visible = false;
            uint id = _cancelling;
            Act(server => server.AuctionCancelAsync(id, CancellationToken.None));
        };
        no.Pressed += () => _confirm.Visible = false;

        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color("#0f0f0f");
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(Greybox.RoundPlate);
        plate.SetContentMarginAll(Main.Gutter);
        PanelContainer ask = new();
        ask.AddThemeStyleboxOverride("panel", plate);
        _confirmText.AddThemeColorOverride("font_color", Greybox.Title);

        HBoxContainer answers = new() { Alignment = BoxContainer.AlignmentMode.Center };
        answers.AddThemeConstantOverride("separation", Main.Gutter);
        answers.AddChild(yes);
        answers.AddChild(no);
        VBoxContainer asking = new();
        asking.AddThemeConstantOverride("separation", Main.Gutter);
        asking.AddChild(_confirmText);
        asking.AddChild(answers);
        ask.AddChild(asking);
        _confirm.AddChild(ask);
        AddChild(_confirm);
    }

    private void AskCancel(AuctionRow row)
    {
        _cancelling = row.Id;
        long fee = (row.Flags & 4) != 0 ? (long)row.Price * 5 / 100 : 0;
        _confirmText.Text = fee > 0
            ? $"{row.Name} 경매를 취소할까요?\n입찰이 있어 수수료 {fee:N0}전(현재가의 5%)을 내고, 보증금은 돌려받지 못합니다."
            : $"{row.Name} 경매를 취소할까요?\n보증금은 돌려받지 못합니다.";
        _confirm.Visible = true;
    }
}
