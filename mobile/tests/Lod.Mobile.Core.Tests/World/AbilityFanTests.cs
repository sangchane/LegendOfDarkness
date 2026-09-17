using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The skill and spell buttons fanned round the attack button. The places are typed by hand, so these tests are what
/// keeps them on the screen and out of each other's way: a button pressed by mistake next to the attack is worse
/// than no button at all.
/// </summary>
public sealed class AbilityFanTests
{
    private const int Gutter = 8;

    private static IEnumerable<(string Name, (int X, int Y) Centre, int Side)> Buttons()
    {
        yield return ("공격", AbilityFan.Attack, AbilityFan.AttackSide);
        yield return ("전환", AbilityFan.Switch, AbilityFan.ButtonSide);
        yield return ("다음", AbilityFan.Next, AbilityFan.ButtonSide);

        for (int index = 0; index < AbilityFan.Slots.Count; index++)
        {
            yield return ($"칸 {index + 1}", AbilityFan.Slots[index], AbilityFan.ButtonSide);
        }
    }

    [Fact]
    public void There_is_a_place_for_every_button_on_a_page()
    {
        Assert.Equal(AbilityFan.PerPage, AbilityFan.Slots.Count);
    }

    [Fact]
    public void Every_button_sits_inside_the_fan()
    {
        foreach ((string name, (int x, int y), int side) in Buttons())
        {
            int half = side / 2;

            Assert.True(x - half >= 0 && x + half <= AbilityFan.Width, $"{name} 이(가) 좌우로 넘친다");
            Assert.True(y - half >= 0 && y + half <= AbilityFan.Height, $"{name} 이(가) 위아래로 넘친다");
        }
    }

    /// <summary>
    /// A button is pressed by its square, not by the circle drawn on it, so two of them are apart only when their squares
    /// are — by the gutter the wireframes ask between different actions.
    /// </summary>
    [Fact]
    public void No_two_buttons_come_closer_than_a_gutter()
    {
        var all = Buttons().ToList();

        for (int first = 0; first < all.Count; first++)
        {
            for (int second = first + 1; second < all.Count; second++)
            {
                var one = all[first];
                var other = all[second];
                int reach = (one.Side + other.Side) / 2 + Gutter;

                bool apart = Math.Abs(one.Centre.X - other.Centre.X) >= reach
                             || Math.Abs(one.Centre.Y - other.Centre.Y) >= reach;

                Assert.True(apart, $"{one.Name} 과(와) {other.Name} 이(가) 너무 붙었다");
            }
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(14, 3)]
    public void Learned_abilities_are_split_six_to_a_page(int learned, int pages)
    {
        Assert.Equal(pages, AbilityFan.Pages(learned));
    }

    [Fact]
    public void A_later_page_holds_what_is_left_and_the_rest_stay_empty()
    {
        string[] learned = [.. Enumerable.Range(1, 14).Select(number => $"기술 {number}")];

        IReadOnlyList<string?> third = AbilityFan.Page(learned, 2);

        Assert.Equal(["기술 13", "기술 14", null, null, null, null], third);
    }

    [Fact]
    public void Next_goes_round_to_the_first_page_after_the_last()
    {
        Assert.Equal(1, AbilityFan.After(0, 14));
        Assert.Equal(0, AbilityFan.After(2, 14));
        Assert.Equal(0, AbilityFan.After(0, 3));
    }

    /// <summary>Forgetting abilities can take away the page being shown; the last one left is shown instead of nothing.</summary>
    [Fact]
    public void A_page_that_no_longer_exists_falls_back_to_the_last_one()
    {
        Assert.Equal(1, AbilityFan.Kept(2, 7));
        Assert.Equal(0, AbilityFan.Kept(1, 0));
        Assert.Equal(["기술 7", null, null, null, null, null], AbilityFan.Page([.. Enumerable.Range(1, 7).Select(number => $"기술 {number}")], 5));
    }
}
