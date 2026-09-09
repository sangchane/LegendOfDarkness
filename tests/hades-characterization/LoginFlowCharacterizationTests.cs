using System.Text;
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
    private const byte CreateAccountCommand = 0x02;
    private const byte CreateCharacterCommand = 0x04;
    private const byte LoginCommand = 0x03;

    // Synthetic throwaway credentials. The isolated run starts with an empty character directory, so these
    // never collide with anything real and never leave this repository.
    private const string SyntheticName = "lodharness";
    private const string SyntheticSecret = "not-a-real-secret";

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

        redirected.UseEncryption(parameters);

        redirected.SendSecured(CreateAccountCommand, ordinal: 0, Credentials());
        observed.Add(Describe("C2S", CreateAccountCommand));
        observed.Add(Describe("S2C", redirected.Receive().Command));

        redirected.SendSecured(CreateCharacterCommand, ordinal: 0, 0x01, 0x01, 0x01);
        observed.Add(Describe("C2S", CreateCharacterCommand));
        observed.Add(Describe("S2C", redirected.Receive().Command));

        redirected.SendSecured(LoginCommand, ordinal: 0, Credentials());
        observed.Add(Describe("C2S", LoginCommand));
        observed.Add(Describe("S2C", redirected.Receive().Command));

        PacketFrame gameRedirect = redirected.Receive();
        observed.Add(Describe("S2C", gameRedirect.Command));

        RedirectTarget game = Hades718TestClient.ParseRedirect(gameRedirect);
        using Hades718TestClient world = Hades718TestClient.Connect(game.Port);
        world.SendRedirectRequest(game);
        observed.Add(Describe("C2S", RedirectRequestCommand));

        Assert.Equal(LoadRecordedFlow(), observed);
        Assert.Equal(server.GamePort, game.Port);
        WaitForLog(server, $"{SyntheticName} : Welcome to Lorule", TimeSpan.FromSeconds(30));
    }

    private static byte[] Credentials() => [.. LengthPrefixed(SyntheticName), .. LengthPrefixed(SyntheticSecret)];

    // Synthetic names are ASCII. Korean names travel as CP949 and are covered by the unit-test boundary
    // in the stabilization plan, not here.
    private static byte[] LengthPrefixed(string value) => [(byte)value.Length, .. Encoding.ASCII.GetBytes(value)];

    private static void WaitForLog(IsolatedHadesServer server, string expected, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (server.ConsoleOutput.Contains(expected, StringComparison.Ordinal))
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new InvalidOperationException(
            $"The server never logged '{expected}' within {timeout}.{Environment.NewLine}{server.ConsoleOutput}");
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
