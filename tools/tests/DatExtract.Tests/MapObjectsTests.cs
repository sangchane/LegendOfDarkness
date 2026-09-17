namespace Lod.DatExtract.Tests;

/// <summary>
/// What stands on a map (<see cref="MapObjects" />): which numbers have a picture and which palette colours one.
/// Both go wrong silently — a picture in the wrong palette still draws, just in the wrong colours.
/// </summary>
public sealed class MapObjectsTests
{
    [Fact]
    public void The_first_twelve_of_each_ten_thousand_have_no_picture()
    {
        // da-lib IntExtensions.IsRenderedTileIndex.
        Assert.False(MapObjects.IsDrawn(0));
        Assert.False(MapObjects.IsDrawn(12));
        Assert.True(MapObjects.IsDrawn(13));
        Assert.True(MapObjects.IsDrawn(10013));
        Assert.True(MapObjects.IsDrawn(20005));
    }

    [Fact]
    public void A_range_line_colours_every_number_in_it()
    {
        MapObjects.PaletteChoice choice = MapObjects.PaletteChoice.Read("2 13 0\n14 25 7\n");

        Assert.Equal(7, choice.For(14));
        Assert.Equal(7, choice.For(25));
        Assert.Equal(0, choice.For(13));
    }

    [Fact]
    public void A_single_line_beats_a_range_whichever_comes_first()
    {
        MapObjects.PaletteChoice before = MapObjects.PaletteChoice.Read("15415 54\n15400 15420 47\n");
        MapObjects.PaletteChoice after = MapObjects.PaletteChoice.Read("15400 15420 47\n15415 54\n");

        Assert.Equal(54, before.For(15415));
        Assert.Equal(54, after.For(15415));
        Assert.Equal(47, after.For(15416));
    }

    [Fact]
    public void A_number_no_line_names_takes_the_first_palette()
    {
        Assert.Equal(0, MapObjects.PaletteChoice.Read("2 13 5\n").For(999));
    }
}
