using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 남의 장비창(2026-10-01) — 사람을 누르면(0x43) 서버가 그 사람 장비창(0x34)을 보낸다. 모바일은 거기서 이름·직업·걸친 것·그룹 받기
/// 상태를 읽어(<see cref="OtherProfile" />) 원작 장비 그림에 얹는다. 그룹 받기는 0x2F 로 켜고 끄고, 내 프로필(0x39)과 남이 보는
/// 장비창(0x34) 양쪽이 따라 바뀐다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class OtherGearWindowTests : IDisposable
{
    private const string Looker = "gearlook";
    private const string Wearer = "gearwear";
    private static readonly (int Map, int X, int Y) Town = (20373, 37, 29);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Pressing_somebody_shows_their_gear_and_group_requests_toggle()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Waiting.MakeGameMaster(server, Wearer);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Looker);
        LoginFlow.TryCreateAccount(server, Wearer);

        using WorldSession lookSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Looker, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient look = new(lookSession);
        _ = look.PumpAsync(_deadline.Token);
        using WorldSession wearSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Wearer, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient wear = new(wearSession);
        _ = wear.PumpAsync(_deadline.Token);

        await Waiting.Until(() => look.Others.Any(other => other.Serial == wear.Serial),
            "보는 쪽에 입는 사람이 보이지 않습니다.", _deadline.Token);

        // 아무 직업이나 1레벨에 신는 신발을 신긴다(신발 자리 13).
        await wear.SayAsync("/give \"체력의신발\" 1", _deadline.Token);
        InventoryItem? boots = null;
        await Waiting.Until(() => (boots = wear.Pack.FirstOrDefault(item => item.Name.Contains("체력의신발", StringComparison.Ordinal))) is not null,
            "신발을 받지 못했습니다.", _deadline.Token);
        await wear.UseAsync(boots!.Slot, _deadline.Token);
        await Waiting.Until(() => wear.Worn.Any(gear => gear.Slot == 13), "신발을 신지 못했습니다.", _deadline.Token);
        int icon = wear.Worn.First(gear => gear.Slot == 13).Icon;

        OtherProfile seen = await Press(look, wear.Serial);

        Assert.Equal(wear.Serial, seen.Serial);
        Assert.Equal(Wearer, seen.Name, ignoreCase: true);
        Assert.NotEmpty(seen.Path);
        Assert.Contains(seen.Worn, gear => gear.Slot == 13 && gear.Icon == icon);

        // 그룹 받기를 뒤집으면 내 프로필도, 남이 보는 장비창도 따라 바뀐다.
        await wear.AskProfileAsync(_deadline.Token);
        await Waiting.Until(() => wear.GroupOpen is not null, "프로필(0x39)에서 그룹 받기를 읽지 못했습니다.", _deadline.Token);
        bool before = wear.GroupOpen!.Value;
        Assert.Equal(before, seen.GroupOpen);

        await wear.ToggleGroupAsync(_deadline.Token);
        await wear.AskProfileAsync(_deadline.Token);
        await Waiting.Until(() => wear.GroupOpen == !before, "0x2F 뒤에 그룹 받기가 바뀌지 않았습니다.", _deadline.Token);

        Assert.Equal(!before, (await Press(look, wear.Serial)).GroupOpen);
    }

    /// <summary>
    /// 확인 사진 — 격리 서버에 실제 앱(보는 쪽)과 알맹이 하나(신발 신은 쪽). 앱이 내 장비창을 연 채 그 사람을 누르면(<c>--look</c>)
    /// 내 장비창·소지품은 닫히고 그 사람 장비창만 뜬다. <c>LOD_GEAR_SHOT</c> 에 png 경로를 줄 때만 돈다.
    /// </summary>
    [Fact]
    public async Task Photograph_somebody_elses_gear()
    {
        if (Environment.GetEnvironmentVariable("LOD_GEAR_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Waiting.MakeGameMaster(server, Wearer);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Looker);
        LoginFlow.TryCreateAccount(server, Wearer);

        using WorldSession wearSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Wearer, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient wear = new(wearSession);
        _ = wear.PumpAsync(_deadline.Token);
        await Waiting.Until(() => wear.State is not null, "입는 사람이 서지 못했습니다.", _deadline.Token);

        await wear.SayAsync("/give \"체력의신발\" 1", _deadline.Token);
        InventoryItem? boots = null;
        await Waiting.Until(() => (boots = wear.Pack.FirstOrDefault(item => item.Name.Contains("체력의신발", StringComparison.Ordinal))) is not null,
            "신발을 받지 못했습니다.", _deadline.Token);
        await wear.UseAsync(boots!.Slot, _deadline.Token);
        await Waiting.Until(() => wear.Worn.Any(gear => gear.Slot == 13), "신발을 신지 못했습니다.", _deadline.Token);

        // 같은 칸에 서 있으면 장비 그림이 그 사람을 가려 누를 수 없다 — 서·남으로 번갈아 걸어 화면 왼쪽으로 비켜 세운다.
        foreach (Direction step in new[] { Direction.West, Direction.South, Direction.West, Direction.South, Direction.West, Direction.South })
        {
            await wear.WalkAsync(step, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        File.Delete(shot);
        string orient = Environment.GetEnvironmentVariable("LOD_GEAR_ORIENT") ?? "landscape";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     // 화면 밖(-3000)에 띄우면 맥 Godot 가 그리지 않아 사진이 끝내 안 남는다(2026-10-01) — 보이는 자리에 잠깐 띄운다.
                     "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{Looker}:{LoginFlow.SyntheticSecret}",
                     "--orient", orient, "--size", orient == "portrait" ? "360x780" : "852x393",
                     "--look", Wearer, "--shot", shot, "--shot-after", Environment.GetEnvironmentVariable("LOD_GEAR_SHOT_AFTER") ?? "14"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        Assert.Contains($"GREYBOX_LOOK {Wearer} 소지품 닫힘", await said, StringComparison.Ordinal);
    }

    private async Task<OtherProfile> Press(WorldClient world, uint serial)
    {
        OtherProfile? seen = null;
        await world.ClickAsync(serial, _deadline.Token);
        await Waiting.Until(() => (seen = world.TakeSeen()) is not null, "누른 사람의 장비창(0x34)이 오지 않았습니다.", _deadline.Token);

        return seen!;
    }
}
