using System.Net;
using System.Net.Sockets;
using Darkages.Network;
using Darkages.Security;

namespace Lod.Hades.Characterization;

/// <summary>One framed 7.18 packet: the command byte and everything after it.</summary>
public sealed record PacketFrame(byte Command, byte[] Payload);

/// <summary>
/// Where the server tells the client to continue. Name, salt and serial are kept as raw bytes because the
/// client only echoes them back.
/// </summary>
public sealed record RedirectTarget(IPAddress Address, int Port, byte Seed, byte[] Salt, byte[] Name, byte[] Serial);

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

    private const byte ServerParametersCommand = 0x00;
    private const byte RedirectCommand = 0x03;
    private const byte RedirectRequestCommand = 0x10;

    // ServerFormat00 payload: type(1) + server table hash(4), then seed, salt length and salt.
    private const int SeedOffset = 5;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private SecurityProvider? _encryption;

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

    /// <summary>Adopts the seed and salt the server handed out, so later packets use the session cipher.</summary>
    public void UseEncryption(PacketFrame serverParameters)
    {
        if (serverParameters.Command != ServerParametersCommand)
        {
            throw new InvalidOperationException(
                $"Encryption parameters arrive on command 0x{ServerParametersCommand:X2}, " +
                $"but this frame was 0x{serverParameters.Command:X2}.");
        }

        byte[] payload = serverParameters.Payload;

        if (payload.Length < SeedOffset + 2)
        {
            throw new InvalidOperationException("The encryption parameters packet ended before the seed.");
        }

        byte seed = payload[SeedOffset];
        int saltLength = payload[SeedOffset + 1];
        int saltEnd = SeedOffset + 2 + saltLength;

        if (payload.Length < saltEnd)
        {
            throw new InvalidOperationException($"The encryption salt claims {saltLength} bytes but the packet is shorter.");
        }

        _encryption = new SecurityProvider(new SecurityParameters(seed, payload[(SeedOffset + 2)..saltEnd]));
    }

    /// <summary>Sends a packet whose body the session cipher covers. The ordinal itself stays in the clear.</summary>
    public void SendSecured(byte command, byte ordinal, params byte[] payload)
    {
        SecurityProvider cipher = _encryption
            ?? throw new InvalidOperationException("The server has not handed out its parameters yet; call UseEncryption first.");

        byte[] body = [command, ordinal, .. payload];
        NetworkPacket packet = new(body, body.Length);
        cipher.Transform(packet);

        Send(command, [ordinal, .. packet.Data]);
    }

    /// <summary>
    /// Reads a lobby or game redirect. The address arrives with its bytes reversed, and the port follows in
    /// network order.
    /// </summary>
    public static RedirectTarget ParseRedirect(PacketFrame frame)
    {
        if (frame.Command != RedirectCommand)
        {
            throw new InvalidOperationException(
                $"A redirect arrives on command 0x{RedirectCommand:X2}, but this frame was 0x{frame.Command:X2}.");
        }

        byte[] payload = frame.Payload;
        int offset = 0;

        byte[] address = Take(payload, ref offset, 4);
        Array.Reverse(address);
        int port = (Take(payload, ref offset, 1)[0] << 8) | Take(payload, ref offset, 1)[0];
        _ = Take(payload, ref offset, 1);
        byte seed = Take(payload, ref offset, 1)[0];
        byte[] salt = TakeLengthPrefixed(payload, ref offset);
        byte[] name = TakeLengthPrefixed(payload, ref offset);
        byte[] serial = Take(payload, ref offset, 4);

        return new RedirectTarget(new IPAddress(address), port, seed, salt, name, serial);
    }

    /// <summary>Echoes the redirect back on the new connection, which is what admits the client.</summary>
    public void SendRedirectRequest(RedirectTarget target)
    {
        byte[] payload =
        [
            target.Seed,
            (byte)target.Salt.Length, .. target.Salt,
            (byte)target.Name.Length, .. target.Name,
            .. target.Serial
        ];

        Send(RedirectRequestCommand, payload);
    }

    private static byte[] Take(byte[] payload, ref int offset, int count)
    {
        if (offset + count > payload.Length)
        {
            throw new InvalidOperationException(
                $"The packet ended after {payload.Length} bytes while reading {count} at offset {offset}.");
        }

        byte[] slice = payload[offset..(offset + count)];
        offset += count;

        return slice;
    }

    private static byte[] TakeLengthPrefixed(byte[] payload, ref int offset) =>
        Take(payload, ref offset, Take(payload, ref offset, 1)[0]);

    /// <summary>Writes bytes exactly as given, so tests can send frames the codec would never build.</summary>
    public void SendRaw(params byte[] bytes)
    {
        _stream.Write(bytes);
        _stream.Flush();
    }

    /// <summary>
    /// Waits for the server to hang up on this connection. A reset counts as closed; anything the server
    /// still sends is drained while waiting.
    /// </summary>
    public bool WaitForServerToClose(TimeSpan timeout)
    {
        int previousTimeout = _stream.ReadTimeout;
        DateTime deadline = DateTime.UtcNow + timeout;
        byte[] scratch = new byte[256];

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                _stream.ReadTimeout = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);

                if (_stream.Read(scratch, 0, scratch.Length) == 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (IOException error) when (IsReadTimeout(error))
        {
            // Quiet, not closed. Treating a timeout as a close would make every caller pass.
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        finally
        {
            _stream.ReadTimeout = previousTimeout;
        }
    }

    private static bool IsReadTimeout(IOException error) =>
        error.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut };

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
