namespace Lod.Mobile.Core.World;

/// <summary>
/// Whether a still-held press has crossed a hold threshold — one instance per button. Pulled out of
/// <c>AbilityBar</c> (사용자 요청, 2026-09-26: 공격 단추를 0.5초 길게 눌러 자동 사냥을 켜고 끈다) so the
/// long-press-vs-short-press rule is a plain, testable fact instead of a few fields read only from Godot code.
/// </summary>
public sealed class LongPress(TimeSpan threshold)
{
    private TimeSpan _downAt;
    private bool _down;
    private bool _firedLong;

    /// <summary>The finger touched down at <paramref name="now"/>.</summary>
    public void Down(TimeSpan now)
    {
        _down = true;
        _firedLong = false;
        _downAt = now;
    }

    /// <summary>The finger lifted.</summary>
    public void Up() => _down = false;

    /// <summary>
    /// Call every frame while held. True exactly once, the frame the threshold is first crossed — the caller acts on
    /// the long press right then (open a list, toggle a setting); it does not wait for release.
    /// </summary>
    public bool CrossedThreshold(TimeSpan now)
    {
        if (!_down || _firedLong || now - _downAt < threshold)
        {
            return false;
        }

        _firedLong = true;
        return true;
    }

    /// <summary>
    /// Whether the release just now should still count as an ordinary short press — false once
    /// <see cref="CrossedThreshold"/> already fired for this press, so the long action is not followed by the short
    /// one too (the attack button's long press does not also throw a blow).
    /// </summary>
    public bool ShortPress => !_firedLong;
}
