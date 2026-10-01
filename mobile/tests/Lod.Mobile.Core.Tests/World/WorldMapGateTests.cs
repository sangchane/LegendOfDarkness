using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 월드맵 창은 서버가 보낸 대로 띄우되, 고른 뒤에는 서버가 창을 거둘 때까지, 닫은 뒤에는 서버가 새 창을 보낼 때까지
/// 다시 띄우지 않는다 — 두 번 고르거나 닫았는데 도로 열리지 않게.
/// </summary>
public sealed class WorldMapGateTests
{
    [Fact]
    public void A_window_the_server_sent_is_shown_once()
    {
        WorldMapGate gate = new();

        Assert.True(gate.ShouldShow(shown: 1, visible: false));
        Assert.False(gate.ShouldShow(shown: 1, visible: true));
    }

    [Fact]
    public void After_choosing_it_stays_down_until_the_server_withdraws_it()
    {
        WorldMapGate gate = new();
        gate.Chose(3);

        Assert.False(gate.ShouldShow(shown: 1, visible: false));
        Assert.False(gate.ShouldShow(shown: 2, visible: false));

        gate.Withdrawn();

        Assert.True(gate.ShouldShow(shown: 2, visible: false));
    }

    [Fact]
    public void After_closing_only_a_new_window_shows_and_the_map_button_waits()
    {
        WorldMapGate gate = new();
        gate.Closed(shown: 4);

        Assert.True(gate.Closing);
        Assert.False(gate.ShouldShow(shown: 4, visible: false));
        Assert.True(gate.ShouldShow(shown: 5, visible: false));

        gate.Shown();

        Assert.False(gate.Closing);
    }
}
