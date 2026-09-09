using System.Text.Json;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Records the normal 7.18 login flow of the unmodified server. Only the command order is frozen:
/// seeds, salts, hashes and serials change per run, and no real credential ever reaches this file.
/// </summary>
public sealed class LoginFlowCharacterizationTests
{
    private const byte ClientVersionCommand = 0x00;
    private const byte EncryptionReceivedCommand = 0x57;
    private const byte RedirectRequestCommand = 0x10;

    // ClientFormat00: version 718 as a big-endian ushort, then the two bytes the 7.18 client always sends.
    private static readonly byte[] ClientVersionPayload = [0x02, 0xCE, 0x4C, 0x4B];

    [Fact]
    public void Login_handshake_reproduces_the_recorded_command_order()
    {
        List<string> observed = [];

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        using Hades718TestClient client = Hades718TestClient.Connect(server.LoginPort);

        observed.Add(Describe("S2C", client.Receive().Command));
        client.Send(ClientVersionCommand, ClientVersionPayload);
        observed.Add(Describe("C2S", ClientVersionCommand));
        PacketFrame parameters = client.Receive();
        observed.Add(Describe("S2C", parameters.Command));

        client.UseEncryption(parameters);
        client.SendSecured(EncryptionReceivedCommand, ordinal: 0, 0x00);
        observed.Add(Describe("C2S", EncryptionReceivedCommand));
        PacketFrame lobbyRedirect = client.Receive();
        observed.Add(Describe("S2C", lobbyRedirect.Command));

        RedirectTarget target = Hades718TestClient.ParseRedirect(lobbyRedirect);
        using Hades718TestClient redirected = Hades718TestClient.Connect(target.Port);

        observed.Add(Describe("S2C", redirected.Receive().Command));

        redirected.SendRedirectRequest(target);
        observed.Add(Describe("C2S", RedirectRequestCommand));
        observed.Add(Describe("S2C", redirected.Receive().Command));

        Assert.Equal(LoadRecordedFlow(), observed);
    }

    private static string Describe(string direction, byte command) => $"{direction} 0x{command:X2}";

    private static List<string> LoadRecordedFlow()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hades-718-login-flow.json");
        LoginFlow flow = JsonSerializer.Deserialize<LoginFlow>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"'{path}' is empty.");

        return flow.Steps.Select(step => $"{step.Direction} {step.Command}").ToList();
    }

    private sealed record LoginFlow(string Description, IReadOnlyList<FlowStep> Steps);

    private sealed record FlowStep(string Direction, string Command, string Note);
}
