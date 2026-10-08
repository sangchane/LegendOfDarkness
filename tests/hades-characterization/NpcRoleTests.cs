using System.Net;
using Lod.Mobile.Core.Net;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// NPC 역할 아이콘(사용자 2026-10-08 「미니맵에서 찾기 쉽게 … 어느 npc가 뭐하는지」, <c>autopilot/npc-roles/SPEC.md</c>) — 실제 앱이 마을에
/// 들어가 미니맵·문 표지·머리 위 이름표·길 찾기 창에 역할 아이콘을 그리는지 사진으로 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class NpcRoleTests : IDisposable
{
    private const string Name = "rolelook";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 확인 사진 — <c>LOD_ROLE_SHOT</c> 에 png 경로를 줄 때만 돈다. <c>LOD_ROLE_AT</c> = 「맵,x,y」(기본 수오미마을 무기점·방어구점 문
    /// 사이 20355,15,51), <c>LOD_ROLE_ARGS</c> = 앱에 더 줄 인자(빈칸으로 가름 — 예 「--tabmap」 · 「--tabmap-npc 수오미무기점」).
    /// </summary>
    [Fact]
    public async Task Photograph_what_the_npcs_do()
    {
        if (Environment.GetEnvironmentVariable("LOD_ROLE_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        int[] at = [.. (Environment.GetEnvironmentVariable("LOD_ROLE_AT") ?? "20355,15,51").Split(',').Select(int.Parse)];
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (at[0], at[1], at[2]));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        // 한 번 들어갔다 나와 캐릭터를 그 자리에 둔다.
        using (WorldSession session = await HadesLoginClient.LoginAsync(
                   IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token))
        {
            await Task.Delay(2000, _deadline.Token);
        }

        await Task.Delay(2000, _deadline.Token);

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
                     "--orient", "portrait", "--size", "360x780", "--shot", shot, "--shot-after", "12",
                 }.Concat((Environment.GetEnvironmentVariable("LOD_ROLE_ARGS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        _ = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
    }
}
