using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 세계와 닿아 있는 동안과 떠날 때 알맹이가 서버에 하는 말 — 심장박동에 답하고, 나갈 때 먼저 알리고, 맵이 바뀌면 보던 것을 버린다.
/// 가짜 서버(소켓 하나)에 서버가 쓰는 차례 그대로 보낸다.
/// </summary>
public sealed class LeavingTests : IAsyncLifetime
{
    private const uint Me = 7;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(10));
    private readonly EncryptionParameters _cipher = new(HadesCipher.SupportedSeed, "NexonInc."u8.ToArray(), 0);
    private TcpListener _listener = null!;
    private TcpClient _server = null!;
    private WorldSession _session = null!;
    private WorldClient _world = null!;
    private byte _ordinal;

    public async Task InitializeAsync()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        IPEndPoint endpoint = (IPEndPoint)_listener.LocalEndpoint;
        Task<TcpClient> accepting = _listener.AcceptTcpClientAsync(_deadline.Token).AsTask();
        HadesConnection connection = await HadesConnection.ConnectAsync(endpoint.Address, endpoint.Port, _deadline.Token);
        _server = await accepting;
        _session = new WorldSession(connection, new RedirectTarget(IPAddress.Loopback, 0, _cipher.Seed, _cipher.Salt, "monk", 1), _cipher);
        _world = new WorldClient(_session);
        _ = _world.PumpAsync(_deadline.Token);
    }

    public Task DisposeAsync()
    {
        _session.Dispose();
        _server.Dispose();
        _listener.Stop();
        _deadline.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_heartbeat_is_answered_with_0x45_carrying_the_same_bytes()
    {
        await Say(0x3B, [0x00, 0x01]);

        PacketFrame answer = await Heard();
        Assert.Equal(0x45, answer.Command);
        Assert.Equal([0x00, 0x01], HadesCipher.DecodeSecured(answer, _cipher));
    }

    [Fact]
    public async Task Logging_out_says_so_first_waits_for_0x4C_and_then_closes()
    {
        Task leaving = _world.LogOutAsync(_deadline.Token);

        PacketFrame exit = await Heard();
        Assert.Equal(0x0B, exit.Command);
        Assert.Equal([1], HadesCipher.DecodeSecured(exit, _cipher));
        Assert.False(leaving.IsCompleted);

        await Say(0x4C, [1, 0, 0]);
        await leaving.WaitAsync(_deadline.Token);
        Assert.True(_session.IsDisposed);
    }

    [Fact]
    public async Task Logging_out_closes_anyway_when_the_server_never_answers()
    {
        DateTime began = DateTime.UtcNow;
        await _world.LogOutAsync(_deadline.Token);

        Assert.True(_session.IsDisposed);
        Assert.True(DateTime.UtcNow - began < WorldClient.LogOutWait + TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 0x15 가 오면 지난 맵의 괴물·사람을 버린다 — 서버는 곧 새 시야를 다시 보낸다(GameClient.RefreshMap).
    /// 버리지 않으면 사냥터 괴물이 뮤레칸의방·마을 위에 선 채 남아 누르는 손을 가로챈다.
    /// </summary>
    [Fact]
    public async Task A_map_change_forgets_the_creatures_and_people_of_the_map_before()
    {
        await Say(0x05, [0, 0, 0, (byte)Me]);
        await Say(0x15, [0x4E, 0x2F, 60, 60, 0, 0, 0, 0, 0, 0]);
        await Say(0x07, Creature(serial: 900, x: 12, y: 5));
        await Say(0x33, Person(serial: 901, x: 11, y: 5));
        await Until(() => _world.Creatures.Count == 1 && _world.Others.Count == 1);

        await Say(0x15, [0x4E, 0xAA, 30, 30, 0, 0, 0, 0, 0, 0]);

        await Until(() => _world.Creatures.Count == 0 && _world.Others.Count == 0);
    }

    private async Task Say(byte command, byte[] body) =>
        await _server.GetStream().WriteAsync(HadesCipher.EncodeSecured(command, _ordinal++, body, _cipher), _deadline.Token);

    private async Task<PacketFrame> Heard()
    {
        byte[] pending = [];
        NetworkStream stream = _server.GetStream();

        while (true)
        {
            if (PacketFrameCodec.TryDecode(pending, out PacketFrame? frame, out _) == FrameReadStatus.Complete)
            {
                return frame!;
            }

            byte[] buffer = new byte[256];
            int read = await stream.ReadAsync(buffer, _deadline.Token);
            Assert.NotEqual(0, read);
            pending = [.. pending, .. buffer.AsSpan(0, read)];
        }
    }

    private async Task Until(Func<bool> done)
    {
        while (!done())
        {
            await Task.Delay(10, _deadline.Token);
        }
    }

    /// <summary>0x07 한 마리: 칸, serial, 그림(0x4000 넘는 것이 괴물·NPC), 방향, 종류(0 괴물).</summary>
    private static byte[] Creature(uint serial, int x, int y) =>
        [0, 1, 0, (byte)x, 0, (byte)y, 0, 0, (byte)(serial >> 8), (byte)serial, 0x40, 0x01, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>ServerFormat33 모양의 사람 하나(이름 없음).</summary>
    private static byte[] Person(uint serial, int x, int y)
    {
        byte[] body = new byte[32];
        body[1] = (byte)x;
        body[3] = (byte)y;
        body[7] = (byte)(serial >> 8);
        body[8] = (byte)serial;
        body[10] = 1;
        body[11] = 0x10;
        return body;
    }
}
