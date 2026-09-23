namespace Lod.Mobile.Core.World;

/// <summary>Whether one kind of automatic drinking is on, at what share of the bar it fires, and with what.</summary>
/// <param name="Percent">Drinks when the bar is at or below this share of its maximum.</param>
/// <param name="Potion">The item name to drink — the player picks it; nothing else of the kind is used.</param>
public sealed record PotionRule(bool Enabled, int Percent, string Potion);

/// <summary>A potion the player can pick, and the picture the server uses for it (the template's DisplayImage).</summary>
public sealed record Potion(string Name, int Icon);

/// <summary>
/// Decides when to drink the chosen potion as health or mana falls to the chosen line.
/// </summary>
/// <remarks>
/// The server has no cooldown on 0x1C, so asking every frame would empty the stack in a second. A
/// drink waits for its answer — the used stack shrinking or leaving the pack — and gives up after
/// <see cref="Patience"/> so a lost reply cannot switch the feature off for good. Health goes first:
/// one drink at a time is enough to keep the reply easy to recognise.
/// </remarks>
public sealed class AutoPotion
{
    /// <summary>
    /// Healing potions, smallest first (250 · 10000). 쿠라눔 is left out: the server has no such item yet.
    /// </summary>
    public static readonly Potion[] Healing = [new("쿠룸", 32813), new("엑스쿠라눔", 34941)];

    /// <summary>Mana potions, smallest first (100 · 500 · 1000 · 1500 · 2000). Food is left to the player.</summary>
    public static readonly Potion[] Restoring =
    [
        new("마라디움", 32815),
        new("최하급마력포션", 32822),
        new("하급마력포션", 32827),
        new("중급마력포션", 32829),
        new("상급마력포션", 32830),
    ];

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
            (Low(vitals.Health, vitals.MaximumHealth, health) ? Carried(pack, health.Potion) : null)
            ?? (Low(vitals.Mana, vitals.MaximumMana, mana) ? Carried(pack, mana.Potion) : null);

        if (drink is null)
        {
            return null;
        }

        _waiting = drink;
        _askedAt = now;
        return drink.Slot;
    }

    /// <summary>How many of one potion the pack holds, over every slot it is split across.</summary>
    public static int Count(IReadOnlyList<InventoryItem> pack, string name) =>
        pack.Where(one => one.Name == name).Sum(one => Math.Max(1, one.Stacks));

    private static bool Low(int value, int maximum, PotionRule rule) =>
        rule.Enabled && maximum > 0 && value * 100L <= (long)maximum * rule.Percent;

    private static InventoryItem? Carried(IReadOnlyList<InventoryItem> pack, string name) =>
        pack.Where(one => one.Name == name).OrderBy(one => one.Slot).FirstOrDefault();
}
