namespace Lod.Mobile.Core.Protocol.World;

/// <summary>월드 연결 — 접속자 목록(0x18 → 0x36).</summary>
public sealed partial class WorldClient
{
    private IReadOnlyList<OnlineUser>? _users;

    /// <summary>Asks who is on; the answer comes back as 0x36 (<see cref="TakeUsers" />).</summary>
    public Task AskUsersAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.AskUsers, [], cancellationToken);

    /// <summary>Takes the list of who is on the server last sent, once.</summary>
    public IReadOnlyList<OnlineUser>? TakeUsers() => Interlocked.Exchange(ref _users, null);

    private void OnUserList(byte[] body)
    {
        try
        {
            _users = UserList.Read(body);
        }
        catch (ProtocolException cut)
        {
            NoteUnread($"0x36: {cut.Message}");
        }
    }
}
