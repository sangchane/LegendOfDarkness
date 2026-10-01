using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 봇은 화면이 꺼내 가는 큐(소리·동작·이펙트·숫자·맞음·음악)를 꺼내지 않는다 — 상한이 없으면 접속해 있는 내내 쌓였다.
/// 그리고 맵이 바뀌면 지난 맵의 체력 막대(0x13)와 누가 쳤나(0x5D)도 괴물처럼 버린다.
/// </summary>
public sealed class QueueBoundTests
{
    private const uint Me = 7;
    private const uint Other = 9;
    private const uint Beast = 0x0001_2345;

    [Fact]
    public async Task Undrained_queues_stop_growing_and_a_new_map_forgets_old_bars()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(20));
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        IPEndPoint endpoint = (IPEndPoint)listener.LocalEndpoint;
        Task<TcpClient> accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        HadesConnection connection = await HadesConnection.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token);
        using TcpClient server = await accepting;
        EncryptionParameters cipher = new(HadesCipher.SupportedSeed, "NexonInc."u8.ToArray(), 0);
        using WorldSession session = new(
            connection, new RedirectTarget(IPAddress.Loopback, 0, cipher.Seed, cipher.Salt, "monk", 1), cipher);
        WorldClient world = new(session);
        _ = world.PumpAsync(deadline.Token);

        byte ordinal = 0;
        async Task Say(byte command, byte[] body) =>
            await server.GetStream().WriteAsync(HadesCipher.EncodeSecured(command, ordinal++, body, cipher), deadline.Token);
        int reports = 0;
        async Task Settle()
        {
            await Say(0x04, [0, 5, 0, 5]);
            reports++;
            await Until(() => world.PositionReports == reports, deadline.Token);
        }

        // 다른 사람 하나(괴물 모양 짧은 0x33: x·y·방향·serial·0xFFFF).
        byte[] other = [0, 6, 0, 5, 2, 0, 0, 0, (byte)Other, 0xFF, 0xFF];

        await Say(0x05, [0, 0, 0, (byte)Me]);
        await Say(0x15, [0x4E, 0x2F, 60, 60, 0, 0, 0, 0, 0, 0]);
        await Say(0x33, other);

        // 괴물의 체력 막대 50%(0x13: serial·체력·소리 없음)와, 그 괴물을 다른 사람이 친 숫자(0x5D: 대상·친 이·양·피해).
        await Say(0x13, [0x00, 0x01, 0x23, 0x45, 0, 50, 0]);
        await Say(0x5D, [0x00, 0x01, 0x23, 0x45, 0, 0, 0, (byte)Other, 0, 0, 0, 10, 0]);

        // 아무도 꺼내지 않는 효과음을 상한보다 많이.
        for (int i = 0; i < WorldClient.QueueKept + 100; i++)
        {
            await Say(0x19, [0, 0, 5]);
        }

        await Settle();

        Assert.Equal(50, world.Health(Beast));
        Assert.True(world.StruckByOthers(Beast, TimeSpan.FromMinutes(1)));

        int sounds = 0;

        while (world.TakeSound(out _))
        {
            sounds++;
        }

        Assert.Equal(WorldClient.QueueKept, sounds);

        // 다른 맵으로 — 같은 사람이 다시 보여도 지난 맵의 막대와 "누가 쳤나"는 남지 않는다.
        await Say(0x15, [0x4E, 0x30, 60, 60, 0, 0, 0, 0, 0, 0]);
        await Say(0x33, other);
        await Settle();

        Assert.Contains(world.Others, one => one.Serial == Other);
        Assert.Null(world.Health(Beast));
        Assert.False(world.StruckByOthers(Beast, TimeSpan.FromMinutes(1)));
    }

    private static async Task Until(Func<bool> done, CancellationToken cancellationToken)
    {
        while (!done())
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
