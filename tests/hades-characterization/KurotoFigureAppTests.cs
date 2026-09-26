using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(아이폰, 2026-09-26): "쿠로토 회복량 안 보인다". 격리 서버에 무도가(시작 마법 쿠로토)를 만들고 체력을 반으로 둔 뒤,
/// 실제 앱을 띄워 마법 첫 칸(쿠로토)을 누르게 하고(<c>--skill m1</c>) 앱이 초록 "+N" 을 제 머리 위에 올리는지
/// (<c>GREYBOX_FIGURE Healed</c>) 본다. <c>LOD_KUROTO_SHOT</c> 에 png 경로를 줄 때만 돈다 — 앱 빌드와 Godot 이 있어야 한다.
/// </summary>
public sealed class KurotoFigureAppTests(ITestOutputHelper output) : IDisposable
{
    private const string Name = "kurotoshot";
    private const int WoodlandOneOne = 20015;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task The_app_floats_the_kuroto_heal_over_me()
    {
        if (Environment.GetEnvironmentVariable("LOD_KUROTO_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        // LOD_KUROTO_LEARN="호르라마" — 운영자로 만들어 그 마법을 더 배우게 한 뒤(/spell) 앱이 LOD_KUROTO_SKILL 칸을 누르게 한다.
        // 상태 아이콘 줄(내 판)의 실제 화면을 찍을 때 쓴다.
        string learn = Environment.GetEnvironmentVariable("LOD_KUROTO_LEARN") ?? string.Empty;
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, 2, 35));

        if (learn.Length > 0)
        {
            Waiting.MakeGameMaster(server, Name);
        }

        server.Start(TimeSpan.FromMinutes(2));

        using (WorldSession created = await HadesLoginClient.CreateCharacterAsync(
                   IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
                   hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token))
        {
            await Task.Delay(3000, _deadline.Token);

            if (learn.Length > 0)
            {
                Lod.Mobile.Core.World.WorldClient world = new(created);
                _ = world.PumpAsync(_deadline.Token);
                await Task.Delay(1500, _deadline.Token);
                await world.SayAsync($"/spell \"{learn}\" 1", _deadline.Token);
                await Task.Delay(2000, _deadline.Token);
                output.WriteLine($"배운 마법: {string.Join(", ", world.Spells.Select(one => $"{one.Slot}:{one.Name}:{one.TargetType}"))}");
                await world.LogOutAsync(_deadline.Token);
            }
        }

        await Task.Delay(3000, _deadline.Token);

        // 나간 뒤 저장본을 고친다 — 체력 반, 마력 가득(쿠로토를 몇 번 외울 만큼).
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(await File.ReadAllTextAsync(path, _deadline.Token))!;
        saved["_MaximumHp"] = 2000;
        saved["CurrentHp"] = 500;
        saved["_MaximumMp"] = 1000;
        saved["CurrentMp"] = 1000;
        await File.WriteAllTextAsync(path, saved.ToJsonString(), _deadline.Token);

        File.Delete(shot);
        string after = Environment.GetEnvironmentVariable("LOD_KUROTO_SHOT_AFTER") ?? "9";
        string orient = Environment.GetEnvironmentVariable("LOD_KUROTO_ORIENT") ?? "portrait";
        string size = orient == "portrait" ? "360x780" : "800x360";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // LOD_KUROTO_ENGINE: 고도 자체에 줄 인자(예 "--write-movie /tmp/f.png --fixed-fps 10") — 0.8초짜리 숫자를 프레임으로 떨군다.
        List<string> arguments = [.. (Environment.GetEnvironmentVariable("LOD_KUROTO_ENGINE") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        arguments.AddRange(
        [
            "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
            "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{Name}:{LoginFlow.SyntheticSecret}",
            "--orient", orient, "--size", size, "--skill", Environment.GetEnvironmentVariable("LOD_KUROTO_SKILL") ?? "m1", "--shot", shot, "--shot-after", after
        ]);

        if (Environment.GetEnvironmentVariable("LOD_KUROTO_EXTRA") is { Length: > 0 } extra)
        {
            arguments.AddRange(extra.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        string[] lines = [.. (await said).Split('\n').Where(line => line.Contains("GREYBOX_", StringComparison.Ordinal))];

        foreach (string line in lines)
        {
            output.WriteLine(line);
        }

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        // 더 배운 마법을 누르는 사진(상태 아이콘) 실행에서는 회복 숫자 대신 "대상을 먼저 누르라"는 거절이 없는지만 본다.
        if (learn.Length == 0)
        {
            Assert.Contains(lines, line => line.Contains("GREYBOX_FIGURE Healed", StringComparison.Ordinal));
        }
        else
        {
            Assert.DoesNotContain(lines, line => line.Contains("마법 대상을 먼저", StringComparison.Ordinal));
        }
    }
}
