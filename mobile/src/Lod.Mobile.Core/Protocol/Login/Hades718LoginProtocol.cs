using System.Buffers.Binary;
using System.Net;

namespace Lod.Mobile.Core.Protocol.Login;

/// <summary>
/// The parameters the server hands out before a login: which cipher table to use, the nine bytes it is
/// keyed with, and a hash of the server list the client is expected to hold.
/// </summary>
public sealed record EncryptionParameters(byte Seed, ReadOnlyMemory<byte> Salt, uint ServerTableHash);

/// <summary>
/// Where the login server sends the client next, and the ticket it must present on arrival. The seed, salt,
/// name and serial are handed straight back to the game server, which is how it knows the connection was
/// admitted.
/// </summary>
public sealed record RedirectTarget(
    IPAddress Address,
    int Port,
    byte Seed,
    ReadOnlyMemory<byte> Salt,
    string CharacterName,
    uint Serial);

/// <summary>
/// The four messages that carry a client from opening a socket to standing in the world: announce the
/// version, read the cipher parameters, send credentials, follow the redirect into the game server.
/// </summary>
public static class Hades718LoginProtocol
{
    /// <summary>The build this project targets. The server refuses anything else.</summary>
    public const ushort ClientVersion = 718;

    private const byte VersionCommand = 0x00;
    private const byte LoginCommand = 0x03;
    private const byte RedirectCommand = 0x03;
    private const byte GameEntryCommand = 0x10;

    // Two bytes the original client sends after its version. The server reads them and does not use them.
    private static readonly byte[] VersionTrailer = [0x4C, 0x4B];

    public static byte[] CreateVersionRequest()
    {
        byte[] payload =
        [
            VersionCommand,
            (byte)(ClientVersion >> 8),
            (byte)(ClientVersion & 0xFF),
            .. VersionTrailer
        ];

        return PacketFrameCodec.Encode(payload);
    }

    /// <summary>Reads the server's opening message: a type byte, the server-list hash, then seed and salt.</summary>
    public static EncryptionParameters ParseServerParameters(PacketFrame frame)
    {
        if (frame.Command != VersionCommand)
        {
            throw new ProtocolException($"서버 매개변수는 명령 0x{VersionCommand:X2} 인데 0x{frame.Command:X2} 가 왔습니다.");
        }

        ReadOnlySpan<byte> data = frame.Data.Span;

        const int fixedLength = 7;

        if (data.Length < fixedLength)
        {
            throw new ProtocolException($"서버 매개변수가 {fixedLength}바이트보다 짧습니다 ({data.Length}바이트).");
        }

        uint serverTableHash = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(1, 4));
        byte seed = data[5];
        int saltLength = data[6];

        if (data.Length < fixedLength + saltLength)
        {
            throw new ProtocolException($"salt 가 {saltLength}바이트를 예고했는데 {data.Length - fixedLength}바이트만 있습니다.");
        }

        return new EncryptionParameters(seed, data.Slice(fixedLength, saltLength).ToArray(), serverTableHash);
    }

    /// <summary>Builds the credentials message. The body is enciphered; the command and ordinal are not.</summary>
    public static byte[] CreateLoginRequest(
        string username,
        string password,
        EncryptionParameters parameters,
        byte ordinal)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        byte[] body =
        [
            .. LegacyKoreanEncoding.EncodeStringA(username),
            .. LegacyKoreanEncoding.EncodeStringA(password)
        ];

        return HadesCipher.EncodeSecured(LoginCommand, ordinal, body, parameters);
    }

    public static RedirectTarget ParseRedirect(PacketFrame frame)
    {
        if (frame.Command != RedirectCommand)
        {
            throw new ProtocolException($"재접속 안내는 명령 0x{RedirectCommand:X2} 인데 0x{frame.Command:X2} 가 왔습니다.");
        }

        ReadOnlySpan<byte> data = frame.Data.Span;

        const int endpointLength = 7;

        if (data.Length < endpointLength)
        {
            throw new ProtocolException($"재접속 안내에 주소가 없습니다 ({data.Length}바이트).");
        }

        // The four address bytes arrive back to front.
        IPAddress address = new(stackalloc byte[] { data[3], data[2], data[1], data[0] });
        int port = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(4, 2));
        int ticketLength = data[6];

        ReadOnlySpan<byte> ticket = data[endpointLength..];

        if (ticket.Length < ticketLength)
        {
            throw new ProtocolException($"입장권이 {ticketLength}바이트를 예고했는데 {ticket.Length}바이트만 있습니다.");
        }

        ticket = ticket[..ticketLength];

        if (ticket.Length < 2)
        {
            throw new ProtocolException("입장권에 seed 와 salt 길이가 없습니다.");
        }

        byte seed = ticket[0];
        int saltLength = ticket[1];

        if (ticket.Length < 2 + saltLength)
        {
            throw new ProtocolException($"입장권의 salt 가 {saltLength}바이트를 예고했는데 {ticket.Length - 2}바이트만 있습니다.");
        }

        ReadOnlySpan<byte> salt = ticket.Slice(2, saltLength);
        ReadOnlySpan<byte> rest = ticket[(2 + saltLength)..];

        string characterName = LegacyKoreanEncoding.DecodeStringA(rest, out int nameLength);
        rest = rest[nameLength..];

        if (rest.Length < 4)
        {
            throw new ProtocolException($"입장권에 일련번호가 없습니다 ({rest.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(rest);

        return new RedirectTarget(address, port, seed, salt.ToArray(), characterName, serial);
    }

    /// <summary>Presents the ticket to the game server, in the shape the login server handed it over.</summary>
    public static byte[] CreateGameEntryRequest(RedirectTarget redirect)
    {
        ArgumentNullException.ThrowIfNull(redirect);

        byte[] serial = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(serial, redirect.Serial);

        byte[] payload =
        [
            GameEntryCommand,
            redirect.Seed,
            (byte)redirect.Salt.Length,
            .. redirect.Salt.ToArray(),
            .. LegacyKoreanEncoding.EncodeStringA(redirect.CharacterName),
            .. serial
        ];

        return PacketFrameCodec.Encode(payload);
    }

}
