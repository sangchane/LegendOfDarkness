using Godot;

namespace LodClient;

/// <summary>
/// Greybox host. Applies the one theme every screen shares, works out the safe-area insets once, and mounts
/// the screen under test. No colour or art here: greybox judges layout, reach and text, nothing else.
/// </summary>
public partial class Main : Control
{
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

    /// <summary>Where the login server is, unless --server says otherwise.</summary>
    private const string DefaultServer = "127.0.0.1:2610";

    /// <summary>Logical size of a portrait screen. One unit is one dp here too.</summary>
    private static readonly Vector2I PortraitSize = new(360, 780);

    // Windows ships a Korean face; Android and iOS do not have this path. Until a licensed font is bundled,
    // a missing face is reported on screen rather than left to render as empty boxes.
    private const string WindowsKoreanFont = "C:/Windows/Fonts/malgun.ttf";

    /// <summary>Insets that keep text and controls clear of notches and the home indicator.</summary>
    public static (int Left, int Top, int Right, int Bottom) SafeInsets { get; private set; } =
        (Gutter, Gutter, Gutter, Gutter);

    /// <summary>The font actually in use. Printed on screen, because Hangul depends on it.</summary>
    public static string FontName { get; private set; } = "확인 전";

    /// <summary>
    /// Which way the screen is held. Portrait is not landscape squeezed: it has enough height to keep the
    /// controls out of the world, so the screens lay themselves out differently rather than scaling.
    /// </summary>
    public static bool Portrait { get; private set; }

    /// <summary>The login server this run talks to. Pass <c>--server host:port</c> to change it.</summary>
    public static System.Net.IPAddress ServerAddress { get; private set; } = System.Net.IPAddress.Loopback;

    public static int ServerPort { get; private set; }

    /// <summary>
    /// Credentials to fill in and submit without waiting for typing, as <c>--login name:secret</c>. For
    /// checking a build against a local server without a device in hand; empty in a normal run.
    /// </summary>
    public static (string Username, string Password) Rehearsal { get; private set; } = (string.Empty, string.Empty);

    /// <summary>
    /// Steps to walk on their own once the world opens, as <c>--walk NESW</c>. Same purpose as --login: it
    /// lets a build be checked without a hand on the screen.
    /// </summary>
    public static string Rehearse { get; private set; } = string.Empty;

    public override void _Ready()
    {
        Portrait = Flag("--orient") == "portrait";
        ReadServer(Flag("--server"));
        ReadRehearsal(Flag("--login"));
        Rehearse = Flag("--walk");

        if (Portrait)
        {
            GetWindow().ContentScaleSize = PortraitSize;
            DisplayServer.WindowSetSize(PortraitSize * 5 / 4);
        }

        Theme = BuildTheme();
        SafeInsets = ComputeSafeInsets(GetViewportRect().Size);

        AddChild(Flag("--screen") == "game" ? new GameScreen() : new LoginScreen());

        Screenshot.CaptureIfRequested(this);
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

    /// <summary>Reads a value passed after a double dash, as in <c>-- --screen game --orient portrait</c>.</summary>
    private static string Flag(string name)
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == name)
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    /// <summary>Reads <c>host:port</c>, falling back to the local server the run scripts start.</summary>
    private static void ReadServer(string value)
    {
        string[] parts = (value.Length > 0 ? value : DefaultServer).Split(':');

        if (parts.Length != 2
            || !System.Net.IPAddress.TryParse(parts[0], out System.Net.IPAddress? address)
            || !int.TryParse(parts[1], out int port))
        {
            GD.PushWarning($"--server 를 읽을 수 없어 {DefaultServer} 을 씁니다: {value}");
            parts = DefaultServer.Split(':');
            address = System.Net.IPAddress.Parse(parts[0]);
            port = int.Parse(parts[1]);
        }

        ServerAddress = address;
        ServerPort = port;
    }

    private static void ReadRehearsal(string value)
    {
        int split = value.IndexOf(':');

        if (split > 0 && split < value.Length - 1)
        {
            Rehearsal = (value[..split], value[(split + 1)..]);
        }
    }

    private static Theme BuildTheme()
    {
        Theme theme = new() { DefaultFontSize = BodyFontSize };

        if (Godot.FileAccess.FileExists(WindowsKoreanFont))
        {
            FontFile korean = new();
            korean.LoadDynamicFont(WindowsKoreanFont);
            theme.DefaultFont = korean;
            FontName = "Malgun Gothic";
        }
        else
        {
            // Godot's built-in face has no Hangul, so this is the state where Korean text breaks.
            FontName = "없음 — 한글이 깨집니다";
        }

        // Buttons sit over the map now. The engine default is translucent, which the floor shows straight
        // through, so every button carries its own opaque plate.
        theme.SetStylebox("normal", "Button", Greybox.Plate());
        theme.SetStylebox("hover", "Button", Greybox.Plate());
        theme.SetStylebox("pressed", "Button", Greybox.Surface());
        theme.SetStylebox("focus", "Button", Greybox.Plate());
        theme.SetStylebox("disabled", "Button", Greybox.Surface());

        return theme;
    }

    private static (int Left, int Top, int Right, int Bottom) ComputeSafeInsets(Vector2 viewport)
    {
        Vector2I screen = DisplayServer.ScreenGetSize();
        Rect2I safe = DisplayServer.GetDisplaySafeArea();

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
