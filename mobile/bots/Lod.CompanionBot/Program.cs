using System.Net;
using System.Net.Sockets;
using Lod.CompanionBot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Protocol;

// 동료 봇(성직자) — 설정 파일의 계정으로 서버에 접속해 기다리다가, 서버가 주인을 정해 주면(0x5E) 따라다니며 회복·버프를 건다.
// 쓰는 법: Lod.CompanionBot [설정 파일]   (없으면 LOD_BOT_CONFIG, 그것도 없으면 실행 파일 옆 companion-bot.json)
string configPath = args.Length > 0 ? args[0]
    : Environment.GetEnvironmentVariable("LOD_BOT_CONFIG") is { Length: > 0 } fromEnv ? fromEnv
    : Path.Combine(AppContext.BaseDirectory, "companion-bot.json");

BotConfig config = BotConfig.Load(configPath);
MapWalls walls = new(config.MapFolder);

using CancellationTokenSource stop = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();

BotLog log = new(config.Name, config.LogFile);
void Log(string line) => log.Write(line);

// 삼킨 예외도 기록에 — 기다리지 않은 Task 가 터지면 아무 말 없이 사라졌다.
TaskScheduler.UnobservedTaskException += (_, e) => Log($"기다리지 않은 일에서 예외: {BotLogin.Describe(e.Exception)}");
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Log($"잡지 못한 예외(프로그램이 멈춤): {(e.ExceptionObject is Exception failed ? BotLogin.Describe(failed) : e.ExceptionObject)}");

Log($"시작 — {config.Host}:{config.LoginPort} · 벽 파일 {(config.MapFolder.Length > 0 ? config.MapFolder : "없음")} · 기록 파일 {(config.LogFile is { Length: > 0 } logFile ? logFile : "없음")}");

TimeSpan wait = TimeSpan.FromSeconds(5);
int attempt = 0;

while (!stop.IsCancellationRequested)
{
    attempt++;
    DateTime began = DateTime.UtcNow;

    try
    {
        using WorldSession session = await BotLogin.EnterAsync(config, Log, stop.Token);
        using WorldClient world = new(session);
        Task pump = world.PumpAsync(stop.Token);
        Log($"접속했습니다(로그인 {attempt}번째 시도, {(DateTime.UtcNow - began).TotalSeconds:0.0}초) — 주인을 기다립니다.");
        wait = TimeSpan.FromSeconds(5);
        attempt = 0;
        DateTime entered = DateTime.UtcNow;

        CompanionRunner runner = new(world, walls, config.Settings, Log);
        Task running = runner.RunAsync(stop.Token);
        Task ended = await Task.WhenAny(pump, running);

        List<string> why = [];
        if (world.Broke is { } broke)
        {
            why.Add($"받기 끊김: {broke}");
        }

        if (ended.Exception is { } failed)
        {
            why.Add($"{(ended == pump ? "받기" : "판단")} 예외: {BotLogin.Describe(failed.GetBaseException())}");
        }

        if (runner.Stopped is { } stopped)
        {
            why.Add(stopped);
        }

        Log($"끊겼습니다 — {(why.Count > 0 ? string.Join(" · ", why) : ended == pump ? "받기가 끝남" : "판단이 끝남")} · 접속 {(DateTime.UtcNow - entered).TotalMinutes:0.0}분 · 마지막 패킷 {(DateTime.UtcNow - world.LastHeard).TotalSeconds:0}초 전");
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
        break;
    }
    catch (Exception failed) when (!stop.IsCancellationRequested)
    {
        Log($"접속하지 못했습니다({attempt}번째, 다음 시도 {wait.TotalSeconds:0}초 뒤): {BotLogin.Describe(failed)}");
    }

    // 다시 접속 — 5초에서 시작해 1분까지 늘린다.
    try
    {
        await Task.Delay(wait, stop.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }

    wait = TimeSpan.FromSeconds(Math.Min(60, wait.TotalSeconds * 2));
}

Log("멈췄습니다.");
