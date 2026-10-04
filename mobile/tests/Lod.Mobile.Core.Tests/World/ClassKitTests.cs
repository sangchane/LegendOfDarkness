using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>직업마다 보일 기술·마법과 대상 마법의 표(<c>assets/world/class-kit.txt</c>).</summary>
public sealed class ClassKitTests
{
    private static readonly ClassKit Kit = ClassKit.Read(
        "# 직업\t종류\t이름\t대상\n5\tskill\t단각\t-\n5\tspell\t다라밀공\t적\t수동\n5\tspell\t장풍\t적\n5\tspell\t쿠로토\t나\n");

    [Fact]
    public void A_listed_class_shows_only_what_is_listed()
    {
        Assert.True(Kit.Shows(5, false, "단각"));
        Assert.True(Kit.Shows(5, true, "다라밀공 (Lev:3/100)"));
        Assert.False(Kit.Shows(5, false, "통배권"));
        Assert.False(Kit.Shows(5, false, "Assail"));
        Assert.False(Kit.Shows(5, true, "단각"));
    }

    [Fact]
    public void A_class_with_no_lines_shows_everything()
    {
        Assert.True(Kit.Shows(3, true, "아무 마법"));
        Assert.True(Kit.Shows(null, false, "Assail"));
    }

    [Fact]
    public void Only_enemy_spells_aim_at_monsters()
    {
        Assert.True(Kit.AimsAtEnemy(5, "다라밀공 (Lev:1/100)"));
        Assert.False(Kit.AimsAtEnemy(5, "쿠로토"));
        Assert.False(Kit.AimsAtEnemy(3, "다라밀공"));
    }

    [Fact]
    public void A_by_hand_spell_is_left_to_the_player_in_auto_hunt()
    {
        Assert.False(Kit.AutoCasts(5, "다라밀공 (Lev:1/100)"));
        Assert.True(Kit.AutoCasts(5, "장풍"));
    }
}
