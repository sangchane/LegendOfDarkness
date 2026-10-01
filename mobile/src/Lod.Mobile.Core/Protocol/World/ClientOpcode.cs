namespace Lod.Mobile.Core.Protocol.World;

/// <summary>
/// 클라이언트가 보내는 신호 번호. 같은 번호가 오는 쪽(<see cref="ServerOpcode" />)에서는 다른 뜻일 수 있다 —
/// 방향만이 둘을 가른다.
/// </summary>
internal static class ClientOpcode
{
    public const byte Walk = 0x06;

    /// <summary>제자리에서 돈다. 오는 <see cref="ServerOpcode.Turned" /> 와 번호가 같다.</summary>
    public const byte Turn = 0x11;

    /// <summary>대화 답장. 오는 <see cref="ServerOpcode.Status" />(상태 이상 아이콘)와 번호가 같다.</summary>
    public const byte Answer = 0x3A;

    public const byte Refresh = 0x38;

    /// <summary>
    /// 월드맵에서 곳을 고른다. 오는 <see cref="ServerOpcode.Cooldown" /> 와 번호가 같지만 나가는 것은 이쪽이다.
    /// </summary>
    public const byte ChooseField = 0x3F;

    /// <summary>월드맵을 열어 달라는 말. 원작 클라이언트는 0x80 넘는 명령을 보내지 않으므로 이 번호는 우리 것이다.</summary>
    public const byte OpenField = 0xF0;

    /// <summary>동료 봇을 불러 달라·보내 달라(우리 확장 0xF1 — <see cref="World.Companion" />).</summary>
    public const byte Companion = 0xF1;

    /// <summary>상점 일괄 거래(우리 확장 0xF2).</summary>
    public const byte BulkTrade = 0xF2;

    /// <summary>서버 심장박동(<see cref="ServerOpcode.Heartbeat" />)에 대한 답.</summary>
    public const byte HeartbeatReply = 0x45;

    /// <summary>나가겠다는 말(ClientFormat0B). 종류 1 이면 서버가 캐릭터를 곧장 세계에서 빼고 0x4C 로 답한다(LeaveGame).</summary>
    public const byte Exit = 0x0B;

    public const byte Attack = 0x13;

    /// <summary>말하기. 오는 <see cref="ServerOpcode.Remove" /> 와 번호가 같다.</summary>
    public const byte Talk = 0x0E;

    public const byte Use = 0x1C;

    /// <summary>
    /// 땅에 내려놓기. 오는 <see cref="ServerOpcode.Vitals" />(우리 수치)와 번호가 같다 — 어느 쪽으로 가는지만이 둘을 가른다.
    /// </summary>
    public const byte Drop = 0x08;

    public const byte DropGold = 0x24;

    /// <summary>소지품 칸 옮기기. 오는 <see cref="ServerOpcode.Sequence" /> 와 번호가 같다.</summary>
    public const byte Move = 0x30;

    /// <summary>Taking something off. One byte: the worn place, the same number 0x37 names.</summary>
    public const byte TakeOff = 0x44;

    public const byte Click = 0x43;

    /// <summary>Spending one of the points a level handed out. One byte: which attribute.</summary>
    public const byte Raise = 0x47;

    /// <summary>Picking something up off the floor. A pack slot to aim at, then the tile.</summary>
    public const byte PickUp = 0x07;

    public const byte UseSkill = 0x3E;

    public const byte UseSpell = 0x0F;

    /// <summary>그룹 청하기·받아들이기(나가는 쪽). 오는 <see cref="ServerOpcode.WorldMap" /> 와 번호가 같다.</summary>
    public const byte Group = 0x2E;

    /// <summary>내 프로필을 달라는 말. 오는 <see cref="ServerOpcode.RemoveSkill" /> 와 번호가 같다.</summary>
    public const byte ProfileRequest = 0x2D;

    /// <summary>그룹 받기 켜고 끄기. 몸 없이 번호만 — 서버가 지금 상태를 뒤집는다(Hades <c>Format2FHandler</c>).</summary>
    public const byte GroupToggle = 0x2F;

    /// <summary>귓속말. 받는 이 이름이 "!" 이면 그룹말이다.</summary>
    public const byte Whisper = 0x19;
}
