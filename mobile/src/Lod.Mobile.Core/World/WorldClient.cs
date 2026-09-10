using System.Buffers.Binary;
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

    private byte _ordinal;
    private byte _step;

    // Written by the pump, read by whoever is drawing. A whole state at once, so a reader never sees a map
    // from one moment and a tile from another.
    private volatile WorldEntry? _state;
    private volatile int _reports;

    /// <summary>Where the server last said we are, or null until it has said so.</summary>
    public WorldEntry? State => _state;

    /// <summary>
    /// How many times the server has stated our position. It does that on entry, on a refresh, and when it
    /// refuses a step — so a rise the client did not ask for means a walk was turned down.
    /// </summary>
    public int PositionReports => _reports;

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
