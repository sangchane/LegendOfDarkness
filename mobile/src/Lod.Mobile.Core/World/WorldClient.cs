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
public sealed class WorldClient(WorldSession session)
{
    private const byte WalkCommand = 0x06;
    private const byte RefreshCommand = 0x38;
    private const byte MapChangedCommand = 0x15;
    private const byte LocationCommand = 0x04;
    private const byte OwnSerialCommand = 0x05;
    private const byte DisplayCharacterCommand = 0x33;
    private const byte CreatureWalkedCommand = 0x0C;
    private const byte RemoveCommand = 0x0E;
    private const byte AddToPackCommand = 0x0F;
    private const byte ShowCreaturesCommand = 0x07;
    private const byte AttackCommand = 0x13;
    private const byte HealthCommand = 0x13;
    private const byte SpokenCommand = 0x0A;
    private const byte BodyMotionCommand = 0x1A;
    private const byte TalkCommand = 0x0E;
    private const byte UseCommand = 0x1C;
    private const byte WornCommand = 0x37;
    private const byte TookOffCommand = 0x38;

    private byte _ordinal;
    private byte _step;

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

    // Everything on the floor that is not a player, by the same serial the server removes them by.
    private readonly ConcurrentDictionary<uint, Creature> _creatures = new();

    // How hurt each of them is, out of a hundred. The server never says more than that about somebody else.
    private readonly ConcurrentDictionary<uint, int> _health = new();

    // Drained by whoever is drawing, because a motion is a moment rather than a state.
    private readonly ConcurrentQueue<uint> _motions = new();

    private volatile string _said = string.Empty;
    private volatile int _saidCount;

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
    /// Takes the next figure the server said had moved its body, if any. The server tells everyone nearby
    /// when somebody swings, and this is how that reaches whatever is drawing them.
    /// </summary>
    /// <remarks>
    /// It says which motion as well, and we ignore that: there is one motion we can draw. The reference
    /// client does the same — every one of these plays its attack (map-scene.ts). Telling the skill
    /// motions apart needs skill.tbl, which is written up in docs/original-sprite-animation.md section 3.
    /// </remarks>
    public bool TakeMotion(out uint serial) => _motions.TryDequeue(out serial);

    /// <summary>The last thing the server said in words — a refused blow, a greeting, a warning.</summary>
    public string Said => _said;

    /// <summary>How many times it has spoken, so a reader can tell a repeat from a new line.</summary>
    public int SaidCount => _saidCount;

    /// <summary>Monsters and merchants the server has shown us, by serial.</summary>
    public IReadOnlyCollection<Creature> Creatures => (IReadOnlyCollection<Creature>)_creatures.Values;

    /// <summary>What we are carrying, as the server has told us, in slot order.</summary>
    public IReadOnlyList<InventoryItem> Pack => [.. _pack.Values.OrderBy(item => item.Slot)];

    /// <summary>What the character has on, in the order the places are numbered.</summary>
    public IReadOnlyList<WornItem> Worn => [.. _worn.Values.OrderBy(item => item.Slot)];

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
    public async Task PumpAsync(CancellationToken cancellationToken)
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
                    break;

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
                        _motions.Enqueue(BinaryPrimitives.ReadUInt32BigEndian(motion));
                    }
                }

                    continue;

                case HealthCommand:
                    ReadHealth(HadesCipher.DecodeSecured(frame, session.Parameters));
                    continue;

                case SpokenCommand:
                {
                    ReadOnlySpan<byte> spoken = HadesCipher.DecodeSecured(frame, session.Parameters);

                    // A sound with no words is still this packet; there is simply nothing to show.
                    if (spoken.Length > 3)
                    {
                        _said = LegacyKoreanEncoding.DecodeStringB(spoken[1..], out _);
                        _saidCount++;
                    }
                }

                    continue;

                case ShowCreaturesCommand:
                    foreach (Creature creature in ReadCreatures(HadesCipher.DecodeSecured(frame, session.Parameters)))
                    {
                        _creatures[creature.Serial] = creature;
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

                case AddToPackCommand:
                    {
                        InventoryItem carried = ReadPackItem(HadesCipher.DecodeSecured(frame, session.Parameters));
                        _pack[carried.Slot] = carried;
                    }

                    continue;

                case CreatureWalkedCommand:
                    Moved(HadesCipher.DecodeSecured(frame, session.Parameters));
                    continue;

                case RemoveCommand:
                {
                    uint gone = BinaryPrimitives.ReadUInt32BigEndian(
                        HadesCipher.DecodeSecured(frame, session.Parameters));

                    _others.TryRemove(gone, out _);
                    _creatures.TryRemove(gone, out _);
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
    /// Says something out loud. The server treats a line beginning with a known word as a command when the
    /// speaker is allowed to give one, which is how a test gets an item into an empty pack.
    /// </summary>
    public Task SayAsync(string text, CancellationToken cancellationToken) =>
        Send(TalkCommand, [0, .. LegacyKoreanEncoding.EncodeStringA(text)], cancellationToken);

    /// <summary>
    /// Strikes whatever is in front of us. The server decides whether that hits anything — it knows where
    /// everyone stands and how recently we last swung — so nothing is assumed here about the outcome.
    /// One tap is one blow: it does not chase and does not repeat.
    /// </summary>
    public Task AttackAsync(CancellationToken cancellationToken) =>
        Send(AttackCommand, [], cancellationToken);

    /// <summary>
    /// Uses what is in one pack slot. What that means is the item's own business — boots are worn,
    /// food is eaten — so nothing is assumed here beyond the slot number. The server answers a piece
    /// of clothing by describing us again, which is how the figure comes to be redrawn.
    /// </summary>
    public Task UseAsync(int slot, CancellationToken cancellationToken) =>
        Send(UseCommand, [(byte)slot], cancellationToken);

    /// <summary>Asks the server to say where we are again, which it answers with the map and the tile.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken) =>
        Send(RefreshCommand, [], cancellationToken);

    private Task Send(byte command, byte[] body, CancellationToken cancellationToken) =>
        session.Connection.SendAsync(
            HadesCipher.EncodeSecured(command, _ordinal++, body, session.Parameters),
            cancellationToken);

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

        int left = BinaryPrimitives.ReadUInt16BigEndian(body[4..]);

        if (left > 100)
        {
            return;
        }

        _health[BinaryPrimitives.ReadUInt32BigEndian(body)] = left;
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

    /// <summary>The name, if the server got as far as writing one.</summary>
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
