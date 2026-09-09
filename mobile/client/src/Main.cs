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

    // Windows ships a Korean face; Android and iOS do not have this path. Until a licensed font is bundled,
    // a missing face is reported on screen rather than left to render as empty boxes.
    private const string WindowsKoreanFont = "C:/Windows/Fonts/malgun.ttf";

    /// <summary>Insets that keep text and controls clear of notches and the home indicator.</summary>
    public static (int Left, int Top, int Right, int Bottom) SafeInsets { get; private set; } =
        (Gutter, Gutter, Gutter, Gutter);

    /// <summary>The font actually in use. Printed on screen, because Hangul depends on it.</summary>
    public static string FontName { get; private set; } = "확인 전";

    public override void _Ready()
    {
        Theme = BuildTheme();
        SafeInsets = ComputeSafeInsets(GetViewportRect().Size);

        AddChild(ScreenFromCommandLine() == "game" ? new GameScreen() : new LoginScreen());

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

    private static string ScreenFromCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--screen")
            {
                return args[index + 1];
            }
        }

        return "login";
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
