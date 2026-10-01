using Godot;

namespace LodClient;

/// <summary>
/// The light that runs around the attack button's rim while auto-hunt is on (사용자 요청, 2026-09-26) — before
/// this, the ring drawn by <see cref="AbilityBar" /> (<c>PaintAttack</c>) stood still, and a glance could not
/// tell "auto-hunt is on" from "the button just has a border". A short bright arc that keeps turning reads as
/// "running" without anyone having to notice the small "자동" label.
/// </summary>
/// <remarks>
/// Drawn with <c>_Draw</c> rather than a shader — one twelve-point arc, redrawn only while it is actually
/// spinning, costs nothing worth measuring on a phone. While a person's own hand has paused auto-hunt for a
/// few seconds (<see cref="AbilityBar.ShowAutoHunt" />'s <c>paused</c>), the arc keeps its full colour but turns
/// four times slower — the button's ring and "자동" tag already dim to say "paused" (<c>PaintAttack</c>), so
/// slowing the light adds a second, different signal ("still running, just resting") instead of repeating the
/// same dimming twice.
/// </remarks>
public sealed partial class AutoHuntRing : Control
{
    /// <summary>How much of the rim lights up at once — enough to read as an arc, not so much it hides the ring.</summary>
    private const float SweepRadians = Mathf.Pi / 3f;

    private const float FullSpeed = Mathf.Tau; // 초당 한 바퀴.
    private const float PausedSpeed = FullSpeed / 4f;

    // 강조색(Greybox.Accent) 그대로 쓰면 공격 단추 속 칠과 정확히 같은 색이라, 테두리 위에 그려도 "빛"이 아니라
    // 테두리에 뚫린 구멍처럼 보였다(실제로 찍어 보고 발견) — 같은 색 갈래를 밝힌 색이라야 테두리(무딘 회색)와
    // 속(강조색) 둘 다보다 밝게 도드라진다.
    private static readonly Color Light = Greybox.Accent.Lightened(0.55f);

    private float _angle;
    private bool _on;
    private bool _paused;

    public AutoHuntRing()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Starts, stops or slows the spin. Stopping also hides the arc — it does not just freeze in place.</summary>
    public void Show(bool on, bool paused)
    {
        _on = on;
        _paused = paused;
        Visible = on;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_on)
        {
            return;
        }

        _angle = Mathf.Wrap(_angle + (float)delta * (_paused ? PausedSpeed : FullSpeed), 0f, Mathf.Tau);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_on)
        {
            return;
        }

        float radius = Size.X / 2f - 1.5f;
        Vector2 centre = Size / 2f;

        // 4 — 테두리(3)보다 살짝 두꺼워 그 자리의 무딘 회색을 완전히 덮는다.
        DrawArc(centre, radius, _angle, _angle + SweepRadians, 12, Light, 4f, antialiased: true);
    }
}
