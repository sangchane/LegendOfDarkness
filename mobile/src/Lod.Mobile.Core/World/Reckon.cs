namespace Lod.Mobile.Core.World;

/// <summary>
/// 자동 사냥·동료 봇·봇 프로그램이 함께 쓰는 셈 — 칸 거리, 체력·마력 %, "한 번도 안 했다".
/// </summary>
public static class Reckon
{
    /// <summary>TimeSpan.MinValue 에서 빼면 넘친다 — "한 번도 안 했다"는 충분히 먼 옛날로.</summary>
    public static readonly TimeSpan Never = TimeSpan.FromDays(-365);

    /// <summary>두 칸 사이 걸음 수 — 가로와 세로를 더한다(대각선 걸음은 없다).</summary>
    public static int Steps(Tile one, Tile other) => Math.Abs(one.X - other.X) + Math.Abs(one.Y - other.Y);

    /// <summary>체력 %. 모르면 100.</summary>
    public static int HealthPercent(Vitals? vitals) =>
        vitals is { MaximumHealth: > 0 } known ? (int)(known.Health * 100L / known.MaximumHealth) : 100;

    /// <summary>마력 %. 모르면 100.</summary>
    public static int ManaPercent(Vitals? vitals) =>
        vitals is { MaximumMana: > 0 } known ? (int)(known.Mana * 100L / known.MaximumMana) : 100;
}
