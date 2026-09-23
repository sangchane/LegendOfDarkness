namespace Lod.Mobile.Core.World;

/// <summary>Whether one kind of automatic drinking is on, and at what share of the bar it fires.</summary>
/// <param name="Percent">Drinks when the bar is at or below this share of its maximum.</param>
public sealed record PotionRule(bool Enabled, int Percent);

/// <summary>
/// Decides which carried potion to drink when health or mana falls to the chosen line.
/// </summary>
/// <remarks>
/// The server has no cooldown on 0x1C, so asking every frame would empty the stack in a second. A
/// drink waits for its answer — the used stack shrinking or leaving the pack — and gives up after
/// <see cref="Patience"/> so a lost reply cannot switch the feature off for good. Health goes first:
/// one drink at a time is enough to keep the reply easy to recognise.
/// </remarks>
public sealed class AutoPotion
{
    /// <summary>Healing potions, smallest first (쿠룸 250 · 엑스쿠라눔 10000). 쿠라눔 has no template yet.</summary>
    public static readonly string[] Healing = ["쿠룸", "쿠라눔", "엑스쿠라눔"];

    /// <summary>Mana potions, smallest first (100 · 500 · 1000 · 1500 · 2000). Food is left to the player.</summary>
    public static readonly string[] Restoring = ["마라디움", "최하급마력포션", "하급마력포션", "중급마력포션", "상급마력포션"];

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    private InventoryItem? _waiting;
    private TimeSpan _askedAt;

    /// <summary>
    /// Returns the pack slot to use now, or no slot. Call once per frame with the latest snapshot.
    /// </summary>
    public int? Next(Vitals vitals, IReadOnlyList<InventoryItem> pack, PotionRule health, PotionRule mana, TimeSpan now)
    {
        if (_waiting is { } asked)
        {
            bool answered = !pack.Any(one => one.Slot == asked.Slot && one.Name == asked.Name && one.Stacks == asked.Stacks);

            if (!answered && now - _askedAt < Patience)
            {
                return null;
            }

            _waiting = null;
        }

        if (vitals.Health <= 0)
        {
            return null;
        }

        InventoryItem? drink =
            (Low(vitals.Health, vitals.MaximumHealth, health) ? Smallest(pack, Healing) : null)
            ?? (Low(vitals.Mana, vitals.MaximumMana, mana) ? Smallest(pack, Restoring) : null);

        if (drink is null)
        {
            return null;
        }

        _waiting = drink;
        _askedAt = now;
        return drink.Slot;
    }

    private static bool Low(int value, int maximum, PotionRule rule) =>
        rule.Enabled && maximum > 0 && value * 100L <= (long)maximum * rule.Percent;

    private static InventoryItem? Smallest(IReadOnlyList<InventoryItem> pack, string[] order) =>
        pack.Where(one => Array.IndexOf(order, one.Name) >= 0)
            .OrderBy(one => Array.IndexOf(order, one.Name))
            .ThenBy(one => one.Slot)
            .FirstOrDefault();
}
