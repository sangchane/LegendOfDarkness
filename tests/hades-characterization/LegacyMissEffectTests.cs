using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 회귀 시험 — beag suain(5% 확률로 헛침) 같은 옛 기술 스크립트는 헛치면 템플릿의 MissAnimation(적히지 않아 0)을
/// 그대로 보내 화면에 아무것도 그리지 않았다. 오늘의 규칙(<see cref="MissEffectTests" /> 이 보는 Assail.cs ·
/// MonkStrike.cs — 템플릿에 적혀 있으면 그 값, 아니면 115)을 <c>beagsuain.cs</c> 의 <c>OnFailed</c> 에도
/// 적용했다 — DoublePunch·Destroyer·Clobber·wallop·Two-Handed-Attack·rescue·beagsuainia 도 같은 자리를
/// 고쳤지만, 그 파일들의 <c>OnFailed</c> 는 지금 엔진 어디서도 부르지 않아(죽은 길) 실제 판으로는 볼 수 없다 —
/// beagsuain 만 <c>OnUse</c> 가 확률로 직접 부른다. 확률을 격리 서버 사본에서만 100% 로 만들어(켜기 전에
/// 스크립트를 고쳐서) 결정적으로 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class LegacyMissEffectTests : IDisposable
{
    private const string Name = "legacymiss";
    private const int WoodlandOneOne = 20015;
    private const int SkillMiss = 115;
    private static readonly Tile Start = new(2, 35);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Beag_suain_shows_miss_115_on_the_swung_at_tile_when_it_fails()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        Waiting.MakeGameMaster(server, Name);
        ForceBeagSuainToAlwaysFail(server);

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandOneOne && state.Where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.", _deadline.Token);

        await world.SayAsync("/skill \"beag suain\" 1", _deadline.Token);
        await Waiting.Until(() => world.Skills.Any(s => s.Name.StartsWith("beag suain")),
            $"beag suain 이 창에 오지 않았습니다. 서버가 한 말: {world.Said} · 가진 기술: {string.Join(", ", world.Skills.Select(s => s.Name))}",
            _deadline.Token);
        int slot = world.Skills.First(s => s.Name.StartsWith("beag suain")).Slot;

        List<Effect> flashes = [];
        while (world.TakeEffect(out Effect? drained))
        {
            flashes.Add(drained);
        }

        flashes.Clear();
        await world.UseSkillAsync(slot, _deadline.Token);

        await Waiting.Until(() =>
        {
            while (world.TakeEffect(out Effect? flash))
            {
                flashes.Add(flash);
            }

            return flashes.Any(f => f.At is { } at && f.TargetAnimation == SkillMiss
                                    && Math.Abs(at.X - Start.X) + Math.Abs(at.Y - Start.Y) == 1);
        }, $"beag suain: 헛친 칸에 Miss(115)가 오지 않았습니다: {string.Join(", ", flashes)}", _deadline.Token);
    }

    /// <summary>
    /// beagsuain.cs 의 <c>OnUse</c> 는 95% 로 성공한다(<c>rand.Next(1, 101) &gt;= 5</c>). 시험은 헛칠 때를
    /// 결정적으로 보려고 격리 서버 사본에서만 늘 헛치게 고친다 — 원본(<c>database/server/</c>)은 그대로 둔다.
    /// </summary>
    private static void ForceBeagSuainToAlwaysFail(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.ContentLocation, "scripts", "Skills", "beagsuain.cs");
        string original = File.ReadAllText(path);
        string patched = original.Replace("if (rand.Next(1, 101) >= 5)", "if (false)");

        Assert.NotEqual(original, patched);
        File.WriteAllText(path, patched);
    }
}
