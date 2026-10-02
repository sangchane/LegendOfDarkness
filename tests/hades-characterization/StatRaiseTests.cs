using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 레벨업 점수 1 은 능력치 하나에만, 상한에 닿은 능력치에는 쓰이지 않는다(<c>Format47Handler</c>).
/// </summary>
/// <remarks>
/// 원본 하데스는 요청 바이트를 깃발로 읽어 <c>0x1F</c> 하나로 다섯 개를 올렸고, 상한(<c>StatCap</c> 255)에 닿은
/// 능력치를 눌러도 점수를 깎았다. 우리 앱은 한 칸씩만 보내지만 고친 클라이언트로는 부정이 된다.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class StatRaiseTests : IDisposable
{
    private const string Name = "statraise";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task One_point_raises_one_attribute_and_never_one_at_the_cap()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["StatPoints"] = 2;
        character["_Str"] = 255;
        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        using WorldSession held = session;

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.Vitals is { Unspent: 2 }, "점수 2 를 들고 들어가지 못했습니다.");
        Vitals before = world.Vitals!;

        // 다섯 칸을 한 번에, 그리고 상한에 닿은 힘 — 둘 다 거절되고 점수가 그대로여야 한다.
        await world.RaiseAsync((Stat)0x1F, _deadline.Token);
        await Task.Delay(300, _deadline.Token);
        await world.RaiseAsync(Stat.Str, _deadline.Token);
        await Task.Delay(300, _deadline.Token);
        // 그 뒤의 정상 요청이 답을 받으면 앞의 둘도 처리가 끝난 것이다.
        await world.RaiseAsync(Stat.Dex, _deadline.Token);
        await Until(() => world.Vitals!.Dex == before.Dex + 1, $"민첩이 오르지 않았습니다. 서버가 한 말: {world.Said} / {world.Vitals}");

        Vitals after = world.Vitals!;
        Assert.Equal(1, after.Unspent);
        Assert.Equal(255, after.Str);
        Assert.Equal(before.Int, after.Int);
        Assert.Equal(before.Wis, after.Wis);
        Assert.Equal(before.Con, after.Con);
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
