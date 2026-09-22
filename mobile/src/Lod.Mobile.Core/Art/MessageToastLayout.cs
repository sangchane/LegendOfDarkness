namespace Lod.Mobile.Core.Art;

/// <summary>
/// The width of the landscape notice toast. It deliberately follows the three-column direction pad: notices may cover
/// the thumb area, but must never grow into the play area between the two thumb clusters.
/// </summary>
public static class MessageToastLayout
{
    /// <summary>Three touch targets with the two gaps between them.</summary>
    public static int DirectionPadWidth(int touchTarget, int gap) => (touchTarget * 3) + (gap * 2);
}
