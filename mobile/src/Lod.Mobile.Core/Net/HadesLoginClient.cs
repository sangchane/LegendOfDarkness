using System.Net;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Net;

/// <summary>
/// What a completed login leaves in your hands: the game-server connection, the character that was admitted,
/// and the cipher parameters every secured packet from here on needs.
/// </summary>
public sealed class WorldSession(
    HadesConnection connection,
    RedirectTarget character,
    EncryptionParameters parameters) : IDisposable
{
    private int _disposed;
    private int _disposeAttempts;

    public HadesConnection Connection { get; } = connection;

    public RedirectTarget Character { get; } = character;

    public EncryptionParameters Parameters { get; } = parameters;

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal int DisposeAttempts => Volatile.Read(ref _disposeAttempts);

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeAttempts);

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Connection.Dispose();
    }
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
    /// 로그인·만들기 전체(세 번의 접속과 그 사이 기다림 모두)의 제한시간. 접속만 받고 말이 없는 서버 앞에서도
    /// 화면은 버튼을 되살리고 봇은 다시 들어간다(리뷰 2026-10-08 #8). 넘으면 <see cref="TimeoutException" />.
    /// </summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for the redirect after sending credentials. A refused login is answered with a
    /// message box and then silence, so without a deadline a wrong password would simply hang.
    /// 전체 제한시간 안의 더 짧은 기다림 — 이것이 먼저 끝나면 「거절」, 전체가 먼저 끝나면 「응답 없음」이다.
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
    /// wire). Once the save succeeds, the submitted credentials are used only in memory to follow the
    /// ordinary login path and return its world connection. They are never retained by this client.
    /// </summary>
    public static Task<WorldSession> CreateCharacterAsync(
        IPAddress address,
        int loginPort,
        string username,
        string password,
        byte hairStyle,
        byte gender,
        byte hairColor,
        byte path,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        WithinDeadline(
            token => CreateCharacter(address, loginPort, username, password, hairStyle, gender, hairColor, path, progress, token),
            timeout,
            cancellationToken);

    public static Task<WorldSession> LoginAsync(
        IPAddress address,
        int loginPort,
        string username,
        string password,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        WithinDeadline(token => Login(address, loginPort, username, password, progress, token), timeout, cancellationToken);

    /// <summary>
    /// 카카오 캐릭터 지우기(<c>autopilot/game-kakao-accounts/SPEC.md</c>) — 만들기 첫 패킷 0x02 에 이름과 <c>표|delete</c>
    /// (<see cref="KakaoSignIn.Delete" />). 메시지 상자 0x00 이면 지워진 것, 아니면 그 글로 <see cref="ProtocolException" />.
    /// </summary>
    public static Task DeleteCharacterAsync(
        IPAddress address,
        int loginPort,
        string username,
        string token,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        WithinDeadline(
            walk => Manage(address, loginPort, username, KakaoSignIn.Delete(token), look: null, walk),
            timeout,
            cancellationToken);

    /// <summary>
    /// 카카오 캐릭터 생김새 바꾸기 — 0x02 에 이름과 <c>표|look</c>(<see cref="KakaoSignIn.Look" />), 0x00 이면 이어서 0x04.
    /// 서버는 머리 모양·색만 반영한다(성별·직업은 무시하니 지금 값 그대로 보낸다).
    /// </summary>
    public static Task ChangeLookAsync(
        IPAddress address,
        int loginPort,
        string username,
        string token,
        byte hairStyle,
        byte gender,
        byte hairColor,
        byte path,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        WithinDeadline(
            walk => Manage(address, loginPort, username, KakaoSignIn.Look(token), (hairStyle, gender, hairColor, path), walk),
            timeout,
            cancellationToken);

    /// <summary>
    /// 호출자 토큰에 제한시간(기본 <see cref="Deadline" />)을 묶어 한 번에 건다. 제한시간이 끊은 것은 <see cref="TimeoutException" />,
    /// 호출자가 그만둔 것은 그대로 <see cref="OperationCanceledException" />.
    /// </summary>
    private static async Task<T> WithinDeadline<T>(
        Func<CancellationToken, Task<T>> walk,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? Deadline);

        try
        {
            return await walk(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("서버가 응답하지 않습니다");
        }
    }

    private static async Task<WorldSession> CreateCharacter(
        IPAddress address,
        int loginPort,
        string username,
        string password,
        byte hairStyle,
        byte gender,
        byte hairColor,
        byte path,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("로그인 서버에 접속하는 중…");

        (HadesConnection login, EncryptionParameters parameters) = await OpenLobby(address, loginPort, cancellationToken);
        using HadesConnection _ = login;

        progress?.Report("계정을 만드는 중…");

        await login.SendAsync(
            Hades718LoginProtocol.CreateAccountRequest(username, password, parameters, ordinal: 0),
            cancellationToken);
        await login.ReceiveAsync(cancellationToken);

        progress?.Report("캐릭터를 만드는 중…");

        await login.SendAsync(
            Hades718LoginProtocol.CreateCharacterRequest(hairStyle, gender, hairColor, path, parameters, ordinal: 0),
            cancellationToken);

        // Reading the reply also waits for the save to finish before the connection closes. 거절(직업 없음·저장 실패)이면
        // 그 까닭으로 끝낸다 — 넘어가 로그인하면 「없는 계정」만 보였다.
        if (Refusal(await login.ReceiveAsync(cancellationToken), parameters) is { } refused)
        {
            throw new ProtocolException(refused);
        }

        progress?.Report("새 영웅으로 월드에 들어가는 중…");
        return await Login(address, loginPort, username, password, progress, cancellationToken);
    }

    /// <summary>지우기·생김새 — 0x02(이름·동사 칸)가 받아들여지면, 생김새는 0x04 까지. 둘 다 메시지 상자 0x00 이어야 한다.</summary>
    private static async Task<bool> Manage(
        IPAddress address,
        int loginPort,
        string username,
        string secret,
        (byte HairStyle, byte Gender, byte HairColor, byte Path)? look,
        CancellationToken cancellationToken)
    {
        (HadesConnection login, EncryptionParameters parameters) = await OpenLobby(address, loginPort, cancellationToken);
        using HadesConnection _ = login;

        await login.SendAsync(Hades718LoginProtocol.CreateAccountRequest(username, secret, parameters, ordinal: 0), cancellationToken);
        await Accepted(login, parameters, cancellationToken);

        if (look is { } chosen)
        {
            await login.SendAsync(
                Hades718LoginProtocol.CreateCharacterRequest(chosen.HairStyle, chosen.Gender, chosen.HairColor, chosen.Path, parameters, ordinal: 0),
                cancellationToken);
            await Accepted(login, parameters, cancellationToken);
        }

        return true;
    }

    /// <summary>다음 답이 메시지 상자 0x00 이 아니면 그 까닭으로 끝낸다.</summary>
    private static async Task Accepted(HadesConnection login, EncryptionParameters parameters, CancellationToken cancellationToken)
    {
        PacketFrame reply = await login.ReceiveAsync(cancellationToken);

        if (reply.Command != MessageBoxCommand)
        {
            throw new ProtocolException(Explain(null));
        }

        if (Refusal(reply, parameters) is { } refused)
        {
            throw new ProtocolException(refused);
        }
    }

    /// <summary>
    /// 로그인 서버와 암호를 맞추고 로비로 옮겨 인사까지 — 만들기·지우기·생김새가 그 연결에 0x02 를 보낸다. 연결은 호출자가 닫는다.
    /// </summary>
    private static async Task<(HadesConnection Login, EncryptionParameters Parameters)> OpenLobby(
        IPAddress address,
        int loginPort,
        CancellationToken cancellationToken)
    {
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

        HadesConnection login = await HadesConnection.ConnectAsync(address, lobby.Port, cancellationToken);

        try
        {
            await login.ReceiveAsync(cancellationToken);

            await login.SendAsync(Hades718LoginProtocol.CreateGameEntryRequest(lobby), cancellationToken);
            await login.ReceiveAsync(cancellationToken);
        }
        catch
        {
            login.Dispose();
            throw;
        }

        return (login, parameters);
    }

    private static async Task<WorldSession> Login(
        IPAddress address,
        int loginPort,
        string username,
        string password,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
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

        using (HadesConnection login = await HadesConnection.ConnectAsync(address, lobby.Port, cancellationToken))
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

        // 넘겨받는 주소는 IPv4 4바이트뿐이라 IPv6 로 붙은 폰은 따라갈 수 없다. 로비·게임은 로그인과 같은 기계에
        // 있으므로 처음 붙은 주소에 포트만 바꿔 따라간다(아이폰 테더링, 2026-09-24).
        HadesConnection world = await HadesConnection.ConnectAsync(address, game.Port, cancellationToken);

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
        // 이 기다림만 끝났으면 거절이다. 전체 제한시간·호출자가 끊은 것은 그대로 올려 보낸다(WithinDeadline 이 가른다).
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

    /// <summary>메시지 상자의 첫 바이트가 0 이 아니면 거절 — 그 글(없으면 일반 문구). 성공·다른 답·읽지 못한 상자는 null.</summary>
    private static string? Refusal(PacketFrame frame, EncryptionParameters parameters)
    {
        if (frame.Command != MessageBoxCommand)
        {
            return null;
        }

        try
        {
            byte[] body = HadesCipher.DecodeSecured(frame, parameters);
            return body.Length == 0 || body[0] == 0x00 ? null : Explain(body.Length < 2 ? null : LegacyKoreanEncoding.DecodeStringA(body.AsSpan(1), out _));
        }
        catch (ProtocolException)
        {
            return null;
        }
    }

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
