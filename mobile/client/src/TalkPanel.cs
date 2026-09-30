using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

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
    private readonly Button _shopBack = WindowFrame.IconButton(GlyphKind.Back, "상점 처음", width: 56);
    private readonly Button _shopAction = new() { CustomMinimumSize = new Vector2(92, Main.TouchMinimum), Visible = false };
    private readonly Button _sellAll = new() { Text = "전량판매", CustomMinimumSize = new Vector2(104, Main.TouchMinimum), Visible = false };
    private readonly HBoxContainer _headActions = new();
    private readonly Label _words = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _offers = new();
    private readonly VBoxContainer _inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly HBoxContainer _shopFilters = new();
    private readonly OptionButton _categoryFilter = new();
    private readonly OptionButton _genderFilter = new();
    private readonly OptionButton _circleFilter = new();
    private readonly VBoxContainer _goodsList = new();
    private readonly Label _goodsEmpty = new() { Text = "조건에 맞는 물건이 없습니다.", Visible = false };
    private readonly Dictionary<string, int> _buyQuantities = [];
    private readonly Dictionary<int, int> _sellQuantities = [];
    private IReadOnlyList<DialogueGoods> _goods = [];
    private Dialogue? _shopTalk;
    private string _shopCategory = "전체";
    private string _shopGender = "전체";
    private int _shopCircle;
    private string? _selectedBuyName;
    private int? _selectedSellSlot;
    private IReadOnlyList<InventoryItem> _sellable = [];
    private Dialogue? _sellTalk;
    private System.Action? _shopActionHandler;
    private bool _shopWindow;
    private bool _filtersInHeader;
    private uint _shopSpeaker;
    private IReadOnlyList<InventoryItem> _lastPack = [];

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
        head.AddChild(_who);

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        _shopBack.Visible = false;
        _shopBack.Pressed += () => ShopBackRequested?.Invoke(_shopSpeaker);
        _headActions.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _headActions.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _headActions.AddChild(_shopAction);
        _headActions.AddChild(_sellAll);
        _headActions.AddChild(_shopBack);
        _headActions.AddChild(Close);
        head.AddChild(_headActions);
        _shopAction.Pressed += () => _shopActionHandler?.Invoke();
        _sellAll.Pressed += SubmitSellAll;
        Greybox.Commit(_shopAction);
        Greybox.Plain(_sellAll);

        VBoxContainer inside = _inside;
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        _offers.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _shopFilters.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _shopFilters.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddFilterColumn("분류", _categoryFilter);
        AddFilterColumn("성별", _genderFilter);
        AddFilterColumn("서클", _circleFilter);
        _categoryFilter.ItemSelected += index => SelectFilter(_categoryFilter, index, value => _shopCategory = value);
        _genderFilter.ItemSelected += index => SelectFilter(_genderFilter, index, value => _shopGender = value);
        _circleFilter.ItemSelected += index =>
        {
            _shopCircle = (int)index;
            RefreshFilteredGoods();
        };
        inside.AddChild(_shopFilters);
        inside.AddChild(_words);
        inside.AddChild(_goodsList);
        inside.AddChild(_goodsEmpty);

        // 목록만 굴린다. 상점 단추는 제목 줄에, 다른 대화 응답은 창 바닥에 둔다.
        ScrollContainer scroll = _scroll;
        scroll.AddChild(inside);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter);
        body.AddChild(head);
        body.AddChild(scroll);
        body.AddChild(_offers);

        PanelContainer within = new();
        within.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        within.AddChild(body);

        AddChild(within);
    }

    /// <summary>The button that shuts the window, so whoever opened it can decide what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody picked an answer: which NPC, the number it goes back with, and any words that go with it.</summary>
    public event System.Action<uint, ushort, string?>? Answered;

    /// <summary>상점의 여러 물건을 한 번에 처리한다(서버 0xF2).</summary>
    public event System.Action<uint, bool, IReadOnlyList<(string Name, int Slot, int Quantity)>>? BulkTradeRequested;

    /// <summary>상점의 구매/판매 첫 메뉴로 돌아간다.</summary>
    public event System.Action<uint>? ShopBackRequested;

    /// <summary>Shows one window. The pack is needed to put names and pictures to the slots a shop asks about.</summary>
    public void Show(Dialogue talk, IReadOnlyList<InventoryItem> pack)
    {
        _lastPack = pack;
        ResetForSpeaker(talk.Serial);
        SetDialogueHeader(talk);
        ClearDialogueBody();
        RenderDialogue(talk, pack);
    }

    private void ResetForSpeaker(uint speaker)
    {
        if (_shopSpeaker == speaker)
        {
            return;
        }

        _shopSpeaker = speaker;
    }

    private void SetDialogueHeader(Dialogue talk)
    {
        _who.Text = talk.Who.Split('@')[0];
        _words.Text = talk.What;
        _words.Visible = true;
    }

    private void ClearDialogueBody()
    {
        ClearChildren(_offers);
        ClearChildren(_goodsList);
        _goodsEmpty.Visible = false;
        _offers.Visible = false;
        _shopFilters.Visible = false;
        _typingRow = null;
        _shopBack.Visible = false;
        _shopAction.Visible = false;
        _sellAll.Visible = false;
        _shopActionHandler = null;
        _shopWindow = false;
        _goods = [];
        _shopTalk = null;
        _sellTalk = null;
        _sellable = [];
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void RenderDialogue(Dialogue talk, IReadOnlyList<InventoryItem> pack)
    {
        switch (talk.Kind)
        {
            case DialogueKind.Options or DialogueKind.OptionsWithArgs:
                ShowOptions(talk);
                break;
            case DialogueKind.Goods:
                ShowGoods(talk);
                break;
            case DialogueKind.PackSlots:
                ShowPackSlots(talk, pack);
                break;
            case DialogueKind.Skills or DialogueKind.Spells:
                ShowAbilities(talk);
                break;
            case DialogueKind.TextInput:
                ShowTextInput(talk);
                break;
        }
    }

    private void ShowOptions(Dialogue talk)
    {
        _shopBack.Visible = false;
        string? handBack = talk.Kind == DialogueKind.OptionsWithArgs ? talk.Args : null;
        foreach (DialogueOption option in talk.Options)
        {
            Offer(option.Text, null, () => Answered?.Invoke(talk.Serial, option.Step, handBack));
        }
    }

    private void ShowGoods(Dialogue talk)
    {
        _shopWindow = true;
        _shopBack.Visible = true;
        _shopFilters.Visible = true;
        _shopAction.Visible = true;
        _sellAll.Visible = false;
        _shopAction.Text = "구매";
        _shopActionHandler = SubmitBuy;
        PlaceFilters();
        _goods = talk.Goods;
        _shopTalk = talk;
        _shopCategory = "전체";
        _shopGender = "전체";
        _shopCircle = 0;
        _words.Visible = talk.What != "천천히 둘러보십시오.";
        _buyQuantities.Clear();
        _selectedBuyName = _goods.FirstOrDefault()?.Name;
        foreach (DialogueGoods goods in _goods)
        {
            _buyQuantities[goods.Name] = 0;
        }

        BuildShopFilters();
        RenderGoods();

        UpdateBuyButton();
    }

    private void ShowPackSlots(Dialogue talk, IReadOnlyList<InventoryItem> pack)
    {
        _shopWindow = true;
        _shopBack.Visible = true;
        _shopFilters.Visible = false;
        _words.Visible = talk.What != "무엇을 파시겠습니까?";
        _sellQuantities.Clear();

        InventoryItem[] sellable = [.. pack.Where(item => talk.Slots.Contains(item.Slot))];
        _sellable = sellable;
        _sellTalk = talk;
        _selectedSellSlot = sellable.FirstOrDefault() is { } first ? first.Slot : null;
        foreach (InventoryItem item in sellable)
        {
            _sellQuantities[item.Slot] = 1;
        }

        RenderSell(talk, sellable);
        _shopAction.Visible = true;
        _shopAction.Text = "판매";
        _shopActionHandler = () => SubmitSell(talk, sellable);
        UpdateSellButton();
        _sellAll.Visible = sellable.Length > 0;
    }

    private void ShowAbilities(Dialogue talk)
    {
        string sheet = talk.Kind == DialogueKind.Skills ? AbilityBar.SkillSheet : AbilityBar.SpellSheet;
        foreach (DialogueAbility ability in talk.Abilities)
        {
            Offer(ability.Name, AbilityBar.Frame(sheet, ability.Icon),
                () => Answered?.Invoke(talk.Serial, talk.Step, ability.Name));
        }
    }

    private void PlaceFilters()
    {
        bool inHeader = !Main.Portrait;
        if (_filtersInHeader == inHeader)
        {
            return;
        }

        if (inHeader)
        {
            _inside.RemoveChild(_shopFilters);
            _head.AddChild(_shopFilters);
            _head.MoveChild(_shopFilters, 1);
            _who.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        }
        else
        {
            _head.RemoveChild(_shopFilters);
            _inside.AddChild(_shopFilters);
            _inside.MoveChild(_shopFilters, 0);
            _who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }

        _filtersInHeader = inHeader;
    }

    private void ShowTextInput(Dialogue talk)
    {
        _offers.Visible = true;
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
    }

    private void BuildShopFilters()
    {
        AddFilterItems(_categoryFilter, ["전체", "무기", "방어구", "장신구", "소모품"]);
        AddFilterItems(_genderFilter, ["전체", "남성", "여성"]);
        AddFilterItems(_circleFilter, ["전체", "1서클", "2서클", "3서클", "4서클", "5서클"]);
        _categoryFilter.Select(0);
        _genderFilter.Select(0);
        _circleFilter.Select(0);
    }

    private void AddFilterColumn(string title, OptionButton filter)
    {
        Label label = new()
        {
            Text = title,
            CustomMinimumSize = new Vector2(34, Main.TouchMinimum),
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Greybox.Muted);
        filter.CustomMinimumSize = new Vector2(0, Main.TouchMinimum);
        filter.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        HBoxContainer column = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = title == "분류" ? 1.5f : 1f
        };
        column.AddThemeConstantOverride("separation", Main.Gutter / 4);
        column.AddChild(label);
        column.AddChild(filter);
        _shopFilters.AddChild(column);
    }

    private static void AddFilterItems(OptionButton filter, string[] choices)
    {
        filter.Clear();
        foreach (string choice in choices)
        {
            filter.AddItem(choice);
        }
    }

    private void SelectFilter(OptionButton filter, long index, System.Action<string> set)
    {
        set(filter.GetItemText((int)index));
        RefreshFilteredGoods();
    }

    private void RefreshFilteredGoods()
    {
        _selectedBuyName = FilteredGoods().FirstOrDefault()?.Name;
        RenderGoods();
        UpdateBuyButton();
    }

    private void RenderGoods()
    {
        ClearChildren(_goodsList);

        DialogueGoods[] goodsList = [.. FilteredGoods()];
        foreach (DialogueGoods goods in goodsList)
        {
            _goodsList.AddChild(BuildBuyRow(goods));
        }

        _goodsEmpty.Visible = goodsList.Length == 0;
    }

    private IEnumerable<DialogueGoods> FilteredGoods() =>
        _goods.Where(goods => Matches(goods, _shopCategory, _shopGender, _shopCircle));

    private Control BuildBuyRow(DialogueGoods goods)
    {
        int quantity = _buyQuantities.GetValueOrDefault(goods.Name);
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);
        row.AddChild(ItemPicture(goods.Icon));
        LineEdit amount = QuantityInput(quantity, ushort.MaxValue, value => SetBuyQuantity(goods, value));
        amount.FocusEntered += () =>
        {
            _selectedBuyName = goods.Name;
            UpdateBuyButton();
        };
        TouchInput.Zone(amount, row);

        Button product = OfferTo(row, FormatGoods(goods), null, () =>
        {
            _selectedBuyName = goods.Name;
            RenderGoods();
            UpdateBuyButton();
        });
        StyleShopRow(product, goods.Name == _selectedBuyName);

        Button minus = QuantityButton(GlyphKind.Minus, "수량 줄이기");
        Button plus = QuantityButton(GlyphKind.Plus, "수량 늘리기");

        minus.Pressed += () => ChangeBuyQuantity(goods, -1);
        plus.Pressed += () => ChangeBuyQuantity(goods, 1);
        minus.Disabled = quantity == 0;
        plus.Disabled = quantity >= ushort.MaxValue;
        row.AddChild(minus);
        row.AddChild(amount);
        row.AddChild(plus);
        return row;
    }

    private void ChangeBuyQuantity(DialogueGoods goods, int delta)
    {
        _selectedBuyName = goods.Name;
        int current = _buyQuantities.GetValueOrDefault(goods.Name);
        SetBuyQuantity(goods, Math.Clamp(current + delta, 0, ushort.MaxValue));
        RenderGoods();
    }

    private void SetBuyQuantity(DialogueGoods goods, int quantity)
    {
        _selectedBuyName = goods.Name;
        _buyQuantities[goods.Name] = Math.Clamp(quantity, 0, ushort.MaxValue);
        UpdateBuyButton();
    }

    private void UpdateBuyButton()
    {
        if (_shopAction.Visible && _shopTalk is not null)
        {
            _shopAction.Disabled = _selectedBuyName is not { } name || _buyQuantities.GetValueOrDefault(name) == 0;
        }
    }

    private LineEdit QuantityInput(int quantity, int maximum, System.Action<int> changed)
    {
        LineEdit input = new()
        {
            Text = quantity.ToString(),
            CustomMinimumSize = new Vector2(64, Main.TouchMinimum),
            Alignment = HorizontalAlignment.Center,
            SelectAllOnFocus = true,
            VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number
        };
        input.TextChanged += text =>
        {
            if (text.Length == 0)
            {
                changed(0);
            }
            else if (int.TryParse(text, out int value))
            {
                changed(Math.Clamp(value, 0, maximum));
            }
        };
        input.TextSubmitted += _ => input.ReleaseFocus();
        input.FocusExited += () =>
        {
            int value = int.TryParse(input.Text, out int parsed) ? Math.Clamp(parsed, 0, maximum) : 0;
            input.Text = value.ToString();
            changed(value);
        };
        return input;
    }

    private void SubmitBuy()
    {
        if (_shopTalk is not { } talk || _selectedBuyName is not { } name)
        {
            return;
        }

        DialogueGoods? goods = _goods.FirstOrDefault(item => item.Name == name);
        if (goods is null || _buyQuantities.GetValueOrDefault(goods.Name) == 0)
        {
            return;
        }

        BulkTradeRequested?.Invoke(talk.Serial, false,
            [(goods.Name, 0, _buyQuantities.GetValueOrDefault(goods.Name))]);
    }

    private void RenderSell(Dialogue talk, IReadOnlyList<InventoryItem> sellable)
    {
        ClearChildren(_goodsList);
        foreach (InventoryItem item in sellable)
        {
            _goodsList.AddChild(BuildSellRow(talk, item));
        }
    }

    private Control BuildSellRow(Dialogue talk, InventoryItem item)
    {
        int quantity = _sellQuantities.GetValueOrDefault(item.Slot, 1);
        int maximum = Math.Max(1, item.Stacks);
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);
        row.AddChild(ItemPicture(item.Icon));
        LineEdit amount = QuantityInput(quantity, Math.Min(maximum, ushort.MaxValue), value => SetSellQuantity(item, value));
        TouchInput.Zone(amount, row);

        Button product = OfferTo(row,
            item.Stacks > 1 ? $"{item.Name}  ×{item.Stacks}" : item.Name,
            null, () =>
            {
                _selectedSellSlot = item.Slot;
                UpdateSellButton();
                RenderSell(talk, _lastPack.Where(candidate => talk.Slots.Contains(candidate.Slot)).ToArray());
            });
        StyleShopRow(product, item.Slot == _selectedSellSlot);

        Button minus = QuantityButton(GlyphKind.Minus, "수량 줄이기");
        Button plus = QuantityButton(GlyphKind.Plus, "수량 늘리기");

        minus.Disabled = quantity <= 1;
        plus.Disabled = quantity >= Math.Min(maximum, ushort.MaxValue);
        minus.Pressed += () => ChangeSellQuantity(talk, item, -1);
        plus.Pressed += () => ChangeSellQuantity(talk, item, 1);
        row.AddChild(minus);
        row.AddChild(amount);
        row.AddChild(plus);
        return row;
    }

    private static Button QuantityButton(GlyphKind kind, string tooltip)
    {
        Button button = WindowFrame.IconButton(kind, string.Empty, width: Main.TouchMinimum);
        button.TooltipText = tooltip;
        return button;
    }

    private static TextureRect ItemPicture(int icon) => new()
    {
        Texture = ItemIcons.For(icon),
        CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private void ChangeSellQuantity(Dialogue talk, InventoryItem item, int delta)
    {
        _selectedSellSlot = item.Slot;
        int maximum = Math.Min(Math.Max(1, item.Stacks), ushort.MaxValue);
        int current = _sellQuantities.GetValueOrDefault(item.Slot, 1);
        SetSellQuantity(item, Math.Clamp(current + delta, 0, maximum));
        RenderSell(talk, _lastPack.Where(candidate => talk.Slots.Contains(candidate.Slot)).ToArray());
    }

    private void SetSellQuantity(InventoryItem item, int quantity)
    {
        _selectedSellSlot = item.Slot;
        _sellQuantities[item.Slot] = Math.Clamp(quantity, 0, Math.Min(Math.Max(1, item.Stacks), ushort.MaxValue));
        UpdateSellButton();
    }

    private void UpdateSellButton()
    {
        _shopAction.Disabled = _selectedSellSlot is not { } slot
            || _sellable.Count == 0
            || _sellQuantities.GetValueOrDefault(slot) == 0;
    }

    private void SubmitSell(Dialogue talk, IReadOnlyList<InventoryItem> sellable)
    {
        if (_selectedSellSlot is not { } slot)
        {
            return;
        }

        InventoryItem? item = sellable.FirstOrDefault(candidate => candidate.Slot == slot);
        if (item is null || _sellQuantities.GetValueOrDefault(item.Slot) == 0)
        {
            return;
        }

        BulkTradeRequested?.Invoke(talk.Serial, true,
            [(item.Name, item.Slot, _sellQuantities.GetValueOrDefault(item.Slot, 1))]);
    }

    private void SubmitSellAll()
    {
        if (_sellTalk is not { } talk || _selectedSellSlot is not { } slot)
        {
            return;
        }

        InventoryItem? item = _sellable.FirstOrDefault(candidate => candidate.Slot == slot);
        if (item is null)
        {
            return;
        }

        BulkTradeRequested?.Invoke(talk.Serial, true,
            [(item.Name, item.Slot, Math.Clamp(item.Stacks, 1, ushort.MaxValue))]);
    }

    private static string FormatGoods(DialogueGoods goods)
    {
        return goods.Class.Length == 0
            ? $"{goods.Name}  {goods.Price:N0}전"
            : $"{goods.Name}  {goods.Price:N0}전 · {goods.Class}";
    }

    private static void StyleShopRow(Button button, bool selected)
    {
        StyleBoxFlat normal = Greybox.Surface();
        normal.SetCornerRadiusAll(8);
        if (selected)
        {
            normal.BorderColor = Greybox.Accent;
            normal.SetBorderWidthAll(2);
        }

        foreach (string state in new[] { "normal", "hover", "focus", "pressed" })
        {
            button.AddThemeStyleboxOverride(state, normal);
        }
    }

    private static bool Matches(DialogueGoods goods, string category, string gender, int circle)
    {
        bool categoryMatch = category switch
        {
            "전체" => true,
            "무기" => goods.Name.Contains("검") || goods.Name.Contains("도") || goods.Name.Contains("창") || goods.Name.Contains("활") || goods.Name.Contains("너클") || goods.Name.Contains("글러브"),
            "방어구" => goods.Name.Contains("갑옷") || goods.Name.Contains("튜닉") || goods.Name.Contains("로브") || goods.Name.Contains("도복"),
            "장신구" => goods.Name.Contains("반지") || goods.Name.Contains("목걸이") || goods.Name.Contains("귀걸이") || goods.Name.Contains("벨트") || goods.Name.Contains("방패") || goods.Name.Contains("투구") || goods.Name.Contains("각반") || goods.Name.Contains("신발") || goods.Name.Contains("장갑"),
            "소모품" => goods.Name.Contains("쿠룸") || goods.Name.Contains("마라디움") || goods.Name.Contains("포션") || goods.Name.Contains("약"),
            _ => false
        };

        bool genderMatch = gender switch
        {
            "남성" => goods.Gender == 1 || goods.Gender == 255,
            "여성" => goods.Gender == 2 || goods.Gender == 255,
            _ => true
        };
        bool circleMatch = circle == 0 || goods.Circle == circle - 1;
        return categoryMatch && genderMatch && circleMatch;
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
        if (_shopWindow && _filtersInHeader == Main.Portrait)
        {
            PlaceFilters();
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
        float heads = _head.GetCombinedMinimumSize().Y + Main.Gutter;
        float chrome = GetCombinedMinimumSize().Y + (_head.Visible ? 0 : heads);
        float left = bottom - lift - holder.GetGlobalRect().Position.Y - chrome;
        _head.Visible = !typing || left >= Row.Y + Main.Gutter;

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

    private Button Offer(string text, Texture2D? icon, System.Action pressed)
    {
        _offers.Visible = true;
        return OfferTo(_offers, text, icon, pressed);
    }

    private static Button OfferTo(Container parent, string text, Texture2D? icon, System.Action pressed)
    {
        Button button = new()
        {
            Text = text,
            Icon = icon,
            ExpandIcon = icon is not null,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = Row,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }
}
