using System.Buffers.Binary;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>경매 한 줄(찾기·내 경매). 값은 금화, 띠는 남은 시간(0 짧게 · 1 보통 · 2 길게 · 3 아주 길게).</summary>
/// <param name="Flags">1 내가 올림 · 2 내가 최고 입찰 · 4 입찰 있음.</param>
/// <param name="Stats">장비 수치(쪽 끝 꼬리, 소지품 0x0F 와 같은 모양). 꼬리가 없는 서버면 null.</param>
public sealed record AuctionRow(uint Id, ushort Image, byte Color, string Name, ushort Stacks, byte Band, uint Price, uint Buyout, byte Flags, ItemStats? Stats = null);

/// <summary>받을 것 한 줄. 갈래 0 물건 · 1 금화. 까닭 0 낙찰품 · 1 판매 대금 · 2 유찰 · 3 밀린 입찰금 · 4 취소 · 5 나눔 넘침.</summary>
public sealed record AuctionClaim(uint Id, byte Kind, ushort Image, byte Color, string Name, ushort Stacks, uint Gold, byte Reason);

/// <summary>경매 쪽(0x5E 8). 보기 0 찾기 · 1 내 경매는 <see cref="Rows" />, 2 받을 것은 <see cref="Claims" /> 가 찬다.</summary>
public sealed record AuctionPage(byte View, ushort Page, ushort Pages, ushort ClaimCount, IReadOnlyList<AuctionRow> Rows, IReadOnlyList<AuctionClaim> Claims);

/// <summary>경매 결과(0x5E 9) — 내 요청의 답이거나(<see cref="Notice" /> false), 내 물건이 팔리거나 밀렸다는 알림(true, 옛 서버는 늘 false).</summary>
public sealed record AuctionDone(bool Ok, string Message, ushort ClaimCount, bool Notice = false);

/// <summary>룰렛 한 번(0x5E 7). 수는 첫 굴림, 이긴 이는 같은 수를 다시 굴린 뒤의 최종.</summary>
public sealed record LootRoll(ushort Image, byte Color, string Item, IReadOnlyList<(uint Serial, string Name, byte Roll)> Rolls, uint Winner);

/// <summary>
/// 경매장·룰렛 — 우리 확장 0x5E 종류 7·8·9 와 0xF4 (<c>autopilot/loot-auction/05-api-contract.md</c>). 0x5E 는 종류(1) · serial(4, 늘 0) 뒤에 본문.
/// </summary>
public static class Auction
{
    public const byte RollKind = 7;
    public const byte PageKind = 8;
    public const byte DoneKind = 9;

    /// <summary>0xF4 종류 — 05 P-01~08.</summary>
    public const byte Browse = 0, Mine = 1, Claims = 2, Post = 3, Bid = 4, Buyout = 5, Cancel = 6, Take = 7;

    public static LootRoll ReadRoll(ReadOnlySpan<byte> body)
    {
        var cursor = new Cursor(body, 5);
        ushort image = cursor.U16();
        byte color = cursor.U8();
        string item = cursor.Text();
        int count = cursor.U8();
        List<(uint, string, byte)> rolls = [];
        for (int i = 0; i < count; i++)
        {
            rolls.Add((cursor.U32(), cursor.Text(), cursor.U8()));
        }

        return new LootRoll(image, color, item, rolls, cursor.U32());
    }

    public static AuctionPage ReadPage(ReadOnlySpan<byte> body)
    {
        var cursor = new Cursor(body, 5);
        byte view = cursor.U8();
        ushort page = cursor.U16();
        ushort pages = cursor.U16();
        ushort claims = cursor.U16();
        int count = cursor.U8();
        List<AuctionRow> rows = [];
        List<AuctionClaim> claimRows = [];
        for (int i = 0; i < count; i++)
        {
            if (view == 2)
            {
                claimRows.Add(new AuctionClaim(cursor.U32(), cursor.U8(), cursor.U16(), cursor.U8(), cursor.Text(), cursor.U16(), cursor.U32(), cursor.U8()));
            }
            else
            {
                rows.Add(new AuctionRow(cursor.U32(), cursor.U16(), cursor.U8(), cursor.Text(), cursor.U16(), cursor.U8(), cursor.U32(), cursor.U32(), cursor.U8()));
            }
        }

        // 꼬리 — 줄마다 장비 수치(2026-10-07). 모자라면 꼬리 없는 서버로 본다.
        if (view != 2 && cursor.Left >= rows.Count * WorldClient.ItemNumbersSize)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i] = rows[i] with { Stats = WorldClient.ReadItemStats(cursor.Take(WorldClient.ItemNumbersSize)) };
            }
        }

        return new AuctionPage(view, page, pages, claims, rows, claimRows);
    }

    public static AuctionDone ReadDone(ReadOnlySpan<byte> body)
    {
        var cursor = new Cursor(body, 5);
        var done = new AuctionDone(cursor.U8() == 1, cursor.Text(), cursor.U16());
        return cursor.Left > 0 ? done with { Notice = cursor.U8() == 1 } : done;
    }

    /// <summary>0xF4 본문(종류 바이트부터).</summary>
    public static byte[] EncodeBrowse(byte category, byte sort, ushort page, string query) =>
        [Browse, category, sort, (byte)(page >> 8), (byte)page, .. LegacyKoreanEncoding.EncodeStringA(query ?? string.Empty)];

    public static byte[] EncodePage(byte kind, ushort page) => [kind, (byte)(page >> 8), (byte)page];

    public static byte[] EncodePost(byte slot, uint start, uint buyout, byte hours) =>
        [Post, slot, .. U32(start), .. U32(buyout), hours];

    public static byte[] EncodeId(byte kind, uint id) => [kind, .. U32(id)];

    public static byte[] EncodeBid(uint id, uint amount) => [Bid, .. U32(id), .. U32(amount)];

    /// <summary>
    /// 다음 최소 입찰가 — 서버 <c>AuctionHouse.NextBid</c> 와 같은 식: 입찰이 있으면 현재가 + max(1, 현재가의 5%), 없으면 시작가(= 현재가).
    /// ×5 가 uint 를 넘지 않게 ulong 으로 셈하고 uint 끝에서 멈춘다.
    /// </summary>
    public static uint NextBid(uint price, bool hasBid) =>
        hasBid ? (uint)Math.Min(uint.MaxValue, price + Math.Max(1UL, (ulong)price * 5 / 100)) : price;

    private static byte[] U32(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private ref struct Cursor(ReadOnlySpan<byte> body, int at)
    {
        private readonly ReadOnlySpan<byte> _body = body;
        private int _at = at;

        private readonly void Need(int count)
        {
            if (_at + count > _body.Length)
            {
                throw new ProtocolException($"경매 안내(0x5E 종류 {(_body.Length > 0 ? _body[0] : 0)})가 끊겼습니다 ({_body.Length}바이트).");
            }
        }

        public byte U8()
        {
            Need(1);
            return _body[_at++];
        }

        public ushort U16()
        {
            Need(2);
            ushort value = BinaryPrimitives.ReadUInt16BigEndian(_body[_at..]);
            _at += 2;
            return value;
        }

        public uint U32()
        {
            Need(4);
            uint value = BinaryPrimitives.ReadUInt32BigEndian(_body[_at..]);
            _at += 4;
            return value;
        }

        public readonly int Left => _body.Length - _at;

        public ReadOnlySpan<byte> Take(int count)
        {
            Need(count);
            ReadOnlySpan<byte> part = _body.Slice(_at, count);
            _at += count;
            return part;
        }

        public string Text()
        {
            Need(1);
            Need(1 + _body[_at]);
            string text = LegacyKoreanEncoding.DecodeStringA(_body[_at..], out int used);
            _at += used;
            return text;
        }
    }
}
