using System.Buffers.Binary;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>동료 사이의 한쪽 — 봇에게는 주인, 사람에게는 동료 봇.</summary>
/// <param name="Magic">봇에게 — 주인이 체크해 둔 저주·나르콜리(0x5E 종류 1 이름 뒤 한 바이트, 없으면 모두).</param>
public sealed record CompanionTie(uint Serial, string Name, CompanionSpells.Magic Magic = CompanionSpells.Magic.All);

/// <summary>
/// 걸린 것 하나(0x5E 종류 3): 서버 이름(sleep·frozen·horrama·enare …) · 남은 초 · 해로움 · 그림 번호(스펠 시트, 모르면 0 —
/// 목록 뒤에 덧붙어 온다, 2026-09-26).
/// </summary>
public sealed record CompanionStatus(string Name, int Seconds, bool Harmful, int Icon = 0);

/// <summary>봇의 체력·마력 %(0x5E 종류 4) — 봇 칸의 막대.</summary>
public sealed record CompanionLife(uint Serial, int HealthPercent, int ManaPercent);

/// <summary>그룹원 한 사람(0x5E 종류 6) — 파티원 칸의 막대와 상태 그림. 원작은 그룹원 체력을 보내지 않는다.</summary>
public sealed record PartyMemberStatus(uint Serial, int HealthPercent, int ManaPercent, IReadOnlyList<int> Icons, string Name = "");

/// <summary>봇 가방의 겹치는 물건 하나 — 이름 · 그림 · 개수.</summary>
public sealed record CarriedItem(string Name, int Icon, int Stacks);

/// <summary>봇이 입은 것과 봇 가방의 포션(0x5E 종류 5) — 봇 장비창.</summary>
public sealed record CompanionKit(IReadOnlyList<WornItem> Worn, IReadOnlyList<CarriedItem> Carried);

/// <summary>
/// 동료 봇(성직자)을 부르고 보내는 선. <b>우리 확장이다.</b>
/// </summary>
/// <remarks>
/// <para>나가는 0xF1: 몸 한 바이트, 1 부르기 · 0 보내기 (서버 <c>ClientFormatF1</c> — 원작 클라이언트는 0x80 넘는 명령을
/// 보내지 않아 0xF0 월드맵 열기 다음 번호를 썼다).</para>
/// <para>오는 0x5E: 종류(1) · serial(4) · 이름(길이 한 바이트 + 글자). 종류 1 은 봇에게 "주인은 이 사람", 2 는 부른 사람에게
/// "동료는 이 봇". serial 0 이면 끝났다 (서버 <c>ServerFormat5E</c>).</para>
/// </remarks>
public static class Companion
{
    public const byte MasterKind = 1;
    public const byte CompanionKind = 2;
    public const byte StatusesKind = 3;
    public const byte VitalsKind = 4;
    public const byte KitKind = 5;
    public const byte MemberKind = 6;

    public static byte[] Call() => [1];

    public static byte[] Dismiss() => [0];

    /// <summary>내 가방 한 칸을 봇에게(0xF1 2): 장비면 입히고, 겹치는 물건이면 <paramref name="count" /> 개(0 은 다).</summary>
    public static byte[] Give(int slot, int count) => [2, (byte)slot, (byte)(count >> 8), (byte)count];

    /// <summary>봇의 장비 한 자리를 내 가방으로(0xF1 3).</summary>
    public static byte[] TakeOff(int place) => [3, (byte)place];

    /// <summary>내 코마디움으로 혼수인 봇을 깨운다(0xF1 4) — 봇 바로 옆에서.</summary>
    public static byte[] Wake() => [4];

    /// <summary>봇이 혼수인 주인을 깨운다(0xF1 5) — 봇 계정만, 주인 바로 옆에서. 서버가 가려 듣는다.</summary>
    public static byte[] WakeMaster() => [5];

    /// <summary>봇 탭 「마법사」 체크 비트(0xF1 6 — 1 렌토 · 2 나르콜리 · 4 바르도 · 8 데프레코). 서버가 주인 알림(0x5E 1) 꼬리로 봇에게 옮긴다.</summary>
    public static byte[] Magic(CompanionSpells.Magic magic) => [6, (byte)magic];

    /// <summary>0x5E 종류 3 — 한 사람(주인 또는 봇 자신)에게 걸린 것: 이름 · 남은 초 · 해로움.</summary>
    public static (uint Serial, IReadOnlyList<CompanionStatus> Statuses) ReadStatuses(ReadOnlySpan<byte> body)
    {
        Require(body, 6);
        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body[1..]);
        int count = body[5];
        int at = 6;
        List<CompanionStatus> listed = [];

        for (int i = 0; i < count; i++)
        {
            string name = LegacyKoreanEncoding.DecodeStringA(body[at..], out int used);
            at += used;
            Require(body, at + 3);
            listed.Add(new CompanionStatus(name, BinaryPrimitives.ReadUInt16BigEndian(body[at..]), body[at + 2] != 0));
            at += 3;
        }

        // 새 서버는 목록 뒤에 그림 번호(2)를 차례로 덧붙인다. 옛 서버에는 없다 — 그때는 0 그대로.
        if (body.Length >= at + (count * 2))
        {
            for (int i = 0; i < count; i++)
            {
                listed[i] = listed[i] with { Icon = BinaryPrimitives.ReadUInt16BigEndian(body[(at + (i * 2))..]) };
            }
        }

        return (serial, listed);
    }

    /// <summary>0x5E 종류 6 — 그룹원 한 사람: serial(4) · 체력 %(1) · 마력 %(1) · 개수(1) · 그림(2)×개수 · 이름(StringA). serial 0 은 "그룹 끝".</summary>
    public static PartyMemberStatus ReadMember(ReadOnlySpan<byte> body)
    {
        Require(body, 8);
        int count = body[7];
        Require(body, 8 + (count * 2));
        List<int> icons = [];

        for (int i = 0; i < count; i++)
        {
            icons.Add(BinaryPrimitives.ReadUInt16BigEndian(body[(8 + (i * 2))..]));
        }

        int at = 8 + (count * 2);
        string name = body.Length > at ? LegacyKoreanEncoding.DecodeStringA(body[at..], out _) : string.Empty;

        return new PartyMemberStatus(BinaryPrimitives.ReadUInt32BigEndian(body[1..]), body[5], body[6], icons, name);
    }

    /// <summary>0x5E 종류 4 — 봇의 체력·마력 %.</summary>
    public static CompanionLife ReadLife(ReadOnlySpan<byte> body)
    {
        Require(body, 7);
        return new CompanionLife(BinaryPrimitives.ReadUInt32BigEndian(body[1..]), body[5], body[6]);
    }

    /// <summary>0x5E 종류 5 — 봇이 입은 것(0x37 몸 그대로)과 봇 가방의 겹치는 물건(포션).</summary>
    public static CompanionKit ReadKit(ReadOnlySpan<byte> body)
    {
        Require(body, 6);
        int at = 6;
        List<WornItem> worn = [];

        for (int i = 0; i < body[5]; i++)
        {
            int start = at;
            at += 4;
            LegacyKoreanEncoding.DecodeStringA(body[at..], out int name);
            at += name;
            LegacyKoreanEncoding.DecodeStringA(body[at..], out int called);
            at += called + 8;
            Require(body, at);
            worn.Add(WorldClient.ReadWorn(body[start..at]));
        }

        Require(body, at + 1);
        int carriedCount = body[at++];
        List<CarriedItem> carried = [];

        for (int i = 0; i < carriedCount; i++)
        {
            string name = LegacyKoreanEncoding.DecodeStringA(body[at..], out int used);
            at += used;
            Require(body, at + 4);
            carried.Add(new CarriedItem(name, BinaryPrimitives.ReadUInt16BigEndian(body[at..]), BinaryPrimitives.ReadUInt16BigEndian(body[(at + 2)..])));
            at += 4;
        }

        return new CompanionKit(worn, carried);
    }

    private static void Require(ReadOnlySpan<byte> body, int length)
    {
        if (body.Length < length)
        {
            throw new ProtocolException($"봇 안내(0x5E 종류 {(body.Length > 0 ? body[0] : 0)})가 {length}바이트보다 짧습니다 ({body.Length}바이트).");
        }
    }

    /// <summary>0x5E 를 읽는다. serial 0 이면 사이가 끝났다는 뜻이라 null.</summary>
    public static (byte Kind, CompanionTie? Tie) ReadTie(ReadOnlySpan<byte> body)
    {
        if (body.Length < 5)
        {
            throw new ProtocolException($"동료 안내가 5바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body[1..]);
        int used = 0;
        string name = body.Length > 5 ? LegacyKoreanEncoding.DecodeStringA(body[5..], out used) : string.Empty;

        // 이름 뒤 한 바이트(2026-10-03) — 옛 서버는 보내지 않는다, 그때는 모두 켬.
        var magic = body.Length > 5 + used ? (CompanionSpells.Magic)body[5 + used] : CompanionSpells.Magic.All;

        return (body[0], serial == 0 ? null : new CompanionTie(serial, name, magic));
    }
}
