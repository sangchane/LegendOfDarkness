namespace Lod.Mobile.Core.Model;

/// <summary>
/// One thing in a character's pack. The server sends these one at a time, both on the way in and whenever
/// something is picked up.
/// </summary>
/// <param name="Icon">
/// Which picture to draw for it — the same number whether the thing is carried, worn or lying on the floor.
/// </param>
public sealed record InventoryItem(
    int Slot,
    int Icon,
    int Colour,
    string Name,
    int Stacks,
    int Durability,
    int MaxDurability,
    ItemStats? Stats = null);

/// <summary>
/// What a carried or worn thing does, as our server adds after the original 0x0F and 0x37 (우리 확장, 2026-10-01). Bonuses are signed —
/// armour class goes down when it gets better, as in the original. <see cref="Class" /> is 0 for anyone; the elements
/// are the server's numbers (1 불 · 2 물 · 3 바람 · 4 땅 · 5 빛 · 6 어둠). <see cref="Place" /> is the worn place it goes
/// to, as 0x37 numbers it (0 for none).
/// </summary>
public sealed record ItemStats(
    int Ac, int Hit, int Dmg, int Str, int Int, int Wis, int Con, int Dex, int Mr,
    int Hp, int Mp, int DmgMin, int DmgMax,
    int Level, int Class, int Stage, int Weight, int Offense, int Defense, long Value, int Place);

/// <summary>
/// A piece of gear the character has on. The server names the place it sits by number — the same numbers
/// the item templates use in <c>EquipmentSlot</c> — and says nothing about what that place looks like.
/// </summary>
/// <param name="Slot">Where it is worn: 1 weapon, 2 armour, 3 shield, 4 helmet … 13 boots. See <see cref="WornPlace"/>.</param>
/// <param name="Name">What the item is called. <paramref name="Called"/> is that name after any upgrade is spelled into it.</param>
public sealed record WornItem(
    int Slot,
    int Icon,
    string Name,
    string Called,
    long Durability,
    long MaxDurability,
    ItemStats? Stats = null);

/// <summary>
/// The names of the places gear is worn, so a screen can say "신발" rather than "13". Straight from the
/// server's own <c>ItemSlots</c>; the gaps in the middle are the server's, not ours.
/// </summary>
public static class WornPlace
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [1] = "무기", [2] = "갑옷", [3] = "방패", [4] = "투구", [5] = "귀고리",
        [6] = "목걸이", [7] = "왼손", [8] = "오른손", [9] = "왼팔", [10] = "오른팔",
        [11] = "허리", [12] = "다리", [13] = "신발", [14] = "장신구", [15] = "겉옷",
        [16] = "겉투구", [17] = "장신구2", [18] = "장신구3",
    };

    /// <summary>The name of one place, or the number itself when the server uses one we do not know.</summary>
    public static string Of(int slot) => Names.TryGetValue(slot, out string? called) ? called : slot.ToString();
}
