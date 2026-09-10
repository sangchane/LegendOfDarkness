using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The bytes the server writes when it shows what is standing on the floor (ServerFormat07): a count, then
/// a record each. A merchant is named and a monster is not, so the record's own kind decides how far the
/// next one starts.
/// </summary>
public sealed class CreatureTests
{
    private static byte[] Record(int x, int y, uint serial, int sprite, byte direction, byte kind) =>
    [
        (byte)(x >> 8), (byte)x,
        (byte)(y >> 8), (byte)y,
        (byte)(serial >> 24), (byte)(serial >> 16), (byte)(serial >> 8), (byte)serial,
        (byte)(sprite >> 8), (byte)sprite,
        0, 0, 0, 0,          // 서버가 비워 두는 자리
        direction,
        0,
        kind
    ];

    private static byte[] Named(string name) => [(byte)name.Length, .. name.Select(letter => (byte)letter)];

    [Fact]
    public void A_monster_is_read_without_a_name()
    {
        IReadOnlyList<Creature> shown = WorldClient.ReadCreatures(
            [0x00, 0x01, .. Record(24, 27, 900, 16385, 2, 0)]);

        Creature only = Assert.Single(shown);

        Assert.Equal(900u, only.Serial);
        Assert.Equal(new Tile(24, 27), only.Where);
        Assert.Equal(Direction.South, only.Facing);
        Assert.Equal(16385, only.Sprite);
        Assert.Equal(CreatureKind.Hostile, only.Kind);
        Assert.Equal(string.Empty, only.Name);
    }

    /// <summary>
    /// The one that matters: a merchant carries a name, so whatever follows it starts later. Read the
    /// merchant as though it were a monster and the next record is nonsense.
    /// </summary>
    [Fact]
    public void A_merchant_is_named_and_does_not_shift_what_follows_it()
    {
        IReadOnlyList<Creature> shown = WorldClient.ReadCreatures(
        [
            0x00, 0x02,
            .. Record(10, 11, 501, 42, 1, 2), .. Named("Jumo"),
            .. Record(12, 13, 502, 16385, 0, 0)
        ]);

        Assert.Equal(2, shown.Count);
        Assert.Equal("Jumo", shown[0].Name);
        Assert.Equal(CreatureKind.Merchant, shown[0].Kind);

        Assert.Equal(502u, shown[1].Serial);
        Assert.Equal(new Tile(12, 13), shown[1].Where);
        Assert.Equal(Direction.North, shown[1].Facing);
    }

    [Fact]
    public void A_packet_that_stops_short_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadCreatures([0x00]));
        Assert.Throws<ProtocolException>(() =>
            WorldClient.ReadCreatures([0x00, 0x02, .. Record(1, 1, 1, 1, 0, 0)]));
    }
}
