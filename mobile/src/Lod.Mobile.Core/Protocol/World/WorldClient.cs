using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>
/// Talking to the server once a character is in the world.
/// </summary>
/// <remarks>
/// A walk is told, not asked. The server says nothing back when it allows one — it only tells the people
/// nearby — so the client moves its own figure and waits to be corrected. When a step is refused the server
/// sends the character's real tile back, and that is what to snap to.
///
/// Because the server talks whenever it likes, reading has to be a loop rather than a question and answer:
/// <see cref="PumpAsync" /> keeps <see cref="State" /> up to date and passes over everything it does not
/// yet understand.
/// </remarks>
public sealed partial class WorldClient(WorldSession session) : IDisposable
{
    private byte _ordinal;

    // 심장박동 답은 받는 실에서, 나머지는 화면 실에서 보낸다. 번호 매기기와 쓰기를 한 줄로 세워 프레임이 섞이지 않게 한다.
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private byte _step;
    private int _disposed;
    private int _disposeAttempts;

    // 지금 보이는 세계(맵·자리·나·남·괴물)와 들은 말. 받는 실이 쓰고 그리는 실이 읽는다.
    private readonly WorldState _world = new();
    private readonly ChatLog _chat = new();

    // Keyed by the slot the server puts each thing in, which is how it refers to them afterwards.
    private readonly ConcurrentDictionary<int, InventoryItem> _pack = new();

    /// <summary>무엇을 걸치고 있는지, 걸친 자리 번호를 열쇠로.</summary>
    private readonly ConcurrentDictionary<int, WornItem> _worn = new();

    // Learned abilities arrive one at a time on entry, just like carried and worn items.
    private readonly ConcurrentDictionary<int, LearnedSkill> _skills = new();
    private readonly ConcurrentDictionary<int, LearnedSpell> _spells = new();

    // How hurt each of them is, out of a hundred. The server never says more than that about somebody else.
    private readonly ConcurrentDictionary<uint, int> _health = new();
    private readonly ConcurrentDictionary<int, Ailment> _ailing = new();

    // Every report in the order it came, because the latest one is not enough to check a blow against a
    // formula: two blows landing between two reads would leave only the second one's figure behind.
    private readonly ConcurrentQueue<(uint Serial, int Left)> _hurts = new();

    private volatile Vitals? _vitals;

    // Drained by whoever is drawing, because a motion is a moment rather than a state.
    private readonly ConcurrentQueue<Motion> _motions = new();
    private readonly ConcurrentQueue<Effect> _effects = new();
    private readonly ConcurrentQueue<int> _sounds = new();

    // Every figure in the order it came (0x5D), for the floating numbers.
    private readonly ConcurrentQueue<Figure> _figures = new();

    // 누가 마지막으로 누구를 쳤나(0x5D 의 Source) — 자동 사냥이 남이 치는 괴물을 피하려고 본다.
    private readonly ConcurrentDictionary<uint, (uint Source, DateTime At)> _struck = new();
    private readonly ConcurrentQueue<int> _songs = new();

    private volatile WorldMapInfo? _field;
    private volatile int _fieldShown;

    private volatile string? _broke;
    private volatile int _ignored;

    private int? _path;
    private bool? _groupOpen;
    private OtherProfile? _seen;

    // 언제 다시 쓸 수 있나. 받는 쪽과 그리는 쪽이 다른 실이라 한꺼번에 읽고 쓰는 사전을 쓴다.
    private readonly ConcurrentDictionary<(bool Skill, int Slot), DateTime> _cooling = new();

    // 그룹을 청한 사람들, 온 차례대로. 그리는 쪽이 하나씩 꺼내 묻는다.
    private readonly ConcurrentQueue<string> _asks = new();
    private CompanionTie? _master;
    private CompanionTie? _companion;
    private readonly ConcurrentDictionary<uint, IReadOnlyList<CompanionStatus>> _statuses = new();
    private CompanionLife? _companionLife;

    // 그룹원마다 마지막으로 온 체력·마력 %·상태 그림(0x5E 종류 6).
    private readonly ConcurrentDictionary<uint, PartyMemberStatus> _members = new();

    // 그룹원·봇의 체력·마력 숫자(종류 6·4 끝의 꼬리, 2026-09-27). 옛 서버면 비어 있다.
    private readonly ConcurrentDictionary<uint, VitalNumbers> _memberNumbers = new();
    private VitalNumbers? _companionNumbers;
    private CompanionKit? _companionKit;
    private int _companionKitCount;
    private volatile PartyRoster _roster = PartyRoster.Alone;
    private volatile int _rosterCount;

    /// <summary>Where the server last said we are, or null until it has said so.</summary>
    public WorldEntry? State => _world.Entry;

    /// <summary>
    /// How many times the server has stated our position. It does that on entry, on a refresh, and when it
    /// refuses a step — so a rise the client did not ask for means a walk was turned down.
    /// </summary>
    public int PositionReports => _world.Reports;

    /// <summary>
    /// The serial the server uses for our own character in the world. It is not the number the login
    /// server handed over — that one only opened the door.
    /// </summary>
    public uint Serial => _world.Serial;

    /// <summary>
    /// Our own character as the server describes it — what we are wearing and what it calls us. The server
    /// shows us to ourselves like anybody else, so this is the same packet everyone else arrives in.
    /// </summary>
    public Character? Self => _world.Self;

    /// <summary>
    /// How much of somebody's health is left, out of a hundred, or nothing if the server has not said. It
    /// only speaks about this when something is struck, so an untouched monster has no answer here.
    /// </summary>
    public int? Health(uint serial) => _health.TryGetValue(serial, out int left) ? left : null;

    /// <summary>
    /// What is on us right now — a curse, poison, sleep. The server names each with a picture number and says
    /// roughly how long is left; it only ever tells the one afflicted, so this is our own list and nobody else's.
    /// </summary>
    public IReadOnlyCollection<Ailment> Ailments => (IReadOnlyCollection<Ailment>)_ailing.Values;

    /// <summary>
    /// What is on somebody else we can see — another player or a monster — as our server tells bystanders (0x5C).
    /// Empty for anybody it has said nothing about.
    /// </summary>
    public IReadOnlyList<SeenAilment> AilmentsOf(uint serial) =>
        [.. _world.SeenAiling.Values.Where(one => one.Serial == serial)];

    /// <summary>
    /// Every health report the server has sent, oldest first. One report is one blow landing, so this is
    /// the record a test needs when the question is how much a single blow took off. Kept up to <see cref="QueueKept" />.
    /// </summary>
    public IReadOnlyList<(uint Serial, int Left)> Hurts => [.. _hurts];

    /// <summary>
    /// Our own character's numbers, or nothing until the server has stated them — which it does once, in
    /// full, as the character enters, and in pieces after that.
    /// </summary>
    public Vitals? Vitals => _vitals;

    /// <summary>
    /// 월드맵 창이 열려 있으면 그 내용. 열려 있는 동안 서버는 <see cref="ChooseFieldAsync"/> 말고는
    /// 이 접속의 패킷을 모두 버린다(`NetworkServer.cs:141`) — 걸음도 말도 닿지 않는다.
    /// </summary>
    public WorldMapInfo? Field => _field;

    /// <summary>월드맵 안내가 몇 번 왔나. 늘어나면 서버가 <b>새</b> 창을 보낸 것이다 — 묵은 것과 가리는 데 쓴다.</summary>
    public int FieldShown => _fieldShown;

    /// <summary>The last thing the server said in words — a refused blow, a greeting, a warning.</summary>
    public string Said => _chat.Said;

    /// <summary>My class as the profile (0x39) last said — Hades <c>Class</c> number (5 무도가 …); null until asked for.</summary>
    public int? Path => _path;

    /// <summary>Whether we take group requests, as the profile (0x39) last said; null until asked for.</summary>
    public bool? GroupOpen => _groupOpen;

    /// <summary>Takes the equipment window the server last sent for somebody we pressed on (0x34), once.</summary>
    public OtherProfile? TakeSeen() => Interlocked.Exchange(ref _seen, null);

    /// <summary>
    /// Takes the next line the server said (0x0A) with its type byte (Hades <c>ServerFormat0A.MsgType</c>), oldest
    /// first, so a screen can sort it (<see cref="MessageSort.FromServer" />) without missing any.
    /// </summary>
    public bool TakeTold(out byte type, out string text) => _chat.TakeTold(out type, out text);

    /// <summary>The last lines anybody near us said, oldest first.</summary>
    public IReadOnlyList<Spoken> Heard => _chat.Heard;

    /// <summary>How many lines have been heard in all, so a screen can tell a new one from the same one again.</summary>
    public int HeardCount => _chat.HeardCount;

    /// <summary>How many times it has spoken, so a reader can tell a repeat from a new line.</summary>
    public int SaidCount => _chat.SaidCount;

    /// <summary>Monsters and merchants the server has shown us, by serial.</summary>
    public IReadOnlyCollection<Creature> Creatures => (IReadOnlyCollection<Creature>)_world.Creatures.Values;

    /// <summary>The window the last tapped NPC opened, or null if none has. (Not <c>Said</c> — that is
    /// chat overheard in the map; this is a conversation we started by tapping.)</summary>
    public Dialogue? Talking { get; private set; }

    /// <summary>How many windows have opened or shut, so a reader can tell the same words again from nothing new.</summary>
    public int TalkCount => _talkCount;

    private volatile int _talkCount;

    /// <summary>
    /// The last packet the pump knew but could not follow through — a window sequence this client does not draw yet,
    /// or a window cut short under its words — and why, or empty while there has been none. Commands the pump does
    /// not know at all are not noted here.
    /// </summary>
    public string Unread => _unread;

    /// <summary>How many such packets there have been, so a reader can tell a repeat from a new one.</summary>
    public int UnreadCount => _unreadCount;

    private volatile string _unread = string.Empty;
    private volatile int _unreadCount;

    /// <summary>
    /// 서버에서 마지막으로 무엇이든 받은 때(UTC). 서버는 10초마다 심장박동(0x3B)을 보내므로, 이것이 오래되면 접속이 죽은 것이다 —
    /// 소켓이 닫히지 않은 채 조용해진 접속은 받기 루프가 끝나지 않아 달리 알 길이 없다(동료 봇 기록, 2026-09-27).
    /// </summary>
    public DateTime LastHeard => new(Interlocked.Read(ref _lastHeardTicks), DateTimeKind.Utc);

    private long _lastHeardTicks = DateTime.UtcNow.Ticks;

    /// <summary>What we are carrying, as the server has told us, in slot order.</summary>
    public IReadOnlyList<InventoryItem> Pack => [.. _pack.Values.OrderBy(item => item.Slot)];

    /// <summary>What the character has on, in the order the places are numbered.</summary>
    public IReadOnlyList<WornItem> Worn => [.. _worn.Values.OrderBy(item => item.Slot)];

    /// <summary>Learned techniques, in the pane order the server owns.</summary>
    public IReadOnlyList<LearnedSkill> Skills => [.. _skills.Values.OrderBy(skill => skill.Slot)];

    /// <summary>Learned spells, in the pane order the server owns.</summary>
    public IReadOnlyList<LearnedSpell> Spells => [.. _spells.Values.OrderBy(spell => spell.Slot)];

    /// <summary>Everyone else the server has shown us, by serial. Our own character is not in here.</summary>
    public IReadOnlyCollection<Character> Others => (IReadOnlyCollection<Character>)_world.Others.Values;

    /// <summary>The direction bytes the server walks by: 0 north, 1 east, 2 south, 3 west.</summary>
    public static byte ToServer(Direction direction) => direction switch
    {
        Direction.North => 0,
        Direction.East => 1,
        Direction.South => 2,
        Direction.West => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "알 수 없는 방향입니다.")
    };

    /// <summary>
    /// Why the listening stopped, or nothing while it is still going. A screen shows this — a listener that dies
    /// silently leaves the character frozen with everything else looking fine (2026-09-18 조사).
    /// </summary>
    public string? Broke => _broke;

    /// <summary>Whether this world's session has already been released.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal int DisposeAttempts => Volatile.Read(ref _disposeAttempts);

    /// <summary>How many packets were too short to read. A rise means the server and this client disagree.</summary>
    public int Ignored => _ignored;

    /// <summary>Reads until the connection ends or the caller stops asking.</summary>
    public async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Listen(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 나가는 길이다 — 알릴 것이 없다.
        }
        catch (Exception) when (IsDisposed)
        {
            // 로그아웃은 먼저 소켓을 닫고 다음 프레임에 화면을 거둔다. 닫힌 소켓에서 깨어난
            // 받기 루프는 고장이 아니라 그 정상 종료 경로다.
        }
        catch (Exception stopped)
        {
            _broke = stopped.Message;
            Diagnostic?.Invoke(stopped.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// 받기 루프. 아는 번호면 몸을 한 번 해독해 갈래(<see cref="HandlerFor" />)에 넘기고, 모르는 번호는 해독하지 않고 넘긴다.
    /// </summary>
    private async Task Listen(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PacketFrame frame = await session.Connection.ReceiveAsync(cancellationToken);
            Interlocked.Exchange(ref _lastHeardTicks, DateTime.UtcNow.Ticks);

            try
            {
                switch (frame.Command)
                {
                    case ServerOpcode.Exited:
                        _exited.TrySetResult();
                        continue;

                    case ServerOpcode.Heartbeat:
                        await Send(ClientOpcode.HeartbeatReply, HadesCipher.DecodeSecured(frame, session.Parameters), cancellationToken);
                        continue;
                }

                if (HandlerFor(frame.Command) is { } handle)
                {
                    handle(HadesCipher.DecodeSecured(frame, session.Parameters));
                }
            }
            catch (Exception unreadable) when (unreadable is ProtocolException or ArgumentException or IndexOutOfRangeException)
            {
                // 갈래마다 따로 잡지 않은 패킷도 여기서 막는다 — 하나가 어긋났다고 받기 루프(접속)가 죽지 않게.
                NoteUnread($"0x{frame.Command:X2}: {unreadable.Message}");
                Diagnostic?.Invoke("packet_unreadable");
                _ignored++;
            }
        }
    }

    /// <summary>Releases the owned world session. Safe when both logout and tree teardown arrive.</summary>
    public void Dispose()
    {
        Interlocked.Increment(ref _disposeAttempts);

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        session.Dispose();
    }

    /// <summary>Keeps a packet that was passed over where <see cref="Unread" /> can show it, rather than losing it without a trace.</summary>
    private void NoteUnread(string why)
    {
        _unread = why;
        _unreadCount++;
    }

    /// <summary>Seconds left before one slot may be used again, or none when it is ready.</summary>
    public int CoolingFor(bool skill, int slot) =>
        _cooling.TryGetValue((skill, slot), out DateTime ready) ? Cooldown.Left(ready, DateTime.UtcNow) : 0;
}
