using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 괴물이 사람 그림으로 그려지지 않는지(2026-09-24 사용자 보고 — 우드랜드1-1 에서 "윗옷 없는 남자에 녹색 깃털 모자"
/// 다섯이 돌아다니다 괴물로 바뀐다; 그 그림이 모르는 사람에게 입히는 npc-walk.png 다).
/// 서버는 괴물이 한 걸음 옮긴 **뒤의** 자리로 곁의 아이슬링을 골라 0x0C 를 보내지만, 그 아이슬링에게 괴물을 보여 주는
/// 0x07 은 걸음 **전** 자리로 고른다(<c>Sprite.Walk</c> → <c>ObjectComponent.UpdateClientObjects</c>). 그래서 시야
/// 가장자리로 걸어 들어오는 괴물, 0x15 로 화면을 비운 직후 걷는 괴물은 0x07 보다 0x0C 가 먼저 온다.
/// </summary>
public sealed class MonsterShapeTests
{
    private const uint Me = 7;
    private const uint Beast = 0x0001_2345;

    [Fact]
    public async Task A_monster_whose_step_arrives_before_it_is_shown_is_never_a_person()
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
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

        await Say(0x05, [0, 0, 0, (byte)Me]);
        await Say(0x15, [0x4E, 0x2F, 60, 60, 0, 0, 0, 0, 0, 0]);

        // 괴물의 걸음(0x0C: serial, 걸음 전 x·y, 방향)이 그 괴물을 보여 주기(0x07) 전에 온다.
        await Say(0x0C, [0x00, 0x01, 0x23, 0x45, 0, 10, 0, 10, 1]);
        await Say(0x04, [0, 5, 0, 5]);
        await Until(() => world.PositionReports == 1, deadline.Token);

        Assert.DoesNotContain(world.Others, one => one.Serial == Beast);

        // 이어서 서버가 그것을 괴물로 보여 준다(ServerFormat07 한 마리: x, y, serial, 그림, …, 방향, 0, 종류 0).
        byte[] shown = new byte[2 + 17];
        shown[1] = 1;
        shown[3] = 11;
        shown[5] = 10;
        shown[6] = 0x00;
        shown[7] = 0x01;
        shown[8] = 0x23;
        shown[9] = 0x45;
        shown[10] = 0x41;
        shown[11] = 0x0F;
        shown[16] = 1;
        await Say(0x07, shown);
        await Say(0x04, [0, 5, 0, 5]);
        await Until(() => world.PositionReports == 2, deadline.Token);

        Assert.Contains(world.Creatures, one => one.Serial == Beast && one.Where == new Tile(11, 10));
        Assert.DoesNotContain(world.Others, one => one.Serial == Beast);
    }

    private static async Task Until(Func<bool> done, CancellationToken cancellationToken)
    {
        while (!done())
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
