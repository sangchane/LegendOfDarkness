using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 기술 막대 — 괴물을 겨누는 마법(직업 표의 「적」, 다라밀공)을 끌어 조준한다(모바일 롤처럼, 사용자 2026-10-04). 칸을 누른 채 손가락을
/// 단추 밖으로 끌면 조준이 시작되고 끈 방향의 괴물이 골라진다(<see cref="AimMoved" />). 떼면 그 괴물에 쏘고(<see cref="AimReleased" />),
/// 단추 위로 돌아와 떼면 그만둔다(<see cref="AimCancelled" />). 끌지 않고 가만히 0.5초면 지금처럼 배치 목록.
/// </summary>
public partial class AbilityBar
{
    /// <summary>손가락이 이만큼 움직여야 조준으로 본다 — 누르다 조금 흔들린 것은 탭이다.</summary>
    private const float AimStart = 24;

    private readonly Vector2[] _pressedAt = new Vector2[AbilityFan.PerPage];
    private readonly bool[] _aiming = new bool[AbilityFan.PerPage];

    /// <summary>이 마법이 괴물을 겨누나 — 화면이 직업 표로 답한다.</summary>
    public Func<LearnedSpell, bool>? AimsAtEnemy { get; set; }

    /// <summary>조준 중 — 누른 곳에서 손가락까지(화면 픽셀).</summary>
    public event Action<Vector2>? AimMoved;

    /// <summary>조준한 채 단추 밖에서 뗐다 — 이 마법 칸을 쏜다.</summary>
    public event Action<int>? AimReleased;

    /// <summary>단추 위로 돌아와 뗐다 — 쏘지 않는다.</summary>
    public event Action? AimCancelled;

    private void OnSlotMotion(int index, InputEvent @event)
    {
        if (@event is not InputEventMouseMotion || !_down[index] || (_longHeld[index] && !_aiming[index])
            || _drawn[index] is not LearnedSpell spell || AimsAtEnemy?.Invoke(spell) != true)
        {
            return;
        }

        Vector2 drag = GetGlobalMousePosition() - _pressedAt[index];

        if (!_aiming[index] && drag.Length() < AimStart)
        {
            return;
        }

        // 조준이 잡히면 길게 누른 것처럼 다뤄 손을 뗄 때 오는 누름(Use)은 쓰지 않는다 — 쏘는 것은 OnSlotUp 이 정한다.
        _aiming[index] = true;
        _longHeld[index] = true;
        AimMoved?.Invoke(drag);
    }

    /// <summary>손을 뗐다 — 조준 중이었으면 단추 밖이면 쏘고, 단추 위면 그만둔다.</summary>
    private void EndAim(int index)
    {
        if (!_aiming[index])
        {
            return;
        }

        _aiming[index] = false;

        // 단추 위에서 뗐거나, 식는 중이면 쏘지 않는다(짧게 누를 때 Use() 가 가리는 것과 같이).
        if (_slots[index].GetGlobalRect().HasPoint(GetGlobalMousePosition()) || _drawn[index] is not LearnedSpell spell
            || (Cooling is { } ask && ask(false, spell.Slot) > 0))
        {
            AimCancelled?.Invoke();
            return;
        }

        AimReleased?.Invoke(spell.Slot);
    }

    /// <summary>기술 막대에 놓인 마법, 놓인 차례대로 — 비운 칸의 것은 빠진다. 자동 사냥이 이것만 쓴다.</summary>
    public IReadOnlyList<LearnedSpell> PlacedSpells()
    {
        int capacity = AbilityFan.Pages(_learnedSpells.Count) * AbilityFan.PerPage;
        return [.. _spellArrangement.Fill(_learnedSpells, spell => spell.Slot, capacity).OfType<LearnedSpell>()];
    }
}
