using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Which way our own figure stands after the server has said something about us. The server does not answer a
/// step it allows, so what it last said about our facing is a step behind our walking — and following it at the
/// wrong moment stands a figure walking west the way it had faced north (two drawings out of four).
/// </summary>
public sealed class OwnFacingTests
{
    private const uint Me = 7;

    // 이형환위 as the isolated server plays it: facing north with the target one tile ahead, the monk lands two
    // tiles on and turns round (MonkStrike.Step → Refresh: 0x33 describes us, then 0x04 says where we are).
    private static readonly Tile Before = new(2, 35);
    private static readonly Tile Landing = new(2, 33);

    [Fact]
    public void Put_past_the_target_we_stand_the_way_the_server_turned_us()
    {
        Character described = new(Me, Landing, Direction.South);

        Assert.Equal(Direction.South, OwnFacing.PutBy(described, Landing, walkedTo: Before));
    }

    [Fact]
    public void A_report_of_the_tile_we_walked_to_leaves_our_own_facing_alone()
    {
        // We walked west from (5,5); the server last described us facing north before that step, and now only
        // agrees we are where we walked. Taking its facing would draw a walk west as a walk north.
        Tile walked = new(4, 5);
        Character described = new(Me, walked, Direction.North);

        Assert.Null(OwnFacing.PutBy(described, walked, walkedTo: walked));
    }

    [Fact]
    public void A_location_without_a_description_of_us_there_does_not_turn_us()
    {
        // 0x04 on its own (a cast broken by a step, being freed from a corner) — the last 0x33 described us
        // somewhere else, and the way we faced there says nothing about now.
        Character described = new(Me, new Tile(9, 9), Direction.East);

        Assert.Null(OwnFacing.PutBy(described, Landing, walkedTo: Before));
    }

    [Fact]
    public void Before_the_server_has_described_us_there_is_nothing_to_follow()
    {
        Assert.Null(OwnFacing.PutBy(null, Landing, walkedTo: Before));
    }

    /// <summary>
    /// The same over a real socket, in the order the server writes it. A turn we asked for comes back to us and
    /// rewrites how we are described with no location at all — that is what the figure must not follow. The leap's
    /// Client.Refresh (GameClient.Enter: 0x05, 0x15, 0x33, 0x04) describes us on the landing tile before it says
    /// we stand there, so by the time the location is read the description to follow is already in.
    /// </summary>
    [Fact]
    public async Task Over_the_wire_an_echoed_turn_has_no_location_and_a_leap_is_described_where_it_lands()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        IPEndPoint endpoint = (IPEndPoint)listener.LocalEndpoint;
        Task<TcpClient> accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        HadesConnection connection = await HadesConnection.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token);
        using TcpClient server = await accepting;
        EncryptionParameters cipher = new(HadesCipher.SupportedSeed, "NexonInc."u8.ToArray(), 0);
        using WorldClient world = new(new WorldSession(
            connection, new RedirectTarget(IPAddress.Loopback, 0, cipher.Seed, cipher.Salt, "monk", 1), cipher));
        _ = world.PumpAsync(deadline.Token);

        byte ordinal = 0;
        async Task Say(byte command, byte[] body) =>
            await server.GetStream().WriteAsync(HadesCipher.EncodeSecured(command, ordinal++, body, cipher), deadline.Token);
        async Task Refresh(Tile where, byte facing)
        {
            await Say(0x05, [0, 0, 0, (byte)Me]);
            await Say(0x15, [0x4E, 0x2F, 60, 60, 0, 0, 0, 0, 0, 0]);
            await Say(0x33, Described(where, facing));
            await Say(0x04, [0, (byte)where.X, 0, (byte)where.Y]);
        }

        await Refresh(Before, facing: 0);
        await Until(() => world.PositionReports == 1 && world.Self?.Facing == Direction.North, deadline.Token);

        // Our own turn west, sent back to us (Format11Handler → Scope.NearbyAislings).
        await Say(0x11, [0, 0, 0, (byte)Me, 3]);
        await Until(() => world.Self?.Facing == Direction.West, deadline.Token);
        Assert.Equal(1, world.PositionReports);

        // 이형환위 north over the target: two tiles on, turned south (MonkStrike.Step).
        await Refresh(Landing, facing: 2);
        await Until(() => world.PositionReports == 2 && world.State?.Where == Landing, deadline.Token);

        Assert.Equal(Direction.South, OwnFacing.PutBy(world.Self, world.State!.Where, walkedTo: Before));
    }

    /// <summary>A 0x33 as ServerFormat33 writes it: tile, facing, serial, then the wardrobe and an empty name.</summary>
    private static byte[] Described(Tile where, byte facing)
    {
        byte[] body = new byte[32];
        body[1] = (byte)where.X;
        body[3] = (byte)where.Y;
        body[4] = facing;
        body[8] = (byte)Me;
        body[10] = 1; // a head, not a monster's shape (0xFFFF)
        return body;
    }

    private static async Task Until(Func<bool> done, CancellationToken cancellationToken)
    {
        while (!done())
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
