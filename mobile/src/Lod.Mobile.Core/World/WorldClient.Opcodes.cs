using System.Buffers.Binary;
using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.World;

/// <summary>월드 연결 — 신호 번호 표(보내는 것·받는 것). 같은 번호가 방향에 따라 다른 이름일 수 있다.</summary>
public sealed partial class WorldClient
{
    private const byte WalkCommand = 0x06;

    private const byte TurnCommand = 0x11;

    private const byte AnswerCommand = 0x3A;

    private const byte RefreshCommand = 0x38;

    private const byte MapChangedCommand = 0x15;

    /// <summary>월드맵 창이 열렸다는 알림(ServerFormat2E).</summary>
    private const byte WorldMapCommand = 0x2E;

    private const byte LocationCommand = 0x04;

    private const byte OwnSerialCommand = 0x05;

    private const byte DisplayCharacterCommand = 0x33;

    private const byte CreatureWalkedCommand = 0x0C;

    /// <summary>Somebody turning on the spot. The same number as <see cref="TurnCommand" />, coming the other way.</summary>
    private const byte TurnedCommand = 0x11;

    private const byte RemoveCommand = 0x0E;

    private const byte AddToPackCommand = 0x0F;

    private const byte ShowCreaturesCommand = 0x07;

    /// <summary>
    /// 상태 이상 아이콘. 나가는 0x3A(대화 답장)와 번호가 같지만 오는 것은 이쪽이다 — 걸린 본인에게만 온다.
    /// </summary>
    private const byte StatusCommand = 0x3A;

    // 우리 확장 — 남에게 걸린 것(0x5C). 원작은 당사자에게만 0x3A 로 알린다.
    private const byte SeenStatusCommand = 0x5C;

    // 우리 확장 — 한 방이 뺀 만큼·회복이 채운 만큼(0x5D). 원작은 0x13 백분율뿐이다.
    private const byte FigureCommand = 0x5D;

    private const byte AttackCommand = 0x13;

    private const byte HealthCommand = 0x13;

    private const byte SpokenCommand = 0x0A;

    /// <summary>What somebody near us said, as opposed to what the server itself says (<see cref="SpokenCommand" />).</summary>
    private const byte SpeechCommand = 0x0D;

    /// <summary>How long until a skill or spell may be used again.</summary>
    private const byte CooldownCommand = 0x3F;

    /// <summary>
    /// 월드맵에서 곳을 고른다. 오는 <see cref="CooldownCommand" /> 와 번호가 같지만 나가는 것은 이쪽이다.
    /// </summary>
    private const byte ChooseFieldCommand = 0x3F;

    /// <summary>월드맵을 열어 달라는 말. 원작 클라이언트는 0x80 넘는 명령을 보내지 않으므로 이 번호는 우리 것이다.</summary>
    private const byte OpenFieldCommand = 0xF0;

    /// <summary>동료 봇을 불러 달라·보내 달라(우리 확장 0xF1 — <see cref="Companion" />).</summary>
    private const byte CompanionCommand = 0xF1;

    /// <summary>상점 일괄 거래(우리 확장 0xF2).</summary>
    private const byte BulkTradeCommand = 0xF2;

    /// <summary>동료 사이(우리 확장 0x5E) — 봇에게는 주인, 사람에게는 동료.</summary>
    private const byte CompanionTieCommand = 0x5E;

    /// <summary>
    /// 서버의 심장박동(ServerFormat3B, <c>PingComponent</c> 가 PingInterval 마다). 원작 클라이언트는 0x45 로 답한다 —
    /// 답이 끊긴 접속은 서버가 세계에서 뺀다(GameServer.UpdateClients). 소켓을 쥔 채 멈춘 앱(iOS 뒤로 감)도 이것으로 빠진다.
    /// </summary>
    private const byte HeartbeatCommand = 0x3B;

    private const byte HeartbeatReplyCommand = 0x45;

    /// <summary>나가겠다는 말(ClientFormat0B). 종류 1 이면 서버가 캐릭터를 곧장 세계에서 빼고 0x4C 로 답한다(LeaveGame).</summary>
    private const byte ExitCommand = 0x0B;

    private const byte ExitedCommand = 0x4C;

    /// <summary>로그아웃이 서버의 0x4C 를 기다리는 가장 긴 시간. 오지 않아도 소켓을 닫으면 서버는 곧 뺀다.</summary>
    public static readonly TimeSpan LogOutWait = TimeSpan.FromSeconds(1);

    private const byte BodyMotionCommand = 0x1A;

    private const byte AnimationCommand = 0x29;

    private const byte SoundCommand = 0x19;

    private const byte TalkCommand = 0x0E;

    private const byte UseCommand = 0x1C;

    private const byte DropCommand = 0x08;

    /// <summary>
    /// Our own numbers coming back. The same number as <see cref="DropCommand" /> — which way it is going
    /// is the only thing that tells them apart.
    /// </summary>
    private const byte VitalsCommand = 0x08;

    private const byte DropGoldCommand = 0x24;

    private const byte TakeFromPackCommand = 0x10;

    private const byte MoveCommand = 0x30;

    /// <summary>Taking something off. One byte: the worn place, the same number 0x37 names.</summary>
    private const byte TakeOffCommand = 0x44;

    private const byte ClickCommand = 0x43;

    private const byte DialogueCommand = 0x2F;

    /// <summary>
    /// A window the server walks somebody through, or the word that shuts any window. The same number as
    /// <see cref="MoveCommand" />, coming the other way.
    /// </summary>
    private const byte SequenceCommand = 0x30;

    private const byte ClickBySerial = 0x01;

    /// <summary>Spending one of the points a level handed out. One byte: which attribute.</summary>
    private const byte RaiseCommand = 0x47;

    /// <summary>Picking something up off the floor. A pack slot to aim at, then the tile.</summary>
    private const byte PickUpCommand = 0x07;

    /// <summary>
    /// Which pack slot to put a picked-up thing in. The server finds a free one itself
    /// (<c>Format07Handler</c> hands the item to <c>GiveTo</c>, which does not read this), so nothing is
    /// gained by choosing — and choosing wrongly would be a way to lose things.
    /// </summary>
    private const byte AnyPackSlot = 0;

    /// <summary>소지품 칸을 가리키는 번호. 주문·기술 칸도 같은 명령을 쓴다.</summary>
    private const byte InventoryPane = 0x00;

    private const byte WornCommand = 0x37;

    private const byte TookOffCommand = 0x38;

    private const byte AddSkillCommand = 0x2C;

    private const byte AddSpellCommand = 0x17;

    private const byte RemoveSkillCommand = 0x2D;

    private const byte RemoveSpellCommand = 0x18;

    private const byte UseSkillCommand = 0x3E;

    private const byte UseSpellCommand = 0x0F;

    /// <summary>그룹 청하기·받아들이기(나가는 쪽). 오는 <see cref="WorldMapCommand" /> 와 번호가 같다.</summary>
    private const byte GroupCommand = 0x2E;

    /// <summary>누가 그룹을 청한다(오는 쪽, <see cref="Party.ReadAsk" />).</summary>
    private const byte GroupAskCommand = 0x63;

    /// <summary>내 프로필을 달라는 말. 오는 <see cref="RemoveSkillCommand" /> 와 번호가 같다.</summary>
    private const byte ProfileRequestCommand = 0x2D;

    /// <summary>내 프로필 — 그 안에 그룹 목록이 있다(<see cref="Party.ReadRoster" />).</summary>
    private const byte ProfileCommand = 0x39;

    /// <summary>남의 장비창 — 사람을 누르면(0x43) 서버가 보낸다(Hades <c>ServerFormat34</c>).</summary>
    private const byte OtherProfileCommand = 0x34;

    /// <summary>그룹 받기 켜고 끄기. 몸 없이 번호만 — 서버가 지금 상태를 뒤집는다(Hades <c>Format2FHandler</c>).</summary>
    private const byte GroupToggleCommand = 0x2F;

    /// <summary>귓속말. 받는 이 이름이 "!" 이면 그룹말이다.</summary>
    private const byte WhisperCommand = 0x19;
}
