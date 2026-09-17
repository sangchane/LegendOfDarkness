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

    public TalkPanel()
    {
        Name = "Talk";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Plate());

        HBoxContainer head = new();
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
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(inside);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter);
        body.AddChild(head);
        body.AddChild(scroll);

        AddChild(body);
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
                LineEdit typed = new() { CustomMinimumSize = Row };
                _offers.AddChild(typed);
                Offer("확인", null, () => Answered?.Invoke(talk.Serial, talk.Step, typed.Text));

                break;
        }
    }

    /// <summary>
    /// Presses the first answer on offer. Only for a run with no hand on it — it goes through the same button, so the
    /// wiring is checked rather than bypassed.
    /// </summary>
    public bool PressFirst()
    {
        if (_offers.GetChildren().OfType<Button>().FirstOrDefault(button => !button.IsQueuedForDeletion()) is not { } first)
        {
            return false;
        }

        first.EmitSignal(BaseButton.SignalName.Pressed);

        return true;
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
