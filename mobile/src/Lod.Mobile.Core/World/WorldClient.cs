using System.Buffers.Binary;
using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.World;

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

    // Written by the pump, read by whoever is drawing. A whole state at once, so a reader never sees a map
    // from one moment and a tile from another.
    private volatile WorldEntry? _state;
    private volatile int _reports;
    private volatile uint _serial;

    // Keyed by serial, which is the only name the server gives them at first.
    private readonly ConcurrentDictionary<uint, Character> _others = new();

    private volatile Character? _self;

    // Keyed by the slot the server puts each thing in, which is how it refers to them afterwards.
    private readonly ConcurrentDictionary<int, InventoryItem> _pack = new();

    /// <summary>무엇을 걸치고 있는지, 걸친 자리 번호를 열쇠로.</summary>
    private readonly ConcurrentDictionary<int, WornItem> _worn = new();

    // Learned abilities arrive one at a time on entry, just like carried and worn items.
    private readonly ConcurrentDictionary<int, LearnedSkill> _skills = new();
    private readonly ConcurrentDictionary<int, LearnedSpell> _spells = new();

    // Everything on the floor that is not a player, by the same serial the server removes them by.
    private readonly ConcurrentDictionary<uint, Creature> _creatures = new();

    // How hurt each of them is, out of a hundred. The server never says more than that about somebody else.
    private readonly ConcurrentDictionary<uint, int> _health = new();
    private readonly ConcurrentDictionary<int, Ailment> _ailing = new();

    // What is on everybody else we can see, by who and which picture (0x5C).
    private readonly ConcurrentDictionary<(uint Serial, int Icon), SeenAilment> _seenAiling = new();

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

    private volatile string _said = string.Empty;
    private int? _path;
    private bool? _groupOpen;
    private OtherProfile? _seen;
    private volatile int _saidCount;

    // 0x0A 를 타입 바이트와 함께 줄줄이 담는다. _said 는 마지막 한 줄뿐이라, 한 프레임에 둘이 오면(주운 것 + 경험치)
    // 하나를 잃었다.
    private readonly ConcurrentQueue<(byte Type, string Text)> _told = new();

    /// <summary>How many lines of what people said are kept for looking back at.</summary>
    private const int HeardKept = 60;

    // 받는 쪽은 다른 실이다. 목록을 고치는 대신 새 목록으로 바꿔 끼워, 읽는 쪽이 훑는 도중에 바뀌지 않게 한다.
    // 언제 다시 쓸 수 있나. 받는 쪽과 그리는 쪽이 다른 실이라 한꺼번에 읽고 쓰는 사전을 쓴다.
    private readonly ConcurrentDictionary<(bool Skill, int Slot), DateTime> _cooling = new();

    private volatile IReadOnlyList<Spoken> _heard = [];
    private volatile int _heardTotal;

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
    public WorldEntry? State => _state;

    /// <summary>
    /// How many times the server has stated our position. It does that on entry, on a refresh, and when it
    /// refuses a step — so a rise the client did not ask for means a walk was turned down.
    /// </summary>
    public int PositionReports => _reports;

    /// <summary>
    /// The serial the server uses for our own character in the world. It is not the number the login
    /// server handed over — that one only opened the door.
    /// </summary>
    public uint Serial => _serial;

    /// <summary>
    /// Our own character as the server describes it — what we are wearing and what it calls us. The server
    /// shows us to ourselves like anybody else, so this is the same packet everyone else arrives in.
    /// </summary>
    public Character? Self => _self;

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
        [.. _seenAiling.Values.Where(one => one.Serial == serial)];

    /// <summary>
    /// Every health report the server has sent, oldest first. One report is one blow landing, so this is
    /// the record a test needs when the question is how much a single blow took off.
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
    public string Said => _said;

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
    public bool TakeTold(out byte type, out string text)
    {
        if (_told.TryDequeue(out (byte Type, string Text) told))
        {
            (type, text) = told;
            return true;
        }

        (type, text) = (0, string.Empty);
        return false;
    }

    /// <summary>The last lines anybody near us said, oldest first.</summary>
    public IReadOnlyList<Spoken> Heard => _heard;

    /// <summary>How many lines have been heard in all, so a screen can tell a new one from the same one again.</summary>
    public int HeardCount => _heardTotal;

    /// <summary>How many times it has spoken, so a reader can tell a repeat from a new line.</summary>
    public int SaidCount => _saidCount;

    /// <summary>Monsters and merchants the server has shown us, by serial.</summary>
    public IReadOnlyCollection<Creature> Creatures => (IReadOnlyCollection<Creature>)_creatures.Values;

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
    public IReadOnlyCollection<Character> Others => (IReadOnlyCollection<Character>)_others.Values;

    /// <summary>The direction bytes the server walks by: 0 north, 1 east, 2 south, 3 west.</summary>
    public static byte ToServer(Direction direction) => direction switch
    {
        Direction.North => 0,
        Direction.East => 1,
        Direction.South => 2,
        Direction.West => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "알 수 없는 방향입니다.")
    };

    /// <summary>Reads until the connection ends or the caller stops asking.</summary>
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
            throw;
        }
    }

    private async Task Listen(CancellationToken cancellationToken)
    {
        MapInfo? map = null;
        Tile? where = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            PacketFrame frame = await session.Connection.ReceiveAsync(cancellationToken);
            Interlocked.Exchange(ref _lastHeardTicks, DateTime.UtcNow.Ticks);

            try
            {
                switch (frame.Command)
                {
                    case MapChangedCommand:
                    {
                        int before = map?.Id ?? -1;
                        map = ReadMap(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _field = null;
                        _seenAiling.Clear();

                        // 맵이 바뀌면 보던 것을 모두 버린다 — 남겨 두면 지난 맵 괴물이 새 맵 위에 선다. 같은 맵 새로고침
                        // (막힌 걸음·속도 초과가 부르는 GameClient.Refresh)에는 버리지 않는다: 서버는 곁의 것을 곧 0x07 로 다시
                        // 보낼 뿐이고, 버리면 그때까지 괴물이 모두 사라졌다가 돌아온다(사용자 2026-09-25 "보였다가 사라진다").
                        // 시야 밖이 된 것은 서버가 0x0E 로 거둔다.
                        if (map.Id != before)
                        {
                            _creatures.Clear();
                            _others.Clear();
                        }

                        break;
                    }

                    case HeartbeatCommand:
                        await Send(HeartbeatReplyCommand, HadesCipher.DecodeSecured(frame, session.Parameters).ToArray(), cancellationToken);
                        continue;

                    case ExitedCommand:
                        _exited.TrySetResult();
                        continue;

                    case WorldMapCommand:
                        try
                        {
                            _field = ReadWorldMap(HadesCipher.DecodeSecured(frame, session.Parameters));
                            _fieldShown++;
                        }
                        catch (ProtocolException cut)
                        {
                            NoteUnread($"월드맵 안내를 읽다가 끊겼습니다: {cut.Message}");
                        }

                        continue;

                    case LocationCommand:
                        where = ReadLocation(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _reports++;
                        break;

                    case OwnSerialCommand:
                        _serial = BinaryPrimitives.ReadUInt32BigEndian(HadesCipher.DecodeSecured(frame, session.Parameters));

                        // It may arrive after we have already been shown ourselves, in which case we are
                        // standing in the crowd under our own name until now.
                        if (_others.TryRemove(_serial, out Character? mistaken))
                        {
                            _self = mistaken;
                        }

                        continue;

                    case DisplayCharacterCommand:
                        Show(ReadCharacter(HadesCipher.DecodeSecured(frame, session.Parameters)));
                        continue;

                    case BodyMotionCommand:
                    {
                        ReadOnlySpan<byte> motion = HadesCipher.DecodeSecured(frame, session.Parameters);

                        if (motion.Length >= 4)
                        {
                            _motions.Enqueue(ReadMotion(motion));
                        }
                        else
                        {
                            _ignored++;
                        }
                    }

                        continue;

                    case AnimationCommand:
                    {
                        ReadOnlySpan<byte> body = HadesCipher.DecodeSecured(frame, session.Parameters);

                        if (body.Length >= 12)
                        {
                            _effects.Enqueue(ReadEffect(body));
                        }
                        else
                        {
                            _ignored++;
                        }
                    }

                        continue;

                    case SoundCommand:
                    {
                        ReadOnlySpan<byte> body = HadesCipher.DecodeSecured(frame, session.Parameters);

                        if (body.Length >= 3)
                        {
                            // 같은 패킷이 효과음과 배경음악을 함께 나른다 — 번호가 가른다(Music).
                            int number = ReadSound(body);

                            if (Music.Song(number) is { } song)
                            {
                                _songs.Enqueue(song);
                            }
                            else
                            {
                                _sounds.Enqueue(number);
                            }
                        }
                        else
                        {
                            _ignored++;
                        }
                    }

                        continue;

                    case HealthCommand:
                        ReadHealth(HadesCipher.DecodeSecured(frame, session.Parameters));
                        continue;

                    case VitalsCommand:
                        _vitals = ReadVitals(HadesCipher.DecodeSecured(frame, session.Parameters), _vitals);
                        continue;

                    case SpokenCommand:
                    {
                        // A sound with no words is still this packet; there is simply nothing to show.
                        if (ReadTold(HadesCipher.DecodeSecured(frame, session.Parameters)) is { } told)
                        {
                            _said = told.Text;
                            _saidCount++;

                            // 읽는 쪽이 없으면 끝없이 쌓이지 않게 넉넉히 자른다.
                            if (_told.Count < HeardKept)
                            {
                                _told.Enqueue(told);
                            }
                        }
                    }

                        continue;

                    case SpeechCommand:
                    {
                        Spoken spoken = ReadSpoken(HadesCipher.DecodeSecured(frame, session.Parameters));

                        // 지난 말은 다시 볼 수 있어야 하지만 접속해 있는 내내 쌓아 둘 것은 아니다.
                        if (spoken.Text.Length > 0)
                        {
                            _heard = [.. _heard.TakeLast(HeardKept - 1), spoken];
                            _heardTotal++;
                        }
                    }

                        continue;

                    case GroupAskCommand:
                        if (Party.ReadAsk(HadesCipher.DecodeSecured(frame, session.Parameters)) is { } asker && _asks.Count < 8)
                        {
                            _asks.Enqueue(asker);
                        }

                        continue;

                    case CompanionTieCommand:
                        try
                        {
                            ReadOnlySpan<byte> tieBody = HadesCipher.DecodeSecured(frame, session.Parameters);

                            switch (tieBody.Length > 0 ? tieBody[0] : 0)
                            {
                                case World.Companion.MasterKind:
                                    _master = World.Companion.ReadTie(tieBody).Tie;
                                    _statuses.Clear();
                                    break;
                                case World.Companion.CompanionKind:
                                    _companion = World.Companion.ReadTie(tieBody).Tie;

                                    if (_companion is null)
                                    {
                                        _companionLife = null;
                                        _companionNumbers = null;
                                        _companionKit = null;
                                        _companionKitCount++;
                                    }

                                    break;
                                case World.Companion.StatusesKind:
                                    (uint on, IReadOnlyList<CompanionStatus> listed) = World.Companion.ReadStatuses(tieBody);
                                    _statuses[on] = listed;
                                    break;
                                case World.Companion.VitalsKind:
                                    _companionLife = World.Companion.ReadLife(tieBody);
                                    _companionNumbers = PartyNumbers.ReadLife(tieBody);
                                    break;
                                case World.Companion.MemberKind:
                                    PartyMemberStatus member = World.Companion.ReadMember(tieBody);

                                    // serial 0 — 그룹이 끝났다(나갔거나 흩어졌다). 모두 지운다.
                                    if (member.Serial == 0)
                                    {
                                        _members.Clear();
                                        _memberNumbers.Clear();
                                    }
                                    else
                                    {
                                        _members[member.Serial] = member;

                                        if (PartyNumbers.ReadMember(tieBody) is { } numbers)
                                        {
                                            _memberNumbers[member.Serial] = numbers;
                                        }
                                        else
                                        {
                                            _memberNumbers.TryRemove(member.Serial, out _);
                                        }
                                    }

                                    break;
                                case World.Companion.KitKind:
                                    _companionKit = World.Companion.ReadKit(tieBody);
                                    _companionKitCount++;
                                    break;
                            }
                        }
                        catch (ProtocolException cut)
                        {
                            NoteUnread($"0x5E: {cut.Message}");
                        }

                        continue;

                    case OtherProfileCommand:
                        try
                        {
                            _seen = OtherProfile.Read(HadesCipher.DecodeSecured(frame, session.Parameters));
                        }
                        catch (ProtocolException cut)
                        {
                            NoteUnread($"0x34: {cut.Message}");
                        }

                        continue;

                    case ProfileCommand:
                        try
                        {
                            byte[] profile = HadesCipher.DecodeSecured(frame, session.Parameters).ToArray();
                            _path = LearnLadder.PathFromProfile(profile) ?? _path;
                            _groupOpen = LearnLadder.GroupOpenFromProfile(profile) ?? _groupOpen;
                            _roster = Party.ReadRoster(profile);
                            _rosterCount++;
                        }
                        catch (ProtocolException cut)
                        {
                            NoteUnread($"0x39: {cut.Message}");
                        }

                        continue;

                    case CooldownCommand:
                    {
                        Cooldown cooling = ReadCooldown(HadesCipher.DecodeSecured(frame, session.Parameters));

                        _cooling[(cooling.Skill, cooling.Slot)] = DateTime.UtcNow.AddSeconds(cooling.Seconds);
                    }

                        continue;

                    case StatusCommand:
                    {
                        Ailment told = ReadAilment(HadesCipher.DecodeSecured(frame, session.Parameters));

                        // 등급 0 은 풀렸다는 뜻이다(Debuff.OnEnded 가 0 을 보낸다).
                        if (told.Left == 0)
                        {
                            _ailing.TryRemove(told.Icon, out _);
                        }
                        else
                        {
                            _ailing[told.Icon] = told;
                        }
                    }

                        continue;

                    case SeenStatusCommand:
                    {
                        SeenAilment seen = ReadSeenAilment(HadesCipher.DecodeSecured(frame, session.Parameters));

                        if (seen.Left == 0)
                        {
                            _seenAiling.TryRemove((seen.Serial, seen.Icon), out _);
                        }
                        else
                        {
                            _seenAiling[(seen.Serial, seen.Icon)] = seen;
                        }
                    }

                        continue;

                    case FigureCommand:
                        Figure figure = ReadFigure(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _figures.Enqueue(figure);

                        if (figure.Kind == FigureKind.Damage && figure.Source != 0)
                        {
                            _struck[figure.Target] = (figure.Source, DateTime.UtcNow);
                        }

                        continue;

                    case ShowCreaturesCommand:
                        foreach (Creature creature in ReadCreatures(HadesCipher.DecodeSecured(frame, session.Parameters)))
                        {
                            _creatures[creature.Serial] = creature;
                            _others.TryRemove(creature.Serial, out _);
                        }

                        continue;

                    case TakeFromPackCommand:
                    {
                        ReadOnlySpan<byte> gone = HadesCipher.DecodeSecured(frame, session.Parameters);

                        if (gone.Length >= 1)
                        {
                            _pack.TryRemove(gone[0], out _);
                        }
                    }

                        continue;

                    case WornCommand:
                    {
                        WornItem gear = ReadWorn(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _worn[gear.Slot] = gear;
                    }

                        continue;

                    case TookOffCommand:
                    {
                        ReadOnlySpan<byte> bare = HadesCipher.DecodeSecured(frame, session.Parameters);

                        if (bare.Length >= 1)
                        {
                            _worn.TryRemove(bare[0], out _);
                        }
                    }

                        continue;

                    case AddSkillCommand:
                    {
                        LearnedSkill skill = ReadSkill(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _skills[skill.Slot] = skill;
                    }

                        continue;

                    case AddSpellCommand:
                    {
                        LearnedSpell spell = ReadSpell(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _spells[spell.Slot] = spell;
                    }

                        continue;

                    case RemoveSkillCommand:
                        _skills.TryRemove(ReadAbilitySlot(HadesCipher.DecodeSecured(frame, session.Parameters)), out _);
                        continue;

                    case RemoveSpellCommand:
                        _spells.TryRemove(ReadAbilitySlot(HadesCipher.DecodeSecured(frame, session.Parameters)), out _);
                        continue;

                    case AddToPackCommand:
                        {
                            InventoryItem carried = ReadPackItem(HadesCipher.DecodeSecured(frame, session.Parameters));
                            _pack[carried.Slot] = carried;
                        }

                        continue;

                    case CreatureWalkedCommand:
                        Moved(HadesCipher.DecodeSecured(frame, session.Parameters));
                        continue;

                    case TurnedCommand:
                        Turned(HadesCipher.DecodeSecured(frame, session.Parameters));
                        continue;

                    case RemoveCommand:
                    {
                        uint gone = BinaryPrimitives.ReadUInt32BigEndian(
                            HadesCipher.DecodeSecured(frame, session.Parameters));

                        _others.TryRemove(gone, out _);
                        _creatures.TryRemove(gone, out _);

                        foreach ((uint Serial, int Icon) key in _seenAiling.Keys.Where(key => key.Serial == gone))
                        {
                            _seenAiling.TryRemove(key, out _);
                        }
                    }

                        continue;

                    case DialogueCommand:
                    {
                        Dialogue talk = ReadDialogue(HadesCipher.DecodeSecured(frame, session.Parameters));
                        Talking = talk;
                        _talkCount++;

                        if (talk.Unread is { } cut)
                        {
                            NoteUnread($"0x2F: {cut}");
                        }
                    }

                        continue;

                    case SequenceCommand:
                    {
                        byte[] sequence = HadesCipher.DecodeSecured(frame, session.Parameters);

                        if (ShutsDialogue(sequence))
                        {
                            Talking = null;
                            _talkCount++;
                        }
                        else
                        {
                            // 반응기 창(ReactorSequence·ReactorInputSequence)도 0x30 으로 온다. 아직 그리지 않지만 말없이 버리지는 않는다.
                            string kind = sequence.Length > 0 ? $"0x{sequence[0]:X2}" : "없음";
                            NoteUnread($"0x30: 닫기가 아닌 창 순서입니다 (첫 바이트 {kind}).");
                        }
                    }

                        continue;

                    default:
                        continue;
                }

                if (map is not null && where is not null)
                {
                    _state = new WorldEntry(map, where.Value);
                }
            }
            catch (Exception unreadable) when (unreadable is ProtocolException or ArgumentException or IndexOutOfRangeException)
            {
                // 갈래마다 따로 잡지 않은 패킷도 여기서 막는다 — 하나가 어긋났다고 받기 루프(접속)가 죽지 않게.
                NoteUnread($"0x{frame.Command:X2}: {unreadable.Message}");
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

    /// <summary>Remembers somebody. The server shows us our own character too, and that one is kept apart.</summary>
    private void Show(Character character)
    {
        if (character.Serial == _serial)
        {
            _self = character;
            return;
        }

        _others[character.Serial] = character;
    }

    /// <summary>Whoever we already know by that serial, so a packet without clothes does not undress them.</summary>
    private Character? Known(uint serial) =>
        serial == _serial ? _self : _others.GetValueOrDefault(serial);

    /// <summary>
    /// A step somebody took. The tile in the packet is where they were, not where they are now, so the
    /// direction has to be applied to it.
    /// </summary>
    private void Moved(ReadOnlySpan<byte> body)
    {
        if (body.Length < 9)
        {
            throw new ProtocolException($"걸음 안내가 9바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body);
        int fromX = BinaryPrimitives.ReadUInt16BigEndian(body[4..]);
        int fromY = BinaryPrimitives.ReadUInt16BigEndian(body[6..]);
        Direction facing = FromServer(body[8]);

        (int column, int row) = Facing.TileStep(facing);
        Tile now = new(fromX + column, fromY + row);

        // Monsters walk too, and the server uses this same packet for them. Take somebody we already know
        // to be a monster as a monster — otherwise it joins the crowd as a nameless person and stands there
        // wearing borrowed clothes on the tile its own picture is drawn on.
        if (_creatures.TryGetValue(serial, out Creature? beast))
        {
            _creatures[serial] = beast with { Where = now, Facing = facing };
            return;
        }

        // A step says nothing about clothes, so keep the ones we were shown rather than undressing them.
        // 모르는 serial 의 걸음은 버린다. 서버는 걸음(0x0C)을 걸음 뒤 자리로, 보여 주기(0x07·0x33)는 걸음 전 자리로
        // 곁의 사람을 골라(Sprite.Walk) 시야로 걸어 들어오는 괴물의 걸음이 먼저 온다 — 사람으로 받으면 npc-walk.png 를
        // 입은 이름 없는 사람이 괴물 자리에 선다(2026-09-24 사용자 보고). 곧 올 0x07·0x33 이 제자리에 세운다.
        if (Known(serial) is { } known)
        {
            Show(known with { Where = now, Facing = facing });
        }
    }

    /// <summary>
    /// Somebody turning where they stand. A monster does it right before it swings, so its blow is drawn
    /// towards whoever it hits rather than wherever it last walked.
    /// </summary>
    private void Turned(ReadOnlySpan<byte> body)
    {
        if (body.Length < 5)
        {
            return;
        }

        (uint serial, Direction facing) = ReadTurn(body);

        if (_creatures.TryGetValue(serial, out Creature? beast))
        {
            _creatures[serial] = beast with { Facing = facing };
            return;
        }

        if (Known(serial) is { } known)
        {
            Show(known with { Facing = facing });
        }
    }

    /// <summary>
    /// How hurt somebody is: whose, how much of their health is left out of a hundred, and a sound to play.
    /// A full bar is written as 255 rather than 100 — that is the server saying there is nothing to show,
    /// which is also how it answers a blow that could not land.
    /// </summary>
    private void ReadHealth(ReadOnlySpan<byte> body)
    {
        const int fixedLength = 7;

        if (body.Length < fixedLength)
        {
            throw new ProtocolException($"체력 안내가 {fixedLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        if (ReadHealthSound(body) is int sound)
        {
            _sounds.Enqueue(sound);
        }

        int left = BinaryPrimitives.ReadUInt16BigEndian(body[4..]);

        if (left > 100)
        {
            return;
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body);

        _health[serial] = left;
        _hurts.Enqueue((serial, left));
    }

    /// <summary>A skill pane row: slot, icon, then its display name as a short string.</summary>
    /// <summary>Seconds left before one slot may be used again, or none when it is ready.</summary>
    public int CoolingFor(bool skill, int slot) =>
        _cooling.TryGetValue((skill, slot), out DateTime ready) ? Cooldown.Left(ready, DateTime.UtcNow) : 0;

}
