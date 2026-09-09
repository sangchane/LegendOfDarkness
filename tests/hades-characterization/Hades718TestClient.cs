using System.Net;
using System.Net.Sockets;

namespace Lod.Hades.Characterization;

/// <summary>One framed 7.18 packet: the command byte and everything after it.</summary>
public sealed record PacketFrame(byte Command, byte[] Payload);

/// <summary>
/// Minimal 7.18 wire client used to drive the unmodified server. It only frames packets; anything that
/// needs the session cipher is layered on top.
/// </summary>
/// <remarks>
/// The same frame layout is also implemented in <c>mobile/src/Lod.Mobile.Core</c>. That project cannot be
/// built here yet (it targets net9.0 and no .NET 9 SDK is installed), so the two live apart for now and one
/// of them should become the single source once the mobile core builds.
/// </remarks>
public sealed class Hades718TestClient : IDisposable
{
    private const byte FrameMagic = 0xAA;
    private const int HeaderLength = 3;
    private const int IoTimeoutMilliseconds = 10_000;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    private Hades718TestClient(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _stream.ReadTimeout = IoTimeoutMilliseconds;
        _stream.WriteTimeout = IoTimeoutMilliseconds;
    }

    public static Hades718TestClient Connect(int port)
    {
        TcpClient client = new();
        client.Connect(IPAddress.Loopback, port);

        return new Hades718TestClient(client);
    }

    public PacketFrame Receive()
    {
        byte[] header = new byte[HeaderLength];
        _stream.ReadExactly(header);

        if (header[0] != FrameMagic)
        {
            throw new InvalidOperationException(
                $"Expected frame magic 0x{FrameMagic:X2} but the server sent 0x{header[0]:X2}.");
        }

        int length = (header[1] << 8) | header[2];

        if (length < 1)
        {
            throw new InvalidOperationException($"A frame carries at least a command byte, but its length was {length}.");
        }

        byte[] body = new byte[length];
        _stream.ReadExactly(body);

        return new PacketFrame(body[0], body[1..]);
    }

    public void Send(byte command, params byte[] payload)
    {
        int length = payload.Length + 1;
        byte[] frame = new byte[HeaderLength + length];
        frame[0] = FrameMagic;
        frame[1] = (byte)(length >> 8);
        frame[2] = (byte)length;
        frame[3] = command;
        payload.CopyTo(frame, HeaderLength + 1);

        _stream.Write(frame);
        _stream.Flush();
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
