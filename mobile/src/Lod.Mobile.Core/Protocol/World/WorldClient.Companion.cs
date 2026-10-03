using System.Buffers.Binary;
using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>월드 연결 — 봇 동료·파티원 상태와 봇에게 보내는 요청.</summary>
public sealed partial class WorldClient
{
    /// <summary>Who is in our group, as the profile last said. Alone until asked for (<see cref="AskProfileAsync" />).</summary>
    public PartyRoster Roster => _roster;

    /// <summary>How many times the group list has come — to tell a fresh one from the same one.</summary>
    public int RosterCount => _rosterCount;

    /// <summary>Takes the next person asking us to join their group (0x63), oldest first.</summary>
    public bool TakeAsk([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? name) => _asks.TryDequeue(out name);

    /// <summary>봇일 때 — 서버가 정해 준 주인(0x5E 종류 1). 없으면 null.</summary>
    public CompanionTie? Master => _master;

    /// <summary>사람일 때 — 지금 함께 있는 동료 봇(0x5E 종류 2). 없으면 null.</summary>
    public CompanionTie? Companion => _companion;

    /// <summary>봇일 때 — 서버가 1초마다 알리는, 주인·자기에게 걸린 것(0x5E 종류 3). 알림이 아직 없으면 null.</summary>
    public IReadOnlyList<CompanionStatus>? StatusesOf(uint serial) => _statuses.TryGetValue(serial, out var listed) ? listed : null;

    /// <summary>사람일 때 — 봇의 체력·마력 %(0x5E 종류 4).</summary>
    public CompanionLife? CompanionLife => _companionLife;

    /// <summary>그룹원의 체력·마력 %·상태 그림(0x5E 종류 6, 서버가 1초마다). 아직 없거나 그룹이 끝났으면 null.</summary>
    public PartyMemberStatus? MemberStatus(uint serial) => _members.TryGetValue(serial, out PartyMemberStatus? member) ? member : null;

    /// <summary>봇의 체력·마력 숫자(0x5E 종류 4 꼬리). 옛 서버면 null — 그때는 %만.</summary>
    public VitalNumbers? CompanionNumbers => _companionNumbers;

    /// <summary>그룹원의 체력·마력 숫자(0x5E 종류 6 꼬리). 옛 서버거나 아직 없으면 null — 그때는 %만.</summary>
    public VitalNumbers? MemberNumbers(uint serial) => _memberNumbers.TryGetValue(serial, out VitalNumbers? numbers) ? numbers : null;

    /// <summary>그룹원 이름으로 — 멀리 있어 보이지 않는 그룹원도 목록(0x39)의 이름과 짝짓는다.</summary>
    public PartyMemberStatus? MemberStatus(string name) =>
        _members.Values.FirstOrDefault(member => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>사람일 때 — 봇이 입은 것과 봇 가방의 포션(0x5E 종류 5).</summary>
    public CompanionKit? CompanionKit => _companionKit;

    /// <summary>봇 장비 안내를 받은 횟수 — 바뀌었는지 보려고.</summary>
    public int CompanionKitCount => _companionKitCount;

    /// <summary>동료 봇을 부른다(0xF1 1). 결과는 서버 알림(0x0A)과 0x5E 로 온다.</summary>
    public Task CallCompanionAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.Call(), cancellationToken);

    /// <summary>내 가방 한 칸을 봇에게(0xF1 2) — 장비면 입히고 포션이면 개수만큼(0 은 다).</summary>
    public Task GiveToCompanionAsync(int slot, int count, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.Give(slot, count), cancellationToken);

    /// <summary>봇의 장비 한 자리를 내 가방으로(0xF1 3).</summary>
    public Task TakeOffCompanionAsync(int place, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.TakeOff(place), cancellationToken);

    /// <summary>내 코마디움으로 혼수인 봇을 깨운다(0xF1 4).</summary>
    public Task WakeCompanionAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.Wake(), cancellationToken);

    /// <summary>봇일 때 — 혼수인 주인을 깨운다(0xF1 5, 바로 옆에서).</summary>
    public Task WakeMasterAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.WakeMaster(), cancellationToken);

    /// <summary>봇 탭 「마법사」 — 봇이 저주·나르콜리를 쓸지(0xF1 6). 서버가 주인 이름으로 기억해 봇에게 옮긴다.</summary>
    public Task SendCompanionMagicAsync(bool curse, bool sleep, CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.Magic(curse, sleep), cancellationToken);

    /// <summary>동료 봇을 보낸다(0xF1 0).</summary>
    public Task DismissCompanionAsync(CancellationToken cancellationToken) =>
        Send(ClientOpcode.Companion, World.Companion.Dismiss(), cancellationToken);
}
