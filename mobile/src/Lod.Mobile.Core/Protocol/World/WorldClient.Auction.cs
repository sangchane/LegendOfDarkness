namespace Lod.Mobile.Core.Protocol.World;

/// <summary>월드 연결 — 경매장(0xF4 → 0x5E 8·9)과 그룹 룰렛(0x5E 7). 앱·봇이 같이 쓴다.</summary>
public sealed partial class WorldClient
{
    private AuctionPage? _auctionPage;
    private AuctionDone? _auctionDone;
    private AuctionDone? _auctionNotice;
    private int _auctionNotices;
    private LootRoll? _lastRoll;
    private int _auctionPages;
    private int _auctionDones;
    private int _rolls;

    /// <summary>마지막으로 온 경매 쪽(찾기·내 경매·받을 것). 없으면 null.</summary>
    public AuctionPage? AuctionPage => Volatile.Read(ref _auctionPage);

    /// <summary>마지막으로 온 내 경매 요청의 답(0x5E 9). 남의 조작이 알린 것은 <see cref="AuctionNotice" /> 로 따로 — 섞이면 기다리던 답으로 잘못 안다.</summary>
    public AuctionDone? AuctionDone => Volatile.Read(ref _auctionDone);

    /// <summary>마지막 알림 — 내 물건이 팔림·유찰, 입찰에서 밀림, 낙찰(받을 것 개수가 함께 온다).</summary>
    public AuctionDone? AuctionNotice => Volatile.Read(ref _auctionNotice);

    public int AuctionNoticeCount => Volatile.Read(ref _auctionNotices);

    /// <summary>마지막 룰렛.</summary>
    public LootRoll? LastRoll => Volatile.Read(ref _lastRoll);

    /// <summary>받은 횟수 — 기다리는 쪽이 「새 것이 왔나」를 센다.</summary>
    public int AuctionPageCount => Volatile.Read(ref _auctionPages);

    public int AuctionDoneCount => Volatile.Read(ref _auctionDones);

    public int RollCount => Volatile.Read(ref _rolls);

    public Task AuctionBrowseAsync(byte kind, byte sort, ushort page, string query, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodeBrowse(kind, sort, page, query), cancellationToken);

    public Task AuctionMineAsync(ushort page, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodePage(Auction.Mine, page), cancellationToken);

    public Task AuctionClaimsAsync(ushort page, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodePage(Auction.Claims, page), cancellationToken);

    /// <summary>가방 칸의 물건(묶음은 통째)을 올린다. 즉시 구매가 0 은 없음, 시간은 12·24·48.</summary>
    public Task AuctionPostAsync(byte slot, uint start, uint buyout, byte hours, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodePost(slot, start, buyout, hours), cancellationToken);

    public Task AuctionBidAsync(uint id, uint amount, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodeBid(id, amount), cancellationToken);

    public Task AuctionBuyoutAsync(uint id, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodeId(Auction.Buyout, id), cancellationToken);

    public Task AuctionCancelAsync(uint id, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodeId(Auction.Cancel, id), cancellationToken);

    /// <summary>받을 것 하나를 받는다. 0 이면 모두.</summary>
    public Task AuctionTakeAsync(uint id, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Auction, Auction.EncodeId(Auction.Take, id), cancellationToken);

    private void OnAuction(byte[] body)
    {
        switch (body[0])
        {
            case Auction.RollKind:
                Volatile.Write(ref _lastRoll, Auction.ReadRoll(body));
                Interlocked.Increment(ref _rolls);
                break;
            case Auction.PageKind:
                Volatile.Write(ref _auctionPage, Auction.ReadPage(body));
                Interlocked.Increment(ref _auctionPages);
                break;
            case Auction.DoneKind:
                AuctionDone done = Auction.ReadDone(body);
                if (done.Notice)
                {
                    Volatile.Write(ref _auctionNotice, done);
                    Interlocked.Increment(ref _auctionNotices);
                }
                else
                {
                    Volatile.Write(ref _auctionDone, done);
                    Interlocked.Increment(ref _auctionDones);
                }

                break;
        }
    }
}
