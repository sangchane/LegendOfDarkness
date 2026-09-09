using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Drives the unmodified 7.18 server from its handshake banner into the world with a synthetic account,
/// and reports the command order it observed.
/// </summary>
internal static class LoginFlow
{
    /// <summary>Throwaway name for the isolated run. It never matches a real account.</summary>
    public const string SyntheticName = "lodharness";

    private const string SyntheticSecret = "not-a-real-secret";
    private const byte ClientVersionCommand = 0x00;
    private const byte EncryptionReceivedCommand = 0x57;
    private const byte RedirectRequestCommand = 0x10;
    private const byte CreateAccountCommand = 0x02;
    private const byte CreateCharacterCommand = 0x04;
    private const byte LoginCommand = 0x03;

    // ClientFormat00: version 718 as a big-endian ushort, then the two bytes the 7.18 client always sends.
    private static readonly byte[] ClientVersionPayload = [0x02, 0xCE, 0x4C, 0x4B];

    public static IReadOnlyList<string> EnterWorld(IsolatedHadesServer server)
    {
        List<string> observed = [];

        using Hades718TestClient lobby = Hades718TestClient.Connect(server.LoginPort);
        observed.Add(Describe("S2C", lobby.Receive().Command));

        lobby.Send(ClientVersionCommand, ClientVersionPayload);
        observed.Add(Describe("C2S", ClientVersionCommand));

        PacketFrame parameters = lobby.Receive();
        observed.Add(Describe("S2C", parameters.Command));

        lobby.UseEncryption(parameters);
        lobby.SendSecured(EncryptionReceivedCommand, ordinal: 0, 0x00);
        observed.Add(Describe("C2S", EncryptionReceivedCommand));

        PacketFrame lobbyRedirect = lobby.Receive();
        observed.Add(Describe("S2C", lobbyRedirect.Command));

        RedirectTarget lobbyTarget = Hades718TestClient.ParseRedirect(lobbyRedirect);
        RequireIsolatedPort(lobbyTarget.Port, server.LoginPort, "lobby");

        using Hades718TestClient login = Hades718TestClient.Connect(lobbyTarget.Port);
        observed.Add(Describe("S2C", login.Receive().Command));

        login.SendRedirectRequest(lobbyTarget);
        observed.Add(Describe("C2S", RedirectRequestCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        login.UseEncryption(parameters);

        login.SendSecured(CreateAccountCommand, ordinal: 0, Credentials());
        observed.Add(Describe("C2S", CreateAccountCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        login.SendSecured(CreateCharacterCommand, ordinal: 0, 0x01, 0x01, 0x01);
        observed.Add(Describe("C2S", CreateCharacterCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        login.SendSecured(LoginCommand, ordinal: 0, Credentials());
        observed.Add(Describe("C2S", LoginCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        PacketFrame gameRedirect = login.Receive();
        observed.Add(Describe("S2C", gameRedirect.Command));

        RedirectTarget gameTarget = Hades718TestClient.ParseRedirect(gameRedirect);
        RequireIsolatedPort(gameTarget.Port, server.GamePort, "game");

        using Hades718TestClient world = Hades718TestClient.Connect(gameTarget.Port);
        world.SendRedirectRequest(gameTarget);
        observed.Add(Describe("C2S", RedirectRequestCommand));

        WaitForLog(server, $"{SyntheticName} : Welcome to Lorule", TimeSpan.FromSeconds(30));

        return observed;
    }

    private static string Describe(string direction, byte command) => $"{direction} 0x{command:X2}";

    private static byte[] Credentials() => [.. LengthPrefixed(SyntheticName), .. LengthPrefixed(SyntheticSecret)];

    // Synthetic names are ASCII. Korean names travel as CP949 and belong to the unit-test boundary in the
    // stabilization plan, not to this flow.
    private static byte[] LengthPrefixed(string value) =>
        [(byte)value.Length, .. System.Text.Encoding.ASCII.GetBytes(value)];

    private static void RequireIsolatedPort(int actual, int expected, string stage) =>
        Assert.True(
            actual == expected,
            $"The {stage} redirect pointed at port {actual}, outside the isolated run (expected {expected}).");

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
}
