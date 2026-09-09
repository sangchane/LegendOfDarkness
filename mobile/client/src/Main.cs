using Godot;

namespace LodClient;

/// <summary>
/// Greybox host. Applies the one theme every screen shares, keeps content inside the safe area, and mounts
/// the screen under test. No colour or art here: greybox judges layout, reach and text, nothing else.
/// </summary>
public partial class Main : Control
{
    /// <summary>Smallest touch target, in logical units. One unit is one dp at this project's base size.</summary>
    public const int TouchMinimum = 48;

    /// <summary>Smallest gap between controls, and the least distance kept from the safe-area edge.</summary>
    public const int Gutter = 8;

    private const int BodyFontSize = 16;

    // Windows ships a Korean face; Android and iOS do not have this path. Until a licensed font is bundled,
    // a missing face is reported on screen rather than left to render as empty boxes.
    private const string WindowsKoreanFont = "C:/Windows/Fonts/malgun.ttf";

    public override void _Ready()
    {
        Theme theme = BuildTheme(out string fontName);
        Theme = theme;

        MarginContainer safeArea = new()
        {
            Name = "SafeArea",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };

        AddChild(safeArea);
        ApplySafeAreaInsets(safeArea, GetViewportRect().Size);

        safeArea.AddChild(new LoginScreen(fontName));

        Screenshot.CaptureIfRequested(this);
    }

    private Theme BuildTheme(out string fontName)
    {
        Theme theme = new() { DefaultFontSize = BodyFontSize };

        if (Godot.FileAccess.FileExists(WindowsKoreanFont))
        {
            FontFile korean = new();
            korean.LoadDynamicFont(WindowsKoreanFont);
            theme.DefaultFont = korean;
            fontName = "Malgun Gothic";
        }
        else
        {
            // Godot's built-in face has no Hangul, so this is the state where Korean text breaks.
            fontName = "없음 — 한글이 깨집니다";
        }

        return theme;
    }

    /// <summary>
    /// Keeps content clear of notches and the home indicator. On a desktop window the safe area is the whole
    /// window, so the gutter is what remains.
    /// </summary>
    private static void ApplySafeAreaInsets(MarginContainer container, Vector2 viewport)
    {
        Vector2I screen = DisplayServer.ScreenGetSize();
        Rect2I safe = DisplayServer.GetDisplaySafeArea();

        int left = Gutter;
        int top = Gutter;
        int right = Gutter;
        int bottom = Gutter;

        if (screen.X > 0 && screen.Y > 0 && safe.Size.X > 0 && safe.Size.Y > 0)
        {
            left += Mathf.RoundToInt(safe.Position.X / (float)screen.X * viewport.X);
            top += Mathf.RoundToInt(safe.Position.Y / (float)screen.Y * viewport.Y);
            right += Mathf.RoundToInt((screen.X - safe.End.X) / (float)screen.X * viewport.X);
            bottom += Mathf.RoundToInt((screen.Y - safe.End.Y) / (float)screen.Y * viewport.Y);
        }

        container.AddThemeConstantOverride("margin_left", left);
        container.AddThemeConstantOverride("margin_top", top);
        container.AddThemeConstantOverride("margin_right", right);
        container.AddThemeConstantOverride("margin_bottom", bottom);
    }
}
