using Godot;

namespace LodClient;

/// <summary>앱 시작 — 한글 글꼴 찾기와 테마.</summary>
public partial class Main : Control
{
    // Windows ships a Korean face; Android and iOS do not have this path. Until a licensed font is bundled,
    // a missing face is reported on screen rather than left to render as empty boxes.
    private const string WindowsKoreanFont = "C:/Windows/Fonts/malgun.ttf";

    // Desktops other than Windows may still have a Korean face. Asking the system for one by name is the
    // only way to tell — the question is whether Hangul draws, not whether one particular file exists.
    private static readonly string[] SystemKoreanFonts =
        { "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Malgun Gothic", "NanumGothic" };

    /// <summary>The font actually in use. Printed on screen, because Hangul depends on it.</summary>
    public static string FontName { get; private set; } = "확인 전";

    /// <summary>
    /// The first system face that actually carries Hangul, or null when this machine has none. Names are
    /// tried in order because the engine reports a match for a family it can substitute, not only for one
    /// it has; the glyph check is what decides.
    /// </summary>
    private static SystemFont? SystemKoreanFont()
    {
        foreach (string name in SystemKoreanFonts)
        {
            SystemFont candidate = new() { FontNames = new[] { name } };

            if (candidate.HasChar('가'))
            {
                return candidate;
            }
        }

        return null;
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
        else if (SystemKoreanFont() is SystemFont system)
        {
            // A desktop that is not Windows can still have a Korean face — macOS does. Saying "Hangul
            // breaks" while Hangul is plainly drawing on this very line is worse than saying nothing.
            theme.DefaultFont = system;
            FontName = $"시스템 {system.FontNames[0]}";
        }
        else
        {
            // Godot's built-in face has no Hangul, so this is the state where Korean text breaks.
            // Phones land here: neither the Windows path nor a system Korean face exists.
            FontName = "없음 — 한글이 깨집니다";
        }

        // Buttons sit over the map now. The engine default is translucent, which the floor shows straight
        // through, so every button carries its own opaque plate.
        theme.SetStylebox("normal", "Button", Greybox.Plate());
        theme.SetStylebox("hover", "Button", Greybox.Plate());
        theme.SetStylebox("pressed", "Button", Greybox.Surface());
        theme.SetStylebox("focus", "Button", Greybox.Plate());
        theme.SetStylebox("disabled", "Button", Greybox.Surface());
        // Opaque fields keep the stone title/frame out of small input text, including SpinBox editors.
        foreach (string state in new[] { "normal", "focus", "read_only" })
        {
            StyleBoxFlat field = Greybox.Surface();
            field.SetCornerRadiusAll(8);
            field.SetContentMarginAll(4);
            if (state == "focus") field.BorderColor = Greybox.Muted;
            theme.SetStylebox(state, "LineEdit", field);
        }
        theme.SetIcon("checked", "CheckBox", Greybox.CheckIcon(true));
        theme.SetIcon("unchecked", "CheckBox", Greybox.CheckIcon(false));

        return theme;
    }
}
