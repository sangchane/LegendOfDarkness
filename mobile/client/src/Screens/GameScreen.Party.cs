using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>게임 화면 — 파티 칸과 봇 동료.</summary>
public partial class GameScreen : Control
{
    /// <summary>0x0A kind 11 — somebody talking to the group, not the server saying the group changed.</summary>
    private const byte GroupChat = 11;

    /// <summary>[봇 부르기] — 봇 탭 「마법사」 켬을 먼저 보낸다. 서버는 메모리에만 두어 다시 뜨면 잊으므로 부를 때마다.</summary>
    private async System.Threading.Tasks.Task CallCompanion()
    {
        await SendBotMagic();

        if (_server is { } server)
        {
            await server.CallCompanionAsync(System.Threading.CancellationToken.None);
        }
    }

    private System.Threading.Tasks.Task SendBotMagic() =>
        _server?.SendCompanionOrdersAsync(Main.BotOrdersFor(_server.Vitals?.Level ?? 0), System.Threading.CancellationToken.None)
        ?? System.Threading.Tasks.Task.CompletedTask;

    /// <summary>
    /// Keeps the party column in step: asks for the list once on entering, puts up whoever is asking us, shows the list
    /// with each member's health, and offers 파티 초대 only for a person who could join.
    /// </summary>
    private void KeepParty(double delta)
    {
        PlaceParty();
        KeepBot();

        if (_server is not { } server)
        {
            // --party-preview: 서버 없이 파티원 다섯(+ 나)을 지어 파티원 칸을 그려 본다 — 사진·배치 검사용.
            if (Main.PartyPreview)
            {
                string[] names = ["나", "가나다라마바", "검객", "궁수아이디", "도사", "치유사"];
                // 숫자(종류 6 꼬리)가 있는 칸과, 옛 서버처럼 %만 있는 칸(치유사)을 함께 그려 본다.
                _party.Show(new PartyRoster([.. names.Select((name, at) => new PartyMember(name, at == 1))]), "나", name =>
                {
                    // 도사는 체력 낮음(빨강), 궁수아이디는 쓰러짐(회색)으로 — 타일 색 규칙을 한눈에.
                    int health = name switch { "도사" => 10, "궁수아이디" => 0, _ => 90 - (name.Length * 9) };
                    int mana = 70 - (name.Length * 7), most = 400 + (name.Length * 150);
                    VitalNumbers? numbers = name == "치유사" ? null
                        : name == "가나다라마바" ? new VitalNumbers(12345, 23456, 4321, 9876)
                        : new VitalNumbers(most * health / 100, most, most * mana / 200, most / 2);

                    return new MemberLook(health, mana, StatusBadges.OfIcons(name.Length % 2 == 0 ? [11, 52] : [82]), numbers);
                });
            }

            return;
        }

        if (!_rosterAsked && server.State is not null && server.Self is not null)
        {
            _rosterAsked = true;
            Main.Fire(server.AskProfileAsync(System.Threading.CancellationToken.None));
        }

        while (server.TakeAsk(out string? asker))
        {
            _party.Ask(asker);
            Route(new Notice(MessageChannel.Party, MessagePlace.LogOnly, $"{asker}님이 파티에 초대합니다.", string.Empty));
        }

        string self = server.Self?.Name ?? string.Empty;
        PartyRoster roster = server.Roster;

        // 서버가 1초마다 보내는 그룹원 체력·마력 %·상태(0x5E 종류 6, 이름으로 짝짓는다 — 멀리 있어도). 아직 없으면 보이는 이의
        // 체력바 %(0x13)만.
        _party.Show(roster, self, name => server.MemberStatus(name) is { } told
            ? new MemberLook(told.HealthPercent, told.ManaPercent, StatusBadges.OfIcons(told.Icons), server.MemberNumbers(told.Serial))
            : new MemberLook(server.Others.FirstOrDefault(other => other.Name == name) is { } seen ? server.Health(seen.Serial) : null, null, []));

        Character? picked = server.Others.FirstOrDefault(other => other.Serial == _world.Target);
        bool leading = !roster.Grouped || roster.Members.Any(member => member.Leader && member.Name == self);

        _party.CanInvite(picked is { Name.Length: > 0 } && leading &&
                         !roster.Members.Any(member => member.Name == picked.Name));

        RehearseParty(delta);
    }

    /// <summary>
    /// 봇 칸과 봇 장비창을 서버 소식(0x5E)에 맞춘다. 봇이 없으면 둘 다 숨긴다. <c>--bot-preview</c> 는 서버 없이 지어낸 봇으로
    /// 그려 본다(사진·배치 검사용), <c>--bot-gear</c> 는 창까지 연다.
    /// </summary>
    private void KeepBot()
    {
        CompanionTie? bot = _server?.Companion;
        CompanionKit? kit = _server?.CompanionKit;
        (int? health, int? mana) = BotKit.Bars(_server?.CompanionLife);
        VitalNumbers? numbers = _server?.CompanionNumbers;
        IReadOnlyList<InventoryItem> pack = _server?.Pack ?? [];

        if (bot is null && Main.BotPreview)
        {
            bot = new CompanionTie(1, "동료사제");
            (health, mana) = (72, 45);
            numbers = new VitalNumbers(655, 910, 322, 715);
            kit = new CompanionKit(
                [new WornItem(1, 33318, "홀리파나", "홀리파나", 3000, 3000), new WornItem(2, 32873, "레더로브", "레더로브", 2000, 2000)],
                [new CarriedItem("쿠룸", 32813, 4), new CarriedItem("마라디움", 32815, 2)]);
            pack =
            [
                new InventoryItem(1, 32900, 0, "홀리머큐리아", 0, 3000, 3000),
                new InventoryItem(2, 32878, 0, "맨틀", 0, 2500, 2500),
                new InventoryItem(3, 32813, 0, "쿠룸", 10, 0, 0),
                new InventoryItem(4, 32815, 0, "마라디움", 6, 0, 0),
            ];

            if (Main.BotGearOpen && !_botGearRehearsed)
            {
                _botGearRehearsed = true;
                SetWindow(GameWindow.BotGear, true);
                _botGear.Choose(1);
            }
        }

        IReadOnlyList<StatusBadge> botStatus = bot is null ? []
            : Main.BotPreview && _server is null ? [new StatusBadge(11, 100, 6, false), new StatusBadge(52, 40, 4, false), new StatusBadge(82, 8, 1, true)]
            : StatusBadges.Of([], _server?.StatusesOf(bot.Serial));
        _party.ShowBot(bot?.Name, health, mana, botStatus, numbers);

        if (bot is null)
        {
            if (_botGear.Visible)
            {
                SetWindow(GameWindow.BotGear, false);
            }

            return;
        }

        if (_botGear.Visible)
        {
            _botGear.Show(bot.Name, kit, pack);
        }
    }

    private bool _botGearRehearsed;

    /// <summary>Asks whoever is picked out to join. The server says nothing back to the asker, so this screen says it.</summary>
    private void Invite()
    {
        if (_server?.Others.FirstOrDefault(other => other.Serial == _world.Target) is not { Name.Length: > 0 } person)
        {
            return;
        }

        Main.Fire(_server.AskToGroupAsync(person.Name, System.Threading.CancellationToken.None));
        Route(new Notice(MessageChannel.Party, MessagePlace.Ticker, $"{person.Name}님을 파티에 초대했습니다.", string.Empty));
    }

    /// <summary>
    /// Stands the party column under the top row: at the left edge in portrait, beside the movement pad in landscape —
    /// there the pad and the lines over it reach up close to the top row. Read from where the pad really is, because
    /// the control row is capped and centred on a wide screen.
    /// </summary>
    private void PlaceParty()
    {
        float top = _topRow.GetGlobalRect().End.Y - _over.GetGlobalRect().Position.Y + Main.Gutter;
        Control members = _party.Members;
        Control leave = _party.Leave;
        const int gap = PartyColumn.TileGap;

        // 타일 격자: 화면 왼쪽 끝에 붙인다 — HUD 여백(틈 8)만큼 왼쪽으로 뺀다. 가로 아이폰은 노치 쪽 안전선까지만(SafeInsets 에는
        // 틈 8 이 들어 있어 뺀다). 세로로 쌓다가 방향판에 닿으면 2열(더 많으면 3열) 격자(사용자, 2026-09-27: 그리드 식).
        float edge = Main.SafeInsets.Left - Main.Gutter - _over.GetGlobalRect().Position.X;
        float floor = _necklaces.GetGlobalRect().Position.Y - _over.GetGlobalRect().Position.Y - Main.Gutter;
        int count = Math.Max(1, _party.TileCount);
        int rowsFit = Math.Max(1, (int)((floor - top + gap) / (PartyColumn.TileRow + gap)));
        int columns = (count + rowsFit - 1) / rowsFit;
        int rows = (count + columns - 1) / columns;

        members.OffsetLeft = edge;
        members.OffsetTop = top;
        members.OffsetRight = edge + (columns * PartyColumn.TileWide) + ((columns - 1) * gap);
        // 높이는 직접 센다 — 흐르는 칸은 폭이 바뀐 다음 프레임에야 제 높이를 다시 잰다.
        members.OffsetBottom = top + (rows * PartyColumn.TileRow) + ((rows - 1) * gap);

        // [나가기]: 격자 오른쪽 위 모서리에 붙는 작은 그림 단추(사용자, 2026-09-27: 크고 자리가 뜬금없다).
        leave.OffsetLeft = members.OffsetRight + gap;
        leave.OffsetTop = top;
        leave.OffsetRight = leave.OffsetLeft + PartyColumn.LeaveSide;
        leave.OffsetBottom = leave.OffsetTop + PartyColumn.LeaveSide;

        float below = !members.Visible ? top - Main.Gutter : leave.Visible ? Mathf.Max(members.OffsetBottom, leave.OffsetBottom) : members.OffsetBottom;

        // 파티 기둥(초대 단추·묻기): 세로는 그 아래, 가로는 방향판 오른쪽 옆 — 파티원 칸 줄 아래.
        _party.OffsetLeft = Main.Portrait
            ? 0
            : _pad.GetGlobalRect().End.X - _over.GetGlobalRect().Position.X + Main.Gutter;
        _party.OffsetRight = _party.OffsetLeft + PartyColumn.Wide;
        _party.OffsetTop = Main.Portrait ? below + Main.Gutter : Mathf.Max(top, members.Visible ? members.OffsetBottom + Main.Gutter : top);
    }

    /// <summary>Turns taking group requests on or off, then asks the profile again so the person button shows it.</summary>
    private async System.Threading.Tasks.Task ToggleGroup()
    {
        if (_server is null)
        {
            return;
        }

        await _server.ToggleGroupAsync(System.Threading.CancellationToken.None);
        await _server.AskProfileAsync(System.Threading.CancellationToken.None);
    }
}
