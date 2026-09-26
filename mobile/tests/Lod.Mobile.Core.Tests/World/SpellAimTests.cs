using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Who a spell goes to when it is pressed. A picked target wins; a target spell with nobody picked goes to ourselves —
/// on a phone there is no pointer to aim at our own feet, and 호르라마·쿠로 were refused with "마법 대상을 먼저 누르세요"
/// (격리 서버 실제 앱, 2026-09-26).
/// </summary>
public sealed class SpellAimTests
{
    private const uint Me = 7;

    [Fact]
    public void A_target_spell_goes_to_whoever_is_picked()
    {
        Assert.Equal(42u, SpellAim.Target(SpellTargetType.ChooseTarget, picked: 42, self: Me));
    }

    [Fact]
    public void A_target_spell_with_nobody_picked_goes_to_ourselves()
    {
        Assert.Equal(Me, SpellAim.Target(SpellTargetType.ChooseTarget, picked: 0, self: Me));
    }

    /// <summary>A spell that takes no target sends none, as before — the server aims it.</summary>
    [Theory]
    [InlineData(SpellTargetType.NoTarget)]
    [InlineData(SpellTargetType.Unusable)]
    public void A_spell_without_a_target_sends_none(SpellTargetType type)
    {
        Assert.Equal(0u, SpellAim.Target(type, picked: 42, self: Me));
    }
}
