using System.Net;
using Darkages.Network;
using Darkages.Security;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;
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

    /// <summary>
    /// A stalled exchange should fail the test rather than hold the run. One per test, not one for the
    /// class: xunit builds the class again for each test, and a shared clock would have already run down.
    /// </summary>
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(2));

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
            _deadline.Token);

        Assert.Equal(MobileName, session.Character.CharacterName);
        Assert.Equal(server.GamePort, session.Character.Port);
        Assert.Equal(HadesCipher.SupportedSeed, session.Parameters.Seed);

        // The server logs this line only once the character is standing in a map.
        LoginFlow.WaitForLog(server, LoginFlow.WelcomeMessage(MobileName), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task World_says_which_map_the_character_is_on_and_where()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry entry = await Settled(world, seen => seen is not null);

        // lod1.map, the same 30x31 floor tools/dat-extract draws for the mockups, and where
        // LoruleConfig.json drops a new character.
        Assert.Equal(new MapInfo(1, 30, 31, "Safe House"), entry.Map);
        Assert.Equal(new Tile(4, 4), entry.Where);
    }

    [Fact]
    public async Task Walking_moves_the_character_on_the_server()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry start = await Settled(world, seen => seen is not null);

        // The server drops a walk while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);
        await world.WalkAsync(Direction.East, _deadline.Token);

        // It tells the people nearby rather than the walker, so ask it where we are.
        await world.RefreshAsync(_deadline.Token);

        Tile expected = new(start.Where.X + 1, start.Where.Y);
        WorldEntry after = await Settled(world, seen => seen?.Where == expected);

        Assert.Equal(expected, after.Where);
        Assert.Equal(start.Map, after.Map);
    }

    [Fact]
    public async Task Walking_faster_than_the_server_allows_pushes_the_character_back()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen is not null);

        // The server drops a walk while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        // The control: a step the server is happy with is answered with silence. This is why a client has
        // to move its own figure rather than wait to be told.
        int told = world.PositionReports;

        await world.WalkAsync(Direction.East, _deadline.Token);
        await Task.Delay(TimeSpan.FromSeconds(3), _deadline.Token);

        Assert.True(world.PositionReports == told, "서버가 정상적인 걸음에도 위치를 보내왔습니다.");

        // Now the same direction as fast as the socket will take it. The server watches how quickly a
        // client claims to move and puts it back where it really is, so a client that ignores this will
        // rubber-band on every hurried step.
        for (int step = 0; step < 8; step++)
        {
            await world.WalkAsync(Direction.East, _deadline.Token);
        }

        WorldEntry after = await Settled(world, _ => world.PositionReports > told, TimeSpan.FromSeconds(15));

        Assert.InRange(after.Where.X, 0, 29);
        Assert.InRange(after.Where.Y, 0, 30);
    }

    /// <summary>Waits for the pump to report a state the test is looking for.</summary>
    private async Task<WorldEntry> Settled(
        WorldClient world,
        Func<WorldEntry?, bool> wanted,
        TimeSpan? within = null)
    {
        DateTime giveUp = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < giveUp)
        {
            WorldEntry? seen = world.State;

            if (wanted(seen))
            {
                return seen!;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 기다리는 상태를 알려주지 않았습니다. 마지막으로 본 것: {world.State}");
    }

    private Task<WorldSession> LoginAsync(IsolatedHadesServer server) =>
        HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            MobileName,
            LoginFlow.SyntheticSecret,
            progress: null,
            _deadline.Token);

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
                _deadline.Token));

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
