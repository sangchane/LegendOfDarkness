using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

public sealed class EffectLookTests
{
    [Fact]
    public void Reads_bottom_and_tint_and_skips_notes_and_broken_lines()
    {
        var looks = EffectLook.Read("# 번호 바닥줄 빨강 초록 파랑\n42 22 255 255 255\n257 159 255 162 147\n9 1 2\n10 1 2 3 999\n");

        Assert.Equal(2, looks.Count);
        Assert.Equal(new EffectLook(22, 255, 255, 255), looks[42]);
        Assert.Equal(new EffectLook(159, 255, 162, 147), looks[257]);
    }
}
