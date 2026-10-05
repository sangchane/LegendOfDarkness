using System.Net;
using System.Text.Json;
using Lod.CompanionBot;
using Lod.HuntProxy;
using Lod.Mobile.Core.Ui;

// 대신 사냥 대리 프로그램 — 게임 서버가 적는 작업 파일(ProxyJobFolder/이름.json)을 1초마다 집어, 그 캐릭터로 일회용 열쇠를 대고
// 들어가 앱의 자동 사냥 설정대로 사냥한다. 설계 autopilot/proxy-hunt/. 쓰는 법: Lod.HuntProxy [설정 파일]
string configPath = args.Length > 0 ? args[0]
    : Environment.GetEnvironmentVariable("LOD_PROXY_CONFIG") is { Length: > 0 } fromEnv ? fromEnv
    : Path.Combine(AppContext.BaseDirectory, "hunt-proxy.json");

ProxyConfig config = ProxyConfig.Load(configPath);
BotLog log = new("대신사냥", config.LogFile);
MapWalls walls = new(config.MapFolder);
string guidePath = Path.Combine(config.MapFolder, "guide.txt");
MapGuide guide = config.MapFolder.Length > 0 && File.Exists(guidePath) ? MapGuide.Read(File.ReadAllText(guidePath)) : MapGuide.Empty;

using CancellationTokenSource stop = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
TaskScheduler.UnobservedTaskException += (_, e) => log.Write($"기다리지 않은 일에서 예외: {BotLogin.Describe(e.Exception)}");

log.Write($"시작 — {config.Host}:{config.LoginPort} · 작업 {config.JobFolder} · 벽 {config.MapFolder} · 출구 {(guide == MapGuide.Empty ? "없음" : "있음")} · 동시 {config.Max}");

ProxyHost host = new(config, walls, guide, log.Write);

try
{
    await host.RunAsync(stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
}

log.Write("멈췄습니다.");

namespace Lod.HuntProxy
{
    /// <summary>대리 프로그램 설정(<c>hunt-proxy.json</c>). 비밀은 없다 — 열쇠는 작업 파일에만, 한 번 쓰고 지운다.</summary>
    public sealed record ProxyConfig
    {
        public string Host { get; init; } = "127.0.0.1";
        public int LoginPort { get; init; } = 2610;
        public string JobFolder { get; init; } = string.Empty;
        public string MapFolder { get; init; } = string.Empty;
        public string? LogFile { get; init; }
        public int Max { get; init; } = 10;

        public static ProxyConfig Load(string path)
        {
            ProxyConfig config = JsonSerializer.Deserialize<ProxyConfig>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"'{path}' 가 비어 있습니다.");

            if (config.JobFolder.Length == 0)
            {
                throw new InvalidDataException($"'{path}' 에 JobFolder 를 적어 주세요(서버 설정 ProxyJobFolder 와 같은 곳).");
            }

            string here = Path.GetDirectoryName(Path.GetFullPath(path))!;
            string wanted = config.LogFile ?? Path.Combine("logs", "hunt-proxy.log");

            return config with
            {
                JobFolder = Path.GetFullPath(config.JobFolder, here),
                MapFolder = config.MapFolder.Length == 0 ? string.Empty : Path.GetFullPath(config.MapFolder, here),
                LogFile = wanted.Length == 0 ? string.Empty : Path.GetFullPath(wanted, here),
            };
        }
    }

    /// <summary>서버가 적는 작업 파일 하나.</summary>
    public sealed record ProxyJob(string Name, string Token, DateTime Until, Lod.Mobile.Core.Automation.ProxyOrders Orders)
    {
        /// <summary>작업 파일을 읽는다. 틀리면 null.</summary>
        public static ProxyJob? Parse(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                Lod.Mobile.Core.Automation.ProxyOrders? orders =
                    Lod.Mobile.Core.Automation.ProxyOrders.Parse(root.GetProperty("settings").GetRawText());

                return orders is null
                    ? null
                    : new ProxyJob(root.GetProperty("name").GetString()!, root.GetProperty("token").GetString()!,
                        root.GetProperty("until").GetDateTime().ToUniversalTime(), orders);
            }
            catch (Exception failed) when (failed is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                return null;
            }
        }
    }

    /// <summary>작업 폴더를 보고, 작업마다 접속 하나를 띄운다.</summary>
    public sealed class ProxyHost(ProxyConfig config, MapWalls walls, MapGuide guide, Action<string> log)
    {
        public static readonly TimeSpan Look = TimeSpan.FromSeconds(1);

        private readonly Dictionary<string, Task> _running = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>돌고 있는 대리 수(시험용).</summary>
        public int Running
        {
            get
            {
                lock (_running)
                {
                    return _running.Count;
                }
            }
        }

        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TakeJobs(token);
                await Task.Delay(Look, token);
            }
        }

        /// <summary>작업 파일을 집는다 — 읽고 바로 지운다(열쇠는 한 번이다).</summary>
        public void TakeJobs(CancellationToken token)
        {
            if (!Directory.Exists(config.JobFolder))
            {
                return;
            }

            foreach (string path in Directory.GetFiles(config.JobFolder, "*.json"))
            {
                string text;
                try
                {
                    text = File.ReadAllText(path);
                    File.Delete(path);
                }
                catch (IOException)
                {
                    continue;
                }

                if (ProxyJob.Parse(text) is not { } job)
                {
                    log($"읽지 못한 작업 파일을 버림: {Path.GetFileName(path)}");
                    continue;
                }

                lock (_running)
                {
                    if (_running.ContainsKey(job.Name))
                    {
                        log($"{job.Name}: 이미 대신 사냥 중 — 새 작업은 버림");
                        continue;
                    }

                    if (_running.Count >= config.Max)
                    {
                        log($"{job.Name}: 동시 {config.Max} 명이 넘어 버림");
                        continue;
                    }

                    _running[job.Name] = Task.Run(() => Hunt(job, token), token);
                }
            }
        }

        private async Task Hunt(ProxyJob job, CancellationToken token)
        {
            DateTime began = DateTime.UtcNow;
            string why = "끝";

            try
            {
                IPAddress address = IPAddress.TryParse(config.Host, out IPAddress? parsed)
                    ? parsed
                    : (await Dns.GetHostAddressesAsync(config.Host, token))[0];

                // 열쇠는 비밀번호 자리에 — 서버가 같은 기계에서 온 것만 받는다. 기록에 적지 않는다.
                using Lod.Mobile.Core.Net.WorldSession session = await Lod.Mobile.Core.Net.HadesLoginClient.LoginAsync(
                    address, config.LoginPort, job.Name, job.Token, null, token);
                using Lod.Mobile.Core.Protocol.World.WorldClient world = new(session);
                Task pump = world.PumpAsync(token);
                log($"{job.Name}: 대리 접속 — {job.Until:HH:mm} UTC 까지");

                HuntProxyRunner runner = new(world, walls, guide, job.Orders, job.Until, line => log($"{job.Name}: {line}"));
                Task ended = await Task.WhenAny(pump, runner.RunAsync(token));
                why = runner.Stopped ?? (world.Broke is { } broke ? $"끊김(앱이 돌아왔거나 서버) — {broke}" : ended == pump ? "받기가 끝남" : "판단이 끝남");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                why = "프로그램이 멈춤";
            }
            catch (Exception failed)
            {
                why = $"들어가지 못함 — {BotLogin.Describe(failed)}";
            }
            finally
            {
                lock (_running)
                {
                    _running.Remove(job.Name);
                }

                log($"{job.Name}: 대신 사냥 끝 — {why} · {(DateTime.UtcNow - began).TotalMinutes:0.0}분");
            }
        }
    }
}
