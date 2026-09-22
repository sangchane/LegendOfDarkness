using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// Where the landscape windows' column starts. The anchors are shares of the width inside the safe margins, so a share
/// worked out from the whole screen gave the gear window less than it needs and it spilled past the right edge.
/// </summary>
public sealed class SideColumnTests
{
    // 장비 창 한 벌: 고리 312(칸 48 x 4 + 종이인형 120, 가로는 틈 0) + 굴림 막대 8 + 속 여백 16 + 돌 틀 2. 고도에서 잰 값.
    private const float GearWindow = 338;

    private const float Most = 0.6f;

    private static float Width(float screen, float left, float right) =>
        (1 - SideColumn.LeftAnchor(screen, left, right, GearWindow, Most)) * (screen - left - right);

    /// <summary>16:9, the narrowest landscape the layout check walks. Before, the window ran to 642 on a 640 screen.</summary>
    [Fact]
    public void On_a_16_9_screen_the_column_holds_the_whole_gear_window()
    {
        Assert.True(Width(640, 8, 8) >= GearWindow - 0.01f);
    }

    /// <summary>An iPhone on its side keeps about 59 on each side for the notch and the corners, on top of the gutter.</summary>
    [Fact]
    public void An_iphone_on_its_side_gives_the_gear_window_its_whole_width_inside_the_notch_margins()
    {
        Assert.True(Width(852, 67, 67) >= GearWindow - 0.01f);
    }

    [Fact]
    public void The_column_is_exactly_the_window_wide_where_it_needs_more_than_the_share()
    {
        Assert.Equal(GearWindow, Width(800, 8, 8), 0.01f);
    }

    /// <summary>On a wide screen the column stops at the share it was given, a bit over a third.</summary>
    [Fact]
    public void A_wide_screen_keeps_the_column_at_its_share()
    {
        Assert.Equal(Most, SideColumn.LeftAnchor(1280, 8, 8, GearWindow, Most));
    }
}
