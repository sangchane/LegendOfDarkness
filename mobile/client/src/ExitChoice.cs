using System;
using Godot;

namespace LodClient;

/// <summary>
/// What the top row's [종료] opens: a small stone plate with [로그아웃] · [게임 종료] · [취소], just under the top
/// row. It covers the whole screen with an invisible catch so a tap anywhere else closes it — the plate itself keeps
/// clear of the pad and the attack fan (user, 2026-09-24): upright it hangs under the button, where only the map is;
/// on its side the fan and the party column fill the right edge below the top row, so it stands in the middle.
/// </summary>
/// <remarks>
/// iOS does not let an app close itself (Godot's <c>SceneTree.Quit</c> does nothing there, following Apple's
/// guidelines), so on iOS [게임 종료] logs out instead — back to the login screen.
/// </remarks>
public sealed partial class ExitChoice : Control
{
    private const int Width = 168;

    private readonly PanelContainer _plate;

    public ExitChoice()
    {
        Name = "ExitChoice";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);

        VBoxContainer list = new();
        list.AddThemeConstantOverride("separation", Main.Gutter);

        LogOut = Choice("로그아웃");
        Quit = Choice("게임 종료");
        Cancel = Choice("취소");

        // 끝내는 단추 하나만 강조색 — 창마다 하나(Greybox.Commit).
        Greybox.Commit(Quit);

        list.AddChild(LogOut);
        list.AddChild(Quit);
        list.AddChild(Cancel);

        PanelContainer inner = new();
        inner.AddThemeStyleboxOverride("panel", Greybox.Plate());
        inner.AddChild(list);

        _plate = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0) };
        _plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        _plate.AddChild(inner);
        AddChild(_plate);

        Cancel.Pressed += Shut;
    }

    public Button LogOut { get; }

    public Button Quit { get; }

    public Button Cancel { get; }

    /// <summary>Whether [게임 종료] can really close the app here. It cannot on iOS.</summary>
    public static bool CanQuit => OS.GetName() != "iOS";

    /// <summary>
    /// Opens the plate under <paramref name="under" /> (the button's screen rectangle) — its right edge on the
    /// button's, or across the middle of the screen when <paramref name="centred" />.
    /// </summary>
    public void Open(Rect2 under, bool centred)
    {
        Visible = true;
        _plate.ResetSize();

        Vector2 size = _plate.GetCombinedMinimumSize();
        Vector2 screen = GetViewportRect().Size;
        float x = Math.Clamp(centred ? (screen.X - size.X) / 2 : under.End.X - size.X,
            Main.Gutter, Math.Max(Main.Gutter, screen.X - size.X - Main.Gutter));
        float y = Math.Min(under.End.Y + Main.Gutter, Math.Max(0, screen.Y - size.Y - Main.Gutter));

        _plate.GlobalPosition = new Vector2(x, y);
        GD.Print($"GREYBOX_EXIT_CHOICE at {_plate.GetGlobalRect()} screen {screen}");
    }

    public void Shut() => Visible = false;

    public override void _GuiInput(InputEvent @event)
    {
        // 판 밖을 누르면 닫는다. 판 안의 단추는 제 누름을 먼저 가져간다.
        if (@event is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
        {
            Shut();
            AcceptEvent();
        }
    }

    private static Button Choice(string text)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(Width - 2 * Main.Gutter, Main.TouchMinimum)
        };

        Greybox.Plain(button);

        return button;
    }
}
