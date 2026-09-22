using System.Net;
using System.Net.Sockets;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Net;

/// <summary>
/// One socket, read and written a frame at a time. TCP has no message boundaries, so what arrives may be a
/// piece of a frame, exactly one, or several — this keeps the leftovers between calls.
/// </summary>
public sealed class HadesConnection : IDisposable
{
    private const int ReadSize = 4096;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    private byte[] _pending = [];
    private int _disposed;
    private int _disposeAttempts;

    private HadesConnection(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    public static async Task<HadesConnection> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        TcpClient client = new();

        try
        {
            await client.ConnectAsync(address, port, cancellationToken);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        // Login is a short conversation of small packets; waiting to coalesce them only adds delay.
        client.NoDelay = true;

        return new HadesConnection(client);
    }

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) =>
        await _stream.WriteAsync(frame, cancellationToken);

    /// <summary>Whether this socket has already crossed its one-way ownership boundary.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal int DisposeAttempts => Volatile.Read(ref _disposeAttempts);

    /// <summary>Returns the next complete frame, reading from the socket until one is there.</summary>
    public async Task<PacketFrame> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            FrameReadStatus status = PacketFrameCodec.TryDecode(_pending, out PacketFrame? frame, out int consumed);

            if (status == FrameReadStatus.Complete)
            {
                _pending = _pending[consumed..];

                return frame!;
            }

            if (status == FrameReadStatus.Invalid)
            {
                // Not this protocol, or the stream lost its place. Either way it cannot be resynchronised.
                throw new ProtocolException(
                    $"프레임으로 읽을 수 없는 바이트를 받았습니다: {Convert.ToHexString(_pending.AsSpan(0, Math.Min(8, _pending.Length)))}");
            }

            byte[] buffer = new byte[ReadSize];
            int read = await _stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                throw new ProtocolException(
                    $"프레임이 끝나기 전에 상대가 연결을 닫았습니다 (받아 둔 바이트 {_pending.Length}개).");
            }

            _pending = [.. _pending, .. buffer.AsSpan(0, read)];
        }
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeAttempts);

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stream.Dispose();
        _client.Dispose();
    }
}
