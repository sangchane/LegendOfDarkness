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

/// <summary>한 마을 가게 자리 — guide.txt <c>about … 판매:</c> 줄. <paramref name="Healing" /> = 파는 체력 물약 가짓수.</summary>
public sealed record EcoStop(int Map, Tile Where, int Healing);

/// <summary>봇이 함께 보는 세상 자료 — 맵 벽·맵 사이 길·사냥터·가게·직업 기술.</summary>
public sealed class EcoWorld(MapWalls walls, EcoLinks links, IReadOnlyList<EcoGround> grounds, IReadOnlyList<EcoStop> stops, ClassKit kit)
{
    public MapWalls Walls => walls;
    public EcoLinks Links => links;
    public IReadOnlyList<EcoGround> Grounds => grounds;
    /// <summary>체력 물약을 가장 여러 가지 파는 가게(음식점의 엑스쿠라눔 하나짜리가 아니라 물약 가게).</summary>
    public EcoStop? PotionStop => stops.Where(stop => stop.Healing > 0).MaxBy(stop => stop.Healing);

    /// <summary>체력 물약을 하나도 안 파는 가게 — 무기·옷·장신구.</summary>
    public IReadOnlyList<EcoStop> GearStops => [.. stops.Where(stop => stop.Healing == 0)];
    public ClassKit Kit => kit;

    public static EcoWorld Load(string folder)
    {
        string Text(string name) => folder.Length > 0 && File.Exists(Path.Combine(folder, name)) ? File.ReadAllText(Path.Combine(folder, name)) : string.Empty;

        return new EcoWorld(new MapWalls(folder), EcoLinks.Read(Text("links.txt")), EcoGrounds.Read(Text("eco-grounds.txt")), Stops(Text("guide.txt")),
            ClassKit.Read(Text("class-kit.txt")));
    }

    /// <summary>guide.txt 의 <c>about &lt;맵&gt; &lt;x&gt; &lt;y&gt; 판매: 이름, 이름…</c> 줄 — 같은 것을 파는 가게가 여럿이면 처음 것만.</summary>
    public static IReadOnlyList<EcoStop> Stops(string guide)
    {
        HashSet<string> seen = [];
        List<EcoStop> stops = [];

        foreach (string line in guide.Split('\n'))
        {
            string[] part = line.Trim().Split(' ', 5);
            if (part.Length < 5 || part[0] != "about" || !part[4].StartsWith("판매:", StringComparison.Ordinal)
                || !int.TryParse(part[1], out int map) || !int.TryParse(part[2], out int x) || !int.TryParse(part[3], out int y)
                || !seen.Add(part[4]))
            {
                continue;
            }

            int healing = part[4][3..].Split(',').Count(name => EcoShopping.IsHealing(name.Trim()));
            stops.Add(new EcoStop(map, new Tile(x, y), healing));
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
/// (<see cref="EcoParties" />, 결정 19), 5분마다 요약 한 줄.
/// </summary>
public sealed class EcoHost(EcoConfig config, EcoWorld world, EcoEvents events, Action<string> log)
{
    public static readonly TimeSpan Stagger = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan Summary = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Matching = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, EcoRunner> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _names = new(config.Bots.Select(bot => bot.Name), StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly List<EcoParty> _parties = [];

    public IReadOnlyCollection<EcoRunner> Running => [.. _running.Values];

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
