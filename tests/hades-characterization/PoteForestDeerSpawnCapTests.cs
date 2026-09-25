using System.Net;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 포테의숲1~3존은 넓어서(50x50·50x50·70x70) <c>MonolithComponent</c>의 넓이-스케일링이 정의 마릿수 1인
/// 사슴도 <c>round(1 × √spread × 0.7)</c> = 2 로 늘려 버렸다(사용자 보고 "사슴이 너무 강하다",
/// 2026-09-26 — 조사해 보니 사슴 한 마리가 아니라 몰래 두 마리가 서 있었다). 사용자 결정: 원래 정의 마릿수가
/// 1인 것은 스케일링에서 빼고 늘 한 마리로 둔다. 이 시험은 그 한 마리를 확인한다 — 나머지(정의가 여럿인
/// 몬스터가 계속 늘어나는지)는 <see cref="SpawnCountTests"/> 가 이미 지킨다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class PoteForestDeerSpawnCapTests : IDisposable
{
    private const string DeerName = "사슴";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Theory]
    [InlineData(20263, 50)] // 포테의숲1존
    [InlineData(20264, 50)] // 포테의숲2존
    [InlineData(20265, 70)] // 포테의숲3존
    public async Task Only_one_deer_stands_in_the_wide_forest_zones(int areaId, int side)
    {
        string name = $"deercap{areaId}";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (areaId, side / 2, side / 2));
        IsolateTheDeer(server, areaId);
        Waiting.MakeGameMaster(server, name);

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == areaId, $"포테의숲({areaId})에 들어가지 못했습니다.", _deadline.Token);

        // 다 서도록 넉넉히 기다린다 — SpawnCountTests 와 같은 근거(1초에 한 마리씩, 그 두 배로 여유를 둔다).
        await Task.Delay(TimeSpan.FromSeconds(15), _deadline.Token);

        // 시야(12칸)보다 넓은 맵이라 열 칸 간격으로 옮겨 다니며 모은다. 사슴은 젠 뒤 자리를 지키지 않을 수
        // 있으므로(Wander) 세렬 번호로 셈해 같은 개체를 두 번 세지 않는다.
        HashSet<uint> deer = [];

        for (int y = 5; y < side; y += 10)
        {
            for (int x = 5; x < side; x += 10)
            {
                await world.SayAsync($"/tp \"{Zone(areaId)}\" {x} {y}", _deadline.Token);
                await Task.Delay(1200, _deadline.Token);

                // 몬스터는 이름을 안 보낸다(ReadCreatures — Merchant 만 이름이 실린다). 그래서 이름이 아니라
                // "이 존에서 사슴 말고는 아무것도 못 서게" 지워서 남는 Hostile 이 곧 사슴이게 만든다.
                foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
                {
                    deer.Add(mob.Serial);
                }
            }
        }

        Assert.True(deer.Count == 1,
            $"포테의숲{areaId}에 사슴이 {deer.Count}마리 섰습니다(정의 마릿수는 1) — 세렬: {string.Join(", ", deer)}");
    }

    private static string Zone(int areaId) => areaId switch
    {
        20263 => "포테의숲1존",
        20264 => "포테의숲2존",
        20265 => "포테의숲3존",
        _ => throw new ArgumentOutOfRangeException(nameof(areaId))
    };

    /// <summary>
    /// 사슴의 체력·공격력·방어·마릿수(1)는 손대지 않고 젠 간격만 짧게 줄인다. 같은 존의 다른 정의(팜팻 등)는
    /// <c>SpawnMax</c>를 0으로 죽여, 화면에 남는 Hostile 이 사슴 하나뿐이게 한다(몬스터는 이름을 안 보낸다).
    /// </summary>
    private static void IsolateTheDeer(IsolatedHadesServer server, int areaId)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex zone = new($"\"AreaID\"\\s*:\\s*{areaId}\\b");

        bool foundDeer = false;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!zone.IsMatch(text))
            {
                continue;
            }

            if (text.Contains($"\"Name\": \"{DeerName}\""))
            {
                foundDeer = true;
                File.WriteAllText(path, Regex.Replace(text, "\"SpawnRate\"\\s*:\\s*\\d+", "\"SpawnRate\": 1"));
            }
            else
            {
                File.WriteAllText(path, Regex.Replace(text, "\"SpawnMax\"\\s*:\\s*\\d+", "\"SpawnMax\": 0"));
            }
        }

        Assert.True(foundDeer, $"포테의숲({areaId}) 사슴 정의를 찾지 못했습니다.");
    }
}
