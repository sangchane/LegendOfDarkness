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
/// </remarks>
public sealed class AbilityArrangement
{
    public const int Cleared = -1;

    private readonly Dictionary<int, int> _positions = [];

    /// <summary>Every touched position, for saving — position, then what is there (a Slot number, or <see cref="Cleared"/>).</summary>
    public IReadOnlyDictionary<int, int> Positions => _positions;

    public void Assign(int position, int slot) => _positions[position] = slot;

    public void Clear(int position) => _positions[position] = Cleared;

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

        HashSet<int> placed = [];

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

            if (parts.Length != 3 || !int.TryParse(parts[1], out int position))
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
            .Select(pair => $"{kind} {pair.Key} {(pair.Value == AbilityArrangement.Cleared ? "empty" : pair.Value.ToString())}");
}
