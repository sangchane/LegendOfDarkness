using System.Net;
using System.Net.Sockets;
using Lod.CompanionBot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

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

void Log(string line) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} [{config.Name}] {line}");

Log($"시작 — {config.Host}:{config.LoginPort} · 벽 파일 {(config.MapFolder.Length > 0 ? config.MapFolder : "없음")}");

TimeSpan wait = TimeSpan.FromSeconds(5);

while (!stop.IsCancellationRequested)
{
    try
    {
        using WorldSession session = await BotLogin.EnterAsync(config, Log, stop.Token);
        using WorldClient world = new(session);
        Task pump = world.PumpAsync(stop.Token);
        Log("접속했습니다 — 주인을 기다립니다.");
        wait = TimeSpan.FromSeconds(5);

        CompanionRunner runner = new(world, walls, config.Settings, Log);
        await Task.WhenAny(pump, runner.RunAsync(stop.Token));
        Log($"끊겼습니다{(world.Broke is { } why ? $": {why}" : string.Empty)}");
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
        break;
    }
    catch (Exception failed) when (!stop.IsCancellationRequested)
    {
        Log($"접속하지 못했습니다: {BotLogin.Describe(failed)}");
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
