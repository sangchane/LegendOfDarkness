using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 경매장·룰렛(우리 확장 0x5E 7·8·9) — 서버 <c>AuctionHouse</c>·<c>GroupLoot</c> 가 쓰는 모양 그대로: 종류(1) · serial 0(4) · 본문,
/// 숫자는 빅엔디언, 글자는 길이(1) + EUC-KR (<c>autopilot/loot-auction/05-api-contract.md</c> E-01~03).
/// </summary>
public sealed class AuctionReadTests
{
    private static List<byte> Head(byte kind) => [kind, 0, 0, 0, 0];

    private static byte[] U16(int value) => [(byte)(value >> 8), (byte)value];

    private static byte[] U32(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    [Fact]
    public void Reads_a_browse_page()
    {
        List<byte> body = Head(Auction.PageKind);
        body.AddRange([0, .. U16(1), .. U16(4), .. U16(3), 1]);
        body.AddRange([.. U32(12), .. U16(0x8123), 7]);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("강화된 롱소드"));
        body.AddRange([.. U16(1), 2, .. U32(12_000), .. U32(30_000), 4]);

        AuctionPage page = Auction.ReadPage([.. body]);

        Assert.Equal((0, 1, 4, 3), (page.View, page.Page, page.Pages, page.ClaimCount));
        Assert.Equal(new AuctionRow(12, 0x8123, 7, "강화된 롱소드", 1, 2, 12_000, 30_000, 4), Assert.Single(page.Rows));
        Assert.Empty(page.Claims);
    }

    [Fact]
    public void Reads_a_claims_page()
    {
        List<byte> body = Head(Auction.PageKind);
        body.AddRange([2, .. U16(0), .. U16(1), .. U16(1), 1]);
        body.AddRange([.. U32(5), 1, .. U16(0), 0]);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("금화"));
        body.AddRange([.. U16(1), .. U32(9_546), 1]);

        AuctionPage page = Auction.ReadPage([.. body]);

        Assert.Equal(new AuctionClaim(5, 1, 0, 0, "금화", 1, 9_546, 1), Assert.Single(page.Claims));
        Assert.Empty(page.Rows);
    }

    [Fact]
    public void Reads_a_result_and_a_roll()
    {
        List<byte> done = Head(Auction.DoneKind);
        done.Add(0);
        done.AddRange(LegacyKoreanEncoding.EncodeStringA("금화가 모자랍니다"));
        done.AddRange(U16(2));
        Assert.Equal(new AuctionDone(false, "금화가 모자랍니다", 2), Auction.ReadDone([.. done]));

        List<byte> roll = Head(Auction.RollKind);
        roll.AddRange([.. U16(0x8001), 3]);
        roll.AddRange(LegacyKoreanEncoding.EncodeStringA("에페"));
        roll.Add(2);
        roll.AddRange(U32(11));
        roll.AddRange(LegacyKoreanEncoding.EncodeStringA("전사"));
        roll.Add(87);
        roll.AddRange(U32(22));
        roll.AddRange(LegacyKoreanEncoding.EncodeStringA("사제"));
        roll.Add(12);
        roll.AddRange(U32(11));

        LootRoll read = Auction.ReadRoll([.. roll]);
        Assert.Equal(("에페", 11u), (read.Item, read.Winner));
        Assert.Equal([(11u, "전사", (byte)87), (22u, "사제", (byte)12)], read.Rolls);
    }

    [Fact]
    public void A_cut_page_is_refused_not_half_read()
    {
        List<byte> body = Head(Auction.PageKind);
        body.AddRange([0, .. U16(0), .. U16(1), .. U16(0), 1, .. U32(12)]);

        Assert.Throws<ProtocolException>(() => Auction.ReadPage([.. body]));
    }
}
