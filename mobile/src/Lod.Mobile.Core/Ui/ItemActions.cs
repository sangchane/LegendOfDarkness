using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Ui;

/// <summary>
/// What the action row beside a picked item says (인벤토리 창, 2026-09-26): the main button's word and one line under
/// the name. The server says nothing about what a thing is for, so wear decides it: a thing that wears out is gear.
/// </summary>
public static class ItemActions
{

    /// <summary>Gear is what wears out — the 장비 tab of the pack, and the 입기 word.</summary>
    public static bool IsGear(InventoryItem item) => item.MaxDurability > 0;

    public static string Line(InventoryItem item) =>
        item.MaxDurability > 0 ? $"내구 {item.Durability}/{item.MaxDurability}"
        : item.Stacks > 1 ? $"{item.Stacks}개"
        : string.Empty;

    /// <summary>
    /// The numbers under the name in the info box, only those that say something — what other games' item bubble shows
    /// (사용자 2026-10-01). Given what is <paramref name="worn" /> in the same place, each number also says how much better
    /// (▲) or worse (▼) it is than that, and a number only the worn thing has shows as 0 ▼. Armour class is better lower.
    /// Empty when the server sent none.
    /// </summary>
    public static IReadOnlyList<StatLine> Stats(ItemStats? s, ItemStats? worn = null)
    {
        if (s is null)
        {
            return [];
        }

        List<StatLine> lines = [];

        if (s.DmgMax > 0 || worn?.DmgMax > 0) lines.Add(new("공격력", $"{s.DmgMin}~{s.DmgMax}", Change(s.DmgMax, worn?.DmgMax, false)));

        foreach ((string name, int value, int? before, bool lowerIsBetter) in new[]
                 {
                     ("방어", s.Ac, worn?.Ac, true), ("명중", s.Hit, worn?.Hit, false), ("타격", s.Dmg, worn?.Dmg, false),
                     ("힘", s.Str, worn?.Str, false), ("지능", s.Int, worn?.Int, false), ("지혜", s.Wis, worn?.Wis, false),
                     ("체력", s.Con, worn?.Con, false), ("민첩", s.Dex, worn?.Dex, false), ("마법 방어", s.Mr, worn?.Mr, false),
                     ("HP", s.Hp, worn?.Hp, false), ("MP", s.Mp, worn?.Mp, false)
                 })
        {
            if (value != 0 || before is not (null or 0))
            {
                lines.Add(new(name, $"{value:+0;-0;0}", Change(value, before, lowerIsBetter)));
            }
        }

        if (s.HealthRestore > 0) lines.Add(new("체력 회복", $"+{s.HealthRestore:N0}", 0, Numeric: false));
        if (s.ManaRestore > 0) lines.Add(new("마력 회복", $"+{s.ManaRestore:N0}", 0, Numeric: false));

        if (Element(s.Offense) is { Length: > 0 } offense) lines.Add(new("공격 속성", offense, 0, Numeric: false));
        if (Element(s.Defense) is { Length: > 0 } defense) lines.Add(new("방어 속성", defense, 0, Numeric: false));

        List<string> needs = [];
        if (s.Level > 0) needs.Add($"레벨 {s.Level}");
        if (s.Class is >= 1 and <= 5) needs.Add(Paths[s.Class]);
        if (needs.Count > 0) lines.Add(new("요구", string.Join(" · ", needs), 0, Numeric: false));

        // 무게 줄은 숨긴다 — 서버가 무게 제한을 껐다(사용자 2026-10-05 "일단"). 되살리려면:
        // if (s.Weight > 0) lines.Add(new("무게", $"{s.Weight}", 0, Numeric: false));

        return lines;
    }

    public static IReadOnlyList<StatLine> Stats(InventoryItem item, ItemStats? worn = null) => Stats(item.Stats, worn);

    /// <summary>How much better (above 0) or worse (below 0) than what was worn, 0 when the same or nothing to weigh.</summary>
    private static int Change(int now, int? before, bool lowerIsBetter) =>
        before is not { } was ? 0 : lowerIsBetter ? was - now : now - was;

    /// <summary>
    /// What is worn where a carried thing would go, so the info box can set them side by side — null when that place
    /// is empty (a ring or a gauntlet has two places; it counts as empty while either is). Null too for what is not
    /// gear or when the server sent no place.
    /// </summary>
    public static WornItem? WornInstead(InventoryItem item, IReadOnlyList<WornItem> worn) => WornInstead(item.Stats, worn);

    /// <summary>The same for a thing known only by its numbers — a shop's goods.</summary>
    public static WornItem? WornInstead(ItemStats? stats, IReadOnlyList<WornItem> worn)
    {
        if (stats is not { Place: > 0 } s)
        {
            return null;
        }

        int[] places = s.Place switch { 7 or 8 => [7, 8], 9 or 10 => [9, 10], _ => [s.Place] };
        WornItem?[] there = [.. places.Select(place => worn.FirstOrDefault(on => on.Slot == place))];

        return there.Any(on => on is null) ? null : there.FirstOrDefault(on => on!.Slot == s.Place) ?? there[0];
    }

    /// <summary>The main button for a carried thing — the server's 0x1C does all three; only the word differs: 교체 when it takes a worn thing's place, 장착 into an empty one, 사용 otherwise.</summary>
    public static string Primary(InventoryItem item, IReadOnlyList<WornItem> worn) =>
        !IsGear(item) ? "사용" : WornInstead(item, worn) is not null ? "교체" : "장착";

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

/// <summary>
/// One number in the item info box: what it is, its value, and against what is worn how much better (above 0) or
/// worse (below 0). <see cref="Numeric" /> numbers stand in the aligned table; the rest (elements, needs, weight) go
/// under it as one line.
/// </summary>
public sealed record StatLine(string Name, string Value, int Change, bool Numeric = true)
{
    /// <summary>The way it reads on one line — ▲ better, ▼ worse.</summary>
    public string Text => Change == 0 ? $"{Name} {Value}" : $"{Name} {Value} {(Change > 0 ? "▲" : "▼")}{Math.Abs(Change)}";
}
