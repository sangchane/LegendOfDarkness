namespace Lod.Mobile.Core.Ui;

/// <summary>금화를 좁은 칸에 — 만·억으로 줄이되 가진 것보다 많아 보이게 올려 적지 않는다.</summary>
public static class GoldFormat
{
    /// <summary>Shortens gold without rounding up to money the character does not have.</summary>
    public static string Short(long gold) => gold >= 100_000_000 ? $"{Math.Floor(gold / 10_000_000d) / 10:0.#}억"
        : gold >= 10_000 ? $"{Math.Floor(gold / 1_000d) / 10:0.#}만" : $"{gold:N0}";
}
