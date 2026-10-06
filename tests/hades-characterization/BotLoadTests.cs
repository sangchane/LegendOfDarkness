using System.Diagnostics;
using System.Net;
using Lod.CompanionBot;
using Lod.HuntProxy;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 봇 수용량 실측(설계 <c>autopilot/eco-bots/</c>) — 봇 N 개를 <b>이 프로그램 하나</b>에서 접속시켜 자동 사냥(대신 사냥과 같은 판단)을 돌리고,
/// 서버 프로세스 CPU·메모리와 봇당 경험치를 잰다. 평소에는 건너뛴다: <c>LOD_BOT_LOAD=25,50,100</c> 이면 돈다.
/// <c>LOD_BOT_LOAD_MAPS=20015,20263</c> 이면 그 맵들에 고루 나눈다(없으면 한 맵 — 가장 나쁜 경우).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class BotLoadTests(ITestOutputHelper output)
{
    private const int WoodlandOneOne = 20015;
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Measure_server_cost_per_hunting_bot()
    {
        if (Environment.GetEnvironmentVariable("LOD_BOT_LOAD") is not { Length: > 0 } counts)
        {
            return;
        }

        int[] maps = Environment.GetEnvironmentVariable("LOD_BOT_LOAD_MAPS") is { Length: > 0 } listed
            ? [.. listed.Split(',').Select(int.Parse)]
            : [WoodlandOneOne];

        foreach (int count in counts.Split(',').Select(int.Parse))
        {
            string line = await Run(count, maps);
            output.WriteLine(line);
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "lod-bot-load.txt"), line + Environment.NewLine);
        }
    }

    private async Task<string> Run(int count, int[] maps)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(20));
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        Process process = server.Process!;

        MapWalls walls = new(HadesWorkspace.MapLayoutFolder);
        Random dice = new(count);
        string[] names = [.. Enumerable.Range(0, count).Select(i => $"loadbot{i}")];

        for (int i = 0; i < count; i++)
        {
            int map = maps[i % maps.Length];
            Tile where = Walkable(map, walls, dice);
            LoginFlow.TryCreateAccount(server, names[i]);
            CompanionCallTests.Edit(server, names[i], saved =>
            {
                // 측정 동안 죽지 않게 — 죽으면 사망 맵(뮤레칸의방) 한 곳에 모여 사냥 부하가 아니게 된다.
                saved["ExpLevel"] = 30;
                saved["_MaximumHp"] = 100_000;
                saved["CurrentHp"] = 100_000;
                saved["_Str"] = 100;
                saved["CurrentMapId"] = map;
                saved["XPos"] = where.X;
                saved["YPos"] = where.Y;
            });
        }

        double idleCores = await Cores(process, TimeSpan.FromSeconds(10), deadline.Token);

        List<(WorldSession Session, WorldClient World)> bots = [];
        List<Task> running = [];
        DateTime until = DateTime.UtcNow + TimeSpan.FromMinutes(15);

        foreach (string name in names)
        {
            WorldSession session = await HadesLoginClient.LoginAsync(
                IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, null, deadline.Token);
            WorldClient world = new(session);
            _ = world.PumpAsync(deadline.Token);
            bots.Add((session, world));
        }

        await Waiting.Until(() => bots.All(bot => bot.World.State is not null && bot.World.Serial != 0),
            "봇이 다 서지 못했습니다.", deadline.Token, within: TimeSpan.FromMinutes(2));

        foreach ((_, WorldClient world) in bots)
        {
            ProxyOrders orders = new()
            {
                Radius = 12, Map = world.State!.Map.Id, X = world.State.Where.X, Y = world.State.Where.Y,
                Hp = new PotionRule(false, 70, "쿠룸"),
            };
            HuntProxyRunner runner = new(world, walls, MapGuide.Empty, orders, until);
            running.Add(runner.RunAsync(deadline.Token));
        }

        await Task.Delay(Warmup, deadline.Token);

        Process me = Process.GetCurrentProcess();
        long expBefore = bots.Sum(bot => bot.World.Vitals?.Experience ?? 0);
        TimeSpan botCpuBefore = me.TotalProcessorTime;
        double cores = await Cores(process, Window, deadline.Token);
        me.Refresh();
        double botCores = (me.TotalProcessorTime - botCpuBefore).TotalSeconds / Window.TotalSeconds;
        long expAfter = bots.Sum(bot => bot.World.Vitals?.Experience ?? 0);
        process.Refresh();
        long serverMb = process.WorkingSet64 / (1024 * 1024);
        int alive = bots.Count(bot => !bot.World.IsDisposed && bot.World.Broke is null);
        int distinctMaps = bots.Select(bot => bot.World.State?.Map.Id).Distinct().Count();
        string spread = string.Join(",", bots.GroupBy(bot => bot.World.State?.Map.Id).OrderByDescending(group => group.Count()).Take(5).Select(group => $"{group.Key}:{group.Count()}"));

        foreach ((WorldSession session, _) in bots)
        {
            session.Dispose();
        }

        return $"봇 {count} · 맵 {distinctMaps}개({spread}) · 살아 있음 {alive} · 서버 CPU {cores:0.00}코어(봇 없을 때 {idleCores:0.00}) · " +
               $"서버 메모리 {serverMb}MB · 봇 프로그램 CPU {botCores:0.00}코어 · 봇당 경험치/분 {(expAfter - expBefore) / (double)count / Window.TotalMinutes:0}";
    }

    private static async Task<double> Cores(Process process, TimeSpan window, CancellationToken token)
    {
        process.Refresh();
        TimeSpan before = process.TotalProcessorTime;
        await Task.Delay(window, token);
        process.Refresh();
        return (process.TotalProcessorTime - before).TotalSeconds / window.TotalSeconds;
    }

    /// <summary>맵 가운데쯤의 걸을 수 있는 칸 하나.</summary>
    private static Tile Walkable(int map, MapWalls walls, Random dice)
    {
        Func<Tile, bool> blocked = walls.For(map);

        for (int tries = 0; tries < 2000; tries++)
        {
            Tile tile = new(dice.Next(5, 55), dice.Next(5, 55));
            if (!blocked(tile))
            {
                return tile;
            }
        }

        return new Tile(25, 25);
    }
}
