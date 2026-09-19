namespace Lod.Mobile.Core.World;

/// <summary>
/// Which head numbers exist for a gender, and the two rules the 만들기 화면 needs around them: skip a
/// missing number while stepping through the list, and land on the nearest one that still exists when a
/// gender switch takes the number away.
/// </summary>
/// <remarks>
/// Counted from the original archive, not guessed — <c>data/character-creation/hairstyles.json</c>
/// (commit c7a181bc), also written down in <c>plans/character-creation.md</c>'s "사실" table: 남 59가지
/// (1~60, 26 결번) · 여 56가지(18·26·32·33 결번). Every female number is also a male number, so only the
/// female side ever needs to move.
/// </remarks>
public static class HairStyles
{
    private const int Lowest = 1;
    private const int Highest = 60;

    private static readonly HashSet<int> MaleMissing = [26];
    private static readonly HashSet<int> FemaleMissing = [18, 26, 32, 33];

    /// <summary>The head numbers that exist for this gender (1=남 · 2=여), lowest first.</summary>
    public static IReadOnlyList<int> For(int gender)
    {
        HashSet<int> missing = gender == 2 ? FemaleMissing : MaleMissing;
        List<int> numbers = [];

        for (int number = Lowest; number <= Highest; number++)
        {
            if (!missing.Contains(number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    /// <summary>
    /// One step forward (+1) or back (-1) through this gender's list, skipping the numbers that do not
    /// exist for it. Stepping past either end wraps to the other.
    /// </summary>
    public static int Step(int current, int gender, int direction)
    {
        IReadOnlyList<int> numbers = For(gender);
        int at = 0;

        for (int index = 0; index < numbers.Count; index++)
        {
            if (numbers[index] == current)
            {
                at = index;
                break;
            }
        }

        return numbers[((at + direction) % numbers.Count + numbers.Count) % numbers.Count];
    }

    /// <summary>
    /// Where a hair number lands after a gender switch. Already there for the new gender — unchanged.
    /// Otherwise the nearest number that does exist, and when a lower and a higher one are equally near,
    /// the lower one (2026-09-19 결정, plans/character-creation.md Task B). No confirmation, no warning —
    /// the caller just gets a valid number back.
    /// </summary>
    public static int ClosestFor(int hairStyle, int newGender)
    {
        IReadOnlyList<int> numbers = For(newGender);

        if (numbers.Contains(hairStyle))
        {
            return hairStyle;
        }

        for (int distance = 1; distance <= Highest; distance++)
        {
            if (numbers.Contains(hairStyle - distance))
            {
                return hairStyle - distance;
            }

            if (numbers.Contains(hairStyle + distance))
            {
                return hairStyle + distance;
            }
        }

        return numbers[0];
    }
}
