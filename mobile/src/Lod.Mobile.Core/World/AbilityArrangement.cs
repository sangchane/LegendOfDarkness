namespace Lod.Mobile.Core.World;

/// <summary>
/// Where a long press moves one learned skill or spell to a chosen ability-bar slot (사용자 요청, 2026-09-25) —
/// pure bookkeeping, no Godot, so the swap/clear/autofill rules are tested without a screen.
/// </summary>
/// <remarks>
/// A position is <c>page * AbilityFan.PerPage + index</c>. A position nobody has touched fills itself from
/// whatever is learned and not placed anywhere else, in the server's own order — the way a freshly learned
/// skill has always landed in an empty slot. A touched position either names the learned item's own
/// <see cref="LearnedSkill.Slot" />/<see cref="LearnedSpell.Slot" /> to show, or <see cref="Cleared" /> for a
/// slot a person emptied on purpose, which must stay empty rather than being refilled from the pool.
///
/// Clearing a slot that was only ever showing something by the pool's own default (never explicitly placed)
/// used to let that very skill re-enter the pool and pop back into the next open position, shoving everything
/// after it over by one (사용자 버그 리포트, 2026-09-26). <see cref="Clear" /> now also remembers the slot that
/// was cleared in <see cref="RemovedSlots" />, so the pool never offers it again — only picking it from the
/// list (<see cref="Place" />) brings it back.
/// </remarks>
public sealed class AbilityArrangement
{
    public const int Cleared = -1;

    private readonly Dictionary<int, int> _positions = [];
    private readonly HashSet<int> _removedSlots = [];

    /// <summary>Every touched position, for saving — position, then what is there (a Slot number, or <see cref="Cleared"/>).</summary>
    public IReadOnlyDictionary<int, int> Positions => _positions;

    /// <summary>Slots a person cleared on purpose — kept out of the pool everywhere, not just at the position they were cleared from, until picked again.</summary>
    public IReadOnlySet<int> RemovedSlots => _removedSlots;

    public void Assign(int position, int slot)
    {
        _removedSlots.Remove(slot);
        _positions[position] = slot;
    }

    /// <summary>
    /// Empties <paramref name="position"/>. When <paramref name="slot"/> (what was showing there — 0 if
    /// nothing, or unknown, such as an old save line) is given, that skill is barred from the pool everywhere
    /// until someone picks it again, so it cannot pop back into a different empty slot.
    /// </summary>
    public void Clear(int position, int slot = 0)
    {
        _positions[position] = Cleared;

        if (slot > 0)
        {
            _removedSlots.Add(slot);
        }
    }

    public void MarkRemoved(int slot) => _removedSlots.Add(slot);

    /// <summary>Where a learned item sits, if a person has ever put it somewhere in particular.</summary>
    public int? PositionOf(int slot)
    {
        foreach ((int position, int there) in _positions)
        {
            if (there == slot)
            {
                return position;
            }
        }

        return null;
    }

    /// <summary>
    /// Puts <paramref name="slot"/> at <paramref name="position"/>. <paramref name="displaced"/> is whatever the
    /// ability bar is showing at <paramref name="position"/> right now (or null for an empty slot) — it takes
    /// <paramref name="slot"/>'s old spot, so picking something already placed elsewhere swaps the two
    /// ("이미 다른 슬롯에 있는 기술을 고르면 두 슬롯을 맞바꾼다").
    /// </summary>
    public void Place(int position, int slot, int? displaced)
    {
        _removedSlots.Remove(slot); // 목록에서 다시 골랐다 — 더는 "뺀 기술"이 아니다.

        if (PositionOf(slot) is { } oldPosition && oldPosition != position)
        {
            if (displaced is { } other)
            {
                _positions[oldPosition] = other;
            }
            else
            {
                _positions[oldPosition] = Cleared;
            }
        }

        _positions[position] = slot;
    }

    /// <summary>
    /// The learned items in slot order, <paramref name="count"/> long. A forgotten skill's old spot (still
    /// remembered here, but no longer among <paramref name="learned"/>) is treated as untouched, so the pool
    /// still reaches it rather than leaving it stuck empty.
    /// </summary>
    public IReadOnlyList<T?> Fill<T>(IReadOnlyList<T> learned, Func<T, int> slotOf, int count) where T : class
    {
        Dictionary<int, T> bySlot = [];

        foreach (T item in learned)
        {
            bySlot[slotOf(item)] = item;
        }

        HashSet<int> placed = [.. _removedSlots];

        foreach (int there in _positions.Values)
        {
            if (there != Cleared)
            {
                placed.Add(there);
            }
        }

        Queue<T> pool = new(learned.Where(item => !placed.Contains(slotOf(item))));
        T?[] result = new T?[count];

        for (int position = 0; position < count; position++)
        {
            if (_positions.TryGetValue(position, out int there))
            {
                if (there == Cleared)
                {
                    continue;
                }

                if (bySlot.TryGetValue(there, out T? found))
                {
                    result[position] = found;
                    continue;
                }

                // 잊은 기술 — 자리를 계속 비워 두지 않고 아래에서 다음 것을 채운다.
            }

            if (pool.Count > 0)
            {
                result[position] = pool.Dequeue();
            }
        }

        return result;
    }
}

/// <summary>
/// Turns a per-character arrangement into the lines <c>Main</c> writes to <c>user://</c> and back — the same
/// one-line-per-thing shape as <c>potion.cfg</c>. One kind word ("skill"/"spell"), a position, and either a
/// learned item's own Slot number or "empty".
/// </summary>
public static class AbilitySlotSave
{
    public static IEnumerable<string> ToLines(AbilityArrangement skills, AbilityArrangement spells) =>
        Lines("skill", skills).Concat(Lines("spell", spells));

    public static void Parse(IEnumerable<string> lines, AbilityArrangement skills, AbilityArrangement spells)
    {
        foreach (string line in lines)
        {
            string[] parts = line.Trim().Split(' ');

            if (parts.Length != 3)
            {
                continue;
            }

            AbilityArrangement? target = parts[0] switch
            {
                "skill" => skills,
                "spell" => spells,
                _ => null
            };

            if (target is null)
            {
                continue;
            }

            // "skill removed 3" — 뺀 기술 하나. 자리(position)가 아니라 슬롯 번호를 적는 줄이라 먼저 본다.
            if (parts[1] == "removed" && int.TryParse(parts[2], out int removedSlot))
            {
                target.MarkRemoved(removedSlot);
                continue;
            }

            if (!int.TryParse(parts[1], out int position))
            {
                continue;
            }

            if (parts[2] == "empty")
            {
                target.Clear(position);
            }
            else if (int.TryParse(parts[2], out int slot))
            {
                target.Assign(position, slot);
            }
        }
    }

    private static IEnumerable<string> Lines(string kind, AbilityArrangement arrangement) =>
        arrangement.Positions
            .OrderBy(pair => pair.Key)
            .Select(pair => $"{kind} {pair.Key} {(pair.Value == AbilityArrangement.Cleared ? "empty" : pair.Value.ToString())}")
            .Concat(arrangement.RemovedSlots.OrderBy(slot => slot).Select(slot => $"{kind} removed {slot}"));
}
