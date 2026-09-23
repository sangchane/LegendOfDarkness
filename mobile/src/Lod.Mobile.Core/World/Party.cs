using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.World;

/// <summary>One person in the group, as the server's profile lists them.</summary>
public sealed record PartyMember(string Name, bool Leader);

/// <summary>Who is in our group. Alone means nobody else — the server lists nobody then.</summary>
public sealed record PartyRoster(IReadOnlyList<PartyMember> Members)
{
    public static PartyRoster Alone { get; } = new([]);

    /// <summary>A group of one is not a group — the server breaks it up (Party.RemovePartyMember).</summary>
    public bool Grouped => Members.Count > 1;
}

/// <summary>
/// The group (그룹 — the original's word; the screen says 파티) on the wire: asking somebody, taking an ask, leaving,
/// talking to the group, and reading back who is in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asking.</b> 0x2E with a kind byte and a name (Arbiter <c>ClientGroupInviteMessage</c>): 2 asks that person,
/// 3 takes the ask of the person named. The server asks the other side with 0x63 kind 1 and the asker's name
/// (<c>ServerGroupMessage</c>, Ask). The 5.99 Korean client has the same window (<c>Legend.exe</c>:
/// <c>GroupAskList</c>, <c>GroupAlertPane</c>, <c>packet\SGroup.cpp</c>). Naming yourself leaves the group.
/// </para>
/// <para>
/// <b>Who is in it.</b> Nothing pushes the list; it is part of our own profile (0x39), asked for with 0x2D.
/// Hades writes it as one text: <c>"그룹구성원\n* 이름\n  이름\n총 N명"</c>, the leader starred, or
/// <c>"Adventuring Alone"</c> (ServerFormat39.cs).
/// </para>
/// <para>
/// <b>Talking.</b> A whisper (0x19) to the name <c>"!"</c>; everyone in the group hears it as 0x0A kind 11.
/// </para>
/// </remarks>
public static class Party
{
    /// <summary>The whisper name that means "the whole group".</summary>
    public const string ChatName = "!";

    private const byte AskKind = 0x02;
    private const byte AcceptKind = 0x03;

    /// <summary>0x63 kind 1: somebody is asking us.</summary>
    private const byte AskedKind = 0x01;

    /// <summary>Body of 0x2E asking <paramref name="name" /> to join.</summary>
    public static byte[] Ask(string name) => [AskKind, .. LegacyKoreanEncoding.EncodeStringA(name)];

    /// <summary>Body of 0x2E taking the ask of <paramref name="name" />.</summary>
    public static byte[] Accept(string name) => [AcceptKind, .. LegacyKoreanEncoding.EncodeStringA(name)];

    /// <summary>Body of 0x19 saying <paramref name="text" /> to the group.</summary>
    public static byte[] Chat(string text) =>
        [.. LegacyKoreanEncoding.EncodeStringA(ChatName), .. LegacyKoreanEncoding.EncodeStringA(text)];

    /// <summary>Who is asking us (0x63), or null when it is a kind this screen does not answer.</summary>
    public static string? ReadAsk(ReadOnlySpan<byte> body)
    {
        if (body.Length < 2 || body[0] != AskedKind)
        {
            return null;
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[1..], out _);

        return name.Length > 0 ? name : null;
    }

    /// <summary>
    /// The group out of our profile (0x39): a nation byte, the clan, eight fixed bytes, then the group text.
    /// </summary>
    public static PartyRoster ReadRoster(ReadOnlySpan<byte> body)
    {
        const int fixedAfterClan = 8;

        if (body.Length < 2)
        {
            throw new ProtocolException($"프로필이 2바이트보다 짧습니다 ({body.Length}바이트).");
        }

        LegacyKoreanEncoding.DecodeStringA(body[1..], out int clan);
        int at = 1 + clan + fixedAfterClan;

        if (at >= body.Length)
        {
            throw new ProtocolException($"프로필이 그룹 칸 앞에서 끊겼습니다 ({body.Length}바이트).");
        }

        return new PartyRoster(Members(LegacyKoreanEncoding.DecodeStringA(body[at..], out _)));
    }

    /// <summary>The names in Hades' group text; nobody when it is not that text.</summary>
    public static IReadOnlyList<PartyMember> Members(string text)
    {
        string[] lines = text.Replace("\r", string.Empty).Split('\n');

        if (lines.Length < 2 || !lines[0].StartsWith("그룹", StringComparison.Ordinal))
        {
            return [];
        }

        List<PartyMember> members = [];

        foreach (string line in lines.Skip(1))
        {
            if (line.Length < 2 || line.StartsWith("총 ", StringComparison.Ordinal))
            {
                continue;
            }

            string name = line[1..].Trim();

            if (name.Length > 0)
            {
                members.Add(new PartyMember(name, line[0] == '*'));
            }
        }

        return members;
    }
}
