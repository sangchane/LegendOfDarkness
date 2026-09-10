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

    private byte _ordinal;
    private byte _step;

    // Written by the pump, read by whoever is drawing. A whole state at once, so a reader never sees a map
    // from one moment and a tile from another.
    private volatile WorldEntry? _state;
    private volatile int _reports;
    private volatile uint _serial;

    // Keyed by serial, which is the only name the server gives them at first.
    private readonly ConcurrentDictionary<uint, Character> _others = new();

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
                    // It may arrive after we have already been shown ourselves.
                    _others.TryRemove(_serial, out _);
                    continue;

                case DisplayCharacterCommand:
                    Show(ReadCharacter(HadesCipher.DecodeSecured(frame, session.Parameters)));
                    continue;

                case CreatureWalkedCommand:
                    Moved(HadesCipher.DecodeSecured(frame, session.Parameters));
                    continue;

                case RemoveCommand:
                    _others.TryRemove(
                        BinaryPrimitives.ReadUInt32BigEndian(HadesCipher.DecodeSecured(frame, session.Parameters)),
                        out _);
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

    /// <summary>Asks the server to say where we are again, which it answers with the map and the tile.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken) =>
        Send(RefreshCommand, [], cancellationToken);

    private Task Send(byte command, byte[] body, CancellationToken cancellationToken) =>
        session.Connection.SendAsync(
            HadesCipher.EncodeSecured(command, _ordinal++, body, session.Parameters),
            cancellationToken);

    /// <summary>Remembers somebody, unless it is us — the server shows us our own character too.</summary>
    private void Show(Character character)
    {
        if (character.Serial == _serial)
        {
            return;
        }

        _others[character.Serial] = character;
    }

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

        // A step says nothing about clothes, so keep the ones we were shown rather than undressing them.
        Show(_others.TryGetValue(serial, out Character? known)
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
