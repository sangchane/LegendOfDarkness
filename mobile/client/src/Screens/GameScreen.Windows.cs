using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>게임 화면 — 창 띄우기·닫기(`SetWindow` `Cover`)와 가방·대화·장비·상점 창.</summary>
public partial class GameScreen : Control
{
    /// <summary>
    /// Shows or hides one big window. Opening one first puts away whichever was open (<see cref="OneWindow" />), and the
    /// world stops taking taps and steps only while a window that lies over it is up.
    /// </summary>
    private void SetWindow(GameWindow window, bool open)
    {
        if (open)
        {
            if (_windows.Open(window) is { } before)
            {
                PutAway(before);
            }

            WindowOf(window).Visible = true;

            if (window == GameWindow.TabMap)
            {
                _tabMap.Open();
            }

            // 열 때마다 새로 묻는다(0x18 → 0x36). 창은 선 자리에 맞게 통째로 줄인다.
            if (window == GameWindow.Users)
            {
                _users.Fit(((Control)_users.GetParent()).Size);
                _usersAsked = 0;
                AskUsers();
            }

            if (window == GameWindow.Auction)
            {
                _auction.Open();
            }
        }
        else
        {
            _windows.Shut(window);
            WindowOf(window).Visible = false;
        }

        _world.Frozen = _windows.Freezing;

        // 장비창이 열려 있어도 사람은 눌린다 — 누르면 그 사람 장비창이 내 것 대신 뜬다(사용자 2026-10-01).
        _world.PeopleOnly = _windows.IsOpen(GameWindow.Gear);
    }

    private void AskUsers()
    {
        _usersAsking = _server is not null && ++_usersAsked < 10;
        _usersAskIn = 1;
        Main.Fire(_server?.AskUsersAsync(System.Threading.CancellationToken.None));
    }

    private Control WindowOf(GameWindow window) => window switch
    {
        GameWindow.Pack => _pack,
        GameWindow.Gear => _gearPanel,
        GameWindow.Talk => _talk,
        GameWindow.Chat => _chat,
        GameWindow.WorldMap => _field,
        GameWindow.Settings => _settings,
        GameWindow.TabMap => _tabMap,
        GameWindow.Users => _users,
        GameWindow.Auction => _auction,
        _ => _botGear
    };

    /// <summary>
    /// Puts a window away because another is taking its place. An NPC's talk and the world map have to tell the server
    /// — it keeps walking us through a menu, or holds every other packet, until it hears they were shut.
    /// </summary>
    private void PutAway(GameWindow window)
    {
        switch (window)
        {
            case GameWindow.Talk:
                _talk.Visible = false;
                Main.Fire(_server?.ShutDialogueAsync(System.Threading.CancellationToken.None));
                break;

            case GameWindow.WorldMap:
                CancelField();
                break;

            case GameWindow.Gear:
                _gearPanel.Visible = false;
                _pack.Visible = false;
                break;

            default:
                WindowOf(window).Visible = false;
                break;
        }
    }

    /// <summary>Takes the world map down and tells the server "none" (map 0), which is what gives the hands back.</summary>
    private void CancelField()
    {
        _field.Visible = false;
        _mapGate.Closed(_server?.FieldShown);
        Main.Fire(_server?.CloseFieldAsync(System.Threading.CancellationToken.None));
    }

    /// <summary>
    /// Lays the pack — and an NPC's window, in the same place — over the screen rather than in a row of its own.
    /// Neither shape leaves a row tall enough for a grid of pictures — landscape leaves less than one cell — and the
    /// wireframes already call both modals, so covering the control row costs nothing: it is dead while one is open.
    /// </summary>
    /// <remarks>
    /// A MarginContainer stretches its children to fill it, which throws away anchors. One plain Control
    /// in between restores them.
    /// </remarks>
    private void Cover(Control hud)
    {
        Control over = new() { MouseFilter = MouseFilterEnum.Ignore };

        hud.AddChild(over);

        // 얻은 것은 오른쪽 위 줄 바로 아래에 쌓인다 — 방향판에서 멀고, 가운데 캐릭터 옆을 비켜 간다. 가로는 부채꼴(공격)이
        // 위 줄 바로 밑까지 올라오므로 부채꼴 왼쪽 끝에 맞춘다(PlaceToasts). 창들보다 먼저 넣어 창이 열리면 그 아래로 간다.
        over.AddChild(_toasts);
        _over = over;

        // [접속자]·[봇] 단추 — 메인 메뉴 아래 오른쪽(BuildSideButtons). 알림 줄은 그 아래에서 시작한다.
        Control side = BuildSideButtons();
        over.AddChild(side);
        side.AnchorLeft = 1;
        side.AnchorRight = 1;
        side.GrowHorizontal = GrowDirection.Begin;
        side.OffsetRight = -Main.Gutter;
        side.OffsetLeft = side.OffsetRight - (SideButtonSize * 2) - (Main.Gutter / 2);

        // 파티는 위 줄 바로 아래 왼쪽에 — 세로는 방향판·부채꼴·기록 줄이 모두 아래에 있어 비어 있는 자리다. 가로는 왼쪽 아래
        // 방향판과 그 위 기록 줄이 위 줄 가까이까지 올라오므로 방향판 오른쪽 옆으로 비킨다(PlaceParty). 창들보다 먼저
        // 넣어 창이 열리면 그 아래로 간다.
        over.AddChild(_party);
        _party.AnchorLeft = 0;
        _party.AnchorRight = 0;
        _party.CustomMinimumSize = new Vector2(PartyColumn.Wide, 0);
        // 봇·파티원 타일 격자 — 파티 기둥 밖, 화면 왼쪽 가장자리에 딱 붙는다(사용자, 2026-09-26 · 2026-09-27 "그리드" 식),
        // 그 아래(자리가 없으면 옆)에 [나가기](PlaceParty).
        over.AddChild(_party.Members);
        over.AddChild(_party.Leave);
        _toasts.AnchorLeft = 1;
        _toasts.AnchorRight = 1;
        _toasts.OffsetLeft = -ToastWidth;
        _toasts.OffsetRight = 0;
        _toasts.GrowHorizontal = GrowDirection.Begin;
        _topRow.Resized += () =>
        {
            // 가로는 가운데 한 줄(배너)과 높이가 겹쳐 레벨이 오를 때 글자가 포개졌다 — 그 아래에서 시작한다.
            side.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + (Main.Gutter / 2);
            side.OffsetBottom = side.OffsetTop + SideButtonSize;
            _toasts.OffsetTop = side.OffsetBottom + Main.Gutter + (Main.Portrait ? 0 : 40);
            _toasts.OffsetBottom = _toasts.OffsetTop + 120;
            Callable.From(() => DodgePotions(side)).CallDeferred();
        };
        _controlRow.ItemRectChanged += () => Callable.From(() => DodgePotions(side)).CallDeferred();
        _abilities.ItemRectChanged += () => Callable.From(() => DodgePotions(side)).CallDeferred();

        // 룰렛 띠는 위 줄 바로 아래 가운데 — 창들을 다 넣은 뒤에 넣어(아래 PlaceColumn 뒤) 소지품·장비·설정·경매장이 열려 있어도 위에 뜬다
        // (사용자 2026-10-07). 손을 받지 않아 아래 창을 누르는 데 걸리지 않는다.
        _roll.AnchorLeft = 0.5f;
        _roll.AnchorRight = 0.5f;
        _roll.GrowHorizontal = GrowDirection.Both;
        _topRow.Resized += () => _roll.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter;

        // 큰일은 가운데, 캐릭터 머리보다 위에 — 위 줄과 캐릭터 사이.
        over.AddChild(_banner);
        _banner.AnchorLeft = 0;
        _banner.AnchorRight = 1;
        _banner.AnchorTop = Main.Portrait ? 0.27f : 0.24f;
        _banner.AnchorBottom = _banner.AnchorTop;
        _banner.OffsetTop = -20;
        _banner.OffsetBottom = 20;

        // 창은 아래에 붙는다. 대화 창과 장비 고리는 남는 높이를 다 쓰고, 소지품 한 장은 제 높이만큼만 올라와
        // 위쪽 맵을 남긴다(PackPanel.ShowTab 이 정한다).
        _talk.SizeFlagsVertical = SizeFlags.ExpandFill;

        // 월드맵 카드 — 세로는 제 높이만큼만 아래에 붙어 위쪽 맵을 남기고(카드가 많으면 창 안에서 굴린다), 가로는 오른쪽 기둥을 다 쓴다.
        _field.SizeFlagsVertical = Main.Portrait ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill;

        // 대화 창은 제 높이만큼만 아래에 붙는다 — 소지품 한 장과 같다. 긴 이야기는 창 안에서 굴린다.
        _chat.SizeFlagsVertical = SizeFlags.ShrinkEnd;

        // 길 찾기 창도 제 높이만큼만 — 세로는 그 위로 걸어가는 캐릭터가 보이고, 가로는 오른쪽 기둥 전체를 쓴다.
        _tabMap.SizeFlagsVertical = Main.Portrait ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill;

        // 길을 걷는 동안의 표시 — 창을 닫아도 어디로 가는지와 멈추는 단추가 남는다. 창들보다 먼저 넣어 창 아래로 간다.
        over.AddChild(_guideChip = BuildGuideChip());

        List<VBoxContainer> holders = [];

        foreach (Control panel in new Control[] { _pack, _gearPanel, _talk, _chat, _field, _settings, _tabMap, _botGear, _users, _auction })
        {
            VBoxContainer holder = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
            over.AddChild(holder);
            holder.AddChild(panel);
            holders.Add(holder);

            if (panel == _talk)
            {
                _talkHolder = holder;
                if (!Main.Portrait)
                {
                    holder.OffsetTop = 0;
                    holder.Alignment = BoxContainer.AlignmentMode.Center;
                    _talk.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                    _talk.CustomMinimumSize = new Vector2(Mathf.Min(640, GetViewportRect().Size.X - Main.SafeInsets.Left - Main.SafeInsets.Right), 0);
                }
            }

            if (panel == _chat)
            {
                _chatHolder = holder;
            }

            if (panel == _settings)
            {
                _settingsHolder = holder;
            }

            if (panel == _botGear)
            {
                _botGearHolder = holder;
            }

            if (panel == _pack)
            {
                _packHolder = holder;
            }

            // 장비창은 그림 한 장이라 제 크기만큼만 — 세로는 위 줄 바로 아래 가운데(소지품은 그 바로 아래), 가로는 소지품 기둥
            // 바로 왼쪽(혼자면 오른쪽 끝) — 내 장비창은 소지품과 같이 열어 입고 벗는다(사용자 2026-10-01).
            // 접속자 창은 설정 창처럼 위 줄 바로 아래 가운데 — 가로에서 오른쪽 기둥에 서면 공격 단추를 덮는다.
            if (panel == _users)
            {
                holder.Alignment = BoxContainer.AlignmentMode.Begin;
                _users.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            }

            // 경매장 창 — 세로는 위 줄 바로 아래 폭 전체. 가로는 가운데에 600 폭(목록 옆에 입찰·올리기 칸이 선다), 높이가 모자라 소지품처럼 위 줄을 덮는다.
            // 높이는 화면 아래(안전 구역)까지 — 목록이 남는 만큼 늘어나고, 안전 구역으로 줄어도 입찰 줄이 안 잘린다.
            if (panel == _auction)
            {
                holder.Alignment = BoxContainer.AlignmentMode.Begin;
                _auction.SizeFlagsVertical = SizeFlags.ExpandFill;
                if (!Main.Portrait)
                {
                    _auction.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                    _auction.CustomMinimumSize = new Vector2(Mathf.Min(600, GetViewportRect().Size.X - (Main.Gutter * 4)), 0);
                }
            }

            if (panel == _gearPanel)
            {
                _gearHolder = holder;
                holder.Alignment = BoxContainer.AlignmentMode.Begin;
                _gearPanel.SizeFlagsHorizontal = Main.Portrait ? SizeFlags.ShrinkCenter : SizeFlags.ShrinkEnd;
            }

            holder.SetAnchorsPreset(LayoutPreset.FullRect);
            holder.OffsetLeft = 0;
            holder.OffsetTop = Main.TouchMinimum + (Main.Gutter * 3);
            holder.OffsetRight = 0;
            holder.OffsetBottom = 0;

            // 가로 소지품·장비 창은 화면 높이를 거의 다 쓴다 — 위 줄 아래에서 시작하면 장비 고리 여섯 줄이 한 화면에 안
            // 들어 굴려야 했고, 사용자가 그건 못 쓴다고 했다(2026-09-23). 열려 있는 동안 오른쪽 위 줄을 덮고, 닫기는 창 안에 있다.
            // 가로 봇 장비창은 고리 옆에 목록을 두어 넓다 — 오른쪽 기둥에 안 들어가 가운데에, 화면 높이를 다 쓴다.
            if (panel == _botGear && !Main.Portrait)
            {
                holder.OffsetTop = 0;
                holder.Alignment = BoxContainer.AlignmentMode.Center;
                _botGear.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                continue;
            }

            if ((panel == _pack || panel == _tabMap || panel == _talk || panel == _auction) && !Main.Portrait)
            {
                holder.OffsetTop = 0;
                continue;
            }

            // 가로 장비창은 그림 키(302)가 위 줄 아래에 안 든다 — 맨 위부터. 닫기는 그림 속 Close.
            if (panel == _gearPanel && !Main.Portrait)
            {
                holder.OffsetTop = 0;
                continue;
            }

            // 대화(기록) 창은 화면 가운데 아래에(사용자, 2026-09-26) — 가로는 오른쪽 기둥 대신 가운데에 제 폭만큼. 세로는 원래 폭 전체.
            if (panel == _chat && !Main.Portrait)
            {
                _chat.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                _chat.CustomMinimumSize = new Vector2(Mathf.Min(460, GetViewportRect().Size.X - (Main.Gutter * 4)), 0);
            }

            // 가로 설정 창은 오른쪽 기둥에 서면 공격 단추와 기술 부채꼴을 덮었다(사용자, 2026-09-23). 설정은 월드를 멈추지
            // 않으므로 조작이 살아 있어야 한다 — 위 줄 바로 아래, 방향판과 부채꼴 사이 가운데에 제 크기만큼만 선다.
            if (panel == _settings && !Main.Portrait)
            {
                holder.Alignment = BoxContainer.AlignmentMode.Begin;
                _settings.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            }

            // 창은 위 줄이 실제로 끝나는 곳 아래에서 시작한다. 세로 위 줄은 단추 줄이 붙어 두 줄(120)이라, 가로 위 줄
            // 높이로 박아 둔 자리(72)에서 시작하면 장비 탭이 인벤토리·지도·로그아웃을 덮었다.
            _topRow.Resized += () => holder.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter;
        }

        PlaceColumn(holders);
        over.AddChild(_roll);

        // 창의 최소 폭은 글자를 잰 뒤에야 맞는다. 처음 잰 값이 모자랐다(314 — 창은 438) — 바뀔 때마다 다시 세운다. 화면 크기가 바뀌어도.
        _pack.MinimumSizeChanged += () => PlaceColumn(holders);
        GetViewport().SizeChanged += () => PlaceColumn(holders);
    }

    /// <summary>
    /// Stands the windows in landscape in a column against the right edge, full width upright. The column is a bit over
    /// a third of the width, more where the pack window needs more — on a 16:9 screen it is about half. The window's
    /// width is its own minimum; the share is of the width inside the safe margins, not of the screen — counted from the
    /// screen, the gear window came up 9 short and ran past the right edge of a 640 screen.
    /// </summary>
    private void PlaceColumn(IReadOnlyList<VBoxContainer> holders)
    {
        float across = GetViewportRect().Size.X;
        float column = across > 0
            ? SideColumn.LeftAnchor(across, Main.SafeInsets.Left, Main.SafeInsets.Right, _pack.GetCombinedMinimumSize().X, 0.6f)
            : 0.6f;

        foreach (VBoxContainer holder in holders)
        {
            // 넓은 세로 화면은 창을 폰 폭 그대로 가운데에 — 폭 전체로 늘면 소지품 칸이 납작해지고 슬라이더가 길어졌다.
            if (Main.Portrait && across > WideFrom)
            {
                holder.AnchorLeft = holder.AnchorRight = 0.5f;
                holder.OffsetLeft = -PortraitWindowMost / 2;
                holder.OffsetRight = PortraitWindowMost / 2;
                continue;
            }

            holder.AnchorLeft = Main.Portrait || holder == _settingsHolder || holder == _botGearHolder || holder == _gearHolder || holder == _chatHolder || holder == _talkHolder || holder == _users.GetParent() || holder == _auction.GetParent() ? 0 : column;
        }
    }

    /// <summary>
    /// Where the pack sits when it is open: against the right edge, over rather than beside the world, and
    /// taking a bit over a third of the width. The rest of the row lets taps through to the floor.
    /// </summary>
    private Control BuildPackRow()
    {
        HBoxContainer row = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };

        if (!Main.Portrait)
        {
            // 가로에서는 이 줄이 위 줄과 조작 줄 사이의 빈 자리이기도 하다. 닫혀 있어도 남아 있어야
            // 조작 줄이 위로 올라오지 않는다. 패널은 오른쪽 3분의 1 남짓만 덮는다.
            row.AddChild(new Control
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 62,
                MouseFilter = MouseFilterEnum.Ignore
            });
        }

        // 패널은 이 줄이 아니라 HUD 위에 덮어 놓는다(Cover). 줄은 조작 줄이 올라오지 않게
        // 자리만 지킨다.

        return row;
    }

    /// <summary>Throws one slot on the floor, at our own feet — the only tile we can be sure of.</summary>
    private async Task Throw(int slot, int count)
    {
        if (_server is not { State: { } standing } server)
        {
            return;
        }

        _world.Threw(standing.Where);
        await server.DropAsync(slot, count, standing.Where, System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// Pulls everything to the front of the pack. The server only swaps two slots at a time, so this is a
    /// run of swaps; it is worked out in one go from what we are holding now, before any of them land.
    /// </summary>
    private async Task Straighten()
    {
        if (_server is not { } server)
        {
            return;
        }

        foreach ((int from, int to) in PackOrder.Tidy(server.Pack))
        {
            await server.MoveAsync(from, to, System.Threading.CancellationToken.None);
        }
    }

    /// <summary>How much of the screen the on-screen keyboard is taking, in this screen's own units.</summary>
    private float Lifted()
    {
        int keyboard = DisplayServer.VirtualKeyboardGetHeight();
        Vector2I screen = DisplayServer.ScreenGetSize();

        return keyboard > 0 && screen.Y > 0 ? keyboard / (float)screen.Y * GetViewportRect().Size.Y : 0;
    }

    /// <summary>
    /// Opens or shuts what has been said. Like the pack it lies over the world, so the world takes no taps or steps
    /// while it is open, and the two never lie on top of each other.
    /// </summary>
    private void Chatting(bool open)
    {
        if (open)
        {
            _chat.Show(_history);
        }

        SetWindow(GameWindow.Chat, open);
    }

    /// <summary>
    /// Opens or shuts the pack. While it is open the world takes no taps and no steps — the panel lies over
    /// it, and a thumb aimed at the list must not walk the character. Whatever window was open is put away first
    /// (<see cref="SetWindow" />) — an NPC's window the way its own close button does.
    /// </summary>
    private void Carrying(bool open)
    {
        // 내 장비창과 같이 열린 소지품을 닫으면 둘 다 닫는다.
        if (!open && _windows.IsOpen(GameWindow.Gear))
        {
            Dressing(false);
            return;
        }

        SetWindow(GameWindow.Pack, open);

        if (open)
        {
            _pack.Show(_server?.Pack ?? LayoutCheck.PretendPack, Mine.Gold);
        }
    }

    /// <summary>
    /// Opens or shuts the gear window — like the pack, it lies over the world and puts away whatever was open. Ours opens
    /// with the pack beside it, so things can be put on and taken off (사용자 2026-10-01); somebody else's
    /// (<paramref name="other" />) opens alone.
    /// </summary>
    private void Dressing(bool open, OtherProfile? other = null)
    {
        SetWindow(GameWindow.Gear, open);
        _pack.Visible = open && other is null;

        if (!open)
        {
            return;
        }

        if (other is not null)
        {
            _gearPanel.ShowOther(other, GearRoom());
            return;
        }

        _gearPanel.ShowMine();
        _pack.Show(_server?.Pack ?? LayoutCheck.PretendPack, Mine.Gold);

        // 직업·그룹 받기는 프로필(0x39)에서 온다 — 열 때마다 새로 묻는다.
        Main.Fire(_server?.AskProfileAsync(System.Threading.CancellationToken.None));
    }

    /// <summary>
    /// The room the gear picture may grow into (whole multiples only): upright, half the height under the top row when the
    /// pack stands under it; on its side, the left part of the screen beside the pack's column.
    /// </summary>
    private Vector2 GearRoom()
    {
        Vector2 screen = GetViewportRect().Size;
        float top = _gearHolder?.OffsetTop ?? 0;

        return Main.Portrait
            ? new Vector2(screen.X - (Main.Gutter * 2), (screen.Y - top) / 2)
            : new Vector2(screen.X / 2, screen.Y - top);
    }

    /// <summary>
    /// Opens the window an NPC sent, or shuts it when the server did. While it is open the world takes no taps or steps,
    /// as with the pack, and whatever window was open is put away (<see cref="SetWindow" />) so two never lie on top of each other.
    /// </summary>
    private void Talk(Dialogue? talk)
    {
        // 우리가 닫기를 보내면 서버도 닫기(0x30)로 답한다. 그사이 소지품을 열었으면 그 화면을 건드리지 않는다.
        if (talk is null && !_talk.Visible)
        {
            return;
        }

        SetWindow(GameWindow.Talk, talk is not null);

        if (talk is not null)
        {
            _talk.Show(talk, _server?.Pack ?? [], Mine.Gold);
        }
    }

    /// <summary>Sends the selected order and lets the next server dialog confirm the result.</summary>
    private async Task Trade(uint merchant, bool selling, System.Collections.Generic.IReadOnlyList<(string Name, int Slot, int Quantity)> lines)
    {
        if (_server is null) return;
        try
        {
            await _server.BulkTradeAsync(merchant, selling, lines, System.Threading.CancellationToken.None);
        }
        catch (Exception failure)
        {
            Main.NoteActivityError(failure.GetType().Name);
            _talk.TradeFailed($"거래를 보내지 못했습니다: {failure.Message}");
        }
    }

    /// <summary>Shuts an NPC window and tells the server.</summary>
    private void ShutTalk()
    {
        Talk(null);
        Main.Fire(_server?.ShutDialogueAsync(System.Threading.CancellationToken.None));
    }

    // 부채꼴 위 자동 포션 칸 — [접속자]·[봇] 단추가 비켜 가야 할 자리.
    private readonly System.Collections.Generic.List<Control> _potionChips = [];

    /// <summary>
    /// [접속자]·[봇] 단추가 자동 포션 칸(위의 % 글자까지)과 겹치면 포션 칸 왼쪽으로 비킨다 — 화면이 낮으면 부채꼴 위 포션이
    /// 위 줄 바로 밑까지 올라와 단추 아래에 깔렸다(사용자 2026-10-05).
    /// </summary>
    private void DodgePotions(Control side)
    {
        float width = (SideButtonSize * 2) + (Main.Gutter / 2);
        side.OffsetRight = -Main.Gutter;
        side.OffsetLeft = side.OffsetRight - width;
        Rect2 parent = side.GetParentControl().GetGlobalRect();
        Rect2 mine = new(parent.End.X + side.OffsetLeft, parent.Position.Y + side.OffsetTop, width, SideButtonSize);
        float left = float.MaxValue;

        foreach (Control chip in _potionChips)
        {
            if (!chip.IsVisibleInTree()) continue;
            Rect2 box = chip.GetGlobalRect().GrowSide(Godot.Side.Top, PotionLabel);
            if (box.Intersects(mine)) left = System.Math.Min(left, box.Position.X);
        }

        if (left == float.MaxValue) return;

        float shift = mine.End.X - left + (Main.Gutter / 2);
        side.OffsetRight -= shift;
        side.OffsetLeft -= shift;
    }

    /// <summary>포션 칸 위에 얹힌 「50%」 글자 높이.</summary>
    private const int PotionLabel = 18;
}
