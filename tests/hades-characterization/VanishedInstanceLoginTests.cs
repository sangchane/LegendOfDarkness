using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 개인 사본 맵(60000~)은 서버가 다시 켜지면 사라진다. 거기 저장된 캐릭터는 로그인 서버에서
/// 「60000번 맵이 준비되어 있지 않습니다」로 막혔다 — 게임 서버가 시작 자리로 보내므로 들어와야 한다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class VanishedInstanceLoginTests : IDisposable
{
    private const int VanishedInstance = 60000;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_character_saved_in_a_vanished_instance_logs_in_at_the_start()
    {
        const string who = "lostcopy";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["CurrentMapId"] = VanishedInstance;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id != VanishedInstance,
            $"사라진 사본에 저장된 캐릭터가 들어오지 못했습니다. 마지막: {world.State}", _deadline.Token);
    }
}
