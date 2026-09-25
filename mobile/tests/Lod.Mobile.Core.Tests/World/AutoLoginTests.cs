using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

public sealed class AutoLoginAccountTests
{
    [Fact]
    public void Parse_builds_an_account_from_both_saved_lines()
    {
        Assert.Equal(new AutoLoginAccount("wren", "test1234"), AutoLoginAccount.Parse("wren", "test1234"));
    }

    [Fact]
    public void Parse_rejects_a_missing_username_or_password()
    {
        Assert.Null(AutoLoginAccount.Parse(string.Empty, "test1234"));
        Assert.Null(AutoLoginAccount.Parse("wren", string.Empty));
    }

    [Fact]
    public void ToLines_round_trips_through_Parse()
    {
        AutoLoginAccount account = new("wren", "test1234");

        Assert.Equal(account, AutoLoginAccount.Parse(account.ToLines()[0], account.ToLines()[1]));
    }
}

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
