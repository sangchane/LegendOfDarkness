using System.Text.Json;

namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 생태계 봇 이름별 유찰품 이름 — 경매에서 아무도 안 사 돌아온 것은 다시 올리지 않는다(<see cref="EcoAuction.ToPost" /> 의 refused).
/// 봇 프로그램(<c>EcoHost</c>)이 하나를 들고 재접속마다 새 runner 에 넘기며, 파일(<c>eco-unlisted.json</c>, 봇 이름 → 물건 이름들)에
/// 남겨 프로그램 재시작에도 잊지 않는다(리뷰 2026-10-08 #9). 파일이 없거나 깨졌으면 빈 목록에서 시작한다.
/// </summary>
public sealed class EcoUnlisted(string path)
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _byBot = Read(path);

    /// <summary>이 봇의 유찰품(사본).</summary>
    public IReadOnlyCollection<string> Of(string bot)
    {
        lock (_gate)
        {
            return _byBot.TryGetValue(bot, out HashSet<string>? names) ? [.. names] : [];
        }
    }

    /// <summary>더하고, 새 이름이 있으면 파일을 다시 쓴다(임시 파일 → 바꿔 넣기). 못 썼으면 false — 이 실행 동안은 기억한다.</summary>
    public bool Add(string bot, IEnumerable<string> names)
    {
        lock (_gate)
        {
            if (!_byBot.TryGetValue(bot, out HashSet<string>? known))
            {
                _byBot[bot] = known = [];
            }

            int before = known.Count;
            known.UnionWith(names);
            if (known.Count == before)
            {
                return true;
            }

            try
            {
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_byBot, Options));
                File.Move(temp, path, overwrite: true);
                return true;
            }
            catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private static Dictionary<string, HashSet<string>> Read(string path)
    {
        Dictionary<string, HashSet<string>> byBot = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Dictionary<string, HashSet<string>?>>(File.ReadAllText(path)) is { } read)
            {
                foreach ((string bot, HashSet<string>? names) in read)
                {
                    if (names is not null)
                    {
                        byBot[bot] = names;
                    }
                }
            }
        }
        catch (Exception failed) when (failed is JsonException or IOException or UnauthorizedAccessException)
        {
            // 깨진 파일 — 빈 목록에서 시작하고, 다음 기록이 덮는다.
        }

        return byBot;
    }
}
