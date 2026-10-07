using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>게임 화면 — 손 없이 확인하는 시험 코드(`--skill` `--hold` 같은 실행 인자일 때만 돈다).</summary>
public partial class GameScreen : Control
{
    /// <summary>
    /// Only when checking without a hand: taps the person named by <c>--invite</c> and presses 파티 초대, presses 수락 for
    /// <c>--accept</c>, sends <c>--party-say</c> from the 파티 tab once grouped, and presses 나가기 <c>--leave-after</c>
    /// seconds after that. Each press is the button's own signal, so the wiring is what gets checked.
    /// </summary>
    private void RehearseParty(double delta)
    {
        RehearseLook(delta);
        RehearseUsers();
        RehearseAuction();
        RehearseAimHold(delta);

        if (Main.Inviting.Length > 0 && !_partyRehearsed && Time.GetTicksMsec() > 6000)
        {
            if (_world.TargetName == Main.Inviting && _party.Invite.Visible)
            {
                _partyRehearsed = true;
                _party.Invite.EmitSignal(BaseButton.SignalName.Pressed);
                GD.Print($"GREYBOX_PARTY 초대 {Main.Inviting}");
            }
            else if (_world.TargetName != Main.Inviting)
            {
                _world.TapPerson(Main.Inviting);
            }
        }

        if (Main.Accepting && _party.Asking)
        {
            _party.AcceptButton.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print("GREYBOX_PARTY 수락");
        }

        _groupedFor = _party.Grouped ? Math.Max(_groupedFor, 0) + delta : -1;

        if (Main.PartySaying.Length > 0 && !_partySaid && _groupedFor > 2)
        {
            _partySaid = true;
            Chatting(true);
            _chat.Rehearse(Main.PartySaying);
            GD.Print($"GREYBOX_PARTY 말 {Main.PartySaying}");
        }

        if (Main.LeavingAfter >= 0 && !_partyLeft && _groupedFor > Main.LeavingAfter)
        {
            _partyLeft = true;
            _party.Leave.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print("GREYBOX_PARTY 나가기");
        }

        if (_party.Grouped || _party.Asking || _party.Invite.Visible)
        {
            Rect2 column = _party.GetGlobalRect();

            foreach ((string name, Control part) in new (string, Control)[]
                     { ("방향판", _pad), ("부채꼴", _abilities), ("기록 줄", _messages), ("위 줄", _topRow) })
            {
                if (part.IsVisibleInTree() && column.Intersects(part.GetGlobalRect()))
                {
                    GD.Print($"GREYBOX_PARTY_OVERLAP {name}");
                }
            }
        }
    }

    // --skill 로 기술을 누르는 사이. 손 없이 확인할 때만 돈다.
    private double _skillWait;

    /// <summary>Presses one fan slot every so often, so a run with nobody watching shows what a technique draws.</summary>
    private void RehearseASkill(double delta)
    {
        // "m3" 은 마법 쪽 세 번째 칸이다.
        bool spell = Main.Ability.StartsWith('m');

        if (Main.Ability.Length == 0)
        {
            return;
        }

        // "m다라밀공" — 마법 쪽에서 이 이름이 놓인 칸.
        bool named = !int.TryParse(spell ? Main.Ability[1..] : Main.Ability, out int slot);

        if (named && !spell)
        {
            return;
        }

        _skillWait += delta;

        if (_skillWait < 1.5)
        {
            return;
        }

        _skillWait = 0;

        if (named)
        {
            _abilities.PressSpell(Main.Ability[1..]);
            return;
        }

        _abilities.Press(slot - 1, spell);
    }

    // --hold 로 누르고 있는 시간. 음수면 아직 안 눌렀다.
    private double _held = -1;

    private int _holdWait;

    private double _holdReported;

    /// <summary>
    /// Only when checking without a hand (<c>--hold E</c>): presses the key in the middle with a finger for a second and a
    /// half, lets go, and says where the character stands and how see-through the pad is every quarter second.
    /// </summary>
    private int _minimapTold;

    private int _minimapZoomWait;

    private int _minimapPressed;

    private int _packPickWait;

    /// <summary><c>--minimap</c>: every two seconds, where the minimap stands and what it shows — no thumb needed.</summary>
    private void RehearseMinimap()
    {
        // --minimap-zoom: 60 프레임 뒤부터 10 프레임마다 [+]/[−] 를 한 번씩.
        if (Main.MinimapZoom != 0 && _minimapPressed < Math.Abs(Main.MinimapZoom) && ++_minimapZoomWait >= 60 && _minimapZoomWait % 10 == 0)
        {
            _minimapPressed++;
            (Main.MinimapZoom > 0 ? _minimap.ZoomIn : _minimap.ZoomOut).EmitSignal(BaseButton.SignalName.Pressed);
        }

        if (Main.CheckingMinimap && (_world.MapId > 0 || _server is null) && ++_minimapTold % 120 == 30)
        {
            GD.Print($"GREYBOX_MINIMAP {_minimap.GetGlobalRect()} {_minimap.Describe()}");
        }
    }

    /// <summary><c>--pack-pick N</c>: once the pack is open and filled, taps its N-th picture so the action row shows.</summary>
    private void RehearsePackPick()
    {
        if (Main.PackPick > 0 && _pack.Visible && _packPickWait >= 0 && ++_packPickWait == 45)
        {
            _packPickWait = -1;
            GD.Print(_pack.PickNth(Main.PackPick) ? $"GREYBOX_PACK_PICK {Main.PackPick}" : "GREYBOX_PACK_PICK 없음");
        }
    }

    private void RehearseAHold(double delta)
    {
        // 접속 직후 서버가 화면을 새로 보내는 동안은 걸음을 버린다(CancelWalkingIfRefreshing) — 서버가 있으면 4초 남짓 기다린다.
        if (Main.Holding.Length == 0 || _held > 3 || _holdWait++ < (_server is null ? 30 : 240))
        {
            return;
        }

        Button? key = _keys.FirstOrDefault(one => one.Where.ToString()[0] == char.ToUpperInvariant(Main.Holding[0])).Key;

        if (key is null)
        {
            return;
        }

        if (_held < 0)
        {
            Press(key, true);
            _held = 0;
            GD.Print($"GREYBOX_HOLD 누름 칸 {_world.Standing.X},{_world.Standing.Y}");
            return;
        }

        double before = _held;
        _held += delta;

        if (before < 1.5 && _held >= 1.5)
        {
            Press(key, false);
            GD.Print($"GREYBOX_HOLD 뗌 칸 {_world.Standing.X},{_world.Standing.Y}");
        }

        if (_held - _holdReported >= 0.25)
        {
            _holdReported = _held;
            GD.Print($"GREYBOX_HOLD {_held:0.00}초 칸 {_world.Standing.X},{_world.Standing.Y} 투명도 {_pad.Modulate.A:0.00}");
        }
    }

    // 손가락으로 누른다 — 폰에서처럼 Godot 이 첫 손가락을 마우스로 바꿔 버튼에 준다. 마우스로 누르면 그다음 손가락이
    // 첫 손가락으로 쳐져 폰과 다르게 돈다(두 손가락 시험에서 그랬다).
    private static void Press(Button key, bool down) => Input.ParseInputEvent(
        new InputEventScreenTouch { Index = 0, Pressed = down, Position = key.GetGlobalRect().GetCenter() });

    // --notices 를 한 번만 흘린다.
    private int _noticeWait;

    /// <summary>
    /// Only when checking without a server (<c>--notices</c>): puts lines Hades really sends through the same sorting
    /// the live screen uses, so each place they land can be seen at once.
    /// </summary>
    private void RehearseNotices()
    {
        if (!Main.Noticing || _server is not null || _noticeWait < 0 || _noticeWait++ < 30)
        {
            return;
        }

        _noticeWait = -1;

        foreach ((byte type, string line) in new (byte, string)[]
        {
            (2, "you cast dion."),
            (2, "Your skin is already like stone."),
            (2, "You can't attack that."),
            (2, "길이 막혀 가까운 곳으로 옮겼습니다."),
            (2, "쿠룸 Received."),
            (3, "금전 120전을 주웠습니다."),
            (2, "경험치가 1164 올랐습니다"),
            (2, "Your insight has increased!")
        })
        {
            if (MessageSort.FromServer(type, line) is { } notice)
            {
                Route(notice);
            }
        }
    }

    /// <summary>Only when checking without a hand (<c>--chat 시스템</c>): opens the full log on that tab.</summary>
    private void OpenChatOnItsOwn()
    {
        // 사냥이 몇 줄을 쌓을 틈을 준다 — 창이 열리면 월드가 멈춘다.
        if (Main.ChatTab.Length == 0 || _chat.Visible || _chatSettling < 0 || Time.GetTicksMsec() < ChatAfterMilliseconds)
        {
            return;
        }

        _chatSettling = -1;
        Chatting(true);
        _chat.Choose(Main.ChatTab switch
        {
            "일반" or "general" => MessageChannel.General,
            "파티" or "party" => MessageChannel.Party,
            "시스템" or "system" => MessageChannel.System,
            _ => null
        });
    }

    // --look: 다음 탭까지 남은 초, 끝났나.
    private double _lookIn;

    private bool _looked;

    /// <summary>
    /// 손 없이 확인할 때만(<c>--look 이름</c>): 내 장비창을 열고, 그 위에서 그 사람을 실제 터치로 1초마다 눌러 그 사람 장비창으로
    /// 바뀌면 멈춘다 — 장비창이 열린 채 사람 누르기(WorldView.PeopleOnly)까지 같은 길로 확인된다.
    /// </summary>
    private void RehearseLook(double delta)
    {
        if (Main.Looking.Length == 0 || _looked || Time.GetTicksMsec() < 6000 || (_lookIn -= delta) > 0)
        {
            return;
        }

        _lookIn = 1;

        if (!_gearPanel.Visible)
        {
            Dressing(true);
        }
        else if (!_gearPanel.ShowingOther)
        {
            _world.TapPerson(Main.Looking);
        }
        else
        {
            _looked = true;
            GD.Print($"GREYBOX_LOOK {Main.Looking} 소지품 {(_pack.Visible ? "열림" : "닫힘")}");
        }
    }

    private bool _usersRehearsed;

    /// <summary>손 없이 확인할 때만(<c>--users</c>): 들어간 뒤 [접속자] 창을 한 번 열고, 목록이 오면 이름들을 적는다.</summary>
    private void RehearseUsers()
    {
        if (!Main.ShowingUsers || _usersRehearsed || Time.GetTicksMsec() < 6000)
        {
            return;
        }

        if (!_users.Visible)
        {
            SetWindow(GameWindow.Users, true);
        }
        else if (_users.Names.Count > 0)
        {
            _usersRehearsed = true;
            GD.Print($"GREYBOX_USERS {string.Join(",", _users.Names)}");
        }
    }

    private bool _auctionRehearsed;

    /// <summary>
    /// 손 없이 확인할 때만(<c>--auction</c> · <c>--auction-tab 찾기|올리기|내경매|받을것</c>): 들어간 뒤 [경매장] 창을 한 번 열고, 그 탭의 줄이
    /// 오면 <c>GREYBOX_AUCTION_TAB 탭 줄수</c> 를 적는다(찾기는 줄 이름들도 <c>GREYBOX_AUCTION</c> 으로).
    /// </summary>
    private void RehearseAuction()
    {
        if (!Main.ShowingAuction || _auctionRehearsed || Time.GetTicksMsec() < 6000)
        {
            return;
        }

        if (!_auction.Visible)
        {
            SetWindow(GameWindow.Auction, true);
        }
        else if (_auction.RowCount is { } rows)
        {
            _auctionRehearsed = true;
            GD.Print($"GREYBOX_AUCTION_TAB {_auction.TabName} {rows}");

            if (Main.AuctionConfirm && _auction.RehearseCancel())
            {
                GD.Print("GREYBOX_AUCTION_CONFIRM");
            }

            if (_auction.TabName == "찾기" && _auction.Names.Count > 0)
            {
                GD.Print($"GREYBOX_AUCTION {string.Join(",", _auction.Names)}");
            }
        }
    }

    private bool _rollRehearsed;

    /// <summary>손 없이 확인할 때만(<c>--roll-preview</c>): 6초 뒤 서버 없이 지어낸 룰렛 결과(셋) 하나를 띄운다.</summary>
    private void RehearseRoll()
    {
        if (!Main.RollPreview || _rollRehearsed || Time.GetTicksMsec() < 6000)
        {
            return;
        }

        _rollRehearsed = true;
        _roll.Show(new Lod.Mobile.Core.Protocol.World.LootRoll(
            32957, 0, "청동 방패+2", [(1, "전사", 87), (2, "마법사", 42), (3, "성직자", 65)], 1));
        GD.Print("GREYBOX_ROLL_PREVIEW 전사 87");
    }

    private double _aimRehearsal = -1;

    /// <summary>손 없이 확인할 때만(<c>--aim-hold</c>): 8초에 위로 끈 셈 치고 끝낸 뒤, 1초·4초에 고른 이를 적는다.</summary>
    private void RehearseAimHold(double delta)
    {
        if (!Main.AimHolding || Time.GetTicksMsec() < 8000 || _aimRehearsal > 5)
        {
            return;
        }

        if (_aimRehearsal < 0)
        {
            GD.Print($"GREYBOX_AIM_HOLD 끌기 → {_world.AimEnemy(new Vector2(0, -60))}");
            _world.AimEnded();
            _aimRehearsal = 0;
            return;
        }

        double before = _aimRehearsal;
        _aimRehearsal += delta;

        foreach (double at in new[] { 1.0, 4.0 })
        {
            if (before < at && _aimRehearsal >= at)
            {
                GD.Print($"GREYBOX_AIM_HOLD {at:0}초 → {_world.Target}");
            }
        }
    }
}
