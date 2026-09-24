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
public sealed class WorldClient(WorldSession session) : IDisposable
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

    /// <summary>귓속말. 받는 이 이름이 "!" 이면 그룹말이다.</summary>
    private const byte WhisperCommand = 0x19;

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
    private readonly ConcurrentQueue<int> _songs = new();

    private volatile WorldMapInfo? _field;
    private volatile int _fieldShown;

    private volatile string? _broke;
    private volatile int _ignored;

    private volatile string _said = string.Empty;
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

    /// <summary>
    /// Takes the next figure the server said had moved its body, if any. The server tells everyone nearby
    /// when somebody swings, and this is how that reaches whatever is drawing them.
    /// </summary>
    /// <remarks>
    /// It says which motion as well, and how fast. The reference client ignores that and plays its attack for
    /// every one (map-scene.ts); telling them apart is <see cref="Art.BodyMotion" />, out of skill.tbl.
    /// </remarks>
    public bool TakeMotion([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Motion? motion) =>
        _motions.TryDequeue(out motion);

    /// <summary>Takes the next flash the server asked to be drawn, if any. Like a motion, it is a moment.</summary>
    public bool TakeEffect([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Effect? effect) =>
        _effects.TryDequeue(out effect);

    /// <summary>Takes the next sound the server asked to be played — the number is the file's name.</summary>
    public bool TakeSound(out int sound) => _sounds.TryDequeue(out sound);

    /// <summary>The next song the server asked for, if it asked. <see cref="Music.Silence" /> means stop.</summary>
    public bool TakeMusic(out int song) => _songs.TryDequeue(out song);

    /// <summary>
    /// The next blow the server told us about (0x13): whose it was and what percentage of them is left. A screen
    /// takes these to put a bar over that one's head, which is the only place the original shows how a fight is
    /// going. Serial zero is a swing that hit nothing.
    /// </summary>
    public bool TakeHurt(out uint serial, out int left)
    {
        if (_hurts.TryDequeue(out (uint Serial, int Left) hurt))
        {
            (serial, left) = hurt;
            return true;
        }

        (serial, left) = (0, 0);
        return false;
    }

    /// <summary>Takes the next amount a blow took or a heal gave (0x5D), oldest first.</summary>
    public bool TakeFigure([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Figure? figure) => _figures.TryDequeue(out figure);

    /// <summary>The last thing the server said in words — a refused blow, a greeting, a warning.</summary>
    public string Said => _said;

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

    /// <summary>Who is in our group, as the profile last said. Alone until asked for (<see cref="AskProfileAsync" />).</summary>
    public PartyRoster Roster => _roster;

    /// <summary>How many times the group list has come — to tell a fresh one from the same one.</summary>
    public int RosterCount => _rosterCount;

    /// <summary>Takes the next person asking us to join their group (0x63), oldest first.</summary>
    public bool TakeAsk([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? name) => _asks.TryDequeue(out name);

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

            switch (frame.Command)
            {
                case MapChangedCommand:
                    map = ReadMap(HadesCipher.DecodeSecured(frame, session.Parameters));
                    _field = null;
                    _seenAiling.Clear();

                    // 원작처럼 맵이 바뀌면(같은 맵 새로고침도) 보던 것을 모두 버린다 — 서버는 0x15 뒤에 시야를 비우고
                    // 곁의 것을 다시 보낸다(GameClient.RefreshMap). 남겨 두면 지난 맵 괴물이 새 맵 위에 선다.
                    _creatures.Clear();
                    _others.Clear();
                    break;

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

                case ProfileCommand:
                    try
                    {
                        _roster = Party.ReadRoster(HadesCipher.DecodeSecured(frame, session.Parameters));
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
                    _figures.Enqueue(ReadFigure(HadesCipher.DecodeSecured(frame, session.Parameters)));
                    continue;

                case ShowCreaturesCommand:
                    foreach (Creature creature in ReadCreatures(HadesCipher.DecodeSecured(frame, session.Parameters)))
                    {
                        _creatures[creature.Serial] = creature;
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
    }

    /// <summary>Says we are taking one step. The count rises so the server can see how fast we claim to move.</summary>
    public Task WalkAsync(Direction direction, CancellationToken cancellationToken) =>
        Send(WalkCommand, [ToServer(direction), _step++], cancellationToken);

    /// <summary>
    /// Turns on the spot, without claiming a step.
    /// </summary>
    /// <remarks>
    /// A blow lands in the direction the server has us facing, and the only other thing that sets it is a
    /// step. Walking into the tile a monster stands on to face it is refused — rightly, it is occupied —
    /// and the refusal sends us back where we were, which on screen reads as being yanked a tile. This is
    /// the packet the original client sends for that, so facing costs nothing.
    /// </remarks>
    public Task TurnAsync(Direction direction, CancellationToken cancellationToken) =>
        Send(TurnCommand, [ToServer(direction)], cancellationToken);

    /// <summary>
    /// Picks one of the choices an NPC is offering. <paramref name="choice" /> is the number the server put
    /// beside that line, counted from one.
    /// </summary>
    /// <remarks>
    /// Reading a dialogue was all this client could do — it took <c>0x2F</c> and showed the words, and there
    /// was no way to answer. That leaves every NPC that asks something unreachable, the class chooser
    /// among them, so a character can never stop being a peasant.
    /// <c>GameServerHandlers.Format3AHandler</c> reads a kind byte, the speaker's serial, a script number
    /// and the choice. <b>Not <c>0x39</c></b> — that one answers a menu a script is walking somebody
    /// through, and a choice sent there is looked up in a menu that is not open, so nothing happens at all
    /// and nothing is logged. Numbers go out most significant byte first
    /// (<c>NetworkPacketReader.ReadUInt16</c> shifts the first byte up).
    /// </remarks>
    public Task AnswerAsync(uint speaker, ushort choice, CancellationToken cancellationToken) =>
        Answer(speaker, 0x0000, choice, [NothingTyped], cancellationToken);

    /// <summary>
    /// Answers with words as well — what was typed, or what the window asked to have handed back (the thing to buy,
    /// the slot to sell). <b>Not <c>0x39</c></b> for this either: <c>ClientFormat39</c> reads its words as ASCII, so a
    /// Korean item name arrives as question marks and the shop finds nothing by it. <c>ClientFormat3A</c> reads them
    /// in the server's own code page.
    /// </summary>
    public Task AnswerAsync(uint speaker, ushort choice, string words, CancellationToken cancellationToken) =>
        Answer(speaker, 0x0000, choice, [WordsTyped, .. LegacyKoreanEncoding.EncodeStringA(words)], cancellationToken);

    /// <summary>
    /// Says the window was shut from our side, so the server stops walking us through a menu
    /// (<c>Format3AHandler</c>: step 0 with script 0xFFFF closes the dialog).
    /// </summary>
    public Task ShutDialogueAsync(CancellationToken cancellationToken) =>
        Answer(0, 0xFFFF, 0x0000, [NothingTyped], cancellationToken);

    private Task Answer(uint speaker, ushort script, ushort choice, byte[] tail, CancellationToken cancellationToken) =>
        SendDialog(
            AnswerCommand,
            [
                MundaneSpeaker,
                (byte)(speaker >> 24), (byte)(speaker >> 16), (byte)(speaker >> 8), (byte)speaker,
                (byte)(script >> 8), (byte)script,
                (byte)(choice >> 8), (byte)choice,
                .. tail
            ],
            cancellationToken);

    /// <summary>The kind byte for a person standing in the world, as against a sign or a menu of our own.</summary>
    private const byte MundaneSpeaker = 0x01;

    /// <summary>Closes the packet where a typed line would go. <c>0x02</c> there means one follows.</summary>
    private const byte NothingTyped = 0x01;

    private const byte WordsTyped = 0x02;

    /// <summary>
    /// Says something out loud. The server treats a line beginning with a known word as a command when the
    /// speaker is allowed to give one, which is how a test gets an item into an empty pack.
    /// </summary>
    public Task SayAsync(string text, CancellationToken cancellationToken) =>
        Send(TalkCommand, [0, .. LegacyKoreanEncoding.EncodeStringA(text)], cancellationToken);

    /// <summary>Asks somebody standing near us to join our group. They are asked, not added (<see cref="Party" />).</summary>
    public Task AskToGroupAsync(string name, CancellationToken cancellationToken) =>
        Send(GroupCommand, Party.Ask(name), cancellationToken);

    /// <summary>Takes the ask of the person who asked us. There is no "no" on the wire — not answering is the no.</summary>
    public Task AcceptGroupAsync(string name, CancellationToken cancellationToken) =>
        Send(GroupCommand, Party.Accept(name), cancellationToken);

    /// <summary>Leaves the group the original way: by asking ourselves. Nothing is sent before the server has named us.</summary>
    public Task LeaveGroupAsync(CancellationToken cancellationToken) =>
        _self?.Name is { Length: > 0 } mine
            ? Send(GroupCommand, Party.Ask(mine), cancellationToken)
            : Task.CompletedTask;

    /// <summary>Says something to everyone in our group (a whisper to "!").</summary>
    public Task SayToGroupAsync(string text, CancellationToken cancellationToken) =>
        Send(WhisperCommand, Party.Chat(text), cancellationToken);

    /// <summary>Asks for our own profile, which is where the server lists the group.</summary>
    public Task AskProfileAsync(CancellationToken cancellationToken) =>
        Send(ProfileRequestCommand, [], cancellationToken);

    /// <summary>
    /// Strikes whatever is in front of us. The server decides whether that hits anything — it knows where
    /// everyone stands and how recently we last swung — so nothing is assumed here about the outcome.
    /// One tap is one blow: it does not chase and does not repeat.
    /// </summary>
    public Task AttackAsync(CancellationToken cancellationToken) =>
        Send(AttackCommand, [], cancellationToken);

    /// <summary>Activates one learned technique by its server-owned pane slot.</summary>
    public Task UseSkillAsync(int slot, CancellationToken cancellationToken) =>
        Send(UseSkillCommand, [(byte)slot], cancellationToken);

    /// <summary>
    /// Casts one learned spell. Hades reads the four bytes following the slot as the target serial; zero
    /// means the caster, which is also the safe answer for a spell that does not ask for a target.
    /// The final zero terminates the legacy argument field read by the same packet parser.
    /// </summary>
    public Task UseSpellAsync(int slot, uint target, CancellationToken cancellationToken) =>
        Send(
            UseSpellCommand,
            [(byte)slot, (byte)(target >> 24), (byte)(target >> 16), (byte)(target >> 8), (byte)target, 0],
            cancellationToken);

    /// <summary>
    /// Uses what is in one pack slot. What that means is the item's own business — boots are worn,
    /// food is eaten — so nothing is assumed here beyond the slot number. The server answers a piece
    /// of clothing by describing us again, which is how the figure comes to be redrawn.
    /// </summary>
    public Task UseAsync(int slot, CancellationToken cancellationToken) =>
        Send(UseCommand, [(byte)slot], cancellationToken);

    /// <summary>
    /// Throws one pack slot on the floor. The server decides whether it may be thrown at all — some things
    /// are not — and it lands on the tile we name, which is our own.
    /// </summary>
    public Task DropAsync(int slot, int amount, Tile where, CancellationToken cancellationToken) =>
        Send(
            DropCommand,
            [
                (byte)slot,
                (byte)(where.X >> 8), (byte)where.X,
                (byte)(where.Y >> 8), (byte)where.Y,
                (byte)(amount >> 24), (byte)(amount >> 16), (byte)(amount >> 8), (byte)amount
            ],
            cancellationToken);

    /// <summary>
    /// Throws gold on the floor. Gold is not a pack slot — the server takes the amount straight off the
    /// character — so this says only how much and where. The original merges what lands where gold already
    /// lies, so two throws on one tile leave one larger pile rather than two.
    /// </summary>
    public Task DropGoldAsync(int amount, Tile where, CancellationToken cancellationToken) =>
        Send(
            DropGoldCommand,
            [
                (byte)(amount >> 24), (byte)(amount >> 16), (byte)(amount >> 8), (byte)amount,
                (byte)(where.X >> 8), (byte)where.X,
                (byte)(where.Y >> 8), (byte)where.Y
            ],
            cancellationToken);

    /// <summary>
    /// Takes off whatever is in one worn place. The place is the server's own number — the one it gave us
    /// in <c>0x37</c> when it said the place was filled — and the item goes back into the pack, so nothing
    /// here has to say where. The server answers by describing us again, which redraws the figure.
    /// </summary>
    public Task TakeOffAsync(int place, CancellationToken cancellationToken) =>
        Send(TakeOffCommand, [(byte)place], cancellationToken);

    /// <summary>
    /// Taps someone. This is how a conversation starts: the server finds whatever carries that serial and
    /// hands it the tap, and an NPC with a script answers with a dialogue window. Tapping a monster or a
    /// player does something else or nothing, which is the server's business, not ours — we only say what
    /// was tapped. The first byte picks how we name it; one means by serial.
    /// </summary>
    public Task ClickAsync(uint serial, CancellationToken cancellationToken) =>
        Send(
            ClickCommand,
            [
                ClickBySerial,
                (byte)(serial >> 24), (byte)(serial >> 16), (byte)(serial >> 8), (byte)serial
            ],
            cancellationToken);

    /// <summary>
    /// Picks up whatever lies on one tile. The original has no automatic looting — walking over a thing
    /// leaves it there, and only asking for it takes it — so this is sent when the tile is tapped and at
    /// no other time. The server takes the topmost thing within <c>ClickLootDistance</c> (10 tiles) of us
    /// and says nothing at all when there is none, so a tap on bare floor costs nothing.
    /// </summary>
    public Task PickUpAsync(Tile where, CancellationToken cancellationToken) =>
        Send(
            PickUpCommand,
            [
                AnyPackSlot,
                (byte)(where.X >> 8), (byte)where.X,
                (byte)(where.Y >> 8), (byte)where.Y
            ],
            cancellationToken);

    /// <summary>
    /// Swaps two pack slots. Tidying the pack is a run of these — the server keeps no order of its own, so
    /// whatever order there is, the player made it.
    /// </summary>
    public Task MoveAsync(int from, int to, CancellationToken cancellationToken) =>
        Send(MoveCommand, [InventoryPane, (byte)from, (byte)to], cancellationToken);

    /// <summary>
    /// Spends one of the points a level handed out, on one attribute.
    /// </summary>
    /// <remarks>
    /// The server refuses with a message and changes nothing when there are no points left
    /// (<c>Format47Handler</c>), so asking too often costs nothing but the packet. It answers a successful
    /// spend with the whole of our numbers, which is how the new maximum health arrives.
    /// </remarks>
    public Task RaiseAsync(Stat which, CancellationToken cancellationToken) =>
        Send(RaiseCommand, [(byte)which], cancellationToken);

    /// <summary>Asks the server to say where we are again, which it answers with the map and the tile.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken) =>
        Send(RefreshCommand, [], cancellationToken);

    /// <summary>
    /// 월드맵에서 고른 곳의 맵 번호. 서버는 이 번호로 자기 목록에서 곳을 찾는다
    /// (`GameServerHandlers.cs:1853`) — 그림 위의 점이 아니라 갈 맵이 열쇠다.
    /// </summary>
    public static byte[] FieldChoice(int areaId)
    {
        byte[] body = new byte[4];

        BinaryPrimitives.WriteInt32BigEndian(body, areaId);

        return body;
    }

    /// <summary>
    /// 월드맵에서 한 곳을 골라 보낸다. 창이 열려 있는 동안 서버는 이것 말고 이 접속의 패킷을 모두
    /// 버리므로(`NetworkServer.cs:141`), 고르기 전에는 걷지도 말하지도 못한다 — 닫기로 취소(맵 번호 0)를
    /// 보내면 조작이 돌아온다.
    /// </summary>
    public Task ChooseFieldAsync(int areaId, CancellationToken cancellationToken) =>
        Send(ChooseFieldCommand, FieldChoice(areaId), cancellationToken);

    /// <summary>
    /// 월드맵을 열어 달라고 서버에 말한다. 원작에는 없는 말이다 — 원작은 바닥의 숨은 칸을 밟아야 열렸다.
    /// 마을이 아니면 서버가 거절하고 말 한 줄만 돌려준다(싸우는 중에 열면 손이 묶이기 때문이다).
    /// </summary>
    public Task OpenFieldAsync(CancellationToken cancellationToken) =>
        Send(OpenFieldCommand, [], cancellationToken);

    /// <summary>
    /// 월드맵을 그냥 닫는다. 갈 맵 번호 0 이 취소라고 서버와 약속했다 — 0 은 어느 맵의 번호도 아니다.
    /// </summary>
    public Task CloseFieldAsync(CancellationToken cancellationToken) =>
        Send(ChooseFieldCommand, FieldChoice(0), cancellationToken);

    /// <summary>
    /// Sends one of the two answers an NPC takes. They go in a different envelope — six bytes of header and
    /// a second layer of enciphering — which <see cref="HadesCipher.EncodeDialogSecured" /> explains.
    /// </summary>
    private Task SendDialog(byte command, byte[] fields, CancellationToken cancellationToken) =>
        SendInTurn(() => HadesCipher.EncodeDialogSecured(command, _ordinal++, fields, session.Parameters), cancellationToken);

    private Task Send(byte command, byte[] body, CancellationToken cancellationToken) =>
        SendInTurn(() => HadesCipher.EncodeSecured(command, _ordinal++, body, session.Parameters), cancellationToken);

    private async Task SendInTurn(Func<byte[]> frame, CancellationToken cancellationToken)
    {
        await _sending.WaitAsync(cancellationToken);

        try
        {
            await session.Connection.SendAsync(frame(), cancellationToken);
        }
        finally
        {
            _sending.Release();
        }
    }

    /// <summary>
    /// 원작 클라이언트처럼 나간다고 먼저 말하고(0x0B, 종류 1) 서버가 캐릭터를 뺐다는 답(0x4C)을 잠깐 기다린 뒤 소켓을 닫는다.
    /// 말이 닿지 않아도(끊긴 망) 닫는 것은 반드시 한다 — 서버는 닫힌 소켓으로도 곧 뺀다.
    /// </summary>
    public async Task LogOutAsync(CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            using CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(LogOutWait);
            await Send(ExitCommand, [1], wait.Token);
            await _exited.Task.WaitAsync(wait.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 답이 늦거나 망이 끊겼다 — 닫는 것으로 충분하다.
        }
        finally
        {
            Dispose();
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
        Show(Known(serial) is { } known
            ? known with { Where = now, Facing = facing }
            : new Character(serial, now, facing));
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

    /// <summary>A turn (0x11): whose serial, then which way they now face.</summary>
    public static (uint Serial, Direction Facing) ReadTurn(ReadOnlySpan<byte> body) =>
        (BinaryPrimitives.ReadUInt32BigEndian(body), FromServer(body[4]));

    /// <summary>
    /// A flash (0x29). The server writes the one it lands on first, then whoever made it, then an animation for
    /// each in that order — or, when the first serial is zero, one animation and a tile.
    /// </summary>
    public static Effect ReadEffect(ReadOnlySpan<byte> body)
    {
        uint target = BinaryPrimitives.ReadUInt32BigEndian(body);

        if (target == 0)
        {
            return new Effect(0, 0, BinaryPrimitives.ReadUInt16BigEndian(body[4..]), 0, body[7],
                new Tile(BinaryPrimitives.ReadUInt16BigEndian(body[8..]), BinaryPrimitives.ReadUInt16BigEndian(body[10..])));
        }

        return new Effect(
            target,
            BinaryPrimitives.ReadUInt32BigEndian(body[4..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[8..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[10..]),
            body.Length >= 14 ? BinaryPrimitives.ReadUInt16BigEndian(body[12..]) : 100,
            null);
    }

    /// <summary>A body motion (0x1A): serial, motion number, speed. A short one still names who moved.</summary>
    public static Motion ReadMotion(ReadOnlySpan<byte> body) => new(
        BinaryPrimitives.ReadUInt32BigEndian(body),
        body.Length >= 5 ? body[4] : 0,
        body.Length >= 7 ? BinaryPrimitives.ReadUInt16BigEndian(body[5..]) : 0);

    /// <summary>
    /// 월드맵 창(ServerFormat2E): 그림 이름, 곳의 수, 마당 번호, 그리고 곳마다 점(Y 가 먼저다)·이름·
    /// 갈 맵·그 맵에서 설 칸. 끝의 여섯 바이트는 서버가 채우는 아무 값이라 읽지 않는다.
    /// </summary>
    public static WorldMapInfo ReadWorldMap(ReadOnlySpan<byte> body)
    {
        int at = 0;

        string picture = Words(body, ref at);
        int count = Byte(body, ref at);
        int number = Byte(body, ref at);

        List<WorldMapNode> nodes = new(count);

        for (int index = 0; index < count; index++)
        {
            try
            {
                int pointY = (short)Word(body, ref at);
                int pointX = (short)Word(body, ref at);
                string name = Words(body, ref at);
                int area = (int)Long(body, ref at);
                int x = (short)Word(body, ref at);
                int y = (short)Word(body, ref at);

                nodes.Add(new WorldMapNode(name, area, x, y, pointX, pointY));
            }
            catch (ProtocolException) when (nodes.Count > 0)
            {
                // 개수(0x02 등)와 몸통이 어긋나 중간에서 끊겨도, 이미 다 읽은 곳이 있으면 그것으로
                // 돌려준다 — 사람이 창을 보고 빠져나갈 수 있다. 하나도 못 읽었으면(맨 처음이 끊기면)
                // 아래에서 그대로 던진다 — 그건 어차피 못 빠져나온다.
                break;
            }
        }

        return new WorldMapInfo(picture, number, nodes);
    }

    /// <summary>A sound (0x19): an empty byte, then the number.</summary>
    public static int ReadSound(ReadOnlySpan<byte> body) => BinaryPrimitives.ReadUInt16BigEndian(body[1..]);

    /// <summary>
    /// The sound a health bar (0x13) carries in its last byte — a blow lands with it, and a sound meant for
    /// nobody's bar comes this way too, with the bar left at 255. Zero is what a template with no sound
    /// writes, and 255 is the original's "none"; neither is played.
    /// </summary>
    public static int? ReadHealthSound(ReadOnlySpan<byte> body) =>
        body.Length >= 7 && body[6] is not (0 or byte.MaxValue) ? body[6] : null;

    /// <summary>Somebody to draw: where they are, which way they face, what they wear, and their name.</summary>
    /// <remarks>
    /// Place, direction and serial take nine bytes; then twenty-one bytes of wardrobe, one byte saying
    /// whether the map allows killing, and the name. A dead character is written bare and the server stops
    /// before the name, which is why the name is only read when there are bytes left for it.
    /// </remarks>
    public static Character ReadCharacter(ReadOnlySpan<byte> body)
    {
        const int fixedLength = 31;
        const int wornLength = 11;

        if (body.Length < wornLength)
        {
            throw new ProtocolException($"사람 안내가 {wornLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body[5..]);
        Tile where = new(BinaryPrimitives.ReadUInt16BigEndian(body), BinaryPrimitives.ReadUInt16BigEndian(body[2..]));
        Direction facing = FromServer(body[4]);
        int head = BinaryPrimitives.ReadUInt16BigEndian(body[9..]);

        // Somebody wearing a monster's shape carries its number where the wardrobe would be, and the rest
        // of the packet is laid out differently.
        // ponytail: read the name and leave the shape alone — no map here has monsters to check it against.
        if (head == 0xFFFF)
        {
            return new Character(serial, where, facing, null, ReadName(body, 22));
        }

        if (body.Length < fixedLength)
        {
            throw new ProtocolException($"사람 안내가 {fixedLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        // Byte 15 is the armour written a second time and byte 26 is left empty; both are the server's habit.
        Appearance wearing = new(
            head,
            body[11],
            BinaryPrimitives.ReadUInt16BigEndian(body[12..]),
            body[14],
            body[17],
            body[18],
            body[19],
            body[20],
            BinaryPrimitives.ReadUInt16BigEndian(body[21..]),
            body[23],
            BinaryPrimitives.ReadUInt16BigEndian(body[24..]),
            body[27],
            BinaryPrimitives.ReadUInt16BigEndian(body[28..]));

        return new Character(serial, where, facing, wearing, ReadName(body, fixedLength));
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

    /// <summary>
    /// Our own numbers. The server sends them in four pieces and a leading flag byte says which of them
    /// came, so a piece left out of this packet keeps whatever it said last time — which is what
    /// <paramref name="before" /> is for. It sends all four as the character enters and single pieces
    /// afterwards: what is left of health and mana every time anything is struck, for instance.
    /// </summary>
    /// <remarks>
    /// The pieces are fixed width and always in this order: the standing figures (28 bytes), what is left
    /// of health and mana (8), what has been earned (24), and the fighting figures (13). Two flags the
    /// server always sets, 0x40 and 0x80, say nothing about the body and are passed over.
    /// </remarks>
    public static Vitals ReadVitals(ReadOnlySpan<byte> body, Vitals? before = null)
    {
        const byte Standing = 0x20;
        const byte Remaining = 0x10;
        const byte Earned = 0x08;
        const byte Fighting = 0x04;

        if (body.Length < 1)
        {
            throw new ProtocolException("몸 상태 안내에 조각 표가 없습니다 (0바이트).");
        }

        byte pieces = body[0];
        int at = 1;

        // Static because a local function may not reach a span, and the span is the one thing it does not
        // need: the length is enough to say the piece is not all there.
        static void Require(int have, int wanted, byte pieces)
        {
            if (have < wanted)
            {
                throw new ProtocolException(
                    $"몸 상태 안내가 {wanted}바이트보다 짧습니다 ({have}바이트, 조각 표 0x{pieces:X2}).");
            }
        }

        Vitals now = before ?? Vitals.Unknown;

        if ((pieces & Standing) != 0)
        {
            Require(body.Length, at + 28, pieces);

            now = now with
            {
                // Three bytes the server always writes as 1, 0, 0 come first.
                Level = body[at + 3],
                AbilityLevel = body[at + 4],
                MaximumHealth = (int)BinaryPrimitives.ReadUInt32BigEndian(body[(at + 5)..]),
                MaximumMana = (int)BinaryPrimitives.ReadUInt32BigEndian(body[(at + 9)..]),
                Str = body[at + 13],
                Int = body[at + 14],
                Wis = body[at + 15],
                Con = body[at + 16],
                Dex = body[at + 17],

                // A flag saying there is something to spend, then how much.
                Unspent = body[at + 18] == 0 ? 0 : body[at + 19],
                MaximumWeight = BinaryPrimitives.ReadUInt16BigEndian(body[(at + 20)..]),
                Weight = BinaryPrimitives.ReadUInt16BigEndian(body[(at + 22)..]),
            };

            at += 28;
        }

        if ((pieces & Remaining) != 0)
        {
            Require(body.Length, at + 8, pieces);

            now = now with
            {
                Health = (int)BinaryPrimitives.ReadUInt32BigEndian(body[at..]),
                Mana = (int)BinaryPrimitives.ReadUInt32BigEndian(body[(at + 4)..]),
            };

            at += 8;
        }

        if ((pieces & Earned) != 0)
        {
            Require(body.Length, at + 24, pieces);

            now = now with
            {
                Experience = BinaryPrimitives.ReadUInt32BigEndian(body[at..]),
                ExperienceToGo = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 4)..]),
                AbilityExperience = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 8)..]),
                AbilityExperienceToGo = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 12)..]),
                GamePoints = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 16)..]),
                Gold = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 20)..]),
            };

            at += 24;
        }

        if ((pieces & Fighting) != 0)
        {
            Require(body.Length, at + 13, pieces);

            now = now with
            {
                // Four bytes of nothing, then blindness, then another byte of nothing.
                Blind = body[at + 4] != 0,
                Offense = (Element)body[at + 6],
                Defense = (Element)body[at + 7],
                MagicResistance = body[at + 8],

                // Signed, because armour worth having is below zero.
                Armor = (sbyte)body[at + 10],
                Damage = body[at + 11],
                Hit = body[at + 12],
            };
        }

        return now;
    }

    /// <summary>
    /// Everything the server is showing at once: how many, then that many records.
    /// </summary>
    /// <remarks>
    /// Each record is the same seventeen bytes — place, serial, drawing, four bytes the server leaves
    /// empty, a direction, one more empty, and what kind of thing it is. A merchant is named after that
    /// and nothing else is.
    ///
    /// Things lying on the floor are written shorter than this by our server (thirteen bytes, with no
    /// direction and no kind), and there is nothing in the record to tell them apart from the start of a
    /// monster — so a floor with something dropped on it would be read wrongly from that point. The
    /// original format has no such gap; ours does. Nothing can be dropped here yet, and this is where to
    /// come back when it can be.
    /// </remarks>
    /// <summary>
    /// One status icon (0x3A): which picture, and a grade saying how much longer it lasts. The server works the
    /// grade out in <c>Debuff.Display</c> — 6 is over ninety seconds, 1 is under ten, and 0 means it is over.
    /// </summary>
    public static Ailment ReadAilment(ReadOnlySpan<byte> body)
    {
        if (body.Length < 3)
        {
            throw new ProtocolException($"상태 안내가 3바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Ailment(BinaryPrimitives.ReadUInt16BigEndian(body), body[2]);
    }

    /// <summary>
    /// Something on somebody else (0x5C, our server's own packet): serial(4) · picture(2) · grade(1) · harmful(1) ·
    /// effect(2), all big-endian like the rest.
    /// </summary>
    public static SeenAilment ReadSeenAilment(ReadOnlySpan<byte> body)
    {
        if (body.Length < 10)
        {
            throw new ProtocolException($"남의 상태 안내가 10바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new SeenAilment(
            BinaryPrimitives.ReadUInt32BigEndian(body),
            BinaryPrimitives.ReadUInt16BigEndian(body[4..]),
            body[6],
            body[7] != 0,
            BinaryPrimitives.ReadUInt16BigEndian(body[8..]));
    }

    /// <summary>
    /// How much a blow took or a heal gave (0x5D, our server's own packet): target(4) · source(4) · amount(4) ·
    /// kind(1, 0 damage · 1 heal), all big-endian like the rest. An unknown kind is read as damage.
    /// </summary>
    public static Figure ReadFigure(ReadOnlySpan<byte> body)
    {
        if (body.Length < 13)
        {
            throw new ProtocolException($"피해·회복 안내가 13바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Figure(
            BinaryPrimitives.ReadUInt32BigEndian(body),
            BinaryPrimitives.ReadUInt32BigEndian(body[4..]),
            (int)Math.Min(int.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(body[8..])),
            body[12] == 1 ? FigureKind.Heal : FigureKind.Damage);
    }

    public static IReadOnlyList<Creature> ReadCreatures(ReadOnlySpan<byte> body)
    {
        const int recordLength = 17;

        if (body.Length < 2)
        {
            throw new ProtocolException($"물체 안내에 개수가 없습니다 ({body.Length}바이트).");
        }

        int expected = BinaryPrimitives.ReadUInt16BigEndian(body);
        List<Creature> shown = [];
        ReadOnlySpan<byte> rest = body[2..];

        for (int index = 0; index < expected; index++)
        {
            if (rest.Length < recordLength)
            {
                throw new ProtocolException(
                    $"물체 {index + 1}번째가 {recordLength}바이트보다 짧습니다 ({rest.Length}바이트).");
            }

            CreatureKind kind = (CreatureKind)rest[16];
            string name = string.Empty;
            int read = recordLength;

            if (kind == CreatureKind.Merchant)
            {
                name = LegacyKoreanEncoding.DecodeStringA(rest[recordLength..], out int consumed);
                read += consumed;
            }

            shown.Add(new Creature(
                BinaryPrimitives.ReadUInt32BigEndian(rest[4..]),
                new Tile(BinaryPrimitives.ReadUInt16BigEndian(rest), BinaryPrimitives.ReadUInt16BigEndian(rest[2..])),
                FromServer(rest[14]),
                BinaryPrimitives.ReadUInt16BigEndian(rest[8..]),
                kind,
                name));

            rest = rest[read..];
        }

        return shown;
    }

    /// <summary>
    /// Something the server has put in our pack: which slot, what it looks like, what it is called, how
    /// many, and how worn out.
    /// </summary>
    public static InventoryItem ReadPackItem(ReadOnlySpan<byte> body)
    {
        const int beforeName = 4;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"소지품 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out int consumed);
        ReadOnlySpan<byte> rest = body[(beforeName + consumed)..];

        if (rest.Length < 13)
        {
            throw new ProtocolException($"소지품 안내의 이름 뒤가 13바이트보다 짧습니다 ({rest.Length}바이트).");
        }

        return new InventoryItem(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            body[3],
            name,
            (int)BinaryPrimitives.ReadUInt32BigEndian(rest),
            // Byte 4 says whether it stacks, which the count already tells us.
            (int)BinaryPrimitives.ReadUInt32BigEndian(rest[9..]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(rest[5..]));
    }

    /// <summary>
    /// One piece of gear the character is wearing. The place it sits comes first, then the picture, then a
    /// byte the server always writes as three, then two names — what the item is and what this one is
    /// called — and how worn out it is.
    /// </summary>
    public static WornItem ReadWorn(ReadOnlySpan<byte> body)
    {
        const int beforeName = 4;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"장비 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out int consumed);
        ReadOnlySpan<byte> rest = body[(beforeName + consumed)..];
        string called = LegacyKoreanEncoding.DecodeStringA(rest, out int alsoConsumed);
        ReadOnlySpan<byte> wear = rest[alsoConsumed..];

        if (wear.Length < 8)
        {
            throw new ProtocolException($"장비 안내의 이름 뒤가 8바이트보다 짧습니다 ({wear.Length}바이트).");
        }

        return new WornItem(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            name,
            called,
            BinaryPrimitives.ReadUInt32BigEndian(wear),
            BinaryPrimitives.ReadUInt32BigEndian(wear[4..]));
    }

    /// <summary>A skill pane row: slot, icon, then its display name as a short string.</summary>
    /// <summary>Seconds left before one slot may be used again, or none when it is ready.</summary>
    public int CoolingFor(bool skill, int slot) =>
        _cooling.TryGetValue((skill, slot), out DateTime ready) ? Cooldown.Left(ready, DateTime.UtcNow) : 0;

    /// <summary>Reads how long one slot must wait (0x3F).</summary>
    public static Cooldown ReadCooldown(ReadOnlySpan<byte> body)
    {
        const int wanted = 6;

        if (body.Length < wanted)
        {
            throw new ProtocolException($"다시 쓰기까지의 안내가 {wanted}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Cooldown(body[0] == 1, body[1], (int)BinaryPrimitives.ReadUInt32BigEndian(body[2..]));
    }

    /// <summary>
    /// Reads one line the server says (0x0A): the type byte, then the words as a two-byte-length string
    /// (Hades <c>ServerFormat0A.Serialize</c>). A packet with no words — Hades leaves the string out when it is empty —
    /// is nothing to show.
    /// </summary>
    public static (byte Type, string Text)? ReadTold(ReadOnlySpan<byte> body) =>
        body.Length > 3 ? (body[0], LegacyKoreanEncoding.DecodeStringB(body[1..], out _)) : null;

    /// <summary>Reads one line of speech (0x0D): how it was said, whose it is, and the words.</summary>
    public static Spoken ReadSpoken(ReadOnlySpan<byte> body)
    {
        const int beforeText = 5;

        if (body.Length < beforeText + 1)
        {
            throw new ProtocolException($"누가 한 말이 {beforeText + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Spoken(
            (SpeechKind)body[0],
            BinaryPrimitives.ReadUInt32BigEndian(body[1..]),
            LegacyKoreanEncoding.DecodeStringA(body[beforeText..], out _));
    }

    public static LearnedSkill ReadSkill(ReadOnlySpan<byte> body)
    {
        const int beforeName = 3;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"기술 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new LearnedSkill(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out _));
    }

    /// <summary>A spell pane row, including how it obtains a target and how many chant lines it uses.</summary>
    public static LearnedSpell ReadSpell(ReadOnlySpan<byte> body)
    {
        const int beforeName = 4;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"마법 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out int nameBytes);
        ReadOnlySpan<byte> afterName = body[(beforeName + nameBytes)..];

        if (afterName.Length < 1)
        {
            throw new ProtocolException("마법 안내에 설명이 없습니다.");
        }

        string prompt = LegacyKoreanEncoding.DecodeStringA(afterName, out int promptBytes);
        ReadOnlySpan<byte> afterPrompt = afterName[promptBytes..];

        if (afterPrompt.Length < 1)
        {
            throw new ProtocolException("마법 안내에 시전 줄 수가 없습니다.");
        }

        return new LearnedSpell(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            (SpellTargetType)body[3],
            name,
            prompt,
            afterPrompt[0]);
    }

    /// <summary>The server removes a learned skill or spell by pane slot alone.</summary>
    public static int ReadAbilitySlot(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
        {
            throw new ProtocolException("기술·마법 칸 삭제 안내가 비어 있습니다.");
        }

        return body[0];
    }

    /// <summary>The name, if the server got as far as writing one.</summary>
    /// <summary>
    /// What an NPC answered when tapped. The head is fixed — a kind byte, the NPC's serial and picture,
    /// and five bytes the original client reads and ignores — and then two strings: who is speaking and
    /// what they said. Both are length-prefixed the long way round (a big-endian ushort), unlike almost
    /// everything else in this protocol, which is why this reads them with DecodeStringB.
    /// </summary>
    public static Dialogue ReadDialogue(ReadOnlySpan<byte> body)
    {
        const int beforeName = 14;

        if (body.Length <= beforeName)
        {
            return new Dialogue(0, string.Empty, string.Empty);
        }

        uint serial = (uint)((body[2] << 24) | (body[3] << 16) | (body[4] << 8) | body[5]);
        string who = LegacyKoreanEncoding.DecodeStringB(body[beforeName..], out int consumed);
        ReadOnlySpan<byte> rest = body[(beforeName + consumed)..];
        string what = rest.Length > 0 ? LegacyKoreanEncoding.DecodeStringB(rest, out consumed) : string.Empty;

        Dialogue talk = new(serial, who, what) { Kind = (DialogueKind)body[0] };

        try
        {
            return rest.Length > 0 ? WithWindowData(talk, rest[consumed..]) : talk;
        }
        catch (ProtocolException cut)
        {
            // The words are still worth showing when what follows them is cut short — but the window says so.
            return talk with { Unread = cut.Message };
        }
    }

    /// <summary>
    /// Reads what one kind of window carries under its words, the way each Hades <c>IDialogData</c> writes it.
    /// </summary>
    private static Dialogue WithWindowData(Dialogue talk, ReadOnlySpan<byte> data)
    {
        int at = 0;

        switch (talk.Kind)
        {
            case DialogueKind.Options or DialogueKind.OptionsWithArgs:
            {
                string args = talk.Kind == DialogueKind.OptionsWithArgs ? Words(data, ref at) : string.Empty;
                int count = Byte(data, ref at);
                List<DialogueOption> options = [];

                // OptionsData counts a choice with no words but writes nothing for it, so the bytes decide.
                for (int i = 0; i < count && at < data.Length; i++)
                {
                    options.Add(new DialogueOption(Words(data, ref at), Word(data, ref at)));
                }

                return talk with { Args = args, Options = options };
            }

            case DialogueKind.TextInput or DialogueKind.ForgetSpell or DialogueKind.ForgetSkill:
                return talk with { Step = Word(data, ref at) };

            case DialogueKind.Goods:
            {
                ushort step = Word(data, ref at);
                int count = Word(data, ref at);
                List<DialogueGoods> goods = [];

                for (int i = 0; i < count; i++)
                {
                    goods.Add(new DialogueGoods(
                        Icon: Word(data, ref at),
                        Colour: Byte(data, ref at),
                        Price: Long(data, ref at),
                        Name: Words(data, ref at)));

                    // 직업 이름 — 원작 창은 쓰지 않는다.
                    Words(data, ref at);
                }

                return talk with { Step = step, Goods = goods };
            }

            case DialogueKind.PackSlots:
            {
                // ItemSellData 는 번호를 한 바이트만 쓰고, shop1 은 그 바이트를 한 자리 올린 값(0x05 → 0x0500)을 기다린다.
                ushort step = (ushort)(Byte(data, ref at) << 8);
                int count = Word(data, ref at);
                List<int> slots = [];

                for (int i = 0; i < count; i++)
                {
                    slots.Add(Byte(data, ref at));
                }

                return talk with { Step = step, Slots = slots };
            }

            case DialogueKind.Spells or DialogueKind.Skills:
            {
                ushort step = Word(data, ref at);
                int count = Word(data, ref at);
                List<DialogueAbility> abilities = [];

                for (int i = 0; i < count; i++)
                {
                    Byte(data, ref at); // 마법 2 · 기술 3
                    int icon = Word(data, ref at);
                    Byte(data, ref at);
                    abilities.Add(new DialogueAbility(icon, Words(data, ref at)));
                }

                return talk with { Step = step, Abilities = abilities };
            }

            default:
                return talk;
        }
    }

    /// <summary>
    /// <c>GameClient.CloseDialog</c> sends the raw bytes 0x30 0x00 0x0A 0x00; the 0x00 after the command goes for the
    /// ordinal, so the body starts 0x0A. A sequence opening starts with its kind instead (0x00, or 0x04 for typing).
    /// </summary>
    public static bool ShutsDialogue(ReadOnlySpan<byte> body) => body.Length >= 1 && body[0] == 0x0A;

    private static byte Byte(ReadOnlySpan<byte> data, ref int at) =>
        at < data.Length ? data[at++] : throw new ProtocolException("대화창 자료가 중간에 끊겼습니다.");

    private static ushort Word(ReadOnlySpan<byte> data, ref int at) => (ushort)((Byte(data, ref at) << 8) | Byte(data, ref at));

    private static uint Long(ReadOnlySpan<byte> data, ref int at) => ((uint)Word(data, ref at) << 16) | Word(data, ref at);

    private static string Words(ReadOnlySpan<byte> data, ref int at)
    {
        string words = LegacyKoreanEncoding.DecodeStringA(data[Math.Min(at, data.Length)..], out int consumed);
        at += consumed;

        return words;
    }

    private static string ReadName(ReadOnlySpan<byte> body, int at) =>
        body.Length > at ? LegacyKoreanEncoding.DecodeStringA(body[at..], out _) : string.Empty;

    private static Direction FromServer(byte direction) => direction switch
    {
        0 => Direction.North,
        1 => Direction.East,
        2 => Direction.South,
        _ => Direction.West
    };

    /// <summary>Map number, its size in tiles, flags, a spare word, a hash of the file, then its name.</summary>
    private static MapInfo ReadMap(ReadOnlySpan<byte> body)
    {
        const int fixedLength = 9;

        if (body.Length < fixedLength)
        {
            throw new ProtocolException($"지도 안내가 {fixedLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new MapInfo(
            BinaryPrimitives.ReadUInt16BigEndian(body),
            body[2],
            body[3],
            LegacyKoreanEncoding.DecodeStringA(body[fixedLength..], out _));
    }

    /// <summary>The character's tile, then the size of the view around it, which we do not use yet.</summary>
    private static Tile ReadLocation(ReadOnlySpan<byte> body)
    {
        if (body.Length < 4)
        {
            throw new ProtocolException($"위치 안내가 4바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Tile(
            BinaryPrimitives.ReadInt16BigEndian(body),
            BinaryPrimitives.ReadInt16BigEndian(body[2..]));
    }
}
