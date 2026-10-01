namespace Lod.Mobile.Core.World;

/// <summary>
/// What the action row beside a picked item says (인벤토리 창, 2026-09-26): the main button's word and one line under
/// the name. The server says nothing about what a thing is for, so wear decides it: a thing that wears out is gear.
/// </summary>
public static class ItemActions
{
    /// <summary>The main thing to do with something carried — the server's 0x1C does both; only the word differs.</summary>
    public static string Primary(InventoryItem item) => IsGear(item) ? "입기" : "사용";

    /// <summary>Gear is what wears out — the 장비 tab of the pack, and the 입기 word.</summary>
    public static bool IsGear(InventoryItem item) => item.MaxDurability > 0;

    public static string Line(InventoryItem item) =>
        item.MaxDurability > 0 ? $"내구 {item.Durability}/{item.MaxDurability}"
        : item.Stacks > 1 ? $"{item.Stacks}개"
        : string.Empty;

    /// <summary>
    /// The numbers under the name in the info box, one line each, only those that say something — what the other games'
    /// item bubble shows (사용자 2026-10-01). Empty when the server sent none.
    /// </summary>
    public static IReadOnlyList<string> Stats(InventoryItem item)
    {
        if (item.Stats is not { } s)
        {
            return [];
        }

        List<string> lines = [];

        if (s.DmgMax > 0) lines.Add($"공격력 {s.DmgMin}~{s.DmgMax}");
        if (s.Ac != 0) lines.Add($"방어 {s.Ac:+0;-0}");

        foreach ((string name, int value) in new[]
                 {
                     ("명중", s.Hit), ("타격", s.Dmg), ("힘", s.Str), ("지능", s.Int), ("지혜", s.Wis), ("체력", s.Con),
                     ("민첩", s.Dex), ("마법 방어", s.Mr), ("HP", s.Hp), ("MP", s.Mp)
                 })
        {
            if (value != 0) lines.Add($"{name} {value:+0;-0}");
        }

        if (Element(s.Offense) is { Length: > 0 } offense) lines.Add($"공격 속성 {offense}");
        if (Element(s.Defense) is { Length: > 0 } defense) lines.Add($"방어 속성 {defense}");

        List<string> needs = [];
        if (s.Level > 0) needs.Add($"레벨 {s.Level}");
        if (s.Class is >= 1 and <= 5) needs.Add(Paths[s.Class]);
        if (needs.Count > 0) lines.Add($"요구 {string.Join(" · ", needs)}");

        if (s.Weight > 0) lines.Add($"무게 {s.Weight}");

        return lines;
    }

    private static readonly string[] Paths = ["평민", "전사", "도적", "마법사", "성직자", "무도가"];

    private static string Element(int number) => number switch
    {
        1 => "불", 2 => "물", 3 => "바람", 4 => "땅", 5 => "빛", 6 => "어둠", _ => string.Empty
    };

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
