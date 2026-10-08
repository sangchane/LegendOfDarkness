using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Lod.CompanionBot;
using Lod.Mobile.Core;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace Lod.EcoBots;

/// <summary>봇 하나 — 서버 설정 <c>EcoBots</c> 에 같은 이름이 있어야 한다. 직업 1 전사 · 2 도적 · 4 성직자(파티에서만) · 5 무도가, 성별 1 남 · 2 여.</summary>
public sealed record EcoBotEntry(string Name, int Path, int Gender = 1);

/// <summary>생태계 봇 프로그램 설정(<c>eco-bots.json</c>). 비밀번호는 이 파일에만(클라우드, 권한 600) — 모든 봇이 같다.</summary>
public sealed record EcoConfig
{
    public string Host { get; init; } = "127.0.0.1";
    public int LoginPort { get; init; } = 2610;

    /// <summary>앱의 맵 자료 폴더 — <c>map번호.txt</c>(벽) · <c>guide.txt</c>(가게) · <c>eco-grounds.txt</c>(사냥터) · <c>class-kit.txt</c>(직업 기술).</summary>
    public string MapFolder { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
    public IReadOnlyList<EcoBotEntry> Bots { get; init; } = [];

    /// <summary>함께 접속할 봇 수 상한(설계 03 상수 MaxEcoBots) — 목록 앞에서부터.</summary>
    public int MaxOnline { get; init; } = 50;

    public string? LogFile { get; init; }

    /// <summary>사건 기록(머신러닝 재료) 폴더 — <c>YYYY-MM-DD.jsonl</c>(한국 날짜), 지난 날은 gzip.</summary>
    public string EventFolder { get; init; } = string.Empty;

    public static EcoConfig Load(string path)
    {
        EcoConfig config = JsonSerializer.Deserialize<EcoConfig>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"'{path}' 가 비어 있습니다.");

        if (config.Password.Length == 0 || config.Bots.Count == 0)
        {
            throw new InvalidDataException($"'{path}' 에 Password 와 Bots 를 적어 주세요.");
        }

        string here = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return config with
        {
            MapFolder = config.MapFolder.Length == 0 ? string.Empty : Path.GetFullPath(config.MapFolder, here),
            LogFile = Path.GetFullPath(config.LogFile ?? Path.Combine("logs", "eco-bots.log"), here),
            EventFolder = Path.GetFullPath(config.EventFolder.Length == 0 ? "eco" : config.EventFolder, here),
        };
    }
}

/// <summary>
/// 봇이 들르는 자리 — guide.txt 의 꼬리표(생성기 <c>build-client-guide.py</c>, 서버 NPC 정의에서)로만 정한다. 가게는 <c>stock</c> 줄:
/// <paramref name="Role" /> = 역할 낱말(무기·방어구·물약 …), <paramref name="Healing" /> = 파는 체력 물약 가짓수, <paramref name="Lowest" />~
/// <paramref name="Highest" /> = 입는 물건이 든 서클의 레벨 폭(0 이면 입는 물건이 없다). 그 밖 NPC 는 <c>npc</c> 줄(이름표).
/// 상점을 옮기거나 물목을 바꾸면 생성기를 다시 돌리는 것만으로 봇 동선이 따라간다(사용자 2026-10-08 「모든 정보나 봇들을 라벨링해서
/// 데이터 수정하면 거기에 맞게 적용되도록」).
/// </summary>
public sealed record EcoStop(int Map, Tile Where, int Healing, string Role = "", int Lowest = 0, int Highest = 0)
{
    /// <summary>이 레벨이 장비를 보러 들를 가게인가 — 제 서클 가게만(다섯 마을을 다 돌면 헛걸음).</summary>
    public bool Fits(int level) => Highest == 0 || (Lowest <= level && level <= Highest);
}

/// <summary>봇이 함께 보는 세상 자료 — 맵 벽·사냥터·가게·직업 기술.</summary>
public sealed class EcoWorld(MapWalls walls, IReadOnlyList<EcoGround> grounds, IReadOnlyList<EcoStop> stops, ClassKit kit,
    IReadOnlyDictionary<string, EcoStop>? named = null)
{
    public MapWalls Walls => walls;
    public IReadOnlyList<EcoGround> Grounds => grounds;
    /// <summary>역할이 물약인 가게 중 체력 물약을 가장 여러 가지 파는 곳(음식점의 엑스쿠라눔 하나짜리가 아니라 물약 가게).</summary>
    public EcoStop? PotionStop => stops.Where(stop => stop.Role == "물약" && stop.Healing > 0).MaxBy(stop => stop.Healing);

    /// <summary>역할이 장비인 가게 — 무기·방어구(옛 장신구). 레벨에 맞는 곳만 들르는 것은 <see cref="EcoStop.Fits" />.</summary>
    public IReadOnlyList<EcoStop> GearStops => [.. stops.Where(stop => stop.Role is "무기" or "방어구" or "장신구")];

    /// <summary>이름표로 찾는 NPC(세오·칸·뮤레칸 …) — guide.txt <c>npc</c> 줄. 같은 이름이 여럿이면 처음 것. 없으면 null.</summary>
    public EcoStop? Npc(string name) => named is not null && named.TryGetValue(name, out EcoStop? stop) ? stop : null;
    public ClassKit Kit => kit;

    public static EcoWorld Load(string folder)
    {
        string Text(string name) => folder.Length > 0 && File.Exists(Path.Combine(folder, name)) ? File.ReadAllText(Path.Combine(folder, name)) : string.Empty;

        string guide = Text("guide.txt");
        return new EcoWorld(new MapWalls(folder), EcoGrounds.Read(Text("eco-grounds.txt")), Stops(guide), ClassKit.Read(Text("class-kit.txt")), Named(guide));
    }

    /// <summary>guide.txt 의 <c>npc &lt;맵&gt; &lt;x&gt; &lt;y&gt; &lt;이름&gt;</c> 줄 — 이름마다 처음 자리.</summary>
    public static IReadOnlyDictionary<string, EcoStop> Named(string guide)
    {
        Dictionary<string, EcoStop> named = [];

        foreach (string line in guide.Split('\n'))
        {
            string[] part = line.Trim().Split(' ', 5);
            if (part.Length == 5 && part[0] == "npc" && int.TryParse(part[1], out int map) && int.TryParse(part[2], out int x)
                && int.TryParse(part[3], out int y))
            {
                named.TryAdd(part[4], new EcoStop(map, new Tile(x, y), 0));
            }
        }

        return named;
    }

    /// <summary>
    /// guide.txt 의 <c>stock &lt;맵&gt; &lt;x&gt; &lt;y&gt; &lt;역할&gt; &lt;최저&gt; &lt;최고&gt; 이름, 이름…</c> 줄 — 같은 것을 파는 가게가 여럿이면 처음 것만.
    /// 사람이 읽는 <c>about</c> 줄은 2026-10-08 부터 「판매: 무기 67종 · 레벨 1~99」로 줄여 적어 물건 이름이 없다.
    /// </summary>
    public static IReadOnlyList<EcoStop> Stops(string guide)
    {
        HashSet<string> seen = [];
        List<EcoStop> stops = [];

        foreach (string line in guide.Split('\n'))
        {
            string[] part = line.Trim().Split(' ', 8);
            if (part.Length < 8 || part[0] != "stock"
                || !int.TryParse(part[1], out int map) || !int.TryParse(part[2], out int x) || !int.TryParse(part[3], out int y)
                || !int.TryParse(part[5], out int lowest) || !int.TryParse(part[6], out int highest)
                || !seen.Add(part[7]))
            {
                continue;
            }

            int healing = part[7].Split(',').Count(name => EcoShopping.IsHealing(name.Trim()));
            stops.Add(new EcoStop(map, new Tile(x, y), healing, part[4], lowest, highest));
        }

        return stops;
    }
}

/// <summary>사건 기록 파일 — 봇 모두가 한 파일에 한 줄씩. 한국 날짜로 파일을 나누고, 날이 바뀌면 지난 파일을 gzip, 365일 넘은 것은 지운다.</summary>
public sealed class EcoEvents(string folder)
{
    public const int KeepDays = 365;

    private readonly object _gate = new();
    private string? _day;

    public void Write(string line)
    {
        if (folder.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string day = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd");

                if (_day != day)
                {
                    _day = day;
                    Tidy(day);
                }

                // BOM 없이 — 있으면 첫 줄이 JSON 으로 안 읽힌다.
                File.AppendAllText(Path.Combine(folder, day + ".jsonl"), line + "\n", new UTF8Encoding(false));
            }
            catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"사건 기록을 못 씁니다: {failed.Message}");
            }
        }
    }

    private void Tidy(string today)
    {
        foreach (string old in Directory.GetFiles(folder, "????-??-??.jsonl").Where(path => Path.GetFileNameWithoutExtension(path) != today))
        {
            using (FileStream from = File.OpenRead(old))
            using (FileStream to = File.Create(old + ".gz"))
            using (GZipStream zip = new(to, CompressionLevel.SmallestSize))
            {
                from.CopyTo(zip);
            }

            File.Delete(old);
        }

        string cut = DateTime.UtcNow.AddHours(9).AddDays(-KeepDays).ToString("yyyy-MM-dd");
        foreach (string old in Directory.GetFiles(folder, "????-??-??.jsonl.gz")
                     .Where(path => string.CompareOrdinal(Path.GetFileName(path)[..10], cut) < 0))
        {
            File.Delete(old);
        }
    }
}

/// <summary>
/// 생태계 봇 프로그램 — 봇마다 접속 하나(<see cref="EcoRunner" />)를 2초 간격으로 띄우고, 끊기면 5초~1분 간격으로 다시 들인다.
/// 봇들이 어느 맵에 있는지 함께 세어 한 사냥터에 몰리지 않게 한다(<see cref="BotsOn" />). 10초마다 파티를 짓고 풀며
/// (<see cref="EcoParties" />, 결정 19), 5분마다 요약 한 줄. 유찰품 목록(<see cref="Unlisted" />)은 봇 이름별로 여기서 들고 재접속에 넘긴다.
/// </summary>
public sealed class EcoHost(EcoConfig config, EcoWorld world, EcoEvents events, EcoUnlisted unlisted, Action<string> log)
{
    public static readonly TimeSpan Stagger = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan Summary = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Matching = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, EcoRunner> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _names = new(config.Bots.Select(bot => bot.Name), StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly List<EcoParty> _parties = [];

    public IReadOnlyCollection<EcoRunner> Running => [.. _running.Values];

    /// <summary>봇 이름별 유찰품 — 다시 올리지 않는다. 설정 파일 옆 <c>eco-unlisted.json</c> 에 남아 재시작에도 잊지 않는다.</summary>
    public EcoUnlisted Unlisted => unlisted;

    public bool IsBot(string name) => _names.Contains(name);

    /// <summary>접속 중인 봇.</summary>
    public EcoRunner? Find(string name) => _running.GetValueOrDefault(name);

    /// <summary>이 봇이 든 파티. 없으면 null.</summary>
    public EcoParty? PartyOf(string name)
    {
        lock (_gate)
        {
            return _parties.FirstOrDefault(party => party.All.Contains(name, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 접속이 끝난 봇의 파티를 바로 푼다 — 10초 짓기 사이에 다시 들어오면 파티는 그대로인데 서버 그룹에서는 빠져, 파티장은 이미
    /// 그룹원으로 알고(앱의 그룹원 표는 그룹이 끝나야 비워진다) 다시 청하지 않았다(리뷰 2026-10-07).
    /// </summary>
    private void Disband(string name)
    {
        lock (_gate)
        {
            foreach (EcoParty broken in _parties.Where(party => party.All.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList())
            {
                _parties.Remove(broken);
                log($"파티 풀림({name} 끊김): {string.Join(" ", broken.All)}");
            }
        }
    }

    /// <summary>
    /// 파티 짓기·풀기 — 파티원이 하나라도 접속이 끊겼으면 푼다. 그다음 접속해 자리를 잡은(레벨을 아는) 봇 중 파티가 없는 것으로 짓는다.
    /// </summary>
    public void Match()
    {
        lock (_gate)
        {
            foreach (EcoParty broken in _parties.Where(party => party.All.Any(name => Find(name) is not { Level: > 0 })).ToList())
            {
                _parties.Remove(broken);
                log($"파티 풀림: {string.Join(" ", broken.All)}");
            }

            HashSet<string> taken = new(_parties.SelectMany(party => party.All), StringComparer.OrdinalIgnoreCase);
            EcoMember[] free = [.. _running.Values.Where(runner => runner.Level > 0 && !taken.Contains(runner.Name)).Select(runner => new EcoMember(runner.Name, runner.Path, runner.Level))];

            foreach (EcoParty made in EcoParties.Form(free, Tuning.EcoPartyLevel))
            {
                _parties.Add(made);
                log($"파티 맺음: {string.Join(" ", made.All)}");
            }
        }
    }

    /// <summary>이 맵에 있는 (다른) 생태계 봇 수.</summary>
    public int BotsOn(int map, string except) =>
        _running.Values.Count(runner => runner.Map == map && !string.Equals(runner.Name, except, StringComparison.OrdinalIgnoreCase));

    public async Task RunAsync(CancellationToken token)
    {
        List<Task> keeping = [];

        foreach (EcoBotEntry bot in config.Bots.Take(config.MaxOnline))
        {
            keeping.Add(Task.Run(() => Keep(bot, token), token));
            await Task.Delay(Stagger, token);
        }

        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan nextSummary = Summary;

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(Matching, token);
            Match();

            if (clock.Elapsed < nextSummary)
            {
                continue;
            }

            nextSummary = clock.Elapsed + Summary;
            EcoRunner[] now = [.. _running.Values];
            log($"요약: 접속 {now.Length}/{Math.Min(config.MaxOnline, config.Bots.Count)} · " +
                string.Join(" ", now.GroupBy(runner => runner.Doing).Select(group => $"{group.Key} {group.Count()}")) +
                (now.Length > 0 ? $" · 레벨 평균 {now.Average(runner => runner.Level):0.0} 최고 {now.Max(runner => runner.Level)}" : "") +
                $" · 파티 {_parties.Count}");
        }

        await Task.WhenAll(keeping);
    }

    private async Task Keep(EcoBotEntry bot, CancellationToken token)
    {
        TimeSpan wait = TimeSpan.FromSeconds(5);

        while (!token.IsCancellationRequested)
        {
            EcoRunner runner = new(bot, config, world, this, events, line => log($"{bot.Name}: {line}"));
            _running[bot.Name] = runner;

            try
            {
                await runner.RunAsync(token);
                wait = TimeSpan.FromSeconds(5);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failed)
            {
                log($"{bot.Name}: 접속이 끝남 — {BotLogin.Describe(failed)}");
            }
            finally
            {
                _running.TryRemove(bot.Name, out _);
                Disband(bot.Name);
            }

            await Task.Delay(wait, token);
            wait = TimeSpan.FromSeconds(Math.Min(60, wait.TotalSeconds * 2));
        }
    }
}
