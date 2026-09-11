using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Tidying only ever asks the server to swap two slots, so the answer has to be a run of swaps that ends
/// with everything at the front in the order it started.
/// </summary>
public sealed class PackOrderTests
{
    private static InventoryItem At(int slot) => new(slot, 0, 0, $"item{slot}", 1, 0, 0);

    /// <summary>Replays the swaps the way the server would, so the test checks the result, not the recipe.</summary>
    private static IReadOnlyList<int> After(IReadOnlyList<InventoryItem> carried)
    {
        Dictionary<int, string> shelf = carried.ToDictionary(item => item.Slot, item => item.Name);

        foreach ((int from, int to) in PackOrder.Tidy(carried))
        {
            shelf.TryGetValue(from, out string? moving);
            shelf.TryGetValue(to, out string? displaced);

            shelf.Remove(from);
            shelf.Remove(to);

            if (moving is not null)
            {
                shelf[to] = moving;
            }

            if (displaced is not null)
            {
                shelf[from] = displaced;
            }
        }

        return [.. shelf.Keys.OrderBy(slot => slot)];
    }

    [Fact]
    public void A_pack_with_no_gaps_is_left_alone()
    {
        Assert.Empty(PackOrder.Tidy([At(1), At(2), At(3)]));
    }

    [Fact]
    public void An_empty_pack_needs_nothing()
    {
        Assert.Empty(PackOrder.Tidy([]));
    }

    [Fact]
    public void Gaps_are_closed_from_the_front()
    {
        Assert.Equal([1, 2, 3], After([At(2), At(5), At(9)]));
    }

    [Fact]
    public void One_thing_far_down_comes_to_the_front()
    {
        Assert.Equal([1], After([At(40)]));
    }

    /// <summary>A swap can put something back where it was needed, and the count must still come out right.</summary>
    [Fact]
    public void Something_already_at_the_front_is_not_thrown_away()
    {
        Assert.Equal([1, 2, 3, 4], After([At(1), At(3), At(4), At(8)]));
    }
}
