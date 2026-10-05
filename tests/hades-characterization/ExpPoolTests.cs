using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 99레벨 보유경험치(2026-10-05) — 99 에서는 경험치가 보유경험치로 쌓이고(세오·칸에게 팔아 체력·마력을 산다), 0x08 의 「다음 레벨까지」
/// 자리에 실려 앱 EXP 막대에 「보유」로 보인다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class ExpPoolTests : IDisposable
{
    private const string Name = "exppool";
    private static readonly (int Map, int X, int Y) Town = (20373, 37, 29);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_level_99_character_banks_experience_and_the_app_is_told()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Waiting.MakeGameMaster(server, Name);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 99;
        character["ExpPool"] = 12345;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.Vitals is { Level: 99, ExperienceToGo: 12345 },
            $"보유경험치 12345 가 오지 않았습니다: {world.Vitals}", _deadline.Token);
    }
}
