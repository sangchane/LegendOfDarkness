using Lod.Mobile.Core.Automation;

namespace Lod.Mobile.Core.Tests.World;

public sealed class AutoLoginGateTests
{
    [Fact]
    public void A_fresh_gate_may_submit()
    {
        Assert.True(new AutoLoginGate().MaySubmit);
    }

    [Fact]
    public void An_explicit_logout_blocks_submitting_for_the_rest_of_the_run()
    {
        AutoLoginGate gate = new();

        gate.NoteLogout();

        Assert.False(gate.MaySubmit);
    }

    [Fact]
    public void Logout_stays_blocked_even_if_noted_more_than_once()
    {
        AutoLoginGate gate = new();

        gate.NoteLogout();
        gate.NoteLogout();

        Assert.False(gate.MaySubmit);
    }
}
