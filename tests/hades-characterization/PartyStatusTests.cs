using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 파티원 칸(2026-09-26) — 원작은 그룹원의 체력을 보내지 않는다(맞는 것이 보일 때의 0x13 백분율뿐). 우리 확장 0x5E 종류 6 으로
/// 서버가 1초마다 같은 그룹원에게 서로의 체력 %·마력 %·상태 그림을 보낸다. 둘이 그룹이 되면 서로의 %가 오고, 한 사람의 체력이
/// 바뀌면(쿠로토) 다른 사람에게 온 %도 따라 바뀐다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class PartyStatusTests : IDisposable
{
    private const string Leader = "statuslead";
    private const string Member = "statusmate";
    private static readonly (int Map, int X, int Y) Town = (20373, 37, 29);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Group_members_hear_each_others_health_and_it_follows_changes()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Waiting.MakeGameMaster(server, Member);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Leader);
        LoginFlow.TryCreateAccount(server, Member);

        // 그룹원의 체력을 반으로 — 1000 중 500, 마력 가득.
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Member}.json");
        JsonNode saved = JsonNode.Parse(await File.ReadAllTextAsync(path, _deadline.Token))!;
        saved["_MaximumHp"] = 1000;
        saved["CurrentHp"] = 500;
        saved["_MaximumMp"] = 1000;
        saved["CurrentMp"] = 1000;
        await File.WriteAllTextAsync(path, saved.ToJsonString(), _deadline.Token);

        using WorldSession leadSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Leader, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient lead = new(leadSession);
        _ = lead.PumpAsync(_deadline.Token);
        using WorldSession mateSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Member, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient mate = new(mateSession);
        _ = mate.PumpAsync(_deadline.Token);

        await Waiting.Until(() => lead.State is not null && mate.State is not null && mate.Self?.Name is { Length: > 0 },
            "둘이 월드에 서지 못했습니다.", _deadline.Token);

        // 그룹 전에는 아무것도 오지 않는다.
        await Task.Delay(1500, _deadline.Token);
        Assert.Null(lead.MemberStatus(mate.Serial));

        string? asker = null;

        for (int tries = 0; tries < 15 && asker is null; tries++)
        {
            await lead.AskToGroupAsync(Member, _deadline.Token);

            try
            {
                await Waiting.Until(() => mate.TakeAsk(out asker), "0x63", _deadline.Token, TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.NotNull(asker);
        await mate.AcceptGroupAsync(Leader, _deadline.Token);

        // 서로의 %가 온다 — 그룹원은 반(50).
        await Waiting.Until(() => lead.MemberStatus(mate.Serial) is { HealthPercent: 50, ManaPercent: 100 },
            $"그룹장이 그룹원의 체력 %를 받지 못했습니다: {lead.MemberStatus(mate.Serial)}", _deadline.Token);
        // 그룹장은 새로 만든 캐릭터라 제 체력이 가득이 아닐 수 있다 — 제가 아는 제 체력과 같은 %면 된다.
        await Waiting.Until(() => lead.Vitals is { MaximumHealth: > 0 } own
                                  && mate.MemberStatus(lead.Serial)?.HealthPercent == (int)(100L * own.Health / own.MaximumHealth),
            $"그룹원이 그룹장의 체력 %를 받지 못했습니다: {mate.MemberStatus(lead.Serial)}", _deadline.Token);

        // 이름으로도 짝지어진다 — 멀리 있어 보이지 않는 그룹원도 목록의 이름으로 찾는다.
        Assert.Equal(mate.Serial, lead.MemberStatus(Member)?.Serial);

        // 그룹원이 쿠로토로 100 을 채우면 그룹장에게 온 %도 60 으로.
        await mate.SayAsync("/spell \"쿠로토\" 1", _deadline.Token);
        LearnedSpell? kuroto = null;
        await Waiting.Until(() => (kuroto = mate.Spells.FirstOrDefault(s => s.Name.StartsWith("쿠로토", StringComparison.Ordinal))) is not null,
            "쿠로토를 받지 못했습니다.", _deadline.Token);
        await mate.UseSpellAsync(kuroto!.Slot, 0, _deadline.Token);

        await Waiting.Until(() => lead.MemberStatus(mate.Serial) is { HealthPercent: 60 },
            $"그룹원의 체력이 바뀐 것이 그룹장에게 오지 않았습니다: {lead.MemberStatus(mate.Serial)}", _deadline.Token);

        // 나가면 끝(serial 은 남아도 더는 새로 오지 않는다) — 그룹을 떠난 뒤에는 목록이 비워진다.
        await mate.LeaveGroupAsync(_deadline.Token);
        await Waiting.Until(() => lead.MemberStatus(mate.Serial) is null,
            "그룹이 흩어졌는데 그룹원 상태가 남았습니다.", _deadline.Token);
    }

    /// <summary>
    /// 확인 사진 — 격리 서버에 실제 앱(그룹장)과 알맹이 그룹원 하나. 앱이 그 사람을 눌러 [파티 초대](<c>--invite</c>), 그룹원이 받아들이면
    /// 왼쪽 가장자리에 파티원 칸(이름·체력·마력)이 선다. <c>LOD_PARTY_SHOT</c> 에 png 경로를 줄 때만 돈다.
    /// </summary>
    [Fact]
    public async Task Photograph_the_party_frames()
    {
        if (Environment.GetEnvironmentVariable("LOD_PARTY_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Leader);
        LoginFlow.TryCreateAccount(server, Member);

        string path = Path.Combine(server.ContentLocation, "aislings", $"{Member}.json");
        JsonNode saved = JsonNode.Parse(await File.ReadAllTextAsync(path, _deadline.Token))!;
        saved["_MaximumHp"] = 1000;
        saved["CurrentHp"] = 640;
        await File.WriteAllTextAsync(path, saved.ToJsonString(), _deadline.Token);

        using WorldSession mateSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Member, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient mate = new(mateSession);
        _ = mate.PumpAsync(_deadline.Token);
        await Waiting.Until(() => mate.State is not null, "그룹원이 서지 못했습니다.", _deadline.Token);

        // 앱을 띄우는 동안 그룹원은 청을 기다렸다가 받아들인다.
        _ = Task.Run(async () =>
        {
            while (!_deadline.IsCancellationRequested)
            {
                if (mate.TakeAsk(out string? asker) && asker is not null)
                {
                    await mate.AcceptGroupAsync(asker, _deadline.Token);
                }

                await Task.Delay(200, _deadline.Token);
            }
        });

        File.Delete(shot);
        string orient = Environment.GetEnvironmentVariable("LOD_PARTY_ORIENT") ?? "portrait";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{Leader}:{LoginFlow.SyntheticSecret}",
                     "--orient", orient, "--size", orient == "portrait" ? "360x780" : "800x360",
                     "--invite", Member, "--shot", shot, "--shot-after", Environment.GetEnvironmentVariable("LOD_PARTY_SHOT_AFTER") ?? "14"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        Assert.Contains("GREYBOX_PARTY 초대", await said, StringComparison.Ordinal);
    }
}
