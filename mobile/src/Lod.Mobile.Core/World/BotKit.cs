namespace Lod.Mobile.Core.World;

/// <summary>
/// 봇 장비창의 규칙 — 엔진 없이. 봇이 입을 수 있는지(레벨·직업·성별)는 서버만 안다(<c>Companions.CannotWear</c>), 그래서 여기서는
/// 내 가방에서 **장비로 보이는 것**(내구가 있는 것)과 **포션**만 가려 보이고, 거절은 서버 알림으로 듣는다.
/// </summary>
public static class BotKit
{
    /// <summary>한 번 누르면 넘기는 포션 수.</summary>
    public const int PotionHandful = 5;

    /// <summary>봇에게 입혀 볼 만한 내 가방의 것 — 내구가 있고 포션이 아닌 것, 칸 차례.</summary>
    public static IReadOnlyList<InventoryItem> Wearables(IReadOnlyList<InventoryItem> pack) =>
        [.. pack.Where(one => one.MaxDurability > 0 && !IsPotion(one.Name)).OrderBy(one => one.Slot)];

    /// <summary>봇에게 넘길 수 있는 내 가방의 포션 — 체력 먼저, 작은 등급부터(AutoPotion 의 두 목록).</summary>
    public static IReadOnlyList<InventoryItem> Potions(IReadOnlyList<InventoryItem> pack) =>
        [.. pack.Where(one => IsPotion(one.Name)).OrderBy(one => Rank(one.Name)).ThenBy(one => one.Slot)];

    /// <summary>봇 가방의 포션을 한 줄로 — "쿠룸 4 · 마라디움 2", 없으면 "포션 없음".</summary>
    public static string Summary(CompanionKit? kit) =>
        kit?.Carried.Where(one => IsPotion(one.Name)).OrderBy(one => Rank(one.Name)).ToList() is { Count: > 0 } potions
            ? string.Join(" · ", potions.Select(one => $"{one.Name} {one.Stacks}"))
            : "포션 없음";

    /// <summary>봇 장비 한 자리 — 입은 것이 있으면 그것.</summary>
    public static WornItem? At(CompanionKit? kit, int slot) => kit?.Worn.FirstOrDefault(one => one.Slot == slot);

    /// <summary>봇 칸 막대의 두 값 — 모르면 0 이 아니라 없음(막대를 숨긴다).</summary>
    public static (int? Health, int? Mana) Bars(CompanionLife? life) =>
        life is null ? (null, null) : (Math.Clamp(life.HealthPercent, 0, 100), Math.Clamp(life.ManaPercent, 0, 100));

    public static bool IsPotion(string name) =>
        AutoPotion.Healing.Any(one => one.Name == name) || AutoPotion.Restoring.Any(one => one.Name == name);

    private static int Rank(string name)
    {
        int healing = Array.FindIndex(AutoPotion.Healing, one => one.Name == name);

        return healing >= 0 ? healing : 100 + Array.FindIndex(AutoPotion.Restoring, one => one.Name == name);
    }
}
