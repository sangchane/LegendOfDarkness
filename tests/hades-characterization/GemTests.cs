using System.Net;
using Darkages.Types;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 보석(사용자 2026-10-08, <c>autopilot/gems/SPEC.md</c>) — 장비를 분해하면 레벨대로 보석이 나오고(99 이상은 고가도), 중급 보석은
/// 99레벨 사냥터에서만 떨어지며, 제작 NPC 메린이 보석으로 속성 크리스탈목걸이·홀디트링을 만든다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class GemTests : IDisposable
{
    private const string Name = "gemcut";

    // 마인제조상점 계산대 앞 — 메린은 계산대 뒤(10,6).
    private static readonly (int Map, int X, int Y) Workshop = (20308, 10, 9);

    private static readonly string[] AllGems = [.. Gems.Middle, .. Gems.Precious];

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));
    private IsolatedHadesServer? _server;
    private WorldSession? _session;

    public void Dispose()
    {
        _deadline.Cancel();
        _session?.Dispose();
        _server?.Dispose();
        _deadline.Dispose();
    }

    [Theory]
    [InlineData(99, 0.0, 0, "페리도트")]
    [InlineData(99, 0.0499, 3, "꿈의바다")]
    [InlineData(99, 0.05, 0, "루비")]
    [InlineData(99, 0.54, 3, "진주")]
    [InlineData(99, 0.55, 0, null)]
    [InlineData(120, 0.0, 2, "젤리오팔")]
    [InlineData(98, 0.0, 1, "사파이어")] // 99 아래는 고가가 없다
    [InlineData(41, 0.2, 2, "에메랄드")]
    [InlineData(41, 0.21, 2, null)]
    [InlineData(0, 0.0, 0, null)]
    public void A_gem_from_gear_follows_the_gear_level(int level, double roll, int pick, string? gem) =>
        Assert.Equal(gem, Gems.Roll(level, roll, pick));

    [Fact]
    public void Middle_gems_fall_in_the_level_99_hunting_grounds_only()
    {
        HashSet<int> grounds = Gems.Read(Path.Combine(HadesWorkspace.ServerDataDirectory, "static", "gem-grounds.tsv"));

        Assert.Contains(20797, grounds); // 구광산1-1
        Assert.Contains(20455, grounds); // 뤼케시온해안1-A
        Assert.Contains(20400, grounds); // 드라큐라백작의성동1
        Assert.DoesNotContain(20020, grounds); // 우드랜드14-1 (81)
        Assert.DoesNotContain(20263, grounds); // 포테의숲1존 (21)
    }

    [Fact]
    public async Task Disassembling_takes_dropped_gear_away_for_gems_but_not_potions_or_shop_gear()
    {
        WorldClient world = await Enter(Workshop);

        // 로오의금팔찌(99, 드랍만): 하나에 중급 49.5% + 고가 5% — 20개에 하나도 안 나올 확률은 0.455^20 ≈ 10^-7.
        // 금장갑(99)은 구광산대기실 마시가 4,000골드에 판다 — 사서 분해하면 금화로 보석을 사는 셈이라 거절한다.
        await world.SayAsync("/give \"로오의금팔찌\" 20", _deadline.Token);
        await world.SayAsync("/give \"하급체력포션\" 3", _deadline.Token);
        await world.SayAsync("/give \"금장갑\" 1", _deadline.Token);
        await Waiting.Until(() => world.Pack.Count(item => item.Name == "로오의금팔찌") == 20 && world.Pack.Any(item => item.Name == "하급체력포션")
                                  && world.Pack.Any(item => item.Name == "금장갑"),
            $"팔찌 20·포션·금장갑이 오지 않았습니다: {world.Said}", _deadline.Token);

        foreach (InventoryItem bracelet in world.Pack.Where(item => item.Name == "로오의금팔찌").ToList())
        {
            await world.DisassembleAsync(bracelet.Slot, _deadline.Token);
        }

        await world.DisassembleAsync(world.Pack.First(item => item.Name == "하급체력포션").Slot, _deadline.Token);
        await Waiting.Until(() => world.Pack.All(item => item.Name != "로오의금팔찌") && world.Said.Contains("장비만 분해할 수 있습니다", StringComparison.Ordinal),
            $"팔찌가 남았거나 포션을 거절하지 않았습니다: {string.Join(", ", world.Pack.Select(item => item.Name))} · {world.Said}", _deadline.Token);

        await world.DisassembleAsync(world.Pack.First(item => item.Name == "금장갑").Slot, _deadline.Token);
        await Waiting.Until(() => world.Said.Contains("상점에서 파는 물건은 분해할 수 없습니다", StringComparison.Ordinal),
            $"상점 물건을 거절하지 않았습니다: {world.Said}", _deadline.Token);

        Assert.Contains(world.Pack, item => AllGems.Contains(item.Name));
        Assert.Contains(world.Pack, item => item.Name == "하급체력포션");
        Assert.Contains(world.Pack, item => item.Name == "금장갑");
    }

    [Fact]
    public async Task Merin_sets_a_ruby_into_a_crystal_necklace_and_wants_five_gems_for_a_holditring()
    {
        WorldClient world = await Enter(Workshop);

        await world.SayAsync("/give \"크리스탈목걸이\" 1", _deadline.Token);
        await world.SayAsync("/give \"루비\" 1", _deadline.Token);
        await world.SayAsync("/give \"홀디트링\" 1", _deadline.Token);
        await world.SayAsync("/give \"레드자스퍼\" 4", _deadline.Token);
        await Waiting.Until(() => new[] { "크리스탈목걸이", "루비", "홀디트링", "레드자스퍼" }.All(name => world.Pack.Any(item => item.Name == name)),
            $"재료가 오지 않았습니다: {world.Said}", _deadline.Token);

        Tile merinAt = new(10, 6);
        await world.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => world.Creatures.Any(one => one.Kind == CreatureKind.Merchant && one.Where == merinAt), "메린이 보이지 않습니다.", _deadline.Token);
        Creature merin = world.Creatures.First(one => one.Kind == CreatureKind.Merchant && one.Where == merinAt);

        await Make(world, merin, "화염의크리스탈목걸이");
        await Waiting.Until(() => world.Pack.Any(item => item.Name == "화염의크리스탈목걸이"),
            $"화염의크리스탈목걸이가 오지 않았습니다: {world.Talking?.What} · {world.Said}", _deadline.Token);
        Assert.DoesNotContain(world.Pack, item => item.Name is "루비" or "크리스탈목걸이");

        // 레드자스퍼 넷으로는 안 된다 — 홀디트링도 레드자스퍼도 그대로.
        await Make(world, merin, "화염의홀디트링");
        await Waiting.Until(() => world.Talking?.What.Contains("재료가 모자라요", StringComparison.Ordinal) == true,
            $"모자란다고 하지 않았습니다: {world.Talking?.What} · {world.Said}", _deadline.Token);
        Assert.Contains(world.Pack, item => item.Name == "홀디트링");
        Assert.Contains(world.Pack, item => item.Name == "레드자스퍼" && item.Stacks == 4);
        Assert.DoesNotContain(world.Pack, item => item.Name == "화염의홀디트링");
    }

    /// <summary>
    /// 확인 사진 — 실제 앱이 소지품을 열고 첫 칸(로오의금팔찌)을 누른 뒤 정보 상자의 [분해]를 누르면 「분해할까요?」 판이 선다.
    /// <c>LOD_GEM_SHOT</c> 에 png 경로를 줄 때만 돈다(<c>LOD_GEM_ORIENT</c> = portrait|landscape, <c>LOD_GEM_INFO</c> 를 주면 묻기 전 정보 상자).
    /// </summary>
    [Fact]
    public async Task Photograph_the_pack_asking_to_disassemble()
    {
        if (Environment.GetEnvironmentVariable("LOD_GEM_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        WorldClient world = await Enter(Workshop);
        foreach (string give in new[] { "/give \"로오의금팔찌\" 1", "/give \"루비\" 3", "/give \"크리스탈목걸이\" 1", "/give \"레드자스퍼\" 5" })
        {
            await world.SayAsync(give, _deadline.Token);
        }

        await Waiting.Until(() => world.Pack.Count >= 4, $"물건이 오지 않았습니다: {world.Said}", _deadline.Token);
        await world.LogOutAsync(_deadline.Token);
        await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);

        File.Delete(shot);
        string orient = Environment.GetEnvironmentVariable("LOD_GEM_ORIENT") ?? "portrait";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{_server!.LoginPort}", "--login", $"{Name}:{LoginFlow.SyntheticSecret}",
                     "--orient", orient, "--size", orient == "portrait" ? "360x780" : "800x360",
                     "--pack", "--pack-pick", "1", "--shot", shot, "--shot-after", "14",
                 })
        {
            start.ArgumentList.Add(argument);
        }

        if (Environment.GetEnvironmentVariable("LOD_GEM_INFO") is not { Length: > 0 })
        {
            start.ArgumentList.Add("--pack-break");
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        Assert.Contains("GREYBOX_PACK_PICK 1", await said, StringComparison.Ordinal);
    }

    private async Task<WorldClient> Enter((int Map, int X, int Y) start)
    {
        _server = IsolatedHadesServer.Prepare(startTogether: start);
        Waiting.MakeGameMaster(_server, Name);
        _server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(_server, Name);

        _session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, _server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(_session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.Vitals is not null, "들어가지 못했습니다.", _deadline.Token);
        return world;
    }

    /// <summary>메린을 눌러 <paramref name="made" /> → 「만든다.」.</summary>
    private async Task Make(WorldClient world, Creature merin, string made)
    {
        int seen = world.TalkCount;
        await world.ClickAsync(merin.Serial, _deadline.Token);
        await Waiting.Until(() => world.TalkCount > seen && world.Talking?.Options.Any(option => option.Text == made) == true,
            $"메린의 메뉴가 오지 않았습니다: {world.Talking?.What} · {world.Said}", _deadline.Token);

        seen = world.TalkCount;
        await world.AnswerAsync(merin.Serial, world.Talking!.Options.First(option => option.Text == made).Step, _deadline.Token);
        await Waiting.Until(() => world.TalkCount > seen && world.Talking?.Options.Any(option => option.Text == "만든다.") == true,
            $"만들지 묻지 않았습니다: {world.Talking?.What} · {world.Said}", _deadline.Token);

        await world.AnswerAsync(merin.Serial, world.Talking!.Options.First(option => option.Text == "만든다.").Step, _deadline.Token);
    }
}
