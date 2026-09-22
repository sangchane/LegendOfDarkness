using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.Net;

public sealed class HadesConnectionTests
{
    /// <summary>A hung read should fail the test rather than stall the run.</summary>
    private static readonly CancellationTokenSource Deadline = new(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task Receive_waits_for_a_frame_that_arrives_in_pieces()
    {
        using Peer peer = Peer.Listen();
        using HadesConnection connection = await peer.AcceptedClient();

        byte[] frame = PacketFrameCodec.Encode([0x7E, 0x1B, 0x4F, 0x4B]);

        Task<PacketFrame> pending = connection.ReceiveAsync(Deadline.Token);

        await peer.SendAsync(frame[..2]);
        await peer.SendAsync(frame[2..]);

        PacketFrame received = await pending;

        Assert.Equal((byte)0x7E, received.Command);
        Assert.Equal(new byte[] { 0x1B, 0x4F, 0x4B }, received.Data.ToArray());
    }

    [Fact]
    public async Task Receive_hands_back_frames_that_arrived_in_one_read()
    {
        using Peer peer = Peer.Listen();
        using HadesConnection connection = await peer.AcceptedClient();

        await peer.SendAsync([
            .. PacketFrameCodec.Encode([0x01, 0xAA]),
            .. PacketFrameCodec.Encode([0x02, 0xBB])
        ]);

        PacketFrame first = await connection.ReceiveAsync(Deadline.Token);
        PacketFrame second = await connection.ReceiveAsync(Deadline.Token);

        Assert.Equal((byte)0x01, first.Command);
        Assert.Equal((byte)0x02, second.Command);
    }

    [Fact]
    public async Task Receive_reports_a_peer_that_hangs_up_mid_frame()
    {
        using Peer peer = Peer.Listen();
        using HadesConnection connection = await peer.AcceptedClient();

        await peer.SendAsync([0xAA, 0x00, 0x08, 0x7E]);
        peer.HangUp();

        await Assert.ThrowsAsync<ProtocolException>(
            async () => await connection.ReceiveAsync(Deadline.Token));
    }

    [Fact]
    public async Task Receive_rejects_a_stream_that_is_not_this_protocol()
    {
        using Peer peer = Peer.Listen();
        using HadesConnection connection = await peer.AcceptedClient();

        await peer.SendAsync([0x48, 0x54, 0x54, 0x50]);

        await Assert.ThrowsAsync<ProtocolException>(
            async () => await connection.ReceiveAsync(Deadline.Token));
    }

    [Fact]
    public async Task World_client_dispose_closes_every_owner_once_and_stops_the_pump()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        using Peer peer = Peer.Listen();
        HadesConnection connection = await peer.AcceptedClient();
        EncryptionParameters cipher = new(HadesCipher.SupportedSeed, "NexonInc."u8.ToArray(), 0);
        WorldSession session = new(
            connection,
            new RedirectTarget(IPAddress.Loopback, 0, cipher.Seed, cipher.Salt, "퇴장시험", 1),
            cipher);
        WorldClient world = new(session);

        Task pump = world.PumpAsync(deadline.Token);

        world.Dispose();

        // GameScreen explicitly closes on logout and _ExitTree closes defensively. Direct owners may also
        // be used by engine-free clients, so the top owner must tolerate that same second close without
        // repeating the calls down the ownership chain.
        world.Dispose();

        await pump;

        Assert.True(world.IsDisposed);
        Assert.True(session.IsDisposed);
        Assert.True(connection.IsDisposed);
        Assert.Equal(2, world.DisposeAttempts);
        Assert.Equal(1, session.DisposeAttempts);
        Assert.Equal(1, connection.DisposeAttempts);
        Assert.Equal(0, await peer.ReadAsync(deadline.Token));
    }

    /// <summary>A listener on loopback that plays the far side of one connection.</summary>
    private sealed class Peer : IDisposable
    {
        private readonly TcpListener _listener;
        private TcpClient? _accepted;

        private Peer(TcpListener listener) => _listener = listener;

        public static Peer Listen()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();

            return new Peer(listener);
        }

        public async Task<HadesConnection> AcceptedClient()
        {
            IPEndPoint endpoint = (IPEndPoint)_listener.LocalEndpoint;

            Task<TcpClient> accepting = _listener.AcceptTcpClientAsync();
            HadesConnection connection = await HadesConnection.ConnectAsync(endpoint.Address, endpoint.Port, CancellationToken.None);

            _accepted = await accepting;

            return connection;
        }

        public Task SendAsync(byte[] bytes) =>
            _accepted!.GetStream().WriteAsync(bytes, CancellationToken.None).AsTask();

        public Task<int> ReadAsync(CancellationToken cancellationToken) =>
            _accepted!.GetStream().ReadAsync(new byte[1], cancellationToken).AsTask();

        public void HangUp() => _accepted!.Close();

        public void Dispose()
        {
            _accepted?.Dispose();
            _listener.Dispose();
        }
    }
}
