using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 리뷰 2026-10-08 #2·#5. 접속 중에 바꾼 비밀번호는 디스크에서 따로 읽은 사본에만 들어가, 그 사람의 다음 저장이 옛 비밀번호로 덮었다.
/// 저장이 실패해도 「saved」 줄과 저장 시각을 남기고, 비밀번호 바꾸기는 성공이라고 답했다.
/// </summary>
public sealed class AccountSaveTests : IDisposable
{
    private const string NewSecret = "another-test-secret";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_password_changed_while_playing_survives_the_next_save_and_leaving()
    {
        const string who = "pwonline";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        using (WorldSession session = await Login(server, who, LoginFlow.SyntheticSecret))
        {
            WorldClient world = Pump(session);
            await Until(() => world.State is not null, "들어가지 못했습니다.", TimeSpan.FromSeconds(30));

            Assert.Equal(0x00, LoginFlow.ChangePassword(server.LoginPort, who, LoginFlow.SyntheticSecret, NewSecret));

            // 접속한 채로 한 번 더 저장되게 — 바뀐 비밀번호가 그 사람(메모리)에 없으면 이 저장이 옛 것으로 덮는다.
            int mark = server.ConsoleOutput.Length;
            await Until(() => After(server, mark).Contains(Saved(who)), "접속 중 저장이 오지 않았습니다.", TimeSpan.FromSeconds(90));
        }

        WorldClient again = await LoginAgain(server, who, NewSecret);
        Assert.NotNull(again.State);
        await Assert.ThrowsAnyAsync<Exception>(() => Login(server, who, LoginFlow.SyntheticSecret));
    }

    [Fact]
    public async Task While_the_disk_refuses_nothing_claims_to_have_saved_and_saving_resumes_after()
    {
        if (OperatingSystem.IsWindows())
            return; // 폴더를 읽기 전용으로 만드는 방법이 다르다 — 서버는 맥·리눅스에서 돈다

        const string who = "savefail";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        using WorldSession session = await Login(server, who, LoginFlow.SyntheticSecret);
        WorldClient world = Pump(session);
        await Until(() => world.State is not null, "들어가지 못했습니다.", TimeSpan.FromSeconds(30));

        string folder = Path.Combine(server.ContentLocation, "aislings");
        int refused = server.ConsoleOutput.Length;
        File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            await Until(() => After(server, refused).Contains($"Aisling {who} could not be saved."),
                "저장하지 못한 것이 로그에 없습니다.", TimeSpan.FromSeconds(90));
            Assert.DoesNotContain(Saved(who), After(server, refused));
            Assert.NotEqual(0x00, LoginFlow.ChangePassword(server.LoginPort, who, LoginFlow.SyntheticSecret, NewSecret));
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        int resumed = server.ConsoleOutput.Length;
        await Until(() => After(server, resumed).Contains(Saved(who)), "쓰기를 풀었는데 다시 저장되지 않았습니다.", TimeSpan.FromSeconds(90));
    }

    private static string Saved(string who) => $"Aisling {who} data has been saved.";

    private static string After(IsolatedHadesServer server, int mark) => server.ConsoleOutput[mark..];

    private Task<WorldSession> Login(IsolatedHadesServer server, string who, string secret) =>
        HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, secret, progress: null, _deadline.Token);

    private WorldClient Pump(WorldSession session)
    {
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return world;
    }

    /// <summary>나간 직후에는 서버가 앞 접속을 아직 놓는 중일 수 있어 잠시 다시 해 본다.</summary>
    private async Task<WorldClient> LoginAgain(IsolatedHadesServer server, string who, string secret)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                WorldClient world = Pump(await Login(server, who, secret));
                await Until(() => world.State is not null, "다시 들어가지 못했습니다.", TimeSpan.FromSeconds(30));
                return world;
            }
            catch (Exception) when (DateTime.UtcNow < giveUp)
            {
                await Task.Delay(500, _deadline.Token);
            }
        }
    }

    private async Task Until(Func<bool> condition, string failure, TimeSpan limit)
    {
        DateTime giveUp = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100, _deadline.Token);
        }

        throw new TimeoutException(failure);
    }
}
