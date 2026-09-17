using Godot;

namespace LodClient;

/// <summary>
/// A button the second thumb can press too. Godot turns only the first finger on the screen into a mouse, and a plain
/// <see cref="Button" /> listens only to the mouse — so while one thumb held the movement pad down, the attack and the
/// skills under the other thumb took no notice (tried: the touch reaches the button and the button drops it).
/// </summary>
/// <remarks>
/// The first finger is still left to the button itself, which already has it as a mouse; handling it here as well
/// would press twice.
/// </remarks>
public partial class ThumbButton : Button
{
    private readonly HashSet<int> _fingers = [];

    /// <summary>Whether any finger — the first, as a mouse, or another — is holding the button down.</summary>
    public bool Held => IsPressed() || _fingers.Count > 0;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventScreenTouch { Index: > 0 } touch || Disabled)
        {
            return;
        }

        if (touch.Pressed)
        {
            if (_fingers.Add(touch.Index))
            {
                EmitSignal(BaseButton.SignalName.ButtonDown);
            }
        }
        else if (_fingers.Remove(touch.Index))
        {
            EmitSignal(BaseButton.SignalName.ButtonUp);

            // 마우스 버튼과 같이, 버튼 위에서 뗐을 때만 누른 것으로 친다.
            if (new Rect2(Vector2.Zero, Size).HasPoint(touch.Position))
            {
                EmitSignal(BaseButton.SignalName.Pressed);
            }
        }

        AcceptEvent();
    }
}
