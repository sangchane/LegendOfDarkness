using System.Globalization;
using System.Text.Json;

namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 경매 시세(SPEC <c>autopilot/death-penalty-gear/</c> #4) — 서버 경매 사건 기록(<c>events-YYYY-MM-DD.jsonl</c>, 서버 <c>AuctionHouse.Write</c>)에서
/// 물건 이름별 최근 <see cref="Tuning.EcoMarketDays" />일 낙찰가 중앙값. 낙찰 = <c>sold</c>(기간 끝, gold = 최고 입찰) · <c>buyout</c>(즉시 구매,
/// gold = 값)이고 같은 seq 의 <c>abort</c> 가 있으면 뺀다. 생태 봇은 서버와 같은 기계라 그 폴더를 읽는다 — 봇 프로그램(<c>EcoHost</c>)이 하나를 들고
/// 봇들이 함께 쓰며, 읽기는 <see cref="Tuning.EcoMarketEvery" /> 에 한 번. 폴더가 없거나(설정에 없음) 못 읽으면 빈 시세(올림·사기는 예전 고정식).
/// </summary>
public sealed class EcoMarket(string? folder)
{
    private readonly object _gate = new();
    private IReadOnlyDictionary<string, long> _prices = new Dictionary<string, long>();
    private DateTime _read = DateTime.MinValue;

    public IReadOnlyDictionary<string, long> Prices(DateTime now)
    {
        lock (_gate)
        {
            if (string.IsNullOrEmpty(folder) || now - _read < Tuning.EcoMarketEvery)
            {
                return _prices;
            }

            _read = now;
            try
            {
                _prices = Read(folder, now);
            }
            catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
            {
                // 지난 시세를 그대로 — 다음 읽기에서 다시.
            }

            return _prices;
        }
    }

    /// <summary>폴더의 최근 기록 파일(이름의 날짜가 창 안)을 읽어 <see cref="Medians" />. 서버가 덧붙여 쓰는 중이라 쓰기를 막지 않고 연다.</summary>
    public static IReadOnlyDictionary<string, long> Read(string folder, DateTime now)
    {
        if (!Directory.Exists(folder))
        {
            return new Dictionary<string, long>();
        }

        string from = $"events-{now.AddDays(-Tuning.EcoMarketDays):yyyy-MM-dd}";
        List<string> lines = [];
        foreach (string path in Directory.EnumerateFiles(folder, "events-*.jsonl")
                     .Where(path => string.CompareOrdinal(Path.GetFileNameWithoutExtension(path), from) >= 0))
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream);
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
        }

        return Medians(lines, now);
    }

    /// <summary>이름별 낙찰가 중앙값(짝수 개면 가운데 둘의 평균, 내림). 읽지 못한 줄(꺼지며 반만 쓰인 끝 줄)은 건너뛴다.</summary>
    public static IReadOnlyDictionary<string, long> Medians(IEnumerable<string> lines, DateTime now)
    {
        DateTime since = now.ToUniversalTime().AddDays(-Tuning.EcoMarketDays);
        Dictionary<long, (string Item, long Gold)> trades = [];
        HashSet<long> aborted = [];

        foreach (string line in lines)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                string? ev = root.TryGetProperty("ev", out JsonElement kind) ? kind.GetString() : null;
                long seq = root.TryGetProperty("seq", out JsonElement number) ? number.GetInt64() : 0;

                if (ev == "abort")
                {
                    aborted.Add(seq);
                }
                else if (ev is "sold" or "buyout"
                         && root.TryGetProperty("item", out JsonElement item) && item.GetString() is { Length: > 0 } name
                         && root.TryGetProperty("gold", out JsonElement gold) && gold.GetInt64() > 0
                         && root.TryGetProperty("at", out JsonElement at)
                         && DateTime.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime when)
                         && when >= since)
                {
                    trades[seq] = (name, gold.GetInt64());
                }
            }
            catch (Exception bad) when (bad is JsonException or InvalidOperationException or FormatException)
            {
            }
        }

        return trades.Where(trade => !aborted.Contains(trade.Key))
            .GroupBy(trade => trade.Value.Item, trade => trade.Value.Gold)
            .ToDictionary(group => group.Key, group => Median([.. group.Order()]));
    }

    private static long Median(long[] sorted) =>
        sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
}
