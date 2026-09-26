namespace Lod.Mobile.Core.Art;

/// <summary>
/// How things lying on one tile are stacked when drawn: gold at the bottom, items on top of it
/// (user, 2026-09-26 — "돈은 항상 아이템 밑에"). The floor is y-sorted, and gold and an item on one tile share a
/// y, so whichever arrived later was drawn on top — and a coin pile arrives anew each time piles merge.
/// </summary>
public static class GroundPile
{
    /// <summary>
    /// Gold's pictures: <c>MoneySprites</c> 0x89~0x8E plus the 0x8000 <c>Money.Create</c> adds.
    /// </summary>
    public static bool IsGold(int sprite) => sprite is >= 32905 and <= 32910;

    /// <summary>
    /// How far up gold's sort point moves so it is drawn before anything else on its tile. Far below a pixel,
    /// and far below the half-tile to the row behind, so nothing visibly moves and no row changes order.
    /// </summary>
    public static float SortNudge(int sprite) => IsGold(sprite) ? -0.01f : 0f;
}
