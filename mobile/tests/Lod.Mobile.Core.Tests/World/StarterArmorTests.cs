using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>만들기 미리보기의 레벨 1 갑옷 — 5.99 직업별 그림 번호, 직업을 안 골랐으면 맨몸.</summary>
public sealed class StarterArmorTests
{
    [Theory]
    [InlineData((byte)1, 2)]
    [InlineData((byte)2, 4)]
    [InlineData((byte)3, 6)]
    [InlineData((byte)4, 5)]
    [InlineData((byte)5, 3)]
    public void Each_class_wears_its_own_level_one_armour(byte path, int armour) =>
        Assert.Equal(armour, StarterArmor.For(path));

    [Fact]
    public void No_class_yet_is_bare() => Assert.Equal(0, StarterArmor.For(null));
}
