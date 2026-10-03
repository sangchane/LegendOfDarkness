using Godot;

namespace LodClient;

/// <summary>
/// Greybox host. Applies the one theme every screen shares, works out the safe-area insets once, and mounts
/// the screen under test. No colour or art here: greybox judges layout, reach and text, nothing else.
/// </summary>
public partial class Main : Control
{
    /// <summary>
    /// 결과를 기다리지 않는 서버 요청을 보낸다. 실패하면 말없이 사라지지 않게 오류 기록을 남긴다
    /// (취소 — 화면을 떠나는 길 — 는 기록하지 않는다).
    /// </summary>
    public static void Fire(System.Threading.Tasks.Task? request)
    {
        request?.ContinueWith(
            failed => GD.PushError($"서버 요청 실패: {failed.Exception?.GetBaseException().Message}"),
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>Smallest touch target, in logical units. One unit is one dp at this project's base size.</summary>
    public const int TouchMinimum = 48;

    /// <summary>Smallest gap between controls, and the least distance kept from the safe-area edge.</summary>
    public const int Gutter = 8;

    /// <summary>
    /// Widest the two thumb clusters may sit apart. Past this the world keeps growing but the controls stay
    /// where a thumb can still reach them, which is the rule for 20:9 and wider screens.
    /// </summary>
    public const int ThumbSpanMaximum = 680;

    private const int BodyFontSize = 16;

    /// <summary>Logical size of a portrait screen. One unit is one dp here too.</summary>
    private static readonly Vector2I PortraitSize = new(360, 780);

    /// <summary>Insets that keep text and controls clear of notches and the home indicator.</summary>
    public static (int Left, int Top, int Right, int Bottom) SafeInsets { get; private set; } =
        (Gutter, Gutter, Gutter, Gutter);

    /// <summary>
    /// Which way the screen is held. Portrait is not landscape squeezed: it has enough height to keep the
    /// controls out of the world, so the screens lay themselves out differently rather than scaling.
    /// </summary>
    public static bool Portrait { get; private set; }

    public static event System.Action? LayoutChanged;
    private bool _orientationWasForced;

    public override void _Ready()
    {
        _orientationWasForced = Flag("--orient").Length > 0;
        Portrait = Flag("--orient") == "portrait";
        ReadServer(Flag("--server"));
        ReadRehearsal(Flag("--login"));
        Rehearse = Flag("--walk");
        Holding = Flag("--hold");
        Ability = Flag("--skill");
        Picking = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pick") >= 0;
        PickedLook = ReadPickedLook(Flag("--pick-look"));
        PickedPath = ReadPickedPath(Flag("--pick-job"));
        CreateNow = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--create-now") >= 0;
        Saying = Flag("--say");
        ChatTab = Flag("--chat");
        Inviting = Flag("--invite");
        Looking = Flag("--look");
        Accepting = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--accept") >= 0;
        PartySaying = Flag("--party-say");
        LeavingAfter = double.TryParse(Flag("--leave-after"), out double leaveAfter) ? leaveAfter : -1;
        Noticing = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--notices") >= 0;
        Overhead = Flag("--overhead");
        OpeningPack = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pack") >= 0;
        OpeningSettings = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--settings") >= 0;
        OpeningExit = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--exit-menu") >= 0;
        SettingsTab = Flag("--settings-tab");
        OpeningSettings = OpeningSettings || SettingsTab.Length > 0;
        CheckingMinimap = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--minimap") >= 0;
        PartyPreview = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--party-preview") >= 0;
        MinimapZoom = int.TryParse(Flag("--minimap-zoom"), out int minimapZoom) ? minimapZoom : 0;
        PackPick = int.TryParse(Flag("--pack-pick"), out int packPick) ? packPick : 0;
        PickingPotion = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pick-potion") >= 0;
        SlotHold = int.TryParse(Flag("--slot-hold"), out int slotHold) ? slotHold : 0;
        LearnPreview = Flag("--learn-preview").Split(':') is [var previewPath, var previewLevel]
                       && int.TryParse(previewPath, out int learnPath) && int.TryParse(previewLevel, out int learnLevel)
            ? (learnPath, learnLevel)
            : null;
        PercentOpen = Flag("--percent-open");
        MapGo = Flag("--map-go");
        MapTab = Flag("--map-tab");
        OpeningMap = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--map") >= 0 || MapGo.Length > 0;
        TabMapGo = Flag("--tabmap-go");
        TabMapCloseAfter = double.TryParse(Flag("--tabmap-close"), out double tabMapClose) ? tabMapClose : -1;
        TabMapZoom = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--tabmap-zoom") >= 0;
        OpeningTabMap = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--tabmap") >= 0 || TabMapGo.Length > 0 || TabMapZoom;
        OnGear = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--gear") >= 0;
        Striking = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--strike") >= 0;
        Hunting = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--hunt") >= 0
            || Godot.FileAccess.FileExists(HuntFile);

        // 사냥은 사람이 손대지 않는 채로 오래 돈다. iOS 는 화면이 꺼지면 앱을 재우고, 그러면 _Process 가
        // 멈춰 캐릭터가 그 자리에 굳는다 — 접속은 살아 있어서 서버 쪽에서는 멀쩡해 보인다.
        if (Hunting)
        {
            DisplayServer.ScreenSetKeepOn(true);
        }
        Lifting = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--lift") >= 0;
        Wearing = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--wear") >= 0;
        GearAfter = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--gear-after") >= 0;
        ReadAutoLoot();
        ReadBotMagic();
        ReadMinimapRadius();
        ReadPotions();
        ReadAutoHuntSettings();
        AutoHuntOnStart = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--auto-hunt") >= 0;
        CompanionOnStart = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--companion") >= 0;
        BotPreview = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--bot-preview") >= 0;
        BotGearOpen = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--bot-gear") >= 0;
        AutoHuntPreview = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--auto-hunt-preview") >= 0;
        ReadSavedLogin();
        Throwing = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--throw") >= 0;

        // 입거나 버려 보려면 소지품이 열려 있어야 한다 — 따로 적게 하지 않는다.
        OpeningPack = OpeningPack || Wearing || Throwing;

        // 사진을 찍거나 자리를 재는 실행은 사람이 볼 것이 아니다. 창을 아주 없앨 수는 없다 —
        // --headless 는 아무것도 그리지 않을 뿐 아니라 --size 도 듣지 않아 780x780 을 재게 된다.
        // 그래서 화면 밖으로 내보내고 키보드를 빼앗지 않게 한다. 작업표시줄에만 남는다.
        if (Screenshot.Requested() || LayoutCheck.Requested())
        {
            StayOutOfTheWay();
        }

        // --size wins over the orientation's own default, so a check can walk several shapes of screen.
        Vector2I? asked = SizeFromCommandLine();

        if (asked is { } wanted)
        {
            GetWindow().ContentScaleSize = wanted;
            DisplayServer.WindowSetSize(wanted);
        }
        else if (Portrait)
        {
            GetWindow().ContentScaleSize = PortraitSize;
            DisplayServer.WindowSetSize(PortraitSize * 5 / 4);
        }

        Theme = BuildTheme();

        // 화면 키보드·키보드 치우기·목록 끌기 — 화면마다 따로 하지 않고 여기 한 곳에서(TouchInput).
        AddChild(new TouchInput());
        GetWindow().SizeChanged += RefreshDrawableLayout;
        RefreshDrawableLayout();

        if (Flag("--screen") == "game")
        {
            GameScreen game = new();

            AddChild(game);
            LayoutCheck.RunIfRequested(this, game);
        }
        else if (Flag("--screen") == "create")
        {
            CreateScreen create = BuildCreateScreen();

            AddChild(create);
            LayoutCheck.RunIfRequested(this, create);
        }
        else
        {
            AddChild(BuildLoginScreen());
        }

        Screenshot.CaptureIfRequested(this);
    }

    /// <summary>Re-reads the iOS drawable and safe area after every resize and foreground notification.</summary>
    public override void _Notification(int what)
    {
        base._Notification(what);
        // 1004 is Godot's NOTIFICATION_WM_WINDOW_FOCUS_IN; the C# binding exposes the application
        // notification but not this window-only alias.
        if (what == NotificationApplicationFocusIn || what == 1004)
        {
            RefreshDrawableLayout();
        }
    }

    private void RefreshDrawableLayout()
    {
        if (!IsInsideTree()) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        bool oldPortrait = Portrait;
        if (!_orientationWasForced) Portrait = viewport.Y >= viewport.X;
        SafeInsets = ComputeSafeInsets(viewport);
        ApplySafeInsets(this);
        if (oldPortrait != Portrait) LayoutChanged?.Invoke();
    }

    /// <summary>Touches only SafeArea nodes which are still children of the live host.</summary>
    private static void ApplySafeInsets(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MarginContainer safe && safe.Name.ToString() == "SafeArea" && GodotObject.IsInstanceValid(safe))
            {
                safe.AddThemeConstantOverride("margin_left", SafeInsets.Left);
                safe.AddThemeConstantOverride("margin_top", SafeInsets.Top);
                safe.AddThemeConstantOverride("margin_right", SafeInsets.Right);
                safe.AddThemeConstantOverride("margin_bottom", SafeInsets.Bottom);
            }
            ApplySafeInsets(child);
        }
    }

    private static void StayOutOfTheWay()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        DisplayServer.WindowSetPosition(new Vector2I(-4000, -4000));
    }

    /// <summary>The screen size a run asks for, as <c>--size 360x780</c>, or nothing for the default.</summary>
    private static Vector2I? SizeFromCommandLine()
    {
        string given = Flag("--size");
        string[] parts = given.Split('x', 'X');

        return parts.Length == 2
               && int.TryParse(parts[0], out int across)
               && int.TryParse(parts[1], out int down)
               && across > 0
               && down > 0
            ? new Vector2I(across, down)
            : null;
    }

    /// <summary>
    /// Keeps a row from growing wider than a thumb can travel, without forcing that width on a screen that
    /// is narrower than it.
    /// </summary>
    /// <remarks>
    /// Godot sizes a control by its minimum and has no maximum, so a minimum width of the thumb span made
    /// the row hang off both edges of a 640-wide screen — and dragged the status bar out with it, because
    /// the column is as wide as its widest child. The width is a ceiling, not a floor, so the side margins
    /// are worked out again whenever the box changes shape.
    /// </remarks>
    public static MarginContainer Capped(Control inner, int widest)
    {
        MarginContainer box = new();

        inner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(inner);

        box.Resized += () =>
        {
            int side = Mathf.Max(0, Mathf.RoundToInt((box.Size.X - widest) / 2));

            box.AddThemeConstantOverride("margin_left", side);
            box.AddThemeConstantOverride("margin_right", side);
        };

        return box;
    }

    /// <summary>A container whose padding keeps its contents inside the safe area.</summary>
    public static MarginContainer SafeAreaContainer()
    {
        MarginContainer container = new()
        {
            Name = "SafeArea",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };

        container.AddThemeConstantOverride("margin_left", SafeInsets.Left);
        container.AddThemeConstantOverride("margin_top", SafeInsets.Top);
        container.AddThemeConstantOverride("margin_right", SafeInsets.Right);
        container.AddThemeConstantOverride("margin_bottom", SafeInsets.Bottom);

        return container;
    }

    private static (int Left, int Top, int Right, int Bottom) ComputeSafeInsets(Vector2 viewport)
    {
        Vector2I screen = DisplayServer.ScreenGetSize();
        Rect2I safe = DisplayServer.GetDisplaySafeArea();
        if (Engine.Singleton.HasMeta("safe_area_preview"))
        {
            Godot.Collections.Dictionary preview = Engine.Singleton.GetMeta("safe_area_preview").AsGodotDictionary();
            Vector2 device = preview["screen"].AsVector2();
            screen = new Vector2I((int)device.X, (int)device.Y);
            int top = (int)preview["top"].AsDouble(), bottom = (int)preview["bottom"].AsDouble();
            safe = new Rect2I(0, top, screen.X, screen.Y - top - bottom);
        }

        if (screen.X <= 0 || screen.Y <= 0 || safe.Size.X <= 0 || safe.Size.Y <= 0)
        {
            return (Gutter, Gutter, Gutter, Gutter);
        }

        return (
            Gutter + Mathf.RoundToInt(safe.Position.X / (float)screen.X * viewport.X),
            Gutter + Mathf.RoundToInt(safe.Position.Y / (float)screen.Y * viewport.Y),
            Gutter + Mathf.RoundToInt((screen.X - safe.End.X) / (float)screen.X * viewport.X),
            Gutter + Mathf.RoundToInt((screen.Y - safe.End.Y) / (float)screen.Y * viewport.Y));
    }
}
