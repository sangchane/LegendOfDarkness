using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Our server's own 0x5D says how much one blow took or one heal gave — the original only sends a percentage
/// (0x13). target(4) · source(4) · amount(4) · kind(1), big-endian.
/// </summary>
public sealed class FigureTests
{
    [Fact]
    public void A_blow_is_whose_by_whom_how_much()
    {
        Figure figure = WorldClient.ReadFigure(
            [0x00, 0x01, 0x02, 0x03, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x01, 0x2C, 0x00]);

        Assert.Equal(new Figure(0x00010203, 9, 300, FigureKind.Damage), figure);
    }

    [Fact]
    public void A_heal_says_kind_one()
    {
        Figure figure = WorldClient.ReadFigure(
            [0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A, 0x01]);

        Assert.Equal(FigureKind.Heal, figure.Kind);
        Assert.Equal(42, figure.Amount);
        Assert.Equal(0u, figure.Source);
    }

    /// <summary>A number the reader does not know is still drawn — as damage, the common case.</summary>
    [Fact]
    public void An_unknown_kind_reads_as_damage()
    {
        Figure figure = WorldClient.ReadFigure(
            [0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x05]);

        Assert.Equal(FigureKind.Damage, figure.Kind);
    }

    /// <summary>An amount over int.MaxValue on the wire is held at int.MaxValue rather than turning negative.</summary>
    [Fact]
    public void A_huge_amount_does_not_turn_negative()
    {
        Figure figure = WorldClient.ReadFigure(
            [0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00]);

        Assert.Equal(int.MaxValue, figure.Amount);
    }

    [Fact]
    public void A_short_figure_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadFigure(
            [0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]));
    }
}
