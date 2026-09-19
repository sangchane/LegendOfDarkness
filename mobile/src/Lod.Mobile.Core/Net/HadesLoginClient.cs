using System.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.Net;

/// <summary>
/// What a completed login leaves in your hands: the game-server connection, the character that was admitted,
/// and the cipher parameters every secured packet from here on needs.
/// </summary>
public sealed record WorldSession(
    HadesConnection Connection,
    RedirectTarget Character,
    EncryptionParameters Parameters) : IDisposable
{
    public void Dispose() => Connection.Dispose();
}

/// <summary>
/// Walks the whole login: greet the login server, agree a cipher, follow it to the lobby, hand over
/// credentials, then follow it again into the world. Three sockets in turn, because the server answers each
/// step by pointing somewhere else.
/// </summary>
public static class HadesLoginClient
{
    private const byte EncryptionReceivedCommand = 0x57;
    private const byte MessageBoxCommand = 0x02;
    private const byte RedirectCommand = 0x03;

    /// <summary>
    /// How long to wait for the redirect after sending credentials. A refused login is answered with a
    /// message box and then silence, so without a deadline a wrong password would simply hang.
    /// </summary>
    private static readonly TimeSpan RedirectWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How much longer to wait once the server has said something. On a successful login the redirect
    /// follows its message box immediately, so a refusal need not sit out the whole wait above.
    /// </summary>
    private static readonly TimeSpan GraceAfterMessage = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Creates an account and, on the same connection, the one character the server lets it hold
    /// (<c>Format04Handler</c> only accepts a character right after a <c>Format02Handler</c> account on that
    /// same client — it keeps the pending username and password in <c>client.CreateInfo</c>, not on the
    /// wire). This does not log in; call <see cref="LoginAsync"/> afterward for that.
    /// </summary>
    public static async Task CreateCharacterAsync(
        IPAddress address,
        int loginPort,
        string username,
        string password,
        byte hairStyle,
        byte gender,
        byte hairColor,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("로그인 서버에 접속하는 중…");

        EncryptionParameters parameters;
        RedirectTarget lobby;

        using (HadesConnection greeting = await HadesConnection.ConnectAsync(address, loginPort, cancellationToken))
        {
            await greeting.ReceiveAsync(cancellationToken);

            await greeting.SendAsync(Hades718LoginProtocol.CreateVersionRequest(), cancellationToken);
            parameters = Hades718LoginProtocol.ParseServerParameters(await greeting.ReceiveAsync(cancellationToken));

            // Acknowledging the cipher is what makes the server hand out the lobby address.
            await greeting.SendAsync(
                HadesCipher.EncodeSecured(EncryptionReceivedCommand, ordinal: 0, [0x00], parameters),
                cancellationToken);

            lobby = Hades718LoginProtocol.ParseRedirect(await greeting.ReceiveAsync(cancellationToken));
        }

        progress?.Report("계정을 만드는 중…");

        using HadesConnection login = await HadesConnection.ConnectAsync(lobby.Address, lobby.Port, cancellationToken);

        await login.ReceiveAsync(cancellationToken);

        await login.SendAsync(Hades718LoginProtocol.CreateGameEntryRequest(lobby), cancellationToken);
        await login.ReceiveAsync(cancellationToken);

        await login.SendAsync(
            Hades718LoginProtocol.CreateAccountRequest(username, password, parameters, ordinal: 0),
            cancellationToken);
        await login.ReceiveAsync(cancellationToken);

        progress?.Report("캐릭터를 만드는 중…");

        await login.SendAsync(
            Hades718LoginProtocol.CreateCharacterRequest(hairStyle, gender, hairColor, parameters, ordinal: 0),
            cancellationToken);

        // Reading the reply also waits for the save to finish before the connection closes.
        await login.ReceiveAsync(cancellationToken);
    }

    public static async Task<WorldSession> LoginAsync(
        IPAddress address,
        int loginPort,
        string username,
        string password,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("로그인 서버에 접속하는 중…");

        EncryptionParameters parameters;
        RedirectTarget lobby;

        using (HadesConnection greeting = await HadesConnection.ConnectAsync(address, loginPort, cancellationToken))
        {
            await greeting.ReceiveAsync(cancellationToken);

            await greeting.SendAsync(Hades718LoginProtocol.CreateVersionRequest(), cancellationToken);
            parameters = Hades718LoginProtocol.ParseServerParameters(await greeting.ReceiveAsync(cancellationToken));

            // Acknowledging the cipher is what makes the server hand out the lobby address.
            await greeting.SendAsync(
                HadesCipher.EncodeSecured(EncryptionReceivedCommand, ordinal: 0, [0x00], parameters),
                cancellationToken);

            lobby = Hades718LoginProtocol.ParseRedirect(await greeting.ReceiveAsync(cancellationToken));
        }

        progress?.Report("계정을 확인하는 중…");

        RedirectTarget game;

        using (HadesConnection login = await HadesConnection.ConnectAsync(lobby.Address, lobby.Port, cancellationToken))
        {
            await login.ReceiveAsync(cancellationToken);

            await login.SendAsync(Hades718LoginProtocol.CreateGameEntryRequest(lobby), cancellationToken);
            await login.ReceiveAsync(cancellationToken);

            await login.SendAsync(
                Hades718LoginProtocol.CreateLoginRequest(username, password, parameters, ordinal: 0),
                cancellationToken);

            game = await AwaitRedirect(login, parameters, cancellationToken);
        }

        progress?.Report("월드에 들어가는 중…");

        HadesConnection world = await HadesConnection.ConnectAsync(game.Address, game.Port, cancellationToken);

        try
        {
            await world.SendAsync(Hades718LoginProtocol.CreateGameEntryRequest(game), cancellationToken);
        }
        catch
        {
            world.Dispose();
            throw;
        }

        return new WorldSession(world, game, parameters);
    }

    /// <summary>
    /// Reads until the server says where the world is. A successful login sends a message box first, so a
    /// message box on its own is not yet a refusal — only running out of time is, and then its text is why.
    /// </summary>
    private static async Task<RedirectTarget> AwaitRedirect(
        HadesConnection connection,
        EncryptionParameters parameters,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(RedirectWait);

        string? refusal = null;

        try
        {
            while (true)
            {
                PacketFrame frame = await connection.ReceiveAsync(deadline.Token);

                if (frame.Command == RedirectCommand)
                {
                    return Hades718LoginProtocol.ParseRedirect(frame);
                }

                if (frame.Command == MessageBoxCommand)
                {
                    refusal = ReadMessageBox(frame, parameters);
                    deadline.CancelAfter(GraceAfterMessage);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProtocolException(Explain(refusal));
        }
        catch (ProtocolException) when (refusal is not null)
        {
            // The server left a note and hung up; the note is the reason.
            throw new ProtocolException(Explain(refusal));
        }
    }

    private static string Explain(string? refusal) =>
        string.IsNullOrWhiteSpace(refusal) ? "서버가 로그인을 받아주지 않았습니다." : refusal;

    /// <summary>
    /// A message box is enciphered, unlike the redirect beside it. Inside is a code byte and then the text.
    /// </summary>
    private static string ReadMessageBox(PacketFrame frame, EncryptionParameters parameters)
    {
        try
        {
            byte[] body = HadesCipher.DecodeSecured(frame, parameters);

            return body.Length < 2 ? string.Empty : LegacyKoreanEncoding.DecodeStringA(body.AsSpan(1), out _);
        }
        catch (ProtocolException)
        {
            // The note explaining a refusal is not worth failing the refusal over.
            return string.Empty;
        }
    }
}
