using System.Buffers.Binary;
using System.Globalization;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>그룹원·봇의 체력·마력 숫자 — 현재·최대.</summary>
public sealed record VitalNumbers(int Health, int MaximumHealth, int Mana, int MaximumMana);

/// <summary>
/// 0x5E 종류 6(그룹원)·종류 4(봇) 끝에 덧붙은 숫자(2026-09-27, 파티원·봇 칸의 게이지 안 숫자) — 체력(4) · 최대 체력(4) ·
/// 마력(4) · 최대 마력(4), 빅엔디언. 앞부분(%·그림·이름)은 그대로라 옛 앱은 이 꼬리를 읽지 않고, 옛 서버가 보낸 짧은 몸에서는
/// null 이다 — 그때 칸은 %를 적는다(<see cref="Text" />).
/// </summary>
public static class PartyNumbers
{
    private const int Tail = 16;

    /// <summary>종류 6 — serial(4) · 체력 %(1) · 마력 %(1) · 개수(1) · 그림(2)×개수 · 이름(StringA) 뒤의 숫자. 없으면 null.</summary>
    public static VitalNumbers? ReadMember(ReadOnlySpan<byte> body)
    {
        if (body.Length < 8)
        {
            return null;
        }

        int at = 8 + (body[7] * 2);

        if (body.Length <= at)
        {
            return null;
        }

        LegacyKoreanEncoding.DecodeStringA(body[at..], out int consumed);

        return Read(body, at + consumed);
    }

    /// <summary>종류 4 — serial(4) · 체력 %(1) · 마력 %(1) 뒤의 숫자. 없으면 null.</summary>
    public static VitalNumbers? ReadLife(ReadOnlySpan<byte> body) => Read(body, 7);

    /// <summary>
    /// 게이지 안에 적을 글: 숫자가 있으면 "450/530"(좁은 칸이면 만 이상은 k·M), 없고 %만 알면 "85%", 둘 다 모르면 빈 글.
    /// </summary>
    public static string Text(int? left, int? most, int? percent, bool compact = true)
    {
        if (left is { } l && most is > 0)
        {
            return $"{Number(l, compact)}/{Number(most.Value, compact)}";
        }

        return percent is { } p ? $"{Math.Clamp(p, 0, 100)}%" : string.Empty;
    }

    private static string Number(int value, bool compact) =>
        compact && value >= 10_000 ? ExperienceGauge.Short(value) : value.ToString(CultureInfo.InvariantCulture);

    private static VitalNumbers? Read(ReadOnlySpan<byte> body, int at)
    {
        if (at < 0 || body.Length < at + Tail)
        {
            return null;
        }

        return new VitalNumbers(
            BinaryPrimitives.ReadInt32BigEndian(body[at..]),
            BinaryPrimitives.ReadInt32BigEndian(body[(at + 4)..]),
            BinaryPrimitives.ReadInt32BigEndian(body[(at + 8)..]),
            BinaryPrimitives.ReadInt32BigEndian(body[(at + 12)..]));
    }
}
