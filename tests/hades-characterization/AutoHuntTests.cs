using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 앱의 자동 사냥 판단(<see cref="AutoHunt"/>)을 그대로 격리 서버에 물려 무도가를 굴린다 — 스스로 다가가 돌아서고
/// 평타·기술을 써서 괴물을 잡고 경험치가 오르는가. 앱이 하는 일(걸음 0.4초마다 한 번, 결정 → 패킷)을 흉내 낸다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class AutoHuntTests(ITestOutputHelper output) : IDisposable
{
    private const int WoodlandOneOne = 20015;
    private const int SpawnDefined = 4;
    private const int PathFixed = 2;
    private const int MoodIdle = 1;

    private static readonly Tile Start = new(2, 35);

    /// <summary>두 칸 위 — 한 걸음 다가가야 닿는다.</summary>
    private static readonly Tile TargetTile = new(2, 33);

    /// <summary>걸은 뒤에는 앱의 한 걸음(<c>WorldView.StepSeconds</c> 0.4초)보다 조금 길게 쉰다 — 서버 걷기 제한을 넘지 않게.</summary>
    private static readonly TimeSpan AfterStep = TimeSpan.FromMilliseconds(420);

    /// <summary>그 밖에는 앱처럼 자주 묻는다(앱은 매 프레임). 평타·기술 간격은 AutoHunt 가 잰다.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(120);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monk_left_on_auto_walks_up_kills_a_monster_and_gains_experience()
    {
        const string name = "autohunt";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneWeakTarget(server);
        server.Start(TimeSpan.FromMinutes(2));

        using WorldSession session = await HadesLoginClient.CreateCharacterAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret,
            hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is not null && world.Vitals is not null
                                  && world.Creatures.Any(one => one.Kind == CreatureKind.Hostile),
            "세계에 들어가지 못했거나 표적이 서지 않았습니다.", _deadline.Token);
        await Task.Delay(1000, _deadline.Token);

        long before = world.Vitals!.Experience;
        AutoHunt hunt = new();
        hunt.Start(world.State!.Where, world.State.Map.Id);
        Direction facing = Direction.South;
        Dictionary<HuntAct, int> done = [];
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(4);
        TimeSpan pause = Tick;

        while (world.Vitals!.Experience == before && DateTime.UtcNow < giveUp)
        {
            await Task.Delay(pause, _deadline.Token);

            if (pause == AfterStep)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(Tick, _deadline.Token);
            }

            HuntStep step = hunt.Next(new HuntSight
            {
                Standing = world.State!.Where,
                Facing = facing,
                MapId = world.State.Map.Id,
                Vitals = world.Vitals,
                Creatures = world.Creatures,
                HealthOf = world.Health,
                Skills = world.Skills,
                Spells = world.Spells,
                Cooling = world.CoolingFor,
                Now = TimeSpan.FromMilliseconds(Environment.TickCount64),
            }, new AutoHuntSettings());

            done[step.Act] = done.GetValueOrDefault(step.Act) + 1;
            pause = step.Act == HuntAct.Walk ? AfterStep : Tick;

            if (step.Act != HuntAct.Wait && done.Values.Sum() % 10 == 0)
            {
                output.WriteLine($"{done.Values.Sum()}틱 {world.State.Where} 대상 {hunt.Target} 체력 {world.Health(hunt.Target)}% · 내 체력 {world.Vitals.Health}/{world.Vitals.MaximumHealth} 마력 {world.Vitals.Mana} · {step.Act} {step.Why} · {world.Said}");
            }

            switch (step.Act)
            {
                case HuntAct.Stop:
                    Assert.Fail($"자동 사냥이 멈췄습니다: {step.Why}");
                    break;
                case HuntAct.Walk:
                    facing = step.Toward;
                    await world.WalkAsync(step.Toward, _deadline.Token);
                    break;
                case HuntAct.Face:
                    facing = step.Toward;
                    await world.TurnAsync(step.Toward, _deadline.Token);
                    break;
                case HuntAct.Strike:
                    await world.AttackAsync(_deadline.Token);
                    break;
                case HuntAct.Skill:
                    await world.UseSkillAsync(step.Slot, _deadline.Token);
                    break;
                case HuntAct.Heal:
                    await world.UseSpellAsync(step.Slot, 0, _deadline.Token);
                    break;
            }
        }

        output.WriteLine(string.Join(" · ", done.Select(pair => $"{pair.Key} {pair.Value}")));
        output.WriteLine($"경험치 {before} → {world.Vitals.Experience}, 서버 마지막 말: {world.Said}");

        Assert.True(world.Vitals.Experience > before, $"4분 안에 경험치가 오르지 않았습니다. 한 일: {string.Join(", ", done)}");
        Assert.True(done.GetValueOrDefault(HuntAct.Walk) > 0, "다가가지 않았습니다.");
        Assert.True(done.GetValueOrDefault(HuntAct.Strike) + done.GetValueOrDefault(HuntAct.Skill) > 0, "치지 않았습니다.");
    }

    /// <summary>
    /// 확인 사진 — 격리 서버에 무도가를 만들고, 실제 앱을 <c>--auto-hunt</c> 로 띄워(창은 화면 밖, 소리 끔) 치는 장면을 찍는다.
    /// <c>LOD_AUTOHUNT_SHOT</c> 에 저장할 png 경로를 줄 때만 돈다 — 앱 빌드와 Godot 이 있어야 해서 평소 시험에서는 뺀다.
    /// </summary>
    [Fact]
    public async Task Photograph_the_app_hunting_on_its_own()
    {
        if (Environment.GetEnvironmentVariable("LOD_AUTOHUNT_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        const string name = "autoshot";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneWeakTarget(server, keepOthers: true);
        server.Start(TimeSpan.FromMinutes(2));

        using (WorldSession created = await HadesLoginClient.CreateCharacterAsync(
                   IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret,
                   hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token))
        {
            await Task.Delay(3000, _deadline.Token);
        }

        await Task.Delay(3000, _deadline.Token);
        File.Delete(shot);

        string root = HadesWorkspace.RepositoryRoot;
        string after = Environment.GetEnvironmentVariable("LOD_AUTOHUNT_SHOT_AFTER") ?? "14";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(root, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{name}:{LoginFlow.SyntheticSecret}",
                     "--orient", "portrait", "--size", "360x780",
                     "--auto-hunt", "--shot", shot, "--shot-after", after
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        foreach (string line in (await said).Split('\n').Where(line => line.Contains("GREYBOX_", StringComparison.Ordinal)))
        {
            output.WriteLine(line);
        }

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
    }

    /// <summary>방의 괴물을 모두 빼고, 가장 약한 정의 하나를 두 칸 위에 가만히 세운다(ExperienceNoticeTests 와 같은 모양).</summary>
    private static void StandOneWeakTarget(IsolatedHadesServer server, bool keepOthers = false)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        (string Path, JsonNode Template)[] definitions =
        [
            .. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Select(path => (path, text: File.ReadAllText(path)))
                .Where(file => file.text.Contains($"\"AreaID\": {WoodlandOneOne}", StringComparison.Ordinal))
                .Select(file => (file.path, JsonNode.Parse(file.text, documentOptions: lenient)!))
        ];

        Assert.NotEmpty(definitions);

        JsonNode target = definitions.MinBy(definition => (int?)definition.Template["MaximumHP"] ?? 0).Template.DeepClone();

        foreach ((string path, JsonNode template) in keepOthers ? [] : definitions)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        target["Name"] = "자동사냥표적";
        target["SpawnType"] = SpawnDefined;
        target["SpawnRate"] = 60;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;
        target["PathQualifer"] = PathFixed;
        target["MoodType"] = MoodIdle;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "auto-hunt-target.json"), target.ToJsonString(indented));
    }
}
