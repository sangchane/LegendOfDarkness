using System.Net;
using Lod.Mobile.Core.Net;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(2026-09-27): "지도 클릭하면 5존 26,0 27,0 이동정보 안 뜬다". 이 두 칸은 밟으면 스크립트(`포테의숲오솔길입장`)가 도는
/// 워프라 템플릿의 도착이 제자리(5존)여서, 길 찾기 창에 "포테의숲5존" 으로 적혔다. `build-client-guide.py` 가 스크립트가
/// 짓는 사본 이름을 적게 고쳤다. 격리 서버의 5존에 선 캐릭터로 실제 앱을 띄워 미니맵을 눌러 길 찾기 창을 열고
/// (<c>--tabmap</c>), 그 출구를 이름으로 눌러(<c>--tabmap-go</c>) 걷기 시작하는지 본다. <c>LOD_POTE_TABMAP_SHOT</c> 에 png 경로를
/// 줄 때만 돈다 — 앱 빌드와 Godot 이 있어야 한다.
/// </summary>
public sealed class PoteTabMapAppTests(ITestOutputHelper output) : IDisposable
{
    private const string Name = "potetabmap";
    private const int FifthZone = 20267;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task The_tab_map_names_the_trail_entrance_and_walks_to_it()
    {
        if (Environment.GetEnvironmentVariable("LOD_POTE_TABMAP_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (FifthZone, 16, 16));

        // 5존 괴물은 치운다 — 엔트자이언트의 나르콜리에 잠들면 걷지 못한다(PoteDungeonTests 와 같다).
        foreach (string hunter in Directory.GetFiles(Path.Combine(server.ContentLocation, "templates", "monsters", "5.99"), "*@포테의숲5존.json"))
        {
            File.Delete(hunter);
        }

        server.Start(TimeSpan.FromMinutes(2));

        using (WorldSession created = await HadesLoginClient.CreateCharacterAsync(
                   IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
                   hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token))
        {
            await Task.Delay(3000, _deadline.Token);
        }

        await Task.Delay(3000, _deadline.Token);

        File.Delete(shot);
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{Name}:{LoginFlow.SyntheticSecret}",
                     "--orient", "portrait", "--size", "360x780", "--tabmap", "--tabmap-go", "개인 던전",
                     "--shot", shot, "--shot-after", Environment.GetEnvironmentVariable("LOD_POTE_TABMAP_SHOT_AFTER") ?? "9"
                 })
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
        Assert.Contains(lines, line => line.Contains("GREYBOX_TABMAP_GO 포테의숲오솔길(개인 던전)", StringComparison.Ordinal));
    }
}
