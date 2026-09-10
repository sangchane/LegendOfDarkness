namespace Lod.Mobile.Core.Art;

/// <summary>The four ways a character can face, named as the game names them.</summary>
public enum Direction
{
    North,
    East,
    South,
    West
}

/// <summary>
/// Which of the two drawings an action file holds. The original never drew four directions: it drew a
/// figure seen from behind and one seen from the front, and mirrors those for the other two.
/// </summary>
public enum Side
{
    Back,
    Front
}

/// <summary>
/// How to draw one direction: which of the two sets, and whether to flip it left to right.
/// </summary>
/// <remarks>
/// The floor is laid in diamonds, so the game's compass points read as screen diagonals — north is up and
/// to the right. See docs/original-sprite-animation.md.
/// </remarks>
public readonly record struct Facing(Side Side, bool Mirror)
{
    /// <summary>How far one step moves on screen: half a tile across and half a tile down.</summary>
    public const int StepX = 28;

    public const int StepY = 13;

    public static Facing Of(Direction direction) => direction switch
    {
        Direction.North => new Facing(Side.Back, Mirror: false),
        Direction.West => new Facing(Side.Back, Mirror: true),
        Direction.East => new Facing(Side.Front, Mirror: false),
        Direction.South => new Facing(Side.Front, Mirror: true),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "알 수 없는 방향입니다.")
    };

    /// <summary>How one step changes the tile the character stands on, as the server counts them.</summary>
    public static (int Column, int Row) TileStep(Direction direction) => direction switch
    {
        Direction.North => (0, -1),
        Direction.East => (1, 0),
        Direction.South => (0, 1),
        Direction.West => (-1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "알 수 없는 방향입니다.")
    };

    public static (int X, int Y) Step(Direction direction) => direction switch
    {
        Direction.North => (StepX, -StepY),
        Direction.East => (StepX, StepY),
        Direction.South => (-StepX, StepY),
        Direction.West => (-StepX, -StepY),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "알 수 없는 방향입니다.")
    };
}

/// <summary>
/// Where standing and walking sit inside a wardrobe piece's <c>…01.epf</c>: ten frames, one standing pose
/// and four walking ones for each of the two sides.
/// </summary>
/// <remarks>
/// Skills and spells are not laid out this way — their starts and lengths come from skill.tbl, which is why
/// halving a file is the wrong rule in general. See docs/original-sprite-animation.md §3.
/// </remarks>
public static class WalkMotion
{
    public const int WalkFrames = 4;

    /// <summary>How many drawings one swing takes.</summary>
    public const int StrikeFrames = 2;

    public static int Stand(Side side) => side == Side.Back ? 0 : 5;

    public static int Walk(Side side, int step) =>
        (side == Side.Back ? 1 : 6) + (((step % WalkFrames) + WalkFrames) % WalkFrames);

    /// <summary>
    /// A plain blow. It is drawn in a file of its own — the one ending 02 — which holds only this, so the
    /// frames start again from zero rather than following the walk.
    /// </summary>
    /// <remarks>
    /// The reference client says the same numbers (paper-doll-animations.ts: Attack startUp 0, startDown 2,
    /// two frames), and so does docs/original-sprite-animation.md section 3.3.
    /// </remarks>
    public static int Strike(Side side, int step) =>
        (side == Side.Back ? 0 : StrikeFrames)
        + (((step % StrikeFrames) + StrikeFrames) % StrikeFrames);
}
