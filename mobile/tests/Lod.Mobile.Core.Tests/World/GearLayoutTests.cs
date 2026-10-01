using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Where each worn place sits in the equipment panel, and which drawing fills it while it is empty. The squares are
/// measured off the original picture (<c>equip01.epf</c>) and the drawings come from <c>_nui_eq.txt</c>
/// (docs/original-equipment-window.md) — so these tests are what keeps a hand-typed table honest.
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

    /// <summary>
    /// The picture has thirteen squares and the server eighteen places, so every place is either on the picture or in
    /// the row above it — never both, never neither.
    /// </summary>
    [Fact]
    public void Every_place_is_on_the_picture_or_in_the_row_above_it()
    {
        for (int slot = 1; slot <= 18; slot++)
        {
            Assert.True(GearLayout.Square(slot) is null == GearLayout.Spare.Contains(slot), $"자리 {slot}");
        }

        Assert.Equal(13, Enumerable.Range(1, 18).Count(slot => GearLayout.Square(slot) is not null));
    }

    [Fact]
    public void The_squares_lie_inside_the_picture_and_apart()
    {
        List<(int X, int Y, int Width, int Height)> taken = [];

        for (int slot = 1; slot <= 18; slot++)
        {
            if (GearLayout.Square(slot) is not { } square)
            {
                continue;
            }

            Assert.InRange(square.X + square.Width, 1, GearLayout.PictureWidth);
            Assert.InRange(square.Y + square.Height, 1, GearLayout.PictureHeight);
            Assert.DoesNotContain(taken, other =>
                square.X < other.X + other.Width && other.X < square.X + square.Width &&
                square.Y < other.Y + other.Height && other.Y < square.Y + square.Height);
            taken.Add(square);
        }
    }

    /// <summary>The three big squares across the chest hold weapon, armour and shield, left to right.</summary>
    [Fact]
    public void The_big_squares_are_weapon_armour_and_shield()
    {
        var weapon = GearLayout.Square(1)!.Value;
        var armour = GearLayout.Square(2)!.Value;
        var shield = GearLayout.Square(3)!.Value;

        Assert.All([weapon, armour, shield], square => Assert.True(square.Width >= 40));
        Assert.True(weapon.X < armour.X && armour.X < shield.X);
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
        Assert.Equal(frame, GearLayout.Drawing(slot));
    }

    /// <summary>There are only fourteen drawings, so no place may ask for a fifteenth.</summary>
    [Fact]
    public void No_place_asks_for_a_drawing_that_is_not_in_the_file()
    {
        for (int slot = 1; slot <= 18; slot++)
        {
            Assert.InRange(GearLayout.Drawing(slot), 0, GearLayout.Drawings - 1);
        }
    }

    [Fact]
    public void Asking_for_a_place_the_server_cannot_name_is_refused()
    {
        Assert.Throws<KeyNotFoundException>(() => GearLayout.Drawing(0));
        Assert.Throws<KeyNotFoundException>(() => GearLayout.Drawing(19));
    }
}
