using Darkages.Network;
using Darkages.Security;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;
using Xunit;
using MobileFrame = Lod.Mobile.Core.Protocol.PacketFrame;
using MobileRedirect = Lod.Mobile.Core.Protocol.Login.RedirectTarget;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Proves the mobile client's own protocol code against the real server rather than against a recorded
/// fixture. The fixture says what we believe the bytes are; these say the server agrees.
/// </summary>
public sealed class MobileClientProtocolTests
{
    private const string MobileName = "lodmobile";

    /// <summary>Frames the harness sends and receives, in the mobile library's shape.</summary>
    private static MobileFrame ToMobile(PacketFrame frame) => new(frame.Command, frame.Payload);

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
    public void Mobile_protocol_enters_the_world_on_a_running_server()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        EncryptionParameters parameters;
        MobileRedirect lobby;

        // The lobby hop. Every byte the mobile library owns is built by it; the rest uses the harness.
        using (Hades718TestClient client = Hades718TestClient.Connect(server.LoginPort))
        {
            client.Receive();

            client.SendRaw(Hades718LoginProtocol.CreateVersionRequest());

            PacketFrame announced = client.Receive();
            parameters = Hades718LoginProtocol.ParseServerParameters(ToMobile(announced));

            client.UseEncryption(announced);
            client.SendSecured(0x57, ordinal: 0, 0x00);

            lobby = Hades718LoginProtocol.ParseRedirect(ToMobile(client.Receive()));
        }

        Assert.Equal(HadesCipher.SupportedSeed, parameters.Seed);
        Assert.Equal(server.LoginPort, lobby.Port);

        MobileRedirect game;

        // The login connection: present the lobby ticket, send credentials, read where the world is.
        using (Hades718TestClient client = Hades718TestClient.Connect(lobby.Port))
        {
            client.Receive();

            client.SendRaw(Hades718LoginProtocol.CreateGameEntryRequest(lobby));
            client.Receive();

            client.UseEncryption(announcedFor(parameters));

            client.SendRaw(Hades718LoginProtocol.CreateLoginRequest(
                MobileName,
                LoginFlow.SyntheticSecret,
                parameters,
                ordinal: 0));

            client.Receive();

            game = Hades718LoginProtocol.ParseRedirect(ToMobile(client.Receive()));
        }

        Assert.Equal(MobileName, game.CharacterName);
        Assert.Equal(server.GamePort, game.Port);

        using Hades718TestClient world = Hades718TestClient.Connect(game.Port);
        world.SendRaw(Hades718LoginProtocol.CreateGameEntryRequest(game));

        // The server logs this line only after the character is standing in the map.
        LoginFlow.WaitForLog(server, LoginFlow.WelcomeMessage(MobileName), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Rebuilds the announcement frame the harness client needs to set up its own cipher, from the
    /// parameters the mobile library read out of it.
    /// </summary>
    private static PacketFrame announcedFor(EncryptionParameters parameters)
    {
        byte[] payload =
        [
            0x00,
            (byte)(parameters.ServerTableHash >> 24),
            (byte)(parameters.ServerTableHash >> 16),
            (byte)(parameters.ServerTableHash >> 8),
            (byte)parameters.ServerTableHash,
            parameters.Seed,
            (byte)parameters.Salt.Length,
            .. parameters.Salt.ToArray()
        ];

        return new PacketFrame(0x00, payload);
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
