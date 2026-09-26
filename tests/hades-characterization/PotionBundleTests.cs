using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(2026-09-26) — "번들이니까 여러 개도 드랍될 수 있게". 겹쳐지는 소모품(<c>Consumable|Stackable</c> —
/// 포션·시약)은 괴물이 떨굴 때 <b>1~3개가 한 묶음</b>(바닥 물건 하나, 개수 칸 <c>Stacks</c>)으로 떨어지고,
/// 주우면 가방에 그 개수만큼 들어온다. 앱은 바닥 묶음의 개수를 0x07 에서 읽는다(아이템 칸의 비어 있던 4바이트).
/// </summary>
/// <remarks>
/// 같은 판에서 <c>DropRate</c> 가 1 을 넘는 셈도 본다(<see cref="ManaPotionDropTests" />). 목록이 두 칸
/// [하급마력포션 2.0 · 팜팻의정수 0] 이면 새 셈에서는 한 마리마다 반드시 포션이 떨어지고, 옛 셈(한 칸 고르고
/// 굴리기)에서는 절반만 떨어진다.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class PotionBundleTests : IDisposable
{
    private const int MonsterRoom = 20015;

    private static readonly Tile Start = new(2, 35);

    private static readonly Tile TargetTile = new(2, 34);

    private const string Potion = "하급마력포션";

    private const int Kills = 4;

    private const string Name = "bundlekill";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(9));
    private IsolatedHadesServer? _server;

    public void Dispose()
    {
        _deadline.Dispose();
        _server?.Dispose();
    }

    [Fact]
    public async Task A_potion_falls_as_one_bundle_of_one_to_three_and_all_of_it_goes_into_the_pack()
    {
        WorldClient world = await Enter();
        List<int> bundles = [];

        for (int kill = 0; kill < Kills; kill++)
        {
            await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == TargetTile),
                $"{kill + 1}번째 괴물이 문 앞에 서지 않았습니다.", _deadline.Token);

            for (int swing = 0; swing < 30 && PotionAt(world) is null; swing++)
            {
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(600, _deadline.Token);
            }

            Creature? bundle = PotionAt(world);
            Assert.True(bundle is not null,
                $"{kill + 1}번째 괴물이 포션을 떨구지 않았습니다 — DropRate 2.0 이면 늘 떨어져야 합니다. 바닥 " +
                $"[{string.Join(", ", world.Creatures.Where(c => c.Kind == CreatureKind.Passable).Select(c => $"{c.Sprite}x{c.Count}@{c.Where}"))}]");
            int count = bundle!.Count;
            uint serial = bundle.Serial;
            Assert.InRange(count, 1, 3);

            int before = Carried(world);

            for (int tries = 0; tries < 6 && world.Creatures.Any(c => c.Serial == serial); tries++)
            {
                await world.PickUpAsync(TargetTile, _deadline.Token);
                await Task.Delay(500, _deadline.Token);
            }

            await Waiting.Until(() => Carried(world) == before + count,
                $"바닥 묶음 {count}개를 주웠는데 가방의 {Potion} 이 {before} → {Carried(world)} 입니다.", _deadline.Token);
            bundles.Add(count);
        }

        Assert.True(bundles.Any(count => count > 1),
            $"{Kills}번 모두 한 개씩만 떨어졌습니다 [{string.Join(", ", bundles)}] — 1~3개 묶음이어야 합니다.");
    }

    /// <summary>
    /// 같은 칸에 금화와 포션 묶음이 있는 장면을 앱으로 찍는다 — 금화가 아래, 포션이 위, 옆에 "x3" 같은 개수.
    /// <c>LOD_GOLD_UNDER_SHOT=/경로.png</c> 가 있을 때만 돈다(창은 화면 밖, 소리 끔).
    /// </summary>
    [Fact]
    public async Task Photograph_gold_lying_under_a_potion_bundle()
    {
        if (Environment.GetEnvironmentVariable("LOD_GOLD_UNDER_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        (WorldClient world, WorldSession session) = await EnterWith(spawnRate: 600, exp: 2000);
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == TargetTile),
            "괴물이 문 앞에 서지 않았습니다.", _deadline.Token);

        for (int swing = 0; swing < 30 && (PotionAt(world) is null || CoinsAt(world) is null); swing++)
        {
            await world.AttackAsync(_deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }

        Assert.NotNull(PotionAt(world));
        Assert.NotNull(CoinsAt(world));

        // 쓰러진 칸에는 곧 다음 괴물이 서서 바닥을 가린다 — 주워서 옆 빈칸에 다시 놓는다. 포션을 먼저, 금화를
        // 나중에 놓는다: 예전 앱은 나중에 온 것을 위에 그렸으므로 이 차례가 금화를 위로 올리던 차례다.
        for (int tries = 0; tries < 8 && (PotionAt(world) is not null || CoinsAt(world) is not null); tries++)
        {
            await world.PickUpAsync(TargetTile, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        Tile aside = new(Start.X, Start.Y + 1);
        InventoryItem potions = world.Pack.First(item => item.Name == Potion);
        await world.DropAsync(potions.Slot, Math.Max(1, potions.Stacks), aside, _deadline.Token);
        await Task.Delay(1000, _deadline.Token);
        await world.DropGoldAsync((int)Math.Min(100, world.Vitals!.Gold), aside, _deadline.Token);
        await Waiting.Until(() => world.Creatures.Count(c => c.Kind == CreatureKind.Passable && c.Where == aside) >= 2,
            $"{aside} 에 포션과 금화를 놓지 못했습니다.", _deadline.Token);

        await Task.Delay(1000, _deadline.Token);
        string seen = $"나 {world.State?.Where} · " + string.Join(", ",
            world.Creatures.Select(c => $"{c.Kind}:{c.Sprite}x{c.Count}@{c.Where}"));
        session.Dispose();
        await Task.Delay(3000, _deadline.Token);
        File.Delete(shot);

        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{_server!.LoginPort}", "--login", $"{Name}:{LoginFlow.SyntheticSecret}",
                     "--orient", "portrait", "--size", "360x780",
                     "--shot", shot, "--shot-after", "10"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);
        File.WriteAllText(Path.ChangeExtension(shot, ".txt"), seen + "\n" +
            string.Join('\n', (await said).Split('\n').Where(line => line.Contains("GREYBOX_", StringComparison.Ordinal))));

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
    }

    private static Creature? CoinsAt(WorldClient world) =>
        world.Creatures.FirstOrDefault(c =>
            c.Kind == CreatureKind.Passable && c.Where == TargetTile && c.Sprite is >= 0x8089 and <= 0x808E);

    private static Creature? PotionAt(WorldClient world) =>
        world.Creatures.FirstOrDefault(c =>
            c.Kind == CreatureKind.Passable && c.Where == TargetTile && c.Sprite is not (>= 0x8089 and <= 0x808E));

    private static int Carried(WorldClient world) =>
        world.Pack.Where(item => item.Name == Potion).Sum(item => Math.Max(1, item.Stacks));

    private async Task<WorldClient> Enter() => (await EnterWith(spawnRate: 1)).World;

    private async Task<(WorldClient World, WorldSession Session)> EnterWith(int spawnRate, int exp = 1)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        _server = server;
        SetDropRate(server, Potion, 2.0);
        SetDropRate(server, "팜팻의정수", 0);
        StandOneAtTheDoor(server, spawnRate, exp);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.", _deadline.Token);
        await world.TurnAsync(Direction.North, _deadline.Token);
        return (world, session);
    }

    private static void SetDropRate(IsolatedHadesServer server, string item, double rate)
    {
        string path = Path.Combine(server.ContentLocation, "templates", "items", $"{item}.json");
        JsonNode node = JsonNode.Parse(File.ReadAllText(path))!;
        node["DropRate"] = rate;
        File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>방의 정의는 모두 재우고, 체력 1 짜리 하나를 문 앞칸에 바로바로 세운다(<see cref="RareDropTests" /> 와 같다).</summary>
    private static void StandOneAtTheDoor(IsolatedHadesServer server, int spawnRate, int exp)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        JsonNode? target = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!text.Contains($"\"AreaID\": {MonsterRoom}", StringComparison.Ordinal))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            target ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        Assert.NotNull(target);

        target["Name"] = "포션묶음시험표적";
        target["SpawnType"] = 4; // Defined
        target["SpawnRate"] = spawnRate;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;
        target["PathQualifer"] = 2; // Fixed
        target["MoodType"] = 1; // Idle
        target["Grow"] = false;
        target["MaximumHP"] = 1;
        target["Exp"] = exp; // 1 이면 금화가 0 이라 안 떨어진다
        target["LootType"] = 2; // Random
        target["Drops"] = new JsonObject { ["$values"] = new JsonArray(Potion, "팜팻의정수") };

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "potion-bundle-target.json"), target.ToJsonString(indented));
    }
}
