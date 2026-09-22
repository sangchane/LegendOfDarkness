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

    // 가로 800x360 에서 잰 소지품 창(2026-09-23, 고도 --layout) — 창이 위 줄 아래에 붙어 있던 때다. 위 줄 아래 창이 받는
    // 높이는 360 − 위 여백 8 − 위 줄 자리 72 − 아래 여백 8 = 272 다. 그림 칸을 뺀 나머지(제목줄·탭·장 넘김·입기 줄·틈·틀)가
    // 218, 칸 한 줄은 48 이고 줄 사이는 4 다. 두 줄(100)이면 318 이 되어 입기 줄이 화면 밑으로 38 빠졌다.
    private const float LandscapeRoom = 272;
    private const float PackRest = 218;
    private const float Cell = 48;
    private const float RowGap = 4;

    [Fact]
    public void On_its_side_the_pack_page_drops_to_the_rows_that_leave_the_whole_window_on_the_screen()
    {
        int rows = Paging.RowsThatFit(LandscapeRoom, PackRest, Cell, RowGap, most: 2);

        Assert.Equal(1, rows);
        Assert.True(PackRest + (rows * Cell) + ((rows - 1) * RowGap) <= LandscapeRoom);
    }

    /// <summary>
    /// 지금의 가로 창은 화면 높이를 다 쓰고(344) 탭·입기 줄이 옆 기둥에 있다. 제목줄·틀·틈 50 을 뺀 294 에 장 넘김과 그 틈 56 을
    /// 두고도 네 줄이 든다 — 창이 커진 만큼 줄이 는다(2026-09-23).
    /// </summary>
    [Fact]
    public void On_its_side_the_taller_window_gives_the_pack_page_four_rows()
    {
        Assert.Equal(4, Paging.RowsThatFit(344 - 50, 56, Cell, RowGap, most: 4));
    }

    /// <summary>Upright there is room to spare, so the page keeps the four rows it was laid out with.</summary>
    [Fact]
    public void Upright_the_pack_page_keeps_its_four_rows()
    {
        // 세로 360x780: 780 − 8 − 72 − 8.
        Assert.Equal(4, Paging.RowsThatFit(692, PackRest, Cell, RowGap, most: 4));
    }

    [Fact]
    public void Exactly_enough_room_takes_the_row()
    {
        Assert.Equal(2, Paging.RowsThatFit(PackRest + (2 * Cell) + RowGap, PackRest, Cell, RowGap, most: 2));
        Assert.Equal(1, Paging.RowsThatFit(PackRest + (2 * Cell) + RowGap - 1, PackRest, Cell, RowGap, most: 2));
    }

    /// <summary>A page with no row has nothing to show; one row is kept even when the window cannot fit at all.</summary>
    [Fact]
    public void A_page_never_has_no_row()
    {
        Assert.Equal(1, Paging.RowsThatFit(100, PackRest, Cell, RowGap, most: 2));
    }
}
