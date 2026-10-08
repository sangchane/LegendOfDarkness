using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// An NPC's window: who is speaking, what they say, and whatever they offer under it — choices, goods, the pack's
/// slots to sell, things to learn, a line to type. Picking one answers the NPC; the server replies with the next
/// window or shuts this one, so nothing here decides what happens next.
/// </summary>
/// <remarks>
/// It lies where the pack does (the wireframes' modal: under the world in portrait, a column in landscape). A window
/// is only ever replaced whole, so it is rebuilt from nothing each time one arrives.
/// </remarks>
public sealed partial class TalkPanel : PanelContainer
{
    private static readonly Vector2 Row = new(0, Main.TouchMinimum);

    private readonly Label _who = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _words = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _offers = new();

    // 물건이 많은 상점은 화면을 넘는다 — 이 안에서 굴린다. 글을 칠 때는 입력 줄이 늘 보이게 따라간다.
    private readonly ScrollContainer _scroll = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        FollowFocus = true
    };

    // 글 입력 창일 때의 입력 줄(칸 + 확인), 그리고 키보드 때문에 창을 들어 올린 만큼.
    private Control? _typingRow;
    private readonly HBoxContainer _head = new();
    private float _lift;
    private readonly HBoxContainer _filters = new();
    private readonly VBoxContainer _checkout = new();
    private readonly Label _summary = new();
    private readonly Button _commit = new() { CustomMinimumSize = Row };
    private readonly Button _back = new() { Text = "첫 메뉴", CustomMinimumSize = Row };
    private readonly Button _all = new() { Text = "전량 판매", TooltipText = "고른 물건을 가진 만큼 다 채운 뒤 [선택 판매]로 확정합니다.", CustomMinimumSize = Row };
    private readonly List<(string Name, int Slot, uint Price, SpinBox Count)> _lines = [];
    private Dialogue? _shop;
    private long _gold;
    private bool _waiting;
    private double _waitSeconds;
    private OptionButton? _classFilter, _genderFilter, _circleFilter;

    // 갈래 탭(사용자 2026-10-08 「탭도 안나눠져 있는거 같거든」) — 제목 줄 아래 옆으로 미는 한 줄. 「전체」는 두지 않고 갑옷이 먼저
    // 열린다(사용자 「탭에 전체는 두지말고 갑옷을 디폴트로해」), 갑옷이 없는 상점은 첫 갈래. 「」면 거르지 않는다(갈래가 하나뿐).
    private readonly HBoxContainer _kinds = new();
    private readonly ScrollContainer _kindScroll = new() { VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = false };
    private string _kind = "";
    private readonly List<(Control Row, DialogueGoods Goods)> _goodsRows = [];

    public event System.Action<uint, bool, IReadOnlyList<(string Name, int Slot, int Quantity)>>? Traded;
    public event System.Action<uint>? MenuRequested;

    public void TradeFailed(string message)
    {
        _waiting = false;
        _words.Text = message;
        UpdateSummary();
    }


    // --talk-input: 손 없이 확인할 때 가짜 "글 입력" 창을 스스로 띄운다(서버의 NPC 없이 키보드 자리를 찍으려고).
    private double _rehearseAfter = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--talk-input") >= 0 ? 1.0 : -1;

    public TalkPanel()
    {
        Name = "Talk";
        Visible = false;
        // 틀은 원작 돌, 속은 평평한 어둠 — 무늬 위에 작은 글자를 얹으면 먼저 무너진다(data/ui-vault).
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        HBoxContainer head = _head;
        head.AddThemeConstantOverride("separation", Main.Gutter);
        _who.ClipText = true;
        _who.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _who.CustomMinimumSize = new Vector2(40, 0);
        _who.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        head.AddChild(_filters);
        head.AddChild(_who);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        head.AddChild(Close);

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        _offers.AddThemeConstantOverride("separation", Main.Gutter / 2);
        inside.AddChild(_words);
        inside.AddChild(_offers);

        // 물건이 많은 상점은 화면을 넘는다. 넘치는 것은 스크롤로 두고 이름과 닫기는 늘 남긴다.
        ScrollContainer scroll = _scroll;
        scroll.AddChild(inside);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter);
        // 용문양은 뺀다 — 거르개 셋·상인 이름만으로 세로 360 이 꽉 차, 넣으면 [닫기] 가 화면 밖으로 밀렸다(2026-09-30).
        body.AddChild(Greybox.Header(head, emblem: false));
        _kinds.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _kindScroll.AddChild(_kinds);
        body.AddChild(_kindScroll);
        _filters.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _filters.AddThemeConstantOverride("separation", Main.Gutter / 2);
        body.AddChild(scroll);
        _checkout.AddThemeConstantOverride("separation", Main.Gutter);
        HBoxContainer checkoutButtons = new();
        checkoutButtons.AddThemeConstantOverride("separation", Main.Gutter);
        foreach (Button button in new[] { _back, _all, _commit })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            checkoutButtons.AddChild(button);
        }
        Greybox.Plain(_back);
        Greybox.Plain(_all);
        Greybox.Commit(_commit);
        _checkout.AddChild(_summary);
        _checkout.AddChild(checkoutButtons);
        body.AddChild(_checkout);
        _checkout.Visible = false;
        _filters.Visible = false;
        _back.Pressed += () =>
        {
            if (_shop is { } shop) MenuRequested?.Invoke(shop.Serial);
        };
        _all.Pressed += () =>
        {
            // 고른 줄만 가진 만큼 — 모든 줄을 채워 가방이 통째로 비었다(사용자 2026-10-05).
            var chosen = _lines.Where(line => line.Count.Value > 0).ToList();
            foreach (var line in chosen) line.Count.Value = line.Count.MaxValue;
            _words.Text = chosen.Count == 0 ? "먼저 물건을 고르세요." : $"고른 물건을 모두 채웠습니다. [{_commit.Text}]로 확정하세요.";
        };
        _commit.Pressed += () =>
        {
            if (_shop is not { } shop || _waiting) return;
            var selected = _lines.Where(line => line.Count.Value > 0)
                .Select(line => (line.Name, line.Slot, Quantity: (int)line.Count.Value)).ToArray();
            if (selected.Length == 0) return;
            _waiting = true;
            _waitSeconds = 0;
            UpdateSummary();
            Traded?.Invoke(shop.Serial, shop.Kind == DialogueKind.PackSlots, selected);
        };

        PanelContainer within = new();
        within.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        within.AddChild(body);

        AddChild(within);
    }

    /// <summary>The button that shuts the window, so whoever opened it can decide what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody picked an answer: which NPC, the number it goes back with, and any words that go with it.</summary>
    public event System.Action<uint, ushort, string?>? Answered;

    /// <summary>Shows one window. The pack is needed to put names and pictures to the slots a shop asks about.</summary>
    public void Show(Dialogue talk, IReadOnlyList<InventoryItem> pack, long gold = 0)
    {
        // 이식한 NPC 는 이름에 자리가 붙어 온다(카르마@노비스마을식당#3,10). 화면에는 이름만.
        _who.Text = talk.Who.Split('@')[0];
        _words.Text = talk.What;
        _words.Visible = true;

        foreach (Node old in _offers.GetChildren())
        {
            _offers.RemoveChild(old);
            old.QueueFree();
        }

        _typingRow = null;
        _gold = gold;
        _waiting = false;
        _lines.Clear();
        _goodsRows.Clear();
        foreach (Node old in _filters.GetChildren())
        {
            _filters.RemoveChild(old);
            old.QueueFree();
        }
        foreach (Node old in _kinds.GetChildren())
        {
            _kinds.RemoveChild(old);
            old.QueueFree();
        }
        _kind = "";
        _kindScroll.Visible = false;
        _shop = IsShop(talk) ? talk : null;
        _checkout.Visible = _shop is not null;
        _filters.Visible = _shop?.Kind == DialogueKind.Goods && !Bank;
        _all.Visible = _shop?.Kind == DialogueKind.PackSlots || Bank;
        if (_shop is not null)
        {
            BuildShop(talk, pack);
            return;
        }


        switch (talk.Kind)
        {
            case DialogueKind.Options or DialogueKind.OptionsWithArgs:
                string? handBack = talk.Kind == DialogueKind.OptionsWithArgs ? talk.Args : null;

                foreach (DialogueOption option in talk.Options)
                {
                    Offer(option.Text, null, () => Answered?.Invoke(talk.Serial, option.Step, handBack));
                }

                break;

            case DialogueKind.Goods:
                foreach (DialogueGoods goods in talk.Goods)
                {
                    Offer($"{goods.Name}  {goods.Price}", ItemIcons.For(goods.Icon),
                        () => Answered?.Invoke(talk.Serial, talk.Step, goods.Name));
                }

                break;

            case DialogueKind.PackSlots:
                foreach (InventoryItem item in pack.Where(item => talk.Slots.Contains(item.Slot)))
                {
                    Offer(item.Name, ItemIcons.For(item.Icon),
                        () => Answered?.Invoke(talk.Serial, talk.Step, item.Slot.ToString()));
                }

                break;

            case DialogueKind.Skills or DialogueKind.Spells:
                string sheet = talk.Kind == DialogueKind.Skills ? AbilityBar.SkillSheet : AbilityBar.SpellSheet;

                foreach (DialogueAbility ability in talk.Abilities)
                {
                    Offer(ability.Name, AbilityBar.Frame(sheet, ability.Icon),
                        () => Answered?.Invoke(talk.Serial, talk.Step, ability.Name));
                }

                break;

            case DialogueKind.TextInput:
                // 칸과 [확인]을 한 줄에 — 키보드가 올라와도 둘이 함께 키보드 바로 위에 선다. 엔터도 [확인]이다.
                LineEdit typed = new() { CustomMinimumSize = Row, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                typed.TextSubmitted += words => Answered?.Invoke(talk.Serial, talk.Step, words);

                Button confirm = new() { Text = "확인", CustomMinimumSize = new Vector2(Main.TouchMinimum * 2, Main.TouchMinimum) };
                confirm.Pressed += () => Answered?.Invoke(talk.Serial, talk.Step, typed.Text);

                HBoxContainer row = new();
                row.AddThemeConstantOverride("separation", Main.Gutter);
                row.AddChild(typed);
                row.AddChild(confirm);
                _offers.AddChild(row);

                TouchInput.Zone(typed, row);
                _typingRow = row;

                break;
        }
    }

    // shop1/shop2 use these steps; the bank (Banker.cs) lists what it holds at 0x000A and our pack at 0x0800, and is
    // answered through the same bulk trade (0xF2): buying lines take out, selling lines put in.
    private static bool IsShop(Dialogue talk) =>
        talk.Kind == DialogueKind.Goods && talk.Step is 4 or BankHeld
        || talk.Kind == DialogueKind.PackSlots && talk.Step is 0x0500 or BankPack;

    private const ushort BankHeld = 0x000A, BankPack = 0x0800;

    private bool Bank => _shop?.Step is BankHeld or BankPack;

    private void BuildShop(Dialogue talk, IReadOnlyList<InventoryItem> pack)
    {
        bool selling = talk.Kind == DialogueKind.PackSlots;
        _commit.Text = Bank ? (selling ? "선택 맡기기" : "선택 찾기") : selling ? "선택 판매" : "선택 구매";
        _all.Text = Bank ? (selling ? "전량 맡기기" : "전량 찾기") : "전량 판매";
        if (Bank && !selling)
        {
            // 맡긴 목록 — Price 는 맡긴 개수다. 값·거르개·수치 없이 상점 줄과 같은 모양으로.
            foreach (DialogueGoods goods in talk.Goods)
            {
                int held = (int)System.Math.Min(65535, System.Math.Max(1, goods.Price));
                AddTradeRow(goods.Name, goods.Icon, 0, 0, held, $"맡긴 {held:N0}개");
            }
        }
        else if (selling)
        {
            foreach (InventoryItem item in pack.Where(item => talk.Slots.Contains(item.Slot)))
                AddTradeRow(item.Name, item.Icon, 0, item.Slot, System.Math.Min(65535, System.Math.Max(1, item.Stacks)));
        }
        else
        {
            BuildFilters(talk.Goods);
            foreach (DialogueGoods goods in talk.Goods)
            {
                Control row = AddTradeRow(goods.Name, goods.Icon, goods.Price, 0, 65535);
                AddGoodsInfo(row, goods);
                _goodsRows.Add((row, goods));
            }

            // 첫 갈래(갑옷)만 보이게 열린다.
            if (_kind.Length > 0)
            {
                ApplyFilters();
            }
        }
        if (_lines.Count == 0) _offers.AddChild(new Label { Text = Bank ? (selling ? "맡길 수 있는 물건이 없습니다." : "맡긴 물건이 없습니다.") : selling ? "팔 수 있는 물건이 없습니다." : "판매 중인 물건이 없습니다." });
        UpdateSummary();
    }

    private Control AddTradeRow(string name, int icon, uint price, int slot, int max, string? note = null)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);
        Button select = new()
        {
            ToggleMode = true, CustomMinimumSize = new Vector2(104, Main.TouchMinimum),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = name
        };
        StyleBoxFlat selected = Greybox.Rounded(Greybox.Surface());
        selected.BorderColor = Greybox.Muted;
        select.AddThemeStyleboxOverride("pressed", selected);
        select.AddThemeStyleboxOverride("hover_pressed", selected);
        HBoxContainer content = new() { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", Main.Gutter);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // 그림·글자가 줄 테두리에 붙지 않게.
        content.OffsetLeft = Greybox.Inset;
        content.OffsetRight = -Greybox.Inset;
        select.AddChild(content);
        row.AddChild(select);
        content.AddChild(new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = ItemIcons.For(icon), CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter, TextureFilter = TextureFilterEnum.Nearest
        });
        VBoxContainer words = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        words.AddChild(new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Text = name, TooltipText = name, ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(64, 0)
        });
        Label detail = new() { MouseFilter = MouseFilterEnum.Ignore, Text = note ?? (slot > 0 ? $"보유 {max:N0}개" : $"{price:N0}전 / 개") };
        detail.AddThemeColorOverride("font_color", Greybox.Muted);
        detail.AddThemeFontSizeOverride("font_size", 13);
        words.AddChild(detail);
        content.AddChild(words);
        SpinBox count = new() { MinValue = 0, MaxValue = max, Step = 1, Value = 0, Alignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(96, Main.TouchMinimum) };
        count.GetLineEdit().VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number;
        count.GetLineEdit().FocusEntered += () => _typingRow = row;
        TouchInput.Zone(count.GetLineEdit(), row);
        select.Pressed += () => count.Value = select.ButtonPressed ? System.Math.Max(1, count.Value) : 0;
        count.ValueChanged += _ =>
        {
            select.SetPressedNoSignal(count.Value > 0);
            UpdateSummary();
        };
        row.AddChild(count);
        _lines.Add((name, slot, price, count));
        _offers.AddChild(row);
        return row;
    }

    private void BuildFilters(IReadOnlyList<DialogueGoods> goods)
    {
        _classFilter = Filter("직업 전체");
        foreach (string job in goods.Select(one => one.Class).Where(one => one.Length > 0 && one != "Peasant").Distinct())
        {
            _classFilter.AddItem(job switch { "Warrior" => "전사", "Rogue" => "도적", "Wizard" => "마법사", "Priest" => "사제", "Monk" => "무도가", _ => job });
            _classFilter.SetItemMetadata(_classFilter.ItemCount - 1, job);
        }
        _genderFilter = Filter("성별 전체");
        _genderFilter.AddItem("남", 1);
        _genderFilter.AddItem("여", 2);
        _circleFilter = Filter("서클 전체");
        foreach (byte circle in goods.Select(one => one.Circle).Where(one => one > 0).Distinct().Order())
            _circleFilter.AddItem($"{circle}서클", circle);
        foreach (OptionButton filter in new[] { _classFilter, _genderFilter, _circleFilter })
            filter.ItemSelected += _ => ApplyFilters();

        // 갈래가 둘 이상일 때만 탭 줄 — 무기상은 무기 하나라 숨고 거르지 않는다.
        IReadOnlyList<string> kinds = ShopKinds.In(goods.Select(one => one.Stats));
        ButtonGroup tabs = new();
        string first = kinds.Count > 1 ? (kinds.Contains("갑옷") ? "갑옷" : kinds[0]) : "";
        foreach (string kind in kinds.Count > 1 ? kinds : [])
        {
            Button tab = new() { Text = kind, ToggleMode = true, ButtonGroup = tabs, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(Main.TouchMinimum, 44) };
            Greybox.Tab(tab);
            tab.AddThemeFontSizeOverride("font_size", 13);
            tab.Pressed += () =>
            {
                _kind = kind;
                ApplyFilters();
            };
            tab.ButtonPressed = kind == first;
            _kinds.AddChild(tab);
        }

        _kind = first;
        _kindScroll.Visible = kinds.Count > 1;
    }

    private OptionButton Filter(string all)
    {
        OptionButton filter = new()
        {
            CustomMinimumSize = new Vector2(64, Main.TouchMinimum),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FitToLongestItem = false,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = all
        };
        filter.AddThemeFontSizeOverride("font_size", 14);
        filter.AddItem(all, 0);
        string label = all.Replace(" 전체", "");
        filter.Text = label;
        filter.ItemSelected += index =>
        {
            if (index == 0) filter.Text = label;
            filter.TooltipText = filter.GetItemText((int)index);
        };
        _filters.AddChild(filter);
        return filter;
    }

    private void ApplyFilters()
    {
        foreach (OptionButton filter in new[] { _classFilter!, _genderFilter!, _circleFilter! })
            filter.Text = filter.Selected == 0
                ? filter.GetItemText(0).Replace(" 전체", "")
                : filter.GetItemText(filter.Selected);
        string job = _classFilter!.Selected == 0 ? "" : _classFilter.GetItemMetadata(_classFilter.Selected).AsString();
        int gender = _genderFilter!.GetSelectedId(), circle = _circleFilter!.GetSelectedId();
        foreach (var (row, goods) in _goodsRows)
            row.Visible = (job.Length == 0 || goods.Class.Length == 0 || goods.Class == "Peasant" || goods.Class == job)
                && (gender == 0 || goods.Gender is 0 or 255 || goods.Gender == gender)
                && (circle == 0 || goods.Circle == 0 || goods.Circle == circle)
                && (_kind.Length == 0 || ShopKinds.Of(goods.Stats) == _kind);
        // Hidden rows retain their selection; the summary explicitly counts those too.
        UpdateSummary();
    }

    /// <summary>Runs the actual selection/filter/commit controls in an offline --shop-check rehearsal.</summary>
    public void CheckShop()
    {
        void Require(bool value, string message)
        {
            if (!value) throw new System.InvalidOperationException(message);
        }
        Require(_commit.Disabled && _lines.All(line => line.Count.Value == 0), "Initial quantities must be zero");
        // 아래 거르개 검사는 모든 줄을 본다 — 갈래 탭(기본 갑옷)을 풀고 시작한다.
        if (_kind.Length > 0)
        {
            _kind = "";
            ApplyFilters();
        }

        bool selling = _shop?.Kind == DialogueKind.PackSlots;
        if (selling)
        {
            Require(_all.Visible, "Sell all must be available");
            _all.EmitSignal(BaseButton.SignalName.Pressed);
            Require(_lines.All(line => line.Count.Value == 0), "Sell all must leave unchosen lines alone");
            _lines[0].Count.Value = 1;
            _all.EmitSignal(BaseButton.SignalName.Pressed);
            Require(_lines[0].Count.Value == _lines[0].Count.MaxValue && _lines.Skip(1).All(line => line.Count.Value == 0),
                "Sell all must fill only the chosen line");
        }
        else
        {
            Button select = (Button)_goodsRows[0].Row.GetChild(0);
            select.ButtonPressed = true;
            select.EmitSignal(BaseButton.SignalName.Pressed);
            Require(_lines[0].Count.Value == 1, "Selecting goods must start at one");
            select.ButtonPressed = false;
            select.EmitSignal(BaseButton.SignalName.Pressed);
            Require(_lines[0].Count.Value == 0, "Deselecting goods must return to zero");
            _lines[0].Count.Value = 3;
            _lines[1].Count.Value = 2;
            Require(_summary.Text.Contains("2종 · 5개 · 2,350전"), "Order total must include both lines");
            _classFilter!.Select(2);
            ApplyFilters();
            Require(!_goodsRows[1].Row.Visible && _goodsRows[2].Row.Visible && _goodsRows[0].Row.Visible,
                "Job filter must include common items and selected job");
            _genderFilter!.Select(1);
            ApplyFilters();
            Require(!_goodsRows[2].Row.Visible, "Gender filter must hide female goods");
            _classFilter.Select(0);
            _genderFilter.Select(0);
            _circleFilter!.Select(1);
            ApplyFilters();
            Require(_goodsRows[0].Row.Visible && _goodsRows[1].Row.Visible, "Circle filter must retain common goods");
            _circleFilter.Select(0);
            long originalGold = _gold;
            _gold = 2000;
            UpdateSummary();
            Require(_commit.Disabled, "Insufficient gold must prevent purchase");
            _gold = originalGold;
            ApplyFilters();
        }
        int events = 0;
        System.Action<uint, bool, IReadOnlyList<(string Name, int Slot, int Quantity)>> receive = (merchant, sale, lines) =>
        {
            events++;
            Require(merchant == 1 && sale == selling && lines.Count == 2, "Wrong bulk order");
            Require(lines[0].Quantity == (selling ? 12 : 3), "Wrong item quantity");
        };
        Traded += receive;
        _commit.EmitSignal(BaseButton.SignalName.Pressed);
        _commit.EmitSignal(BaseButton.SignalName.Pressed);
        Traded -= receive;
        Require(events == 1 && _commit.Disabled, "Pending order must prevent duplicate submission");
        TradeFailed("화면 검증용 상점 — 선택 상태 확인");
        Require(!_commit.Disabled, "Failed send must allow recovery");
        GD.Print("GREYBOX_SHOP_CHECK_OK " + (selling ? "sell" : "buy"));
    }

    private void UpdateSummary()
    {
        bool selling = _shop?.Kind == DialogueKind.PackSlots;
        int kinds = _lines.Count(line => line.Count.Value > 0);
        long count = _lines.Sum(line => (long)line.Count.Value);
        long total = _lines.Sum(line => (long)line.Price * (int)line.Count.Value);
        _summary.Text = _waiting ? "거래 결과를 기다리는 중…"
            : Bank ? $"{kinds}종 · {count:N0}개 {(selling ? "맡기기" : "찾기")}"
            : selling ? $"{kinds}종 · {count:N0}개 판매 (값은 상인이 정합니다)"
            : $"{kinds}종 · {count:N0}개 · {total:N0}전 / 보유 {_gold:N0}전";
        int hidden = _goodsRows.Count(one => !one.Row.Visible && one.Row.GetChildren().OfType<SpinBox>().Any(count => count.Value > 0));
        if (!_waiting && hidden > 0) _summary.Text += $" (필터 밖 {hidden}종 포함)";
        if (kinds > 128) _summary.Text += " (한 번에 128종까지)";
        _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _commit.Disabled = _waiting || kinds == 0 || kinds > 128 || (!selling && !Bank && total > _gold);
        _back.Disabled = _waiting;
        _all.Disabled = _waiting;
        foreach (var line in _lines) line.Count.Editable = !_waiting;
    }

    /// <summary>
    /// While a line is being typed, the window's bottom rides just above the keyboard and its list scrolls so the input
    /// row stays in sight; the world behind is not moved. Before, the window kept its full height and the input row
    /// sat under the keyboard.
    /// </summary>
    /// <remarks>The holder belongs to GameScreen and nothing else lifts it; putting it back when the keyboard goes is ours.</remarks>
    public override void _Process(double delta)
    {
        RehearseTextInput(delta);
        if (_waiting && (_waitSeconds += delta) > 10)
        {
            foreach (var line in _lines) line.Count.Value = 0;
            TradeFailed("상점 응답이 늦습니다. 첫 메뉴로 돌아가 결과를 확인하세요.");
        }

        if (!Visible || GetParent() is not Control holder)
        {
            return;
        }

        float bottom = holder.GetGlobalRect().End.Y - holder.OffsetBottom;
        float keyboardTop = GetViewportRect().Size.Y - TouchInput.Covered;
        float lift = Mathf.Max(0, bottom - keyboardTop);

        if (!Mathf.IsEqualApprox(lift, _lift))
        {
            _lift = lift;
            holder.OffsetBottom = -lift;
        }

        bool typing = lift > 0 && _typingRow is { } row && IsInstanceValid(row)
                      && TouchInput.Editing(GetViewport()) is { } field && row.IsAncestorOf(field);

        // 가로 폰은 키보드 위에 100 쯤 남는다 — 이름·닫기 줄까지 두면 입력 줄이 반쯤 잘린다. 그때만 그 줄을 접는다.
        // 지금 접혀 있는지와 상관없이 "탭 줄을 둔다면 굴림 칸에 얼마가 남나"로 정해야 켜졌다 꺼졌다 하지 않는다.
        if (_shop is not null)
        {
            _filters.Visible = _shop.Kind == DialogueKind.Goods && !Bank && !typing;
            _kindScroll.Visible = _filters.Visible && _kinds.GetChildCount() > 1;
            _checkout.Visible = !typing;
            _words.Visible = !typing;
        }
        Control header = (Control)_head.GetParent();
        float heads = header.GetCombinedMinimumSize().Y + Main.Gutter;
        float chrome = GetCombinedMinimumSize().Y + (header.Visible ? 0 : heads);
        float left = bottom - lift - holder.GetGlobalRect().Position.Y - chrome;
        header.Visible = !typing || left >= Row.Y + Main.Gutter;

        if (typing)
        {
            _scroll.EnsureControlVisible(_typingRow!);
        }
    }

    /// <summary>Only when checking without a hand (<c>--talk-input</c>): shows a made-up window that asks for a line.</summary>
    private void RehearseTextInput(double delta)
    {
        if (_rehearseAfter < 0 || (_rehearseAfter -= delta) > 0)
        {
            return;
        }

        _rehearseAfter = -1;
        Show(new Dialogue(0, "카르마@노비스마을식당#3,10", "무엇을 찾으시오? 이름을 적어 주시오.\n\n(글 입력 창을 손 없이 확인하는 가짜 창)")
        {
            Kind = DialogueKind.TextInput
        }, []);
        Visible = true;
    }

    private void Offer(string text, Texture2D? icon, System.Action pressed)
    {
        Button button = new()
        {
            Text = text,
            Icon = icon,
            ExpandIcon = false,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = Row,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        button.Pressed += pressed;
        _offers.AddChild(button);
    }
}
