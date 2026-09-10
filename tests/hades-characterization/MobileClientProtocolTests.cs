using System.Net;
using Darkages.Network;
using Darkages.Security;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;
using Xunit;
namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Proves the mobile client's own protocol code against the real server rather than against a recorded
/// fixture. The fixture says what we believe the bytes are; these say the server agrees.
/// </summary>
public sealed class MobileClientProtocolTests
{
    private const string MobileName = "lodmobile";

    /// <summary>A stalled login should fail the run rather than hold it.</summary>
    private static readonly CancellationTokenSource TestDeadline = new(TimeSpan.FromMinutes(2));

    [Fact]
    public void Mobile_cipher_agrees_with_the_server_cipher()
    {
        EncryptionParameters parameters = new(0, "NexonInc."u8.ToArray(), 0);

        // Around the salt length, because the mixing changes every nine bytes, and at both ordinal ends.
        foreach (byte ordinal in new byte[] { 0, 1, 2, 9, 42, 255 })
        {
            foreach (int length in new[] { 1, 8, 9, 10, 17, 18, 19, 100 })
            {
                byte[] body = new byte[length];

                for (int index = 0; index < length; index++)
                {
                    body[index] = (byte)((index * 7) + 3);
                }

                byte[] expected = ServerTransform(ordinal, body);
                byte[] actual = (byte[])body.Clone();

                HadesCipher.Transform(actual, parameters, ordinal);

                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public async Task Mobile_client_logs_in_and_enters_the_world()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        List<string> reported = [];

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            MobileName,
            LoginFlow.SyntheticSecret,
            new Progress<string>(reported.Add),
            TestDeadline.Token);

        Assert.Equal(MobileName, session.Character.CharacterName);
        Assert.Equal(server.GamePort, session.Character.Port);
        Assert.Equal(HadesCipher.SupportedSeed, session.Parameters.Seed);

        // The server logs this line only once the character is standing in a map.
        LoginFlow.WaitForLog(server, LoginFlow.WelcomeMessage(MobileName), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Mobile_client_reports_the_server_refusal_instead_of_hanging()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        ProtocolException refused = await Assert.ThrowsAsync<ProtocolException>(
            async () => await HadesLoginClient.LoginAsync(
                IPAddress.Loopback,
                server.LoginPort,
                MobileName,
                "definitely-the-wrong-secret",
                progress: null,
                TestDeadline.Token));

        // The server's own words, decrypted — not a timeout of our own making, and never the secret we sent.
        Assert.Contains("Password", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("definitely-the-wrong-secret", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>The server's own cipher, used here as the oracle our implementation must match.</summary>
    private static byte[] ServerTransform(byte ordinal, byte[] body)
    {
        byte[] packet = [0x57, ordinal, .. body];
        NetworkPacket network = new(packet, packet.Length);

        new SecurityProvider().Transform(network);

        return network.Data;
    }
}
