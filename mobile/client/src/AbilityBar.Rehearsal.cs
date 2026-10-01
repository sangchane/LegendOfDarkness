using Godot;
using Lod.Mobile.Core.Model;

namespace LodClient;

/// <summary>기술 부채꼴 — 손 없이 확인하는 시험 코드(`--skill` `--slot-hold` `--auto-hunt-preview` `--learn-preview` 일 때만 돈다).</summary>
public sealed partial class AbilityBar : Control
{
    private int _rehearsedHold; // --slot-hold: 손 없이 확인할 때 프레임을 센다.
    private int _rehearsedAutoHunt; // --auto-hunt-preview: 서버 없이 켜짐 표시만 그려 볼 때 프레임을 센다.

    /// <summary>Presses one slot from outside — for a run with nobody watching (<c>--skill 1</c>, <c>--skill m1</c> for a spell).</summary>
    public void Press(int index, bool spell = false)
    {
        if (_spells != spell)
        {
            _spells = spell;
            _page = 0;
            Redraw();
        }

        if (index >= 0 && index < _slots.Length)
        {
            Use(index);
        }
    }

    /// <summary>--slot-hold N: 서버 없이 확인할 때, 자리를 잡고 잠시 뒤 N번째 칸을 길게 누른 셈 친다.</summary>
    private void RehearseSlotHold()
    {
        if (Main.SlotHold > 0 && Main.SlotHold <= _slots.Length && !_longHeld[Main.SlotHold - 1] && ++_rehearsedHold == 90)
        {
            _longHeld[Main.SlotHold - 1] = true;
            OpenPicker(Main.SlotHold - 1);
        }
    }

    /// <summary>--auto-hunt-preview: 서버가 없어 GameScreen 이 실제로 켤 수 없으니, 여기서 스스로 켜짐 표시만 그려 본다.</summary>
    private void RehearseAutoHunt()
    {
        if (Main.AutoHuntPreview && ++_rehearsedAutoHunt == 90)
        {
            ShowAutoHunt(true);
        }
    }

    /// <summary>--learn-preview 직업:레벨 — 서버 없이, 표에서 그 레벨까지를 배운 셈 친다. 인자가 없으면 받은 그대로.</summary>
    private static (IReadOnlyList<LearnedSkill> Skills, IReadOnlyList<LearnedSpell> Spells) RehearsedLearning(
        IReadOnlyList<LearnedSkill> skills, IReadOnlyList<LearnedSpell> spells)
    {
        if (Main.LearnPreview is not { } preview)
        {
            return (skills, spells);
        }

        List<LadderStep> had = [.. Main.Ladder.Steps.Where(step => step.Path == preview.Path && step.Level <= preview.Level)];
        skills = [new LearnedSkill(1, 1, "Assail (Lev:1/100)"), .. had.Where(step => !step.Spell).Select((step, at) => new LearnedSkill(73 + at, step.Icon, $"{step.Name} (Lev:1/100)"))];
        spells = [.. had.Where(step => step.Spell).Select((step, at) => new LearnedSpell(1 + at, step.Icon, SpellTargetType.NoTarget, $"{step.Name} (Lev:1/100)", string.Empty, 1))];
        return (skills, spells);
    }

    /// <summary>--learn-preview 일 때, 직업을 모르면 그 직업·레벨로 친다.</summary>
    private static (int? Path, int Level) RehearsedStanding((int? Path, int Level) standing) =>
        standing.Path is null && Main.LearnPreview is { } preview ? (preview.Path, preview.Level) : standing;
}
