using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>
/// 서버가 보내는 신호 번호. 같은 번호가 나가는 쪽(<see cref="ClientOpcode" />)에서는 다른 뜻일 수 있다.
/// </summary>
internal static class ServerOpcode
{
    public const byte MapChanged = 0x15;

    /// <summary>월드맵 창이 열렸다는 알림(ServerFormat2E).</summary>
    public const byte WorldMap = 0x2E;

    public const byte Location = 0x04;

    public const byte OwnSerial = 0x05;

    public const byte DisplayCharacter = 0x33;

    public const byte CreatureWalked = 0x0C;

    /// <summary>Somebody turning on the spot. The same number as <see cref="ClientOpcode.Turn" />, coming the other way.</summary>
    public const byte Turned = 0x11;

    public const byte Remove = 0x0E;

    public const byte AddToPack = 0x0F;

    public const byte ShowCreatures = 0x07;

    /// <summary>
    /// 상태 이상 아이콘. 나가는 0x3A(대화 답장)와 번호가 같지만 오는 것은 이쪽이다 — 걸린 본인에게만 온다.
    /// </summary>
    public const byte Status = 0x3A;

    /// <summary>우리 확장 — 남에게 걸린 것(0x5C). 원작은 당사자에게만 0x3A 로 알린다.</summary>
    public const byte SeenStatus = 0x5C;

    /// <summary>우리 확장 — 한 방이 뺀 만큼·회복이 채운 만큼(0x5D). 원작은 0x13 백분율뿐이다.</summary>
    public const byte Figure = 0x5D;

    public const byte Health = 0x13;

    public const byte Spoken = 0x0A;

    /// <summary>What somebody near us said, as opposed to what the server itself says (<see cref="Spoken" />).</summary>
    public const byte Speech = 0x0D;

    /// <summary>How long until a skill or spell may be used again.</summary>
    public const byte Cooldown = 0x3F;

    /// <summary>동료 사이(우리 확장 0x5E) — 봇에게는 주인, 사람에게는 동료.</summary>
    public const byte CompanionTie = 0x5E;

    /// <summary>
    /// 서버의 심장박동(ServerFormat3B, <c>PingComponent</c> 가 PingInterval 마다). 원작 클라이언트는 0x45 로 답한다 —
    /// 답이 끊긴 접속은 서버가 세계에서 뺀다(GameServer.UpdateClients). 소켓을 쥔 채 멈춘 앱(iOS 뒤로 감)도 이것으로 빠진다.
    /// </summary>
    public const byte Heartbeat = 0x3B;

    /// <summary>나가기(<see cref="ClientOpcode.Exit" />)에 대한 서버의 답.</summary>
    public const byte Exited = 0x4C;

    public const byte BodyMotion = 0x1A;

    public const byte Animation = 0x29;

    public const byte Sound = 0x19;

    /// <summary>
    /// Our own numbers coming back. The same number as <see cref="ClientOpcode.Drop" /> — which way it is going
    /// is the only thing that tells them apart.
    /// </summary>
    public const byte Vitals = 0x08;

    public const byte TakeFromPack = 0x10;

    public const byte Dialogue = 0x2F;

    /// <summary>
    /// A window the server walks somebody through, or the word that shuts any window. The same number as
    /// <see cref="ClientOpcode.Move" />, coming the other way.
    /// </summary>
    public const byte Sequence = 0x30;

    public const byte Worn = 0x37;

    public const byte TookOff = 0x38;

    public const byte AddSkill = 0x2C;

    public const byte AddSpell = 0x17;

    public const byte RemoveSkill = 0x2D;

    public const byte RemoveSpell = 0x18;

    /// <summary>누가 그룹을 청한다(오는 쪽, <see cref="Party.ReadAsk" />).</summary>
    public const byte GroupAsk = 0x63;

    /// <summary>내 프로필 — 그 안에 그룹 목록이 있다(<see cref="Party.ReadRoster" />).</summary>
    public const byte Profile = 0x39;

    /// <summary>남의 장비창 — 사람을 누르면(0x43) 서버가 보낸다(Hades <c>ServerFormat34</c>).</summary>
    public const byte OtherProfile = 0x34;
}
