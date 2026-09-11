using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Where each worn place sits in the equipment panel, and which drawing fills it while it is empty. The
/// numbers come from the original's own screen layout — <c>setoa.dat</c> → <c>_nui_eq.txt</c>, written down
/// in docs/original-equipment-window.md — so these tests are what keeps a hand-typed table honest.
/// </summary>
public sealed class GearLayoutTests
{
    [Fact]
    public void Every_place_the_server_can_name_has_somewhere_to_sit()
    {
        for (int slot = 1; slot <= 18; slot++)
        {
            Assert.True(GearLayout.Has(slot), $"자리 {slot} 이 배치에 없다");
        }
    }

    [Fact]
    public void Nothing_the_server_does_not_name_has_a_place()
    {
        Assert.False(GearLayout.Has(0));
        Assert.False(GearLayout.Has(19));
    }

    [Fact]
    public void No_two_places_share_a_cell()
    {
        List<(int Column, int Row)> taken = [];

        for (int slot = 1; slot <= 18; slot++)
        {
            (int column, int row, _) = GearLayout.Of(slot);
            Assert.DoesNotContain((column, row), taken);
            taken.Add((column, row));
        }
    }

    /// <summary>
    /// Five columns and six rows, because the panel is a ring: the middle column belongs to the paper doll
    /// for rows three to five, and the two rows that use it are the ones with no doll beside them.
    /// </summary>
    [Fact]
    public void The_ring_keeps_the_middle_clear_where_the_doll_stands()
    {
        for (int slot = 1; slot <= 18; slot++)
        {
            (int column, int row, _) = GearLayout.Of(slot);

            Assert.InRange(column, 0, 4);
            Assert.InRange(row, 0, 5);

            bool middle = column == 2;
            bool besideTheDoll = row is >= 2 and <= 4;

            Assert.False(middle && besideTheDoll, $"자리 {slot} 이 종이인형 자리에 겹친다");
        }
    }

    [Theory]
    [InlineData(4, 2, 0)]    // 투구 — 맨 위 가운데
    [InlineData(16, 2, 1)]   // 겉투구 — 투구 바로 아래
    [InlineData(5, 1, 1)]    // 귀고리 — 왼쪽 안쪽
    [InlineData(6, 3, 1)]    // 목걸이 — 오른쪽 안쪽
    [InlineData(14, 0, 2)]   // 장신구 — 왼쪽 바깥
    [InlineData(2, 1, 2)]    // 갑옷
    [InlineData(15, 3, 2)]   // 겉옷
    [InlineData(17, 4, 2)]   // 장신구2
    [InlineData(1, 1, 3)]    // 무기
    [InlineData(3, 3, 3)]    // 방패
    [InlineData(18, 4, 3)]   // 장신구3
    [InlineData(9, 0, 4)]    // 왼팔
    [InlineData(7, 1, 4)]    // 왼손
    [InlineData(8, 3, 4)]    // 오른손
    [InlineData(10, 4, 4)]   // 오른팔
    [InlineData(12, 1, 5)]   // 다리
    [InlineData(13, 2, 5)]   // 신발 — 맨 아래 가운데
    [InlineData(11, 3, 5)]   // 허리
    public void Each_place_sits_where_the_original_put_it(int slot, int column, int row)
    {
        (int actualColumn, int actualRow, _) = GearLayout.Of(slot);

        Assert.Equal(column, actualColumn);
        Assert.Equal(row, actualRow);
    }

    /// <summary>
    /// The empty-slot drawings are <c>_nui_eqi.spf</c> frames, and the original names them slot by slot in
    /// <c>_nui_eq.txt</c>. Four of the eighteen places borrow a neighbour's drawing rather than having one
    /// of their own — the overhelm wears the helmet's, and the three trinkets borrow armour and cloak.
    /// </summary>
    [Theory]
    [InlineData(1, 6)]    // 무기
    [InlineData(2, 3)]    // 갑옷
    [InlineData(3, 7)]    // 방패
    [InlineData(4, 0)]    // 투구
    [InlineData(5, 1)]    // 귀고리
    [InlineData(6, 2)]    // 목걸이
    [InlineData(7, 9)]    // 왼손
    [InlineData(8, 10)]   // 오른손
    [InlineData(9, 5)]    // 왼팔
    [InlineData(10, 8)]   // 오른팔
    [InlineData(11, 12)]  // 허리
    [InlineData(12, 11)]  // 다리
    [InlineData(13, 13)]  // 신발
    [InlineData(14, 3)]   // 장신구 — 갑옷 그림을 같이 쓴다
    [InlineData(15, 4)]   // 겉옷
    [InlineData(16, 0)]   // 겉투구 — 투구 그림을 같이 쓴다
    [InlineData(17, 4)]   // 장신구2 — 겉옷 그림을 같이 쓴다
    [InlineData(18, 4)]   // 장신구3 — 겉옷 그림을 같이 쓴다
    public void An_empty_place_shows_the_drawing_the_original_named(int slot, int frame)
    {
        (_, _, int actualFrame) = GearLayout.Of(slot);

        Assert.Equal(frame, actualFrame);
    }

    /// <summary>There are only fourteen drawings, so no place may ask for a fifteenth.</summary>
    [Fact]
    public void No_place_asks_for_a_drawing_that_is_not_in_the_file()
    {
        for (int slot = 1; slot <= 18; slot++)
        {
            (_, _, int frame) = GearLayout.Of(slot);
            Assert.InRange(frame, 0, GearLayout.Drawings - 1);
        }
    }

    [Fact]
    public void Asking_for_a_place_the_server_cannot_name_is_refused()
    {
        Assert.Throws<KeyNotFoundException>(() => GearLayout.Of(0));
        Assert.Throws<KeyNotFoundException>(() => GearLayout.Of(19));
    }
}
