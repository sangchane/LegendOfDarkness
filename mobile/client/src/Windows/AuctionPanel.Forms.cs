using System;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace LodClient;

/// <summary>경매장 창의 입력 — 찾기의 입찰 줄(금액 · [입찰] · [즉시 구매 N전])과 올리기의 양식(시작가 · 즉시 구매가 · 기간 · [올리기]).</summary>
public sealed partial class AuctionPanel
{
    private static readonly byte[] Hours = [12, 24, 48];

    /// <summary>03 상수 표 AUCTION_DEPOSIT_RATE — 기간별 보증금(시작가의 %).</summary>
    private static readonly int[] Rates = [1, 2, 4];

    /// <summary>값의 상한(서버 AUCTION_MAX_PRICE) — 입력 칸이 이보다 큰 값은 받지 않는다. 들고 있는 것이 모자라면 서버가 은행 금화로 낸다.</summary>
    private const double MostGold = 2_000_000_000;

    private readonly SpinBox _bid = Number("입찰 ");
    private readonly Button _bidButton = new() { Text = "입찰", CustomMinimumSize = new Vector2(64, Tall), FocusMode = FocusModeEnum.None };
    private readonly Button _buyoutButton = new() { CustomMinimumSize = new Vector2(48, Tall), FocusMode = FocusModeEnum.None, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly SpinBox _start = Number("시작 ");
    private readonly SpinBox _buyout = Number("즉시 ");
    private readonly Label _hint = Words(string.Empty, Greybox.Muted);
    private readonly Button _postButton = new() { Text = "올리기", CustomMinimumSize = new Vector2(80, Tall), FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private BoxContainer _strip = null!;
    private VBoxContainer _form = null!;
    private Control _side = null!;
    private PercentSelect _hoursSelect = null!;
    private int _hoursAt = 1;

    /// <summary>입찰 줄과 올리기 양식을 담는 칸 — 세로는 목록 아래, 가로는 목록 옆. 둘 다 고른 것이 있을 때만 선다.</summary>
    private Control BuildSide()
    {
        _buyoutButton.AddThemeFontSizeOverride("font_size", 13);
        Greybox.Commit(_bidButton);
        Greybox.Plain(_buyoutButton);
        Greybox.Commit(_postButton);

        _bidButton.Pressed += () =>
        {
            if (_pick is { } row)
            {
                uint amount = (uint)_bid.Value;
                Act(server => server.AuctionBidAsync(row.Id, amount, CancellationToken.None));
            }
        };
        _buyoutButton.Pressed += () =>
        {
            if (_pick is { } row)
            {
                Act(server => server.AuctionBuyoutAsync(row.Id, CancellationToken.None));
            }
        };

        // 세로는 한 줄로, 가로는 세 칸을 쌓는다.
        _strip = new BoxContainer { Vertical = !Main.Portrait };
        _strip.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _bid.SizeFlagsHorizontal = Main.Portrait ? SizeFlags.Fill : SizeFlags.ExpandFill;
        _bid.CustomMinimumSize = new Vector2(Main.Portrait ? 104 : 0, Tall);
        _strip.AddChild(_bid);
        _strip.AddChild(_bidButton);
        _strip.AddChild(_buyoutButton);
        Typing(_bid, _strip);

        _hoursSelect = PercentSelect.Of(["12시간", "24시간", "48시간"], _hoursAt, this, 96, Tall);
        _hoursSelect.Changed += index =>
        {
            _hoursAt = index;
            Hint();
        };
        _postButton.Pressed += Post;
        _start.ValueChanged += _ => Hint();

        _form = new VBoxContainer();
        _form.AddThemeConstantOverride("separation", Main.Gutter / 2);
        if (Main.Portrait)
        {
            _start.SizeFlagsHorizontal = _buyout.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _form.AddChild(Line(_start, _buyout));
        }
        else
        {
            _form.AddChild(_start);
            _form.AddChild(_buyout);
        }

        _form.AddChild(Line(_hoursSelect, _postButton));
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.ClipText = true;
        _hint.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _form.AddChild(_hint);
        Typing(_start, _form);
        Typing(_buyout, _form);

        VBoxContainer side = new() { Visible = false };
        side.AddThemeConstantOverride("separation", Main.Gutter / 2);
        if (!Main.Portrait)
        {
            side.CustomMinimumSize = new Vector2(190, 0);
        }

        side.AddChild(_strip);
        side.AddChild(_form);
        _side = side;

        return side;
    }

    /// <summary>입찰 줄은 찾기 탭에서 남의 물건을 골랐을 때, 양식은 올리기 탭에서 물건을 골랐을 때만 보인다.</summary>
    private void Side()
    {
        _strip.Visible = _tab == 0 && _pick is { } row && (row.Flags & 1) == 0;
        _form.Visible = _tab == 1 && _item is not null;
        _side.Visible = _strip.Visible || _form.Visible;
    }

    /// <summary>찾기 줄을 골랐다(또는 풀었다) — 입찰 칸을 다음 최소 입찰가로 채운다. 내 물건에는 입찰 줄이 안 선다.</summary>
    private void PickRow(AuctionRow? row)
    {
        _pick = row;

        if (row is not null)
        {
            // 입찰이 있으면 현재가 + max(1, 현재가의 5%), 없으면 시작가(= 현재가로 온다).
            uint least = Auction.NextBid(row.Price, (row.Flags & 4) != 0);
            _bid.MaxValue = Math.Max(MostGold, least);
            _bid.MinValue = least;
            _bid.Value = least;
            _buyoutButton.Visible = row.Buyout > 0;
            _buyoutButton.Text = $"즉시 구매 {row.Buyout:N0}전";
        }

        Side();
    }

    /// <summary>가방의 물건을 골랐다(또는 풀었다) — 시작가는 상점가쯤으로, 즉시 구매가는 없음으로 채운다.</summary>
    private void PickItem(InventoryItem? item)
    {
        _item = item;

        if (item is not null)
        {
            long shop = item.Stats is { } stats ? (long)(stats.Value / 1.6) * Math.Max(1, item.Stacks) : 0;
            _start.Value = Math.Max(1, shop);
            _buyout.Value = 0;
        }

        Hint();
        Side();
    }

    /// <summary>
    /// 양식 밑의 한 줄 — 보증금 N전(= max(1, ⌊시작가 × 비율/100⌋))과 즉시 구매가 0 의 뜻.
    /// 올리면 보증금이 바로 빠지고, 팔리면 돌아온다.
    /// </summary>
    private void Hint()
    {
        long deposit = Math.Max(1, (long)_start.Value * Rates[_hoursAt] / 100);
        _hint.Text = $"보증금 {deposit:N0}전 · 즉시 구매가 0 은 없음";
    }

    /// <summary>[올리기] — 가방이 그새 바뀌었으면(칸이 다른 물건을 가리킬 수 있다) 보내지 않고 다시 보인다.</summary>
    private void Post()
    {
        if (_item is not { } item)
        {
            return;
        }

        bool same = Bag?.Invoke().Any(one => one.Slot == item.Slot && one.Name == item.Name && one.Stacks == item.Stacks) == true;
        if (!same)
        {
            ShowBag();
            Say("가방이 바뀌었습니다", Greybox.Accent);
            return;
        }

        byte slot = (byte)item.Slot;
        uint start = (uint)_start.Value;
        uint buyout = (uint)_buyout.Value;
        byte hours = Hours[_hoursAt];
        Act(server => server.AuctionPostAsync(slot, start, buyout, hours, CancellationToken.None));
    }

    /// <summary>금액 칸 — 숫자 자판, 친 글자가 바로 값이 된다(단추를 눌러도 자판이 안 내려가서 Enter 없이 보낸다).</summary>
    private static SpinBox Number(string prefix)
    {
        SpinBox box = new()
        {
            MinValue = 0,
            MaxValue = MostGold,
            Step = 1,
            Prefix = prefix,
            Suffix = "전",
            UpdateOnTextChanged = true,
            SelectAllOnFocus = true,
            CustomMinimumSize = new Vector2(96, Tall)
        };
        box.GetLineEdit().VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number;

        return box;
    }

    /// <summary>칸이 서 있는 줄(<paramref name="zone" />)을 누르는 것은 자판을 내리는 탭이 아니다 — 그 줄의 [입찰]·[올리기] 가 먼저 눌린다.</summary>
    private static void Typing(SpinBox box, Control zone) => TouchInput.Zone(box.GetLineEdit(), zone);
}
