using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The world map's cards: one per place the server offers, in the server's order, each saying what kind of place it is
/// and where it lands — from the <c>area</c> lines of <c>guide.txt</c>. Nothing is made up when a line is missing.
/// </summary>
public sealed class WorldMapCardsTests
{
    private const string Guide = """
        area 20373 1 town 노비스마을
        area 20028 11 field 우드랜드입구
        exit 20373 69 26 노비스평원A
        """;

    private static readonly WorldMapInfo Field = new("field001", 1,
    [
        new WorldMapNode("노비스마을", 20373, 34, 34, 200, 200),
        new WorldMapNode("우드랜드", 20028, 10, 21, 516, 176),
        new WorldMapNode("어딘가", 99999, 1, 1, 10, 10),
    ]);

    [Fact]
    public void Each_place_is_a_card_in_the_servers_order()
    {
        IReadOnlyList<WorldMapCard> cards = WorldMapCards.From(Field, MapGuide.Read(Guide));

        Assert.Equal(["노비스마을", "우드랜드", "어딘가"], cards.Select(card => card.Name));
        Assert.Equal([20373, 20028, 99999], cards.Select(card => card.AreaId));
    }

    [Fact]
    public void A_card_says_the_kind_where_it_lands_and_a_level_only_above_one()
    {
        IReadOnlyList<WorldMapCard> cards = WorldMapCards.From(Field, MapGuide.Read(Guide));

        Assert.Equal("마을", cards[0].Kind);
        Assert.Equal("노비스마을", cards[0].Arrival);
        Assert.Equal(0, cards[0].Level);

        Assert.Equal("사냥터", cards[1].Kind);
        Assert.Equal("우드랜드입구", cards[1].Arrival);
        Assert.Equal(11, cards[1].Level);
    }

    [Fact]
    public void A_place_the_guide_does_not_know_says_nothing_more()
    {
        WorldMapCard card = WorldMapCards.From(Field, MapGuide.Read(Guide))[2];

        Assert.Equal(string.Empty, card.Kind);
        Assert.Equal(string.Empty, card.Arrival);
        Assert.Equal(0, card.Level);
    }

    /// <summary>The new lines do not disturb the old ones.</summary>
    [Fact]
    public void Area_lines_leave_exits_alone()
    {
        MapGuide guide = MapGuide.Read(Guide);

        Assert.Single(guide.ExitsOn(20373));
        Assert.Null(guide.Place(20374));
    }
}
