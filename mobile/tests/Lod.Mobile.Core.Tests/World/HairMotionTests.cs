using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 고를 수 있는 머리 모양마다 동작 그림이 다 있는지. 원작 아카이브는 머리 모양마다 평타(02)·손 들기(03)·직업 동작(b~f)
/// 파일을 갖고(khan.dat mh01202·mh01203·mh012b~f) 동작마다 거기서 머리를 그린다. 앱은 동작 그림이 없는 조각을 동작
/// 동안 빼므로(Actor), 01 만 뽑혀 있던 머리 51가지는 쿠로토·평타 때 머리카락이 사라졌다(사용자, 2026-09-25).
/// </summary>
public sealed class HairMotionTests
{
    private static readonly string[] Moves = ["02", "03", "b", "c", "d", "e", "f"];

    [Fact]
    public void Every_hair_that_can_be_picked_is_cut_for_every_motion()
    {
        string parts = Parts();
        List<string> missing = [];

        foreach ((int gender, char letter) in new[] { (1, 'm'), (2, 'w') })
        {
            foreach (int number in HairStyles.For(gender))
            {
                missing.AddRange(Moves
                    .Select(move => $"{letter}h{number:000}{move}.png")
                    .Where(name => !File.Exists(Path.Combine(parts, name))));
            }
        }

        Assert.True(missing.Count == 0, $"동작 그림이 없는 머리 {missing.Count}개: {string.Join(" ", missing.Take(20))}");
    }

    private static string Parts()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string parts = Path.Combine(at.FullName, "mobile", "client", "assets", "actor", "parts");

            if (Directory.Exists(parts))
            {
                return parts;
            }
        }

        throw new DirectoryNotFoundException("mobile/client/assets/actor/parts 를 찾지 못했습니다.");
    }
}
