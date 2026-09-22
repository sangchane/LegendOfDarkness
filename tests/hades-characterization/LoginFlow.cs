using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Drives the unmodified 7.18 server from its handshake banner into the world with a synthetic account,
/// and reports the command order it observed.
/// </summary>
internal static class LoginFlow
{
    /// <summary>The codepage both sides of the 7.18 protocol use for text.</summary>
    private static readonly System.Text.Encoding LegacyEncoding = CreateLegacyEncoding();

    private static System.Text.Encoding CreateLegacyEncoding()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        return System.Text.Encoding.GetEncoding(949);
    }

    /// <summary>Throwaway name for the isolated run. It never matches a real account.</summary>
    public const string SyntheticName = "lodharness";

    /// <summary>Throwaway secret for the isolated run.</summary>
    public const string SyntheticSecret = "not-a-real-secret";

    // ServerConfig.ServerWelcomeMessage in LoruleConfig.json.
    private const string ServerWelcome = "Welcome to Lorule";
    private const byte ClientVersionCommand = 0x00;
    private const byte EncryptionReceivedCommand = 0x57;
    private const byte RedirectRequestCommand = 0x10;
    private const byte CreateAccountCommand = 0x02;
    private const byte CreateCharacterCommand = 0x04;
    private const byte LoginCommand = 0x03;

    // ClientFormat00: version 718 as a big-endian ushort, then the two bytes the 7.18 client always sends.
    private static readonly byte[] ClientVersionPayload = [0x02, 0xCE, 0x4C, 0x4B];

    /// <summary>
    /// A login-server connection that has cleared the handshake and the lobby redirect, so it is ready for
    /// account, character and login packets.
    /// </summary>
    internal sealed record LoginSession(Hades718TestClient Client, IReadOnlyList<string> Observed) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>Runs the handshake and the lobby redirect, and hands back the connection they lead to.</summary>
    public static LoginSession OpenSession(IsolatedHadesServer server) => OpenSession(server.LoginPort);

    /// <summary>
    /// The same handshake against a login port named directly, for the server a person is looking at rather
    /// than a throwaway one. The lobby sends the connection back to the port it came from either way, so the
    /// guard below still holds.
    /// </summary>
    public static LoginSession OpenSession(int loginPort)
    {
        List<string> observed = [];
        PacketFrame parameters;
        RedirectTarget lobbyTarget;

        using (Hades718TestClient lobby = Hades718TestClient.Connect(loginPort))
        {
            observed.Add(Describe("S2C", lobby.Receive().Command));

            lobby.Send(ClientVersionCommand, ClientVersionPayload);
            observed.Add(Describe("C2S", ClientVersionCommand));

            parameters = lobby.Receive();
            observed.Add(Describe("S2C", parameters.Command));

            lobby.UseEncryption(parameters);
            lobby.SendSecured(EncryptionReceivedCommand, ordinal: 0, 0x00);
            observed.Add(Describe("C2S", EncryptionReceivedCommand));

            PacketFrame lobbyRedirect = lobby.Receive();
            observed.Add(Describe("S2C", lobbyRedirect.Command));

            lobbyTarget = Hades718TestClient.ParseRedirect(lobbyRedirect);
            RequireIsolatedPort(lobbyTarget.Port, loginPort, "lobby");
        }

        Hades718TestClient login = Hades718TestClient.Connect(lobbyTarget.Port);
        observed.Add(Describe("S2C", login.Receive().Command));

        login.SendRedirectRequest(lobbyTarget);
        observed.Add(Describe("C2S", RedirectRequestCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        login.UseEncryption(parameters);

        return new LoginSession(login, observed);
    }

    /// <summary>
    /// Asks the server to create an account and character. Hanging up is a fine answer to a name the server
    /// dislikes, so a closed connection is not treated as a failure here.
    /// </summary>
    public static void TryCreateAccount(IsolatedHadesServer server, string name) =>
        TryCreateAccount(server.LoginPort, name, SyntheticSecret);

    /// <summary>The same, against a login port named directly and with a secret a person can type.</summary>
    public static void TryCreateAccount(int loginPort, string name, string secret)
    {
        using LoginSession session = OpenSession(loginPort);

        try
        {
            session.Client.SendSecured(CreateAccountCommand, ordinal: 0, Credentials(name, secret));
            session.Client.Receive();

            // Format04's fourth byte is the mobile creation class contract. Harness helpers create a
            // disposable warrior; production UI must require an explicit choice.
            session.Client.SendSecured(CreateCharacterCommand, ordinal: 0, 0x01, 0x01, 0x01, 0x01);

            // Reading the reply also waits for the save to finish before the connection closes.
            session.Client.Receive();
        }
        catch (Exception refused) when (refused is EndOfStreamException or IOException)
        {
            // The server closed the connection instead of answering.
        }
    }

    /// <summary>A captured game-entry ticket that has not been used yet.</summary>
    internal sealed record GameTicket(RedirectTarget Target, IReadOnlyList<string> Observed);

    /// <summary>Creates the account, logs in and stops at the game redirect without entering the world.</summary>
    public static GameTicket LoginAndCaptureTicket(IsolatedHadesServer server, string name)
    {
        using LoginSession session = OpenSession(server);
        List<string> observed = [.. session.Observed];
        Hades718TestClient login = session.Client;

        login.SendSecured(CreateAccountCommand, ordinal: 0, Credentials(name, SyntheticSecret));
        observed.Add(Describe("C2S", CreateAccountCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        login.SendSecured(CreateCharacterCommand, ordinal: 0, 0x01, 0x01, 0x01, 0x01);
        observed.Add(Describe("C2S", CreateCharacterCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        login.SendSecured(LoginCommand, ordinal: 0, Credentials(name, SyntheticSecret));
        observed.Add(Describe("C2S", LoginCommand));
        observed.Add(Describe("S2C", login.Receive().Command));

        PacketFrame gameRedirect = login.Receive();
        observed.Add(Describe("S2C", gameRedirect.Command));

        RedirectTarget target = Hades718TestClient.ParseRedirect(gameRedirect);
        RequireIsolatedPort(target.Port, server.GamePort, "game");

        return new GameTicket(target, observed);
    }

    public static IReadOnlyList<string> EnterWorld(IsolatedHadesServer server)
    {
        GameTicket ticket = LoginAndCaptureTicket(server, SyntheticName);
        List<string> observed = [.. ticket.Observed];

        using Hades718TestClient world = Hades718TestClient.Connect(ticket.Target.Port);
        world.SendRedirectRequest(ticket.Target);
        observed.Add(Describe("C2S", RedirectRequestCommand));

        WaitForLog(server, WelcomeMessage(SyntheticName), TimeSpan.FromSeconds(30));

        return observed;
    }

    /// <summary>The only line the server logs on a successful world entry.</summary>
    public static string WelcomeMessage(string name) =>
        $"{name} : {ServerWelcome}";

    private static string Describe(string direction, byte command) => $"{direction} 0x{command:X2}";

    private static byte[] Credentials() => Credentials(SyntheticName, SyntheticSecret);

    private static byte[] Credentials(string name, string secret) =>
        [.. LengthPrefixed(name), .. LengthPrefixed(secret)];

    /// <summary>
    /// StringA on the wire: one length byte, then the CP949 bytes. The count is of bytes, not characters, so
    /// a Korean name (two bytes per syllable) is framed correctly.
    /// </summary>
    private static byte[] LengthPrefixed(string value)
    {
        byte[] encoded = LegacyEncoding.GetBytes(value);

        if (encoded.Length > byte.MaxValue)
        {
            throw new ArgumentException($"'{value}' does not fit in a single length byte.", nameof(value));
        }

        return [(byte)encoded.Length, .. encoded];
    }

    private static void RequireIsolatedPort(int actual, int expected, string stage) =>
        Assert.True(
            actual == expected,
            $"The {stage} redirect pointed at port {actual}, outside the isolated run (expected {expected}).");

    public static void WaitForLog(IsolatedHadesServer server, string expected, TimeSpan timeout)
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
