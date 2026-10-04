using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 무도가 기술 꾸러미·다라밀공 조준(2026-10-04) — 실제 앱. 99레벨 무도가가 다라밀공을 배운 채 우드랜드1-1 에 서고, 앱이 다라밀공 칸을
/// 손 없이 누르면(<c>--skill m다라밀공</c>) 고른 이가 없으니 가장 가까운 괴물에 나간다(<c>GREYBOX_AIM 다라밀공 → 괴물 탭</c>).
/// <c>LOD_AIM_SHOT</c> 에 png 경로를 줄 때만 돈다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class MonkAimTests : IDisposable
{
    private const string Name = "monkaim";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Tapping_daramilgong_with_nobody_picked_goes_at_the_nearest_monster()
    {
        if (Environment.GetEnvironmentVariable("LOD_AIM_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        Waiting.MakeGameMaster(server, Name);
        PutTarget(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["Path"] = 5;
            saved["ExpLevel"] = 99;
            saved["_MaximumHp"] = 5000;
            saved["CurrentHp"] = 5000;
            saved["_MaximumMp"] = 30000;
            saved["CurrentMp"] = 30000;
        });

        // 알맹이로 들어가 다라밀공을 배우고 나온다 — 앱은 그 캐릭터로 다시 들어간다.
        using (WorldSession session = await HadesLoginClient.LoginAsync(
                   IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token))
        {
            WorldClient world = new(session);
            _ = world.PumpAsync(_deadline.Token);
            await Waiting.Until(() => world.State is not null, "무도가가 서지 못했습니다.", _deadline.Token);
            await world.SayAsync("/spell \"다라밀공\" 1", _deadline.Token);
            await Waiting.Until(() => world.Spells.Any(one => one.Name.StartsWith("다라밀공", StringComparison.Ordinal)),
                "다라밀공을 배우지 못했습니다.", _deadline.Token);
            await world.LogOutAsync(_deadline.Token);
            await Task.Delay(2000, _deadline.Token);
        }

        File.Delete(shot);
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{Name}:{LoginFlow.SyntheticSecret}",
                     "--orient", "portrait", "--size", "360x780",
                     "--skill", "m다라밀공", "--shot", shot, "--shot-after", "12"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        string output = await said;
        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        // 쏜 괴물이 실제로 맞았다 — 같은 번호에 피해 숫자(0x5D)가 뜬다. 두 번째부터는 마력이 바닥이라 거절된다(다라밀공은 마력을 다 쓴다).
        Match aim = new Regex(@"GREYBOX_AIM 다라밀공\S* \S+ → (\d+) 탭").Match(output);
        Assert.True(aim.Success && output.Contains($"GREYBOX_FIGURE Dealt", StringComparison.Ordinal)
                    && output.Contains($"on {aim.Groups[1].Value}", StringComparison.Ordinal),
            string.Join("\n", output.Split('\n').Where(line => line.Contains("GREYBOX") || line.Contains("다라밀") || line.Contains("서버")).Take(40)));
    }

    /// <summary>입구 세 칸 위에 움직이지 않는 표적 — 우드랜드1-1 괴물 하나를 본떠 만든다.</summary>
    private static void PutTarget(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "조준시험표적";
        target["BaseName"] = "조준시험표적";
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = Start.X;
        target["DefinedY"] = Start.Y - 3;
        target["MaximumHP"] = 1_000_000_000;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "monk-aim-target.json"), target.ToJsonString());
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }
}
