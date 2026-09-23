using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사제 회복·해제·보조 마법이 **이펙트(0x29)를 보내는가.** 번호는 원작 근거에서 왔다:
///
/// - 5.99 팩 스크립트가 그대로 옮겨진 것 — 파티 그림 `group_hill 회복량, 267`(쿠러스·쿠라누스·쿠라네라·엑스쿠라네라),
///   `group_hill …, 380`(홀리쿠라네라), `group_mobnar_end 281`(디나르콜룸), `group_mobsor_end 282`(디소루미아·일루메눔),
///   `god_bless 86`(신의축복). 속도는 5.99 서버가 박아 둔 값이다(group_hill 60 — `Novaonline.exe 0x44a183`, 나머지 100).
/// - `hprecovery`(콜라마·리젠) 는 스크립트에 그림이 없다. 5.99 서버가 1초마다 그림 22 를 속도 75 로 대상에게 보내며
///   체력을 채운다(`0x46e120`).
/// - 하데스 옛 스크립트(ao 넷·ao puinsein·armachd·deo saighead) 는 템플릿 `Animation` 을 쏜다 — 번호는
///   참고 저장소 Arbiter(`docs/src/effects/spells.md` · `ProxyViewModel.EffectFilters.cs`)의 원작 관찰이다.
/// - 신성력강화는 5.99 스크립트가 비어 있다(지니고만 있는 마법) — 아무것도 안 보낸다.
/// </summary>
public sealed class PriestSpellEffectTests : IDisposable
{
    private const string Name = "priestfx";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Priest_heals_cures_and_buffs_send_their_original_effect()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        Waiting.MakeGameMaster(server, Name);
        PutStationaryTargetAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["ExpLevel"] = 99;
            saved["_MaximumHp"] = 100000;
            saved["CurrentHp"] = 1000;
            saved["_MaximumMp"] = 100000;
            saved["CurrentMp"] = 100000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");
        Creature target = await FindTarget(world);
        uint me = world.Serial;

        // 체력 회복(hprecovery) — 1초마다 그림 22 · 속도 75, 그리고 체력이 오른다. 둘은 5.99 에서 한 칸(0x122)을 같이 쓰므로
        // 콜라마가 끝난 뒤에 리젠을 건다.
        {
            int health = world.Vitals!.Health;
            await Cast(world, "콜라마", me, me, 22, speed: 75);
            await Until(() => world.Vitals!.Health > health, $"콜라마가 체력 {health} 을 채우지 않았습니다.");
            await Task.Delay(TimeSpan.FromSeconds(11), _deadline.Token);
            await Cast(world, "리젠(Lev1)", me, me, 22, speed: 75);
        }

        // 5.99 파티 그림 — 혼자면 나에게.
        await Cast(world, "쿠러스", me, me, 267, speed: 60);
        await Cast(world, "쿠라누스", me, me, 267, speed: 60);
        await Cast(world, "쿠라네라", me, me, 267, speed: 60);
        await Cast(world, "엑스쿠라네라", me, me, 267, speed: 60);
        await Cast(world, "홀리쿠라네라", me, me, 380, speed: 60);
        await Cast(world, "디나르콜룸", me, me, 281, speed: 100);
        await Cast(world, "디소루미아", me, me, 282, speed: 100);
        await Cast(world, "일루메눔", me, me, 282, speed: 100);
        await Cast(world, "신의축복", me, me, 86, speed: 100);

        // 하데스 옛 스크립트 — 템플릿 Animation.
        await Cast(world, "ao beag cradh", me, me, 245);
        await Cast(world, "ao cradh", me, me, 245);
        await Cast(world, "ao mor cradh", me, me, 245);
        await Cast(world, "ao ard cradh", me, me, 245);
        await Cast(world, "ao puinsein", me, me, 279);
        await Cast(world, "armachd", me, me, 20);
        await Cast(world, "deo saighead", target.Serial, target.Serial, 273);

        // 신성력강화 — 원작 스크립트가 비어 있다.
        {
            int slot = await LearnSpell(world, "신성력강화");
            Drain(world);
            await world.UseSpellAsync(slot, me, _deadline.Token);
            await Task.Delay(1500, _deadline.Token);
            List<Effect> flashes = Drain(world);
            // 22 는 앞서 건 리젠(15초)이 아직 1초마다 보내는 것이다.
            Assert.DoesNotContain(flashes, f => f.TargetAnimation is not (0 or 22) && f.Source == me);
        }
    }

    /// <summary>
    /// 한 번 외워 그림을 기다린다. 하데스 옛 마법은 주사위(레벨 100 이면 99%)나 표적의 마법 방어로 빗나갈 수 있어 몇 번 다시 외운다.
    /// </summary>
    private async Task Cast(WorldClient world, string spell, uint aimAt, uint drawnOn, int animation, int? speed = null)
    {
        int slot = await LearnSpell(world, spell);
        List<Effect> seen = [];

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Drain(world);
            await world.UseSpellAsync(slot, aimAt, _deadline.Token);
            DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < giveUp)
            {
                seen.AddRange(Drain(world));
                Effect? hit = seen.FirstOrDefault(f => f.Target == drawnOn && f.TargetAnimation == animation);
                if (hit is not null)
                {
                    if (speed is not null)
                    {
                        Assert.True(hit.Speed == speed, $"{spell} 그림 {animation} 의 속도가 {speed} 가 아니라 {hit.Speed} 입니다.");
                    }

                    return;
                }

                await Task.Delay(50, _deadline.Token);
            }

            await Task.Delay(1100, _deadline.Token);
        }

        throw new TimeoutException($"{spell} 그림 {animation} 이 {drawnOn} 위로 오지 않았습니다. 온 것: {string.Join(", ", seen)}");
    }

    private static List<Effect> Drain(WorldClient world)
    {
        List<Effect> flashes = [];
        while (world.TakeEffect(out Effect? flash))
        {
            flashes.Add(flash);
        }

        return flashes;
    }

    private async Task<int> LearnSpell(WorldClient world, string spell)
    {
        await world.SayAsync($"/spell \"{spell}\" 100", _deadline.Token);
        int? slot = null;
        await Until(() => (slot = world.Spells.FirstOrDefault(s => s.Name.StartsWith(spell))?.Slot) is not null,
            $"{spell}이 창에 오지 않았습니다.");
        return slot!.Value;
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void PutStationaryTargetAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "사제마법시험표적";
        target["BaseName"] = "사제마법시험표적";
        target["AreaID"] = WoodlandOneOne;
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = Ahead.X;
        target["DefinedY"] = Ahead.Y;
        target["MaximumHP"] = 1_000_000;
        target["Ac"] = 0;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "priest-effect-target.json"),
            target.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<Creature> FindTarget(WorldClient world)
    {
        Creature? found = null;
        await Until(() => (found = world.Creatures.FirstOrDefault(c => c.Where == Ahead)) is not null,
            "우드랜드1-1 입구 앞칸에 시험 표적이 나타나지 않았습니다.");
        return found!;
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
