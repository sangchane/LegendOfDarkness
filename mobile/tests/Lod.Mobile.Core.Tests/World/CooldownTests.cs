using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// How long until a skill or spell can be used again (0x3F). Hades writes it as <c>ServerFormat3F</c>: which pane
/// (0 spells, 1 skills), the slot, and the seconds.
/// </summary>
public sealed class CooldownTests
{
    [Fact]
    public void A_skill_says_its_slot_and_how_long()
    {
        Cooldown cooling = WorldClient.ReadCooldown([0x01, 0x05, 0x00, 0x00, 0x00, 0x06]);

        Assert.True(cooling.Skill);
        Assert.Equal(5, cooling.Slot);
        Assert.Equal(6, cooling.Seconds);
    }

    [Fact]
    public void A_spell_says_so()
    {
        Cooldown cooling = WorldClient.ReadCooldown([0x00, 0x02, 0x00, 0x00, 0x00, 0x1E]);

        Assert.False(cooling.Skill);
        Assert.Equal(2, cooling.Slot);
        Assert.Equal(30, cooling.Seconds);
    }

    [Fact]
    public void A_line_too_short_to_hold_anything_is_refused()
    {
        Assert.Throws<Lod.Mobile.Core.Protocol.ProtocolException>(() => WorldClient.ReadCooldown([0x01, 0x05]));
    }

    /// <summary>What is left counts down and stops at nothing — a count that went negative would read as ready again.</summary>
    [Theory]
    [InlineData(0, 6)]
    [InlineData(2, 4)]
    [InlineData(6, 0)]
    [InlineData(9, 0)]
    public void What_is_left_counts_down_to_nothing(int secondsPassed, int left)
    {
        DateTime started = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(left, Cooldown.Left(started.AddSeconds(6), started.AddSeconds(secondsPassed)));
    }
}
