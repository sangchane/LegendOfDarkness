using System.Globalization;
using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// What a phone does differently from a mouse, for every screen at once: how much of the screen the on-screen keyboard
/// takes, putting the keyboard away when a thumb taps somewhere else, and lists that scroll wherever the thumb drags.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyboard.</b> Godot's iOS driver reports the keyboard only after it has finished sliding in (UIKeyboardDidShow) and
/// in pixels of the whole screen; nothing moves the game out of its way. Screens read <see cref="Covered"/> here and
/// slide or shrink one part of themselves (<see cref="KeyboardFit"/>).
/// </para>
/// <para>
/// <b>Tapping away.</b> A tap on something that takes no focus (the map, a label, a panel) leaves the field focused, so
/// the iPhone keyboard — which has no key to hide itself — stayed up until a button happened to be pressed. A tap outside
/// the field, or outside the row it types in (<see cref="Zone"/>), now puts it away.
/// </para>
/// <para>
/// <b>Lists.</b> A ScrollContainer scrolls by dragging only when the drag reaches it. Buttons stop the press by default,
/// so a drag that started on a row — the world-map list, an NPC's goods, the HAIR/COLOR tiles — did nothing; only the
/// scroll bar moved the list. Buttons inside a list now pass the press on, and the list takes over once the thumb has
/// travelled past the dead zone (project setting <c>gui/common/default_scroll_deadzone</c>): the button's press is then
/// cancelled (NOTIFICATION_SCROLL_BEGIN), and the list keeps gliding after the thumb lets go.
/// </para>
/// </remarks>
public sealed partial class TouchInput : Node
{
    /// <summary>How fast the screen follows the keyboard, in units a second — a keyboard's height in about a tenth.</summary>
    private const float Follow = 2400;

    /// <summary>
    /// How long a keyboard that reports 0 while a field is still being typed in counts as still there. Moving from one
    /// field to the next hides and shows the keyboard in one go; without this the screen would drop and jump back.
    /// </summary>
    private const double Hold = 0.4;

    private const string ZoneMeta = "typing_zone";

    /// <summary>How much of the screen's height the keyboard takes, in the screen's units. 0 when it is down.</summary>
    public static float Covered { get; private set; }

    // --keyboard 209: 데스크톱에는 화면 키보드가 없어, 글자 칸을 치는 동안 그만큼 가린 것으로 친다.
    private float _pretend;
    private ColorRect? _pretendKeys;
    private float _lastSeen;
    private double _zeroFor;

    // --type 2: 손 없이 확인할 때 N 번째 글자 칸을 한 번 눌러 둔다.
    private int _typeInto;
    private double _typeAfter = 1.0;

    // --swipe x,y,dy: 손 없이 확인할 때 그 자리에서 손가락으로 dy 만큼 끈다.
    private Vector2 _swipeAt;
    private float _swipeBy;
    private double _swipeAfter = -1;

    public override void _Ready()
    {
        Name = "TouchInput";
        ProcessMode = ProcessModeEnum.Always;

        _pretend = float.TryParse(Flag("--keyboard"), NumberStyles.Float, CultureInfo.InvariantCulture, out float tall) ? tall : 0;
        _typeInto = int.TryParse(Flag("--type"), out int nth) ? nth : 0;
        ReadSwipe(Flag("--swipe"));

        GetTree().NodeAdded += Adopt;

        if (GetParent() is Control { Theme: { } theme })
        {
            Style(theme);
        }
    }

    public override void _ExitTree() => GetTree().NodeAdded -= Adopt;

    /// <summary>
    /// Counts the tap-away rule as not broken while the thumb stays inside <paramref name="zone"/> — the row a field
    /// types in, so its own send button does not put the keyboard away.
    /// </summary>
    public static void Zone(LineEdit field, Control zone) => field.SetMeta(ZoneMeta, zone);

    /// <summary>The field being typed into, if any.</summary>
    public static LineEdit? Editing(Viewport viewport) =>
        viewport.GuiGetFocusOwner() is LineEdit { Editable: true } field && field.IsEditing() ? field : null;

    public override void _Process(double delta)
    {
        float height = GetViewport().GetVisibleRect().Size.Y;
        bool typing = Editing(GetViewport()) is not null;
        float target;

        if (_pretend > 0)
        {
            target = typing ? _pretend : 0;
        }
        else
        {
            // 화상 자판이 없는 곳(데스크톱)에서 물으면 매 프레임 경고가 쏟아진다(docs/mobile-client.md, Mac 3번).
            bool keyboard = DisplayServer.HasFeature(DisplayServer.Feature.VirtualKeyboard);
            target = keyboard
                ? KeyboardFit.Covered(DisplayServer.VirtualKeyboardGetHeight(), DisplayServer.ScreenGetSize().Y, height)
                : 0;

            if (target > 0)
            {
                _lastSeen = target;
                _zeroFor = 0;
            }
            else if (typing && (_zeroFor += delta) < Hold)
            {
                target = _lastSeen;
            }
        }

        Covered = Mathf.MoveToward(Covered, target, Follow * (float)delta);
        DrawPretendKeyboard(height);

        RehearseTyping(delta);
        RehearseSwipe(delta);
    }

    /// <summary>Puts the keyboard away when a tap lands outside the field being typed into.</summary>
    public override void _Input(InputEvent @event)
    {
        // 폰에서는 손가락이 마우스로도 한 번 더 온다(emulate_mouse_from_touch) — 마우스 쪽 하나만 본다.
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press
            || Editing(GetViewport()) is not { } field)
        {
            return;
        }

        Control zone = field.HasMeta(ZoneMeta) && field.GetMeta(ZoneMeta).AsGodotObject() is Control given
                                               && IsInstanceValid(given)
            ? given
            : field;

        if (zone.GetGlobalRect().HasPoint(press.Position) || field.GetGlobalRect().HasPoint(press.Position))
        {
            return;
        }

        // 누른 곳이 먼저 받게 한 뒤에 본다 — 다른 글자 칸이나 단추가 초점을 가져갔으면 키보드는 이미 제 갈 길을 간다.
        Callable.From(() =>
        {
            if (IsInstanceValid(field) && field.HasFocus())
            {
                field.ReleaseFocus();
            }
        }).CallDeferred();
    }

    /// <summary>Buttons inside a list let a drag through to the list, so dragging anywhere in it scrolls.</summary>
    private static void Adopt(Node node)
    {
        if (node is not BaseButton { MouseFilter: Control.MouseFilterEnum.Stop } button)
        {
            return;
        }

        for (Node? up = node.GetParent(); up is not null; up = up.GetParent())
        {
            if (up is ScrollContainer)
            {
                button.MouseFilter = Control.MouseFilterEnum.Pass;
                return;
            }
        }
    }

    /// <summary>
    /// A thin bar that says there is more and where the list stands, rather than a track to aim at: the list moves
    /// under the thumb, so nothing needs to be grabbed.
    /// </summary>
    private static void Style(Theme theme)
    {
        foreach (string bar in new[] { "VScrollBar", "HScrollBar" })
        {
            bool upright = bar == "VScrollBar";
            StyleBoxFlat track = new() { BgColor = Colors.Transparent };
            track.ContentMarginLeft = track.ContentMarginRight = upright ? 2 : 0;
            track.ContentMarginTop = track.ContentMarginBottom = upright ? 0 : 2;

            theme.SetStylebox("scroll", bar, track);
            theme.SetStylebox("scroll_focus", bar, track);

            foreach (string state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
            {
                theme.SetStylebox(state, bar, Grip(state == "grabber" ? 0.55f : 0.85f));
            }
        }

        static StyleBoxFlat Grip(float alpha) => new()
        {
            BgColor = Greybox.Muted with { A = alpha },
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
    }

    /// <summary>With <c>--keyboard</c>, a grey block where the keyboard would be, so a screenshot shows what it covers.</summary>
    private void DrawPretendKeyboard(float height)
    {
        if (_pretend <= 0)
        {
            return;
        }

        if (_pretendKeys is null)
        {
            CanvasLayer layer = new() { Layer = 100 };
            _pretendKeys = new ColorRect { Color = new Color(0.55f, 0.57f, 0.62f, 0.92f), MouseFilter = Control.MouseFilterEnum.Ignore };
            Label label = new() { Text = "화면 키보드 자리 (--keyboard)", Position = new Vector2(12, 8) };
            label.AddThemeColorOverride("font_color", new Color(0.1f, 0.1f, 0.12f));
            _pretendKeys.AddChild(label);
            layer.AddChild(_pretendKeys);
            AddChild(layer);
        }

        float width = GetViewport().GetVisibleRect().Size.X;
        _pretendKeys.Visible = Covered > 0;
        _pretendKeys.Position = new Vector2(0, height - Covered);
        _pretendKeys.Size = new Vector2(width, Covered);
    }

    /// <summary>Only when checking without a hand (<c>--type N</c>): starts typing in the Nth field on screen once.</summary>
    private void RehearseTyping(double delta)
    {
        if (_typeInto <= 0 || (_typeAfter -= delta) > 0)
        {
            return;
        }

        int seen = 0;

        foreach (Node node in GetTree().Root.FindChildren("*", nameof(LineEdit), true, false))
        {
            if (node is LineEdit { Editable: true } field && field.IsVisibleInTree() && ++seen == _typeInto)
            {
                field.Edit();
                _typeInto = 0;
                GD.Print($"GREYBOX_TYPE field={field.GetPath()} rect={field.GetGlobalRect()}");
                return;
            }
        }
    }

    private void ReadSwipe(string given)
    {
        string[] parts = given.Split(',');

        if (parts.Length == 3
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float by))
        {
            _swipeAt = new Vector2(x, y);
            _swipeBy = by;
            _swipeAfter = 1.5;

            // 데스크톱은 손가락이 없다고 여겨 목록이 끌기를 받지 않는다 — 이 실행에서만 마우스를 손가락으로 친다.
            Input.EmulateTouchFromMouse = true;
        }
    }

    /// <summary>
    /// Only when checking without a hand (<c>--swipe x,y,dy</c>): presses at a point, drags by dy in small steps and lets
    /// go, then prints where each list under the point stands. A small dy is a shaky tap, which must still press.
    /// </summary>
    private async void RehearseSwipe(double delta)
    {
        if (_swipeAfter < 0 || (_swipeAfter -= delta) > 0)
        {
            return;
        }

        _swipeAfter = -1;
        List<ScrollContainer> lists = [];

        foreach (Node node in GetTree().Root.FindChildren("*", nameof(ScrollContainer), true, false))
        {
            if (node is ScrollContainer list && list.IsVisibleInTree() && list.GetGlobalRect().HasPoint(_swipeAt))
            {
                lists.Add(list);
            }
        }

        string Where() => string.Join(" ", lists.Select(list => $"{list.Name}:{list.ScrollVertical},{list.ScrollHorizontal}"));
        GD.Print($"GREYBOX_SWIPE before {Where()}");

        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = _swipeAt, GlobalPosition = _swipeAt, ButtonMask = MouseButtonMask.Left });
        Vector2 at = _swipeAt;
        const int Steps = 8;

        for (int step = 0; step < Steps; step++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Vector2 next = at + new Vector2(0, _swipeBy / Steps);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = next, GlobalPosition = next, Relative = next - at, ButtonMask = MouseButtonMask.Left });
            at = next;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });

        for (int frame = 0; frame < 30; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GD.Print($"GREYBOX_SWIPE after {Where()}");
    }

    private static string Flag(string name)
    {
        string[] args = OS.GetCmdlineUserArgs();
        int at = System.Array.IndexOf(args, name);

        return at >= 0 && at < args.Length - 1 ? args[at + 1] : string.Empty;
    }
}
