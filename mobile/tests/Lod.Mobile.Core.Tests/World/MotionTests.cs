using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>A body motion (0x1A, ServerFormat1A): whose serial, which motion, its speed, then a sound byte.</summary>
public sealed class MotionTests
{
    [Fact]
    public void A_motion_names_somebody_the_motion_and_its_speed()
    {
        Motion motion = WorldClient.ReadMotion(
        [
            0x00, 0x00, 0x03, 0x84, // 900
            0x85,                   // 133 돌려차기
            0x00, 0x14,             // 속도 20
            0xFF                    // 소리 없음
        ]);

        Assert.Equal(new Motion(900, 133, 20), motion);
    }
}
