using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// Which drawing of an effect sheet to show at each step. The original plays an effect in the order its row in
/// effect.tbl gives (first line the count, then one row per effect number) — effect 203 is "0 1 1", holding the
/// second drawing twice — not simply 0, 1, 2 … (docs/disassembly.md, 4.51 0x483870).
/// </summary>
public sealed class EffectSheetTests
{
    private const string Written = "# 번호 칸수 바탕가로 바탕세로 기준x 기준y 순서\n"
                                 + "203 2 111 85 55 70 0 1 1\n102 12 111 85 55 70\n7 4 68 75 34 62 0 0 1 1 2 2 3\n";

    [Fact]
    public void An_effect_plays_in_the_order_its_row_gives()
    {
        EffectSheet sheet = EffectSheet.Read(Written)[203];

        Assert.Equal(2, sheet.Frames);
        Assert.Equal([0, 1, 1], Enumerable.Range(0, sheet.Steps).Select(sheet.FrameAt));
    }

    [Fact]
    public void Without_an_order_it_plays_every_drawing_once_in_turn()
    {
        EffectSheet sheet = EffectSheet.Read(Written)[102];

        Assert.Equal(12, sheet.Steps);
        Assert.Equal(11, sheet.FrameAt(11));
    }

    [Fact]
    public void A_drawing_the_order_names_past_the_sheet_is_held_inside_it()
    {
        EffectSheet sheet = new(2, 111, 85, 55, 70, [0, 5]);

        Assert.Equal(1, sheet.FrameAt(1));
    }

    [Fact]
    public void An_effect_carries_the_canvas_and_the_point_that_lands_on_the_target()
    {
        // 일음지. The sparkle is small and high on the canvas; anchored at the feet it plays above the head,
        // which is what the original does and what cutting the drawing out on its own destroyed.
        EffectSheet sheet = EffectSheet.Read(Written)[203];

        Assert.Equal((111, 85), (sheet.Wide, sheet.Tall));
        Assert.Equal((55, 70), (sheet.AnchorX, sheet.AnchorY));
    }

    [Fact]
    public void A_row_without_a_canvas_is_passed_over_rather_than_placed_by_guess()
    {
        Assert.Empty(EffectSheet.Read("203 2 0 1 1\n"));
    }

    [Fact]
    public void Notes_and_blank_lines_are_passed_over()
    {
        Assert.Equal([203, 102, 7], EffectSheet.Read(Written).Keys);
    }
}
