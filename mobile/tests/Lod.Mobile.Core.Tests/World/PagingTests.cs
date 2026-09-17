using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>Splitting a list into pages — the pack's pictures, and the skills round the attack button.</summary>
public sealed class PagingTests
{
    [Theory]
    [InlineData(0, 24, 1)]
    [InlineData(24, 24, 1)]
    [InlineData(25, 24, 2)]
    [InlineData(60, 24, 3)]
    [InlineData(60, 12, 5)]
    public void A_list_takes_as_many_pages_as_it_fills(int count, int perPage, int pages)
    {
        Assert.Equal(pages, Paging.Pages(count, perPage));
    }

    /// <summary>The last page is padded, so a grid of pictures keeps its height and the panel does not jump.</summary>
    [Fact]
    public void The_last_page_is_padded_with_empty_places()
    {
        string[] things = [.. Enumerable.Range(1, 26).Select(number => $"물건 {number}")];

        IReadOnlyList<string?> second = Paging.Page(things, 1, 24);

        Assert.Equal(24, second.Count);
        Assert.Equal(["물건 25", "물건 26"], second.Take(2));
        Assert.All(second.Skip(2), Assert.Null);
    }

    [Fact]
    public void Turning_goes_round_both_ways()
    {
        Assert.Equal(1, Paging.After(0, 60, 24));
        Assert.Equal(0, Paging.After(2, 60, 24));
        Assert.Equal(2, Paging.Before(0, 60, 24));
        Assert.Equal(1, Paging.Before(2, 60, 24));
    }

    [Fact]
    public void A_page_that_is_gone_falls_back_to_the_last()
    {
        Assert.Equal(0, Paging.Kept(2, 20, 24));
        Assert.Equal(1, Paging.Kept(5, 30, 24));
    }
}
