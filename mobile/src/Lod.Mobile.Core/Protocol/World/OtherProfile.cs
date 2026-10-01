using System.Buffers.Binary;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>
/// Somebody else's equipment window, as the server sends it when we press on them (0x34, Hades <c>ServerFormat34</c>):
/// who they are, what they wear, and whether they take group requests. Only what the gear window shows is kept — the
/// legend marks and the portrait after it are passed over.
/// </summary>
/// <param name="GroupOpen">Whether they take group requests (Hades <c>GroupStatus.AcceptingRequests</c>).</param>
public sealed record OtherProfile(
    uint Serial,
    string Name,
    string Path,
    string Clan,
    string ClanTitle,
    bool GroupOpen,
    IReadOnlyList<WornItem> Worn)
{
    /// <summary>
    /// The places in the order the server writes them — seventeen, the third trinket left out
    /// (<c>ServerFormat34.BuildEquipment</c>: …, 다리, 장신구, 신발, 겉옷, 겉투구, 장신구2).
    /// </summary>
    private static readonly int[] Order = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 13, 15, 16, 17];

    /// <summary>Reads the window; anything cut short is refused rather than half read.</summary>
    /// <exception cref="ProtocolException">The packet ends before the name lines.</exception>
    public static OtherProfile Read(ReadOnlySpan<byte> body)
    {
        int need = 4 + (Order.Length * 3) + 1;

        if (body.Length < need)
        {
            throw new ProtocolException($"상대 장비창이 {need}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body);
        int at = 4;
        List<WornItem> worn = [];

        foreach (int slot in Order)
        {
            int icon = BinaryPrimitives.ReadUInt16BigEndian(body[at..]);

            // 이름은 오지 않는다 — 부위 이름을 대신 단다. 빈 자리는 그림 0 이다.
            if (icon != 0)
            {
                worn.Add(new WornItem(slot, icon, WornPlace.Of(slot), WornPlace.Of(slot), 0, 0));
            }

            at += 3;
        }

        // 다음은 상태 한 바이트, 이름, 나라 한 바이트, "Lev N", 그룹 받기 한 바이트, 길드 직위, 직업, 길드.
        at += 1;
        string name = Next(body, ref at);
        at += 1;
        Next(body, ref at);
        bool open = At(body, at++) == 1;
        string clanTitle = Next(body, ref at);
        string path = Next(body, ref at);
        string clan = Next(body, ref at);

        return new OtherProfile(serial, name, path, clan, clanTitle, open, worn);
    }

    private static string Next(ReadOnlySpan<byte> body, ref int at)
    {
        string text = LegacyKoreanEncoding.DecodeStringA(body[System.Math.Min(at, body.Length)..], out int consumed);
        at += consumed;

        return text;
    }

    private static byte At(ReadOnlySpan<byte> body, int at) =>
        at < body.Length ? body[at] : throw new ProtocolException($"상대 장비창이 {at + 1}바이트보다 짧습니다.");
}
