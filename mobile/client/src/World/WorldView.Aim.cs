using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Model;

namespace LodClient;

/// <summary>
/// 월드 화면 — 괴물을 겨누는 마법(직업 표의 「적」)의 대상 고르기(사용자 2026-10-04, 모바일 롤처럼). 탭은 고른 괴물, 없으면 가장 가까운
/// 괴물(<see cref="EnemyTarget" />). 막대 칸을 끌면 내 자리에서 끈 쪽으로 <see cref="AimScale" /> 배 나간 곳에 가장 가까운 괴물(<see cref="AimEnemy" />).
/// 고른 괴물에는 늘 쓰던 고름 표시가 선다. 끌어서 생긴 표시는 끌기가 끝난 뒤 <see cref="AimHoldSeconds" /> 초만 유효하다(사용자 2026-10-04) —
/// 그 안에 다시 탭하면 그 괴물에 나가고, 지나면 끌기 전에 고른 이로 돌아간다.
/// </summary>
public sealed partial class WorldView
{
    /// <summary>끈 거리(화면 픽셀)를 월드에서 몇 배로 보나 — 엄지로 짧게 끌어도 화면 끝 괴물까지 닿게.</summary>
    private const float AimScale = 3;

    /// <summary>끌어서 생긴 고름 표시가 끌기가 끝난 뒤 남는 초.</summary>
    private const double AimHoldSeconds = 3;

    private uint _beforeAim;
    private uint _aimed;
    private double _aimLeft = -1;

    /// <summary>기술 막대에 놓인 마법 — GameScreen 이 AbilityBar 에서 이어 준다. 자동 사냥이 이것만 쓴다.</summary>
    public System.Func<IReadOnlyList<LearnedSpell>>? BarSpells { get; set; }

    /// <summary>이 마법이 괴물을 겨누나(직업 표의 「적」).</summary>
    public System.Func<LearnedSpell, bool>? AimsAtEnemy { get; set; }

    /// <summary>고른 이가 적대 괴물이면 그대로, 아니면 가장 가까운 적대 괴물을 골라 표시한다. 없으면 0.</summary>
    public uint EnemyTarget()
    {
        if (Enemies().Any(one => one.Serial == _target))
        {
            return _target;
        }

        return Pick(_player.Position);
    }

    /// <summary>끌어 조준한 대상 — 고른 이가 적대 괴물일 때만, 아니면 0(그쪽에 괴물이 없어 전에 고른 사람·NPC가 남은 경우).</summary>
    public uint AimedEnemy() => Enemies().Any(one => one.Serial == _target) ? _target : 0;

    /// <summary>내 자리에서 <paramref name="drag" /> 쪽으로 나간 곳에 가장 가까운 적대 괴물을 골라 표시한다. 없으면 0.</summary>
    public uint AimEnemy(Vector2 drag)
    {
        // 끌기를 새로 시작할 때만 그 전 고름을 기억한다 — 유효 시간 안에 또 끌면 처음 것을 그대로 둔다.
        if (_aimed == 0)
        {
            _beforeAim = _target;
        }

        _aimLeft = -1;
        uint picked = Pick(_player.Position + (drag * AimScale));
        _aimed = picked != 0 ? picked : _aimed;

        return picked;
    }

    /// <summary>끌기가 끝났다(쏘았든 그만두었든) — 이때부터 끌어서 생긴 표시의 유효 시간을 센다.</summary>
    public void AimEnded()
    {
        if (_aimed != 0)
        {
            _aimLeft = AimHoldSeconds;
        }
    }

    /// <summary>유효 시간이 지나면 끌기 전 고름으로 — 그사이 다른 것을 골랐으면 그대로 둔다.</summary>
    private void KeepAim(double delta)
    {
        if (_aimLeft < 0 || (_aimLeft -= delta) > 0)
        {
            return;
        }

        if (_target == _aimed)
        {
            _target = _beforeAim;
            Mark();
        }

        (_aimed, _beforeAim, _aimLeft) = (0, 0, -1);
    }

    private uint Pick(Vector2 near)
    {
        uint best = 0;
        float shortest = float.MaxValue;

        foreach ((uint serial, Actor actor) in Enemies())
        {
            float distance = actor.Position.DistanceSquaredTo(near);

            if (distance < shortest)
            {
                shortest = distance;
                best = serial;
            }
        }

        if (best != 0)
        {
            _target = best;
            TargetByHand = true;
            Mark();
        }

        return best;
    }

    /// <summary>그려진 적대 괴물(서버가 보여 준 것만).</summary>
    private IEnumerable<(uint Serial, Actor Actor)> Enemies()
    {
        if (server is null)
        {
            return [];
        }

        HashSet<uint> hostile = [.. server.Creatures.Where(one => one.Kind == CreatureKind.Hostile).Select(one => one.Serial)];

        return _herd.Where(one => hostile.Contains(one.Key)).Select(one => (one.Key, one.Value));
    }
}
