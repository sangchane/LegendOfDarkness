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
        body.AddChild(head);
        body.AddChild(scroll);

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
    public void Show(Dialogue talk, IReadOnlyList<InventoryItem> pack)
    {
        // 이식한 NPC 는 이름에 자리가 붙어 온다(카르마@노비스마을식당#3,10). 화면에는 이름만.
        _who.Text = talk.Who.Split('@')[0];
        _words.Text = talk.What;

        foreach (Node old in _offers.GetChildren())
        {
            old.QueueFree();
        }

        _typingRow = null;

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

    /// <summary>
    /// While a line is being typed, the window's bottom rides just above the keyboard and its list scrolls so the input
    /// row stays in sight; the world behind is not moved. Before, the window kept its full height and the input row
    /// sat under the keyboard.
    /// </summary>
    /// <remarks>The holder belongs to GameScreen and nothing else lifts it; putting it back when the keyboard goes is ours.</remarks>
    public override void _Process(double delta)
    {
        RehearseTextInput(delta);

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
