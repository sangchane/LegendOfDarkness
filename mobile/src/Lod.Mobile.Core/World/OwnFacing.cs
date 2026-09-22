using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.World;

/// <summary>
/// Which way our own figure stands when the server, not our own walking, turns it.
/// </summary>
/// <remarks>
/// <para>
/// A step the server allows is not answered to whoever took it (Sprite.Walk shows 0x0C to everyone nearby but
/// ourselves), so the way the server last described us (<see cref="WorldClient.Self" />) runs a step behind the
/// way we walk. It is also rewritten by things that are not the server turning us — a turn we asked for coming back
/// to us (0x11 goes to every aisling nearby, ourselves included) or a description sent before our last step reached
/// the server. Standing the figure the way that record says whenever it changed (the first try at 이형환위,
/// 2026-09-22) stands a figure walking west the way it faced north: two drawings out of four.
/// </para>
/// <para>
/// When the server does turn us it also moves us. 이형환위 lands two tiles past the target and turns round
/// (MonkStrike.Step → Client.Refresh, which describes us (0x33) and then says where we are (0x04)). So its word on
/// our facing is taken only with a location that puts us somewhere we did not walk to, and only from a description
/// of us standing on that very tile.
/// </para>
/// </remarks>
public static class OwnFacing
{
    /// <summary>The way the server stood us, or nothing when the way we walked is still ours to keep.</summary>
    /// <param name="described">How the server last described us.</param>
    /// <param name="placed">Where the server now says we stand (0x04).</param>
    /// <param name="walkedTo">The tile our own walking had us on before that.</param>
    public static Direction? PutBy(Character? described, Tile placed, Tile walkedTo) =>
        placed != walkedTo && described is { } self && self.Where == placed ? self.Facing : null;
}
