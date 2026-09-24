namespace Lod.Mobile.Core.Art;

/// <summary>
/// How a screen makes room for the on-screen keyboard without squashing itself. Nothing is resized: the screen slides
/// up just far enough that the field being typed into — and the button that finishes the job, when there is room for
/// both — clear the keyboard, and never so far that the field goes under the notch.
/// </summary>
/// <remarks>
/// The old way shrank the whole screen to the part the keyboard left. The form had more rows than that part could
/// hold, so on a landscape iPhone (393 tall, keyboard about half of it) the fields were pushed off the top or under the
/// keyboard, and the rest was squeezed (사용자, 2026-09-24).
/// </remarks>
public static class KeyboardFit
{
    /// <summary>
    /// The keyboard's height in the screen's own units. The phone reports it in pixels of the whole screen; the game is
    /// laid out in fewer, larger units.
    /// </summary>
    public static float Covered(int keyboardPixels, int screenPixels, float viewportHeight) =>
        keyboardPixels > 0 && screenPixels > 0 ? keyboardPixels / (float)screenPixels * viewportHeight : 0;

    /// <summary>
    /// How far to slide the screen up. <paramref name="wantedBottom"/> is the lowest thing worth seeing (the button
    /// that finishes, or the field itself); it is given up before the field's own top would pass <paramref name="safeTop"/>.
    /// The field's bottom always clears the keyboard, even when that costs its top.
    /// </summary>
    public static float Slide(float fieldTop, float fieldBottom, float wantedBottom, float keyboardTop, float safeTop, float gap)
    {
        float least = fieldBottom + gap - keyboardTop;
        float wanted = Math.Max(fieldBottom, wantedBottom) + gap - keyboardTop;
        float most = fieldTop - safeTop;

        return Math.Max(0, Math.Max(least, Math.Min(wanted, most)));
    }

    /// <summary>
    /// How tall a list may stay when a window has to fit between <paramref name="top"/> and the keyboard. The rest of
    /// the window (<paramref name="fixedHeight"/>) keeps its size; the list gives up what is missing, down to nothing.
    /// </summary>
    public static float ListRoom(float preferred, float top, float keyboardTop, float fixedHeight) =>
        Math.Clamp(keyboardTop - top - fixedHeight, 0, preferred);
}
