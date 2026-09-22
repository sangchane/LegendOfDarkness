using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 체력·마력이 저절로 차는 것이 5.99 와 같은가. 5.99 서버(Novaonline.exe 0x46d1a5)는 21초마다(0x4766aa) 체력을
/// 최대 ÷ 100 × 지구력 ÷ 4.3, 마력을 최대 ÷ 100 × 지혜 ÷ 4.3 만큼 채우고, 그 값을 최대의 15%~25% 로 자른다.
/// </summary>
/// <remarks>
/// monk 계정과 같은 25레벨·지구력 65·최대 체력 3083 이면 30 × 65 ÷ 4.3 이 아니라 30.83 × 65 ÷ 4.3 = 466 이고
/// (15% 450 과 25% 750 사이), 지혜 100·최대 마력 1000 이면 232 다. 전에는 5초마다 체력 622 · 마력 206 이 찼다.
/// </remarks>
public sealed class Pack599RecoveryTests : IDisposable
{
    private const string Name = "recovercheck";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Health_and_mana_come_back_every_21_seconds_by_the_599_formula()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        EmptyTheRoom(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        saved["ExpLevel"] = 25;
        saved["_Con"] = 65;
        saved["_Wis"] = 100;
        saved["_MaximumHp"] = 3083;
        saved["CurrentHp"] = 1;
        saved["_MaximumMp"] = 1000;
        saved["CurrentMp"] = 1;
        File.WriteAllText(path, saved.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        Stopwatch since = Stopwatch.StartNew();
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        while (world.Vitals is not { Health: > 1 } && since.Elapsed < TimeSpan.FromSeconds(40))
        {
            await Task.Delay(20, _deadline.Token);
        }

        TimeSpan first = since.Elapsed;
        Vitals? now = world.Vitals;
        Assert.True(now is { Health: > 1 }, "40초를 기다렸는데 체력이 차지 않았습니다.");

        // 들어온 뒤 첫 회복까지 걸린 시간. 타이머는 접속할 때 돌기 시작하니 21초보다 조금 짧을 수 있다.
        Assert.True(first > TimeSpan.FromSeconds(15),
            $"들어온 지 {first.TotalSeconds:0.0}초 만에 체력이 찼습니다 — 5.99 는 21초마다 채웁니다.");
        Assert.Equal(1 + 466, now!.Health);
        Assert.Equal(1 + 232, now.Mana);
    }

    private static void EmptyTheRoom(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (woodland.IsMatch(text))
            {
                JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
                template["SpawnMax"] = 0;
                File.WriteAllText(path, template.ToJsonString());
            }
        }
    }
}
