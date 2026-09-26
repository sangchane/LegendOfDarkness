using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.CompanionBot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 동료 봇 프로그램의 판단 루프(<see cref="CompanionRunner" />)를 격리 서버에 대고 돈다: 사람이 괴물(포테의숲1존 사슴 —
/// <see cref="PoteForestDeerDangerTests" /> 와 같은 한 마리)에게 맞아 체력이 줄면 봇이 회복 마법을 걸어 체력이 오르고,
/// 사람이 걸어가면 봇이 걸어서 따라온다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionBotTests : IDisposable
{
    private const string OwnerName = "botowner";
    private const int ForestOne = 20263;
    private static readonly Tile Start = new(33, 47);
    private static readonly Tile Ahead = new(33, 46);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task The_bot_heals_its_hurt_owner_and_walks_after_them()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (ForestOne, Start.X, Start.Y));
        CompanionCallTests.Configure(server);
        OneDeerAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);
        CompanionCallTests.Edit(server, OwnerName, saved =>
        {
            saved["ExpLevel"] = 30;
            saved["_MaximumHp"] = 3000;
            saved["CurrentHp"] = 3000;
        });
        // 봇은 마을(노비스마을)에서 기다린다 — 사냥터에 세워 두면 부르기 전에 사슴에게 쓰러진다(실제로 그랬다).
        CompanionCallTests.Edit(server, CompanionCallTests.BotName, InTheVillage);

        // 봇 — 프로그램과 같은 루프.
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        List<string> said = [];
        CompanionRunner runner = new(bot, new MapWalls(HadesWorkspace.MapLayoutFolder), new CompanionSettings(), line =>
        {
            lock (said)
            {
                said.Add(line);
            }
        });
        _ = runner.RunAsync(_deadline.Token);

        WorldClient owner = await Enter(server, OwnerName);
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);
        while (!(owner.State?.Where == Start && owner.Creatures.Any(c => c.Where == Ahead)))
        {
            Assert.True(DateTime.UtcNow < giveUp, "사슴 앞칸에 서지 못했습니다.");
            await owner.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
        }

        await owner.TurnAsync(Direction.North, _deadline.Token);
        await owner.CallCompanionAsync(_deadline.Token);
        await Waiting.Until(() => bot.Master?.Serial == owner.Serial, "봇에게 주인이 정해지지 않았습니다.", _deadline.Token);

        // 맞아서 70% 아래로 → 봇이 회복(0x5D 회복 숫자, 자연 회복보다 훨씬 크다 — 쿠라노 = 위즈×20).
        int lowest = int.MaxValue;
        Figure? healed = null;
        await Waiting.Until(() =>
        {
            lowest = Math.Min(lowest, owner.Vitals?.Health ?? int.MaxValue);

            while (owner.TakeFigure(out Figure? figure))
            {
                if (figure.Target == owner.Serial && figure.Kind == FigureKind.Heal && figure.Amount >= 500 && lowest < 2100)
                {
                    healed = figure;
                }
            }

            return healed is not null;
        }, $"봇이 회복하지 않았습니다. 가장 낮던 체력 {lowest} · 봇이 한 일: {Joined(said)}", _deadline.Token, TimeSpan.FromSeconds(90));

        Assert.Contains(Joined(said).Split(" | "), line => line.StartsWith("주인 회복", StringComparison.Ordinal) ||
                                                        line.StartsWith("파티 회복", StringComparison.Ordinal));
        await Waiting.Until(() => owner.Vitals?.Health > lowest, "회복 뒤 체력이 오르지 않았습니다.", _deadline.Token);

        // 사람이 서쪽으로 여섯 칸 — 봇이 걸어서 따라온다(서버가 옮겨 주는 12칸보다 가깝다).
        Tile botBefore = Where(owner, bot.Serial) ?? throw new InvalidOperationException("사람 화면에 봇이 없습니다.");

        for (int step = 0; step < 6; step++)
        {
            await owner.WalkAsync(Direction.West, _deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }

        await Waiting.Until(() =>
                Where(bot, owner.Serial) is { } o && Where(owner, bot.Serial) is { } b && b != botBefore &&
                Math.Abs(o.X - b.X) + Math.Abs(o.Y - b.Y) <= 3,
            $"봇이 따라오지 않았습니다: 사람 {Where(bot, owner.Serial)} · 봇 {Where(owner, bot.Serial)}", _deadline.Token);
        Assert.Equal(ForestOne, bot.State?.Map.Id);
    }

    /// <summary>
    /// 사진 한 장(손 없이): 앱으로 들어온 사람이 설정 창의 [동료 부르기] 를 스스로 누르고(<c>--companion</c>), 사슴에게 맞는 동안
    /// 봇이 곁에서 회복을 건다. <c>LOD_COMPANION_SHOT=경로</c> 가 있을 때만 돈다 — 창은 화면 밖, 소리 끔.
    /// </summary>
    [Fact]
    public async Task Photograph_the_bot_healing_its_owner()
    {
        if (Environment.GetEnvironmentVariable("LOD_COMPANION_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (ForestOne, Start.X, Start.Y));
        CompanionCallTests.Configure(server);
        OneDeerAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);
        CompanionCallTests.Edit(server, OwnerName, saved =>
        {
            saved["ExpLevel"] = 30;
            saved["_MaximumHp"] = 3000;
            saved["CurrentHp"] = 2200;
        });
        CompanionCallTests.Edit(server, CompanionCallTests.BotName, InTheVillage);

        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        List<string> said = [];
        _ = new CompanionRunner(bot, new MapWalls(HadesWorkspace.MapLayoutFolder), new CompanionSettings(), line =>
        {
            lock (said)
            {
                said.Add($"{DateTime.Now:HH:mm:ss.f} {line}");
            }
        }).RunAsync(_deadline.Token);

        File.Delete(shot);
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{OwnerName}:{LoginFlow.SyntheticSecret}",
                     "--orient", "portrait", "--size", "360x780", "--companion",
                     "--shot", shot, "--shot-after", Environment.GetEnvironmentVariable("LOD_COMPANION_SHOT_AFTER") ?? "16"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> printed = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        // 무엇이 있었는지 사진 옆에 남긴다 — 앱의 GREYBOX 줄과 봇이 한 일.
        File.WriteAllLines(shot + ".txt",
        [
            .. (await printed).Split('\n').Where(line => line.Contains("GREYBOX_", StringComparison.Ordinal)),
            .. said,
        ]);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
    }

    private static void InTheVillage(JsonNode saved)
    {
        saved["CurrentMapId"] = 20373;
        saved["XPos"] = 37;
        saved["YPos"] = 29;
    }

    /// <summary>한 사람의 화면에서 다른 사람이 선 칸(0x33·0x0C).</summary>
    private static Tile? Where(WorldClient viewer, uint serial) =>
        viewer.Others.FirstOrDefault(one => one.Serial == serial)?.Where;

    private static string Joined(List<string> lines)
    {
        lock (lines)
        {
            return string.Join(" | ", lines);
        }
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Serial != 0, $"{who} 가 월드에 서지 못했습니다.", _deadline.Token);
        return world;
    }

    /// <summary>포테의숲1존에 사슴 한 마리만 — 사람 앞칸에 붙박이로, 먼저 덤빈다(<see cref="PoteForestDeerDangerTests" /> 와 같다).</summary>
    private static void OneDeerAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex zoneOne = new($"\"AreaID\"\\s*:\\s*{ForestOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!zoneOne.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;

            if ((string?)template["Name"] == "사슴")
            {
                template["SpawnType"] = 4;
                template["SpawnRate"] = 100_000;
                template["DefinedX"] = Ahead.X;
                template["DefinedY"] = Ahead.Y;
                template["MoodType"] = 2;
                template["PathQualifer"] = 2;
            }
            else
            {
                template["SpawnMax"] = 0;
            }

            File.WriteAllText(path, template.ToJsonString());
        }
    }
}
