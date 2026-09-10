namespace Lod.Mobile.Core.Protocol.Login;

/// <summary>
/// The cipher every secured packet passes through, in both directions. It is its own inverse: each byte is
/// mixed with the salt byte at its position, the packet's ordinal, and how many salt-lengths into the body
/// it sits, so applying it twice gives the original bytes back.
/// </summary>
/// <remarks>
/// Only the body is enciphered. The command and the ordinal travel in the clear, because the far side needs
/// the ordinal to undo the mixing.
/// </remarks>
public static class HadesCipher
{
    /// <summary>
    /// The only substitution table this client implements. The server's own provider defaults to it; the
    /// nine others exist for configurations we have never seen announced.
    /// </summary>
    /// <remarks>ponytail: seed 0 only — port the remaining tables if a server ever announces another.</remarks>
    public const byte SupportedSeed = 0;

    public static void Transform(Span<byte> body, EncryptionParameters parameters, byte ordinal)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Seed != SupportedSeed)
        {
            // Names the seeds and nothing else: credentials pass through this method.
            throw new ProtocolException(
                $"이 클라이언트는 seed {SupportedSeed} 만 다룹니다. 서버가 seed {parameters.Seed} 를 요구했습니다.");
        }

        ReadOnlySpan<byte> salt = parameters.Salt.Span;

        if (salt.Length == 0)
        {
            throw new ProtocolException("서버가 빈 salt 를 보냈습니다.");
        }

        for (int index = 0; index < body.Length; index++)
        {
            int block = (index / salt.Length) & 0xFF;

            // For seed 0 the substitution table is the identity, so the ordinal and block stand for themselves.
            body[index] ^= (byte)(salt[index % salt.Length] ^ ordinal ^ block);

            if (ordinal == block)
            {
                body[index] ^= ordinal;
            }
        }
    }

    /// <summary>
    /// Takes a secured frame apart. The first byte after the command is the ordinal, in the clear, because
    /// the reader needs it to undo the mixing; everything after it is the enciphered body.
    /// </summary>
    public static byte[] DecodeSecured(PacketFrame frame, EncryptionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(frame);

        ReadOnlySpan<byte> data = frame.Data.Span;

        if (data.Length == 0)
        {
            throw new ProtocolException($"보안 패킷 0x{frame.Command:X2} 에 서수가 없습니다.");
        }

        byte[] body = data[1..].ToArray();

        Transform(body, parameters, data[0]);

        return body;
    }

    /// <summary>Builds a secured frame: command and ordinal in the clear, body enciphered.</summary>
    public static byte[] EncodeSecured(byte command, byte ordinal, ReadOnlySpan<byte> body, EncryptionParameters parameters)
    {
        byte[] enciphered = body.ToArray();

        Transform(enciphered, parameters, ordinal);

        return PacketFrameCodec.Encode([command, ordinal, .. enciphered]);
    }
}
