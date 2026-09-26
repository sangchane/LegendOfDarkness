namespace Lod.Mobile.Core.World;

/// <summary>
/// What the action row beside a picked item says (인벤토리 창, 2026-09-26): the main button's word and one line under
/// the name. The server says nothing about what a thing is for, so wear decides it: a thing that wears out is gear.
/// </summary>
public static class ItemActions
{
    /// <summary>The main thing to do with something carried — the server's 0x1C does both; only the word differs.</summary>
    public static string Primary(InventoryItem item) => item.MaxDurability > 0 ? "입기" : "사용";

    public static string Line(InventoryItem item) =>
        item.MaxDurability > 0 ? $"내구 {item.Durability}/{item.MaxDurability}"
        : item.Stacks > 1 ? $"{item.Stacks}개"
        : string.Empty;

    public static string Line(WornItem gear) =>
        gear.MaxDurability > 0 ? $"{WornPlace.Of(gear.Slot)} · 내구 {gear.Durability}/{gear.MaxDurability}" : WornPlace.Of(gear.Slot);
}

/// <summary>Two taps on the same thing within <see cref="Window" /> seconds — the shortcut for the main action.</summary>
public sealed class DoubleTap
{
    public const double Window = 0.35;

    private int? _key;
    private double _at;

    /// <summary>A tap on <paramref name="key" /> at <paramref name="now" /> (seconds). True when it completes a double tap.</summary>
    public bool Tap(int key, double now)
    {
        if (_key == key && now - _at <= Window)
        {
            _key = null;
            return true;
        }

        _key = key;
        _at = now;

        return false;
    }
}
