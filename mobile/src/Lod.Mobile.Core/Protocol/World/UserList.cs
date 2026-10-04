using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>One person in the list of who is on — Hades <c>Class</c> number (0 농부 … 5 무도가), the name and the guild (empty when none).</summary>
public sealed record OnlineUser(int Path, string Name, string Guild = "");

/// <summary>
/// Who is on (0x36, Hades <c>ServerFormat36</c>), in the order the server sorted it (레벨, 그다음 체력 + 마력×2). Only the class
/// and name are kept — colour, status, title and stage are passed over. After the list our server adds each person's guild
/// name in the same order; an older server that leaves it off reads as no guilds.
/// </summary>
public static class UserList
{
    /// <summary>Reads the list; anything cut short is refused rather than half read.</summary>
    /// <exception cref="ProtocolException">The packet ends inside a person.</exception>
    public static IReadOnlyList<OnlineUser> Read(ReadOnlySpan<byte> body)
    {
        if (body.Length < 4)
        {
            throw new ProtocolException($"접속자 목록이 4바이트보다 짧습니다 ({body.Length}바이트).");
        }

        int count = (body[2] << 8) | body[3];
        int at = 4;
        List<OnlineUser> users = [];

        for (int i = 0; i < count; i++)
        {
            if (at + 6 > body.Length)
            {
                throw new ProtocolException($"접속자 목록이 {i + 1}번째 사람에서 끊겼습니다.");
            }

            // 서버는 직업에 늘 0x88 을 더해 보낸다 — 아래 세 비트만 직업이다.
            int path = body[at] & 0x07;
            at += 5;
            string name = LegacyKoreanEncoding.DecodeStringA(body[at..], out int consumed);
            at += consumed;
            users.Add(new OnlineUser(path, name));
        }

        // 꼬리: 사람 차례대로 길드명(우리 확장). 없으면 옛 서버 — 길드 없이 둔다.
        for (int i = 0; i < users.Count && at < body.Length; i++)
        {
            users[i] = users[i] with { Guild = LegacyKoreanEncoding.DecodeStringA(body[at..], out int consumed) };
            at += consumed;
        }

        return users;
    }
}
