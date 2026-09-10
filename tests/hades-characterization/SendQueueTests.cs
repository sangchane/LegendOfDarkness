using System.Net;
using System.Net.Sockets;
using Darkages.Network;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// What a connection has to survive: a packet far larger than the socket's buffer, a run of packets that
/// must arrive in the order they were handed over, and a far side that has stopped reading altogether.
/// </summary>
public sealed class SendQueueTests
{
    [Fact]
    public async Task Every_byte_arrives_in_the_order_it_was_sent()
    {
        using Pair pair = Pair.Connected(sendBuffer: 512);

        // Far more than the send buffer holds, so this only arrives whole if the write is seen through.
        byte[] packet = new byte[200_000];

        for (int index = 0; index < packet.Length; index++)
        {
            packet[index] = (byte)index;
        }

        Task<byte[]> reading = pair.ReadAsync(packet.Length);

        SendQueue.SendAll(pair.Sender, packet);

        Assert.Equal(packet, await reading);
    }

    [Fact]
    public async Task Packets_keep_their_order_through_the_queue()
    {
        using Pair pair = Pair.Connected(sendBuffer: 512);
        using SendQueue queue = new(pair.Sender, capacity: 64, failed: () => { });

        byte[] expected = new byte[64 * 1000];

        for (int packet = 0; packet < 64; packet++)
        {
            byte[] one = new byte[1000];
            Array.Fill(one, (byte)packet);
            one.CopyTo(expected, packet * 1000);

            Assert.True(queue.Enqueue(one));
        }

        Assert.Equal(expected, await pair.ReadAsync(expected.Length));
    }

    [Fact]
    public void A_client_that_stopped_reading_is_given_up_rather_than_waited_for()
    {
        using Pair pair = Pair.Connected(sendBuffer: 512);

        bool given = false;
        using SendQueue queue = new(pair.Sender, capacity: 8, failed: () => given = true);

        // Nothing is read from the far side, so the socket buffers fill and the queue backs up behind them.
        byte[] one = new byte[16_000];
        bool refused = false;

        for (int packet = 0; packet < 500 && !refused; packet++)
        {
            refused = !queue.Enqueue(one);
        }

        Assert.True(refused, "읽지 않는 연결에 무한정 쌓였습니다.");
        Assert.False(given, "연결을 포기한 것이 아니라 쓰기가 실패한 것으로 처리됐습니다.");
    }

    /// <summary>Two connected sockets on loopback, with the sender's buffer made small on purpose.</summary>
    private sealed class Pair : IDisposable
    {
        private readonly TcpListener _listener;

        public required Socket Sender { get; init; }
        public required Socket Receiver { get; init; }

        private Pair(TcpListener listener) => _listener = listener;

        public static Pair Connected(int sendBuffer)
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();

            TcpClient sender = new();
            sender.Connect((IPEndPoint)listener.LocalEndpoint);

            Socket accepted = listener.AcceptSocket();

            sender.Client.SendBufferSize = sendBuffer;
            accepted.ReceiveBufferSize = sendBuffer;

            return new Pair(listener) { Sender = sender.Client, Receiver = accepted };
        }

        public Task<byte[]> ReadAsync(int count) => Task.Run(() =>
        {
            byte[] read = new byte[count];
            int got = 0;

            while (got < count)
            {
                int block = Receiver.Receive(read, got, count - got, SocketFlags.None);

                if (block <= 0)
                {
                    break;
                }

                got += block;
            }

            return read;
        });

        public void Dispose()
        {
            Sender.Dispose();
            Receiver.Dispose();
            _listener.Dispose();
        }
    }
}
