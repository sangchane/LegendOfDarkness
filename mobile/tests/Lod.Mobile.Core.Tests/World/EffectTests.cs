using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The bytes the server writes for a skill's flash (ServerFormat29) and its sound (ServerFormat19).
/// </summary>
/// <remarks>
/// A flash comes in two shapes. On somebody it names the one it lands on first, then whoever made it, then an
/// animation for each in that same order. On the ground the first serial is zero and a tile follows instead.
/// </remarks>
public sealed class EffectTests
{
    [Fact]
    public void A_flash_on_somebody_puts_the_first_animation_on_the_first_serial()
    {
        Effect effect = WorldClient.ReadEffect(
        [
            0x00, 0x00, 0x03, 0x84, // 맞는 쪽 900
            0x00, 0x00, 0x00, 0x07, // 쓴 쪽 7
            0x00, 0xF9,             // 맞는 쪽 그림 249
            0x00, 0x0C,             // 쓴 쪽 그림 12
            0x00, 0x4B              // 속도 75
        ]);

        Assert.Equal(new Effect(900, 7, 249, 12, 75, null), effect);
    }

    [Fact]
    public void A_flash_on_the_ground_names_a_tile_instead_of_anybody()
    {
        Effect effect = WorldClient.ReadEffect(
        [
            0x00, 0x00, 0x00, 0x00, // 아무도 아니다
            0x00, 0x48,             // 그림 72
            0x00,
            0x64,                   // 속도 100
            0x00, 0x18,             // x 24
            0x00, 0x1B              // y 27
        ]);

        Assert.Equal(new Effect(0, 0, 72, 0, 100, new Tile(24, 27)), effect);
    }

    [Fact]
    public void A_sound_is_its_number_after_one_empty_byte()
    {
        Assert.Equal(81, WorldClient.ReadSound([0x00, 0x00, 0x51]));
    }

    [Fact]
    public void A_blow_brings_its_sound_in_the_last_byte_of_the_health_bar()
    {
        Assert.Equal(7, WorldClient.ReadHealthSound([0x00, 0x00, 0x03, 0x84, 0x00, 0x32, 0x07]));

        // 체력바 없이 소리만 보낼 때(하데스 SendSound)도 같은 모양이다 — 체력 자리가 255.
        Assert.Equal(14, WorldClient.ReadHealthSound([0x00, 0x00, 0x00, 0x07, 0x00, 0xFF, 0x0E]));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    public void A_health_bar_with_no_sound_plays_nothing(byte sound)
    {
        Assert.Null(WorldClient.ReadHealthSound([0x00, 0x00, 0x03, 0x84, 0x00, 0x32, sound]));
    }
}
