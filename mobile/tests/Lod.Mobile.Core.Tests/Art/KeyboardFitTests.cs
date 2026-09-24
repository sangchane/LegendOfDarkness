using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

public sealed class KeyboardFitTests
{
    // 가로 아이폰 15 Pro: 393 높이, 키보드는 그 절반쯤(209pt) — 키보드 위 끝은 184.
    private const float KeyboardTop = 184;
    private const float SafeTop = 8;
    private const float Gap = 8;

    [Fact]
    public void Keyboard_pixels_become_screen_units()
    {
        // 1179 픽셀 높이 화면을 393 단위로 그린다(3배). 627 픽셀 = 209 단위.
        Assert.Equal(209, KeyboardFit.Covered(627, 1179, 393), 3);
        Assert.Equal(0, KeyboardFit.Covered(0, 1179, 393));
        Assert.Equal(0, KeyboardFit.Covered(627, 0, 393));
    }

    [Fact]
    public void Nothing_moves_when_the_field_and_button_already_clear_the_keyboard()
    {
        Assert.Equal(0, KeyboardFit.Slide(40, 88, 150, KeyboardTop, SafeTop, Gap));
    }

    [Fact]
    public void Slides_just_enough_to_bring_the_button_above_the_keyboard()
    {
        // 버튼 아래 끝 230 → 230 + 8 - 184 = 54 만 올린다. 칸 위 끝 100 은 54 올려도 안전선 아래다.
        Assert.Equal(54, KeyboardFit.Slide(100, 148, 230, KeyboardTop, SafeTop, Gap));
    }

    [Fact]
    public void Gives_up_the_button_before_the_field_goes_under_the_notch()
    {
        // 버튼까지 보이려면 120 을 올려야 하지만 칸 위 끝(60)이 안전선(8)을 넘는다 — 52 에서 멈춘다.
        Assert.Equal(52, KeyboardFit.Slide(60, 108, 296, KeyboardTop, SafeTop, Gap));
    }

    [Fact]
    public void The_field_itself_always_clears_the_keyboard()
    {
        // 칸이 키보드 밑(250~298)에 있으면 위 끝이 안전선을 넘더라도 칸 아래 끝은 키보드 위로 올라온다.
        float slide = KeyboardFit.Slide(250, 298, 298, KeyboardTop, 280, Gap);

        Assert.Equal(298 + Gap - KeyboardTop, slide);
    }

    [Fact]
    public void Never_slides_down()
    {
        Assert.Equal(0, KeyboardFit.Slide(10, 58, 20, KeyboardTop, SafeTop, Gap));
    }

    [Fact]
    public void A_list_gives_up_height_so_the_input_bar_rides_on_the_keyboard()
    {
        // 창이 80 에서 시작하고, 머리·입력 줄이 120 을 쓴다. 184 - 80 - 120 < 0 → 목록은 0.
        Assert.Equal(0, KeyboardFit.ListRoom(150, 80, KeyboardTop, 120));

        // 머리를 접으면(입력 줄만 64) 목록에 40 이 남는다.
        Assert.Equal(40, KeyboardFit.ListRoom(150, 80, KeyboardTop, 64));

        // 자리가 넉넉하면 원래 높이를 넘지 않는다.
        Assert.Equal(150, KeyboardFit.ListRoom(150, 80, 700, 64));
    }
}
