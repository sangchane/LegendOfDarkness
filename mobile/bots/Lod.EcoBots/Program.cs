using Lod.CompanionBot;
using Lod.EcoBots;
using Lod.Mobile.Core.Automation;

// 생태계 봇 프로그램 — 설정의 봇들이 혼자 사냥·장사하며 99레벨까지 자란다(설계 autopilot/eco-bots/). 봇마다 프로그램을 띄우지 않고
// 이 프로그램 하나가 봇 여럿을 접속시킨다(실측: 봇 220개에 0.5코어). 쓰는 법: Lod.EcoBots [설정 파일]
string configPath = args.Length > 0 ? args[0]
    : Environment.GetEnvironmentVariable("LOD_ECO_CONFIG") is { Length: > 0 } fromEnv ? fromEnv
    : Path.Combine(AppContext.BaseDirectory, "eco-bots.json");

EcoConfig config = EcoConfig.Load(configPath);
BotLog log = new("생태계", config.LogFile);
EcoWorld world = EcoWorld.Load(config.MapFolder);

using CancellationTokenSource stop = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
TaskScheduler.UnobservedTaskException += (_, e) => log.Write($"기다리지 않은 일에서 예외: {BotLogin.Describe(e.Exception)}");

log.Write($"시작 — {config.Host}:{config.LoginPort} · 봇 {Math.Min(config.MaxOnline, config.Bots.Count)}/{config.Bots.Count} · 사냥터 {world.Grounds.Count} · " +
          $"가게 물약 {(world.PotionStop is null ? 0 : 1)}·장비 {world.GearStops.Count} · 사건 {config.EventFolder}");

try
{
    EcoUnlisted unlisted = new(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, "eco-unlisted.json"));
    await new EcoHost(config, world, new EcoEvents(config.EventFolder), unlisted, log.Write).RunAsync(stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
}

log.Write("멈췄습니다.");
