namespace Lod.Mobile.Core.World;

/// <summary>
/// 경험치 게이지 한 칸: 이번 레벨에 모은 양과 이번 레벨에 드는 양(사용자 요청 2026-09-26).
/// </summary>
/// <remarks>
/// 서버는 다음 레벨까지 <b>남은</b> 양(0x08)만 보내고 그 레벨에 얼마가 드는지는 보내지 않는다. 드는 양은 서버
/// <c>ExperienceCurve</c> 와 같은 원작 표 — 5.99 <c>db/server/experience.txt</c> 직업 1~5 줄의 넷째 칸, 누적이 아니라 그 한 레벨의
/// 값이다(5.99 <c>Novaonline.exe</c> 0x4699cb · 0x469b15).
/// </remarks>
public sealed record ExperienceGauge(long Earned, long Need)
{
    /// <summary>자리 = 그 레벨이 되는 데 드는 값, 레벨 0~99.</summary>
    private static readonly long[] Table =
    [
        0, 0, 600, 2400, 5400, 9600, 15450, 22248, 30282, 39552,
        50058, 61800, 74778, 88992, 104442, 121128, 139050, 158208, 178602, 200232,
        223098, 247200, 272538, 299112, 326922, 355968, 386250, 417768, 450522, 484512,
        519738, 556200, 593898, 632832, 673002, 714408, 757050, 800928, 846042, 892392,
        939978, 988800, 1038858, 1090152, 1142682, 1196448, 1251450, 1307688, 1365162, 1423872,
        1484872, 1547682, 1612112, 1678232, 1746220, 1814208, 1883931, 1955573, 2029306, 2105199,
        2183331, 2263802, 2346683, 2432048, 2520020, 2610641, 2704073, 2800417, 2899774, 3030646,
        3164727, 3302150, 3442996, 3587478, 3735583, 3887556, 4043377, 4202736, 4366198, 4533823,
        4705775, 4882130, 5062983, 5248444, 5438595, 5633552, 5833406, 6038257, 6248219, 6463368,
        6683821, 6909673, 7141024, 7377982, 7620645, 7869109, 8123470, 8383835, 8582713, 8990567
    ];

    /// <summary><paramref name="level" /> 에서 다음 레벨까지 <paramref name="toGo" /> 가 남았을 때의 게이지. 레벨을 모르거나 99 면 없다.</summary>
    public static ExperienceGauge? Of(int level, long toGo)
    {
        if (level <= 0 || level + 1 >= Table.Length)
        {
            return null;
        }

        long need = Table[level + 1];

        return new ExperienceGauge(Math.Clamp(need - toGo, 0, need), need);
    }

    /// <summary>
    /// 게이지 옆 숫자를 짧게 — 1,000 아래는 그대로, 그 위는 k·M 에 세 자리까지(2.4k · 121k · 1.48M). 버린다: 반올림하면
    /// 999,999 가 1000k 가 되고, 모자란 양이 다 찬 것처럼 보인다.
    /// </summary>
    public static string Short(long value)
    {
        if (value < 1_000)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        (double scaled, string unit) = value < 1_000_000 ? (value / 1_000d, "k") : (value / 1_000_000d, "M");
        int decimals = scaled < 10 ? 2 : scaled < 100 ? 1 : 0;
        double step = Math.Pow(10, decimals);
        double cut = Math.Floor(scaled * step + 1e-9) / step;

        return cut.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + unit;
    }
}
