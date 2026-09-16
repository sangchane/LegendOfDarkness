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

    /// <summary>
    /// The same, for the two commands that answer an NPC — <c>0x39</c> and <c>0x3A</c>. They carry six bytes
    /// ahead of their fields and a second layer of enciphering over the first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>NetworkClient.Read</c> singles these two out: it undoes the ordinary enciphering, then runs
    /// <c>TransFormDialog</c>, then <b>starts reading at offset six</b>. A packet built the ordinary way is
    /// read six bytes late — every field lands on the wrong bytes, so the serial names nobody and the
    /// answer is dropped without a word. Nothing is logged and no error comes back; the NPC simply goes on
    /// asking the same question.
    /// </para>
    /// <para>
    /// The six leading bytes are a checksum the server never checks — it reads past them — but the first
    /// two are not free: the whole second layer keys off <c>Data[1] ^ (Data[0] - 0x2D)</c>, so whatever is
    /// put there has to be the same on both sides. Zeros are, and that is what is written.
    /// </para>
    /// </remarks>
    public static byte[] EncodeDialogSecured(
        byte command, byte ordinal, ReadOnlySpan<byte> fields, EncryptionParameters parameters)
    {
        byte[] body = new byte[DialogHeader + fields.Length];
        fields.CopyTo(body.AsSpan(DialogHeader));

        TransformDialog(body);

        return EncodeSecured(command, ordinal, body, parameters);
    }

    /// <summary>How many bytes sit ahead of a dialogue answer's fields.</summary>
    private const int DialogHeader = 6;

    /// <summary>
    /// The second layer over a dialogue answer, mirroring <c>NetworkClient.TransFormDialog</c>. It is its
    /// own inverse — every step is an exclusive-or — and the key it uses comes from the first two bytes,
    /// which it never touches, so running it here and again on the far side leaves the fields as written.
    /// </summary>
    private static void TransformDialog(byte[] body)
    {
        byte key = (byte)(body[1] ^ (byte)(body[0] - 0x2D));

        if (body.Length > 2) body[2] ^= (byte)(key + 0x73);
        if (body.Length > 3) body[3] ^= (byte)(key + 0x73);
        if (body.Length > 4) body[4] ^= (byte)(key + 0x28);
        if (body.Length > 5) body[5] ^= (byte)(key + 0x29);

        for (int i = body.Length - DialogHeader - 1; i >= 0; i--)
        {
            body[i + DialogHeader] ^= (byte)(((byte)(key + 0x28) + i + 2) % 0x100);
        }
    }
}
