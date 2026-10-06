using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.World;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 생태계 봇의 서버 쪽(설계 <c>autopilot/eco-bots/05-api-contract.md</c> E1~E4) — 설정 <c>EcoBots</c> 의 이름만, 같은 기계에서만
/// 순간이동(0xF1 8)·로그인·만들기가 되고, [접속자] 목록의 길드명 자리에 <c>AI</c> 가 보인다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class EcoBotServerTests(ITestOutputHelper output) : IDisposable
{
    internal const string Bot = "ecobot";
    private const string Unmade = "ecobot2";
    private const string Human = "ecohuman";
    private const int Woodland = 20015;
    private static readonly (int Map, int X, int Y) Town = (20373, 37, 29);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Only_an_eco_bot_may_jump_and_the_user_list_marks_it_AI()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Configure(server, Bot);
        server.Start(TimeSpan.FromMinutes(2));

        WorldClient bot = await Enter(server, Bot);
        WorldClient human = await Enter(server, Human);

        await bot.EcoMoveAsync(Woodland, 10, 10, _deadline.Token);
        await Waiting.Until(() => bot.State?.Map.Id == Woodland, $"봇이 우드랜드로 가지 않았습니다: 맵 {bot.State?.Map.Id}", _deadline.Token);

        await human.EcoMoveAsync(Woodland, 10, 10, _deadline.Token);
        await Task.Delay(2000, _deadline.Token);
        Assert.Equal(Town.Map, human.State!.Map.Id);

        IReadOnlyList<OnlineUser>? users = null;

        for (int tries = 0; users is null && tries < 10; tries++)
        {
            await human.AskUsersAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
            users = human.TakeUsers();
        }

        Assert.NotNull(users);
        Assert.Equal("AI", users!.Single(one => one.Name.Equals(Bot, StringComparison.OrdinalIgnoreCase)).Guild);
        Assert.Equal("", users!.Single(one => one.Name.Equals(Human, StringComparison.OrdinalIgnoreCase)).Guild);
    }

    [Fact]
    public async Task An_eco_bot_can_neither_log_in_nor_be_made_from_another_machine()
    {
        if (OtherAddress() is not { } lan)
        {
            output.WriteLine("루프백 말고 쓸 주소가 없어 건너뜀");
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        Configure(server, Bot, Unmade);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Bot);

        ProtocolException refused = await Assert.ThrowsAsync<ProtocolException>(() =>
            HadesLoginClient.LoginAsync(lan, server.LoginPort, Bot, LoginFlow.SyntheticSecret, null, _deadline.Token));
        Assert.Contains("비밀번호", refused.Message, StringComparison.Ordinal);

        await Assert.ThrowsAnyAsync<Exception>(() => HadesLoginClient.CreateCharacterAsync(
            lan, server.LoginPort, Unmade, LoginFlow.SyntheticSecret, hairStyle: 1, gender: 1, hairColor: 1, path: 1,
            cancellationToken: _deadline.Token));
        Assert.False(File.Exists(Path.Combine(server.ContentLocation, "aislings", $"{Unmade}.json")));

        // 같은 기계에서는 된다.
        using WorldSession here = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Bot, LoginFlow.SyntheticSecret, null, _deadline.Token);
    }

    internal static void Configure(IsolatedHadesServer server, params string[] bots)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["EcoBots"] = new JsonArray([.. bots.Select(name => JsonValue.Create(name))]);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>이 기계의 루프백이 아닌 IPv4 — 서버는 모든 주소에서 듣는다. 거기로 붙으면 서버가 보는 주소가 루프백이 아니다.</summary>
    private static IPAddress? OtherAddress() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(card => card.OperationalStatus == OperationalStatus.Up && card.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(card => card.GetIPProperties().UnicastAddresses)
            .Select(one => one.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string name)
    {
        LoginFlow.TryCreateAccount(server, name);
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Serial != 0, $"{name} 가 들어가지 못했습니다.", _deadline.Token);
        return world;
    }
}
