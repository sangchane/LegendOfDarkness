using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Ui;

/// <summary>
/// 방향키를 누르고 있는 동안: 보고 있지 않은 쪽을 누르면 먼저 돌기만 하고, <see cref="Tuning.TurnHoldSeconds" /> 더
/// 누르고 있어야 걷는다(원작처럼, 사용자 2026-10-02). 이미 보고 있는 쪽이면 곧장 걷는다.
/// </summary>
public sealed class DirectionHold
{
    private Direction? _holding;
    private double _holdFor;
    private bool _turnedFirst;

    /// <summary>지금 잡고 있는 방향과 다른 쪽인가 — 그러면 먼저 돌아야 한다.</summary>
    public bool IsNew(Direction where) => _holding != where;

    /// <summary>새 쪽을 눌렀다. <paramref name="looking" /> 은 그쪽을 이미 보고 있었나.</summary>
    public void Pressing(bool looking) => _turnedFirst = !looking;

    /// <summary>그쪽으로 돌았다 — 이제부터 누른 시간을 잰다.</summary>
    public void Began(Direction where)
    {
        _holding = where;
        _holdFor = 0;
    }

    /// <summary>같은 쪽을 계속 누르고 있다.</summary>
    public void Held(double seconds) => _holdFor += seconds;

    /// <summary>걸어도 되나 — 돌지 않고 시작했거나, 돈 뒤 충분히 눌렀다.</summary>
    public bool MayWalk => !_turnedFirst || _holdFor >= Tuning.TurnHoldSeconds;

    /// <summary>방향키를 모두 뗐다.</summary>
    public void Released() => _holding = null;
}
