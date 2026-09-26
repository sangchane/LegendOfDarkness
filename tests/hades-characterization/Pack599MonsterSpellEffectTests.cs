using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 괴물이 나에게 마법을 쓸 때 그 그림이 맞는 나 위에 그려지는가(사용자, 아이폰: "몬스터 마법 이펙트가 몬스터 자신에게
/// 표시된다"). 5.99 괴물 마법(`Mob_Spell.txt`)은 맞는 사람 쪽에서 돌아 `effect @get_myid, 그림, 0, 속도` 로 쓴다 —
/// 그 블록의 주인도 맞는 사람이라 둘째 자리(쓴 쪽 그림)도 맞는 사람 위에 그려진다. 괴물을 쓴 쪽으로 보내면 그림이
/// 괴물 위로 간다.
/// </summary>
/// <remarks>
/// 포테의숲2존 그린팜팻 등 16종이 쓰는 <c>Monster_마레노</c>(그림 10)를 우드랜드1-1 입구 앞칸의 괴물 하나에게 쥐여 준다.
/// </remarks>
public sealed class Pack599MonsterSpellEffectTests : IDisposable
{
    private const string Name = "mobspellfx";
    private const int WoodlandOneOne = 20015;
    private const int MarenoPicture = 10;

    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monster_spell_picture_is_drawn_over_the_one_it_hits()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneCasterAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        MakeSturdy(server);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);
        while (!(world.State?.Where == Start && world.Creatures.Any(c => c.Where == Ahead)))
        {
            Assert.True(DateTime.UtcNow < giveUp, "우드랜드1-1 입구 앞칸에 괴물이 서지 않았습니다.");
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
        }

        uint me = world.Serial;
        uint caster = world.Creatures.First(c => c.Where == Ahead).Serial;
        List<Effect> seen = [];
        Effect? spell = null;
        DateTime until = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (spell is null && DateTime.UtcNow < until)
        {
            while (world.TakeEffect(out Effect? flash))
            {
                seen.Add(flash);
            }

            spell = seen.FirstOrDefault(f => f.TargetAnimation == MarenoPicture || f.SourceAnimation == MarenoPicture);
            await Task.Delay(50, _deadline.Token);
        }

        Assert.True(spell is not null, $"60초 동안 괴물이 마레노(그림 {MarenoPicture})를 쓰지 않았습니다. 온 것: {string.Join(", ", seen)}");

        // 앱(WorldView.Flashes)은 첫 그림을 Target 위에, 둘째 그림을 Source 위에 그린다.
        uint drawnOn = spell.TargetAnimation == MarenoPicture ? spell.Target : spell.Source;
        Assert.True(drawnOn == me,
            $"마레노 그림이 맞는 나({me}) 위가 아니라 {(drawnOn == caster ? $"쓴 괴물({caster})" : drawnOn.ToString())} 위로 갑니다: {spell}");
    }

    /// <summary>
    /// 앱 사진: 같은 괴물 앞에 서서 앱을 화면 밖에 띄우고 사진을 남긴다. <c>LOD_MOBSPELL_SHOT</c> 에 png 경로를 줄 때만 돈다.
    /// <c>LOD_MOBSPELL_ENGINE</c> 는 고도 자체에 줄 인자(예 "--write-movie /tmp/f.png --fixed-fps 10").
    /// </summary>
    [Fact]
    public async Task Photograph_a_monster_spell_over_me()
    {
        if (Environment.GetEnvironmentVariable("LOD_MOBSPELL_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneCasterAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        MakeSturdy(server);

        File.Delete(shot);
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        List<string> arguments = [.. (Environment.GetEnvironmentVariable("LOD_MOBSPELL_ENGINE") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        arguments.AddRange(
        [
            "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
            "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"{Name}:{LoginFlow.SyntheticSecret}",
            "--orient", "portrait", "--size", "360x780", "--shot", shot,
            "--shot-after", Environment.GetEnvironmentVariable("LOD_MOBSPELL_SHOT_AFTER") ?? "12"
        ]);

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        _ = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
    }

    private static void MakeSturdy(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        saved["_MaximumHp"] = 100_000;
        saved["CurrentHp"] = 100_000;
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void StandOneCasterAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonNode? caster = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!woodland.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            caster ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString());
        }

        Assert.NotNull(caster);
        caster["Name"] = "괴물마법그림시험괴물";
        caster["BaseName"] = "괴물마법그림시험괴물";
        caster["SpawnMax"] = 1;
        caster["SpawnType"] = 4;
        caster["SpawnRate"] = 1;
        caster["DefinedX"] = Ahead.X;
        caster["DefinedY"] = Ahead.Y;
        caster["MaximumHP"] = 1_000_000;
        caster["DmgMin"] = 1;
        caster["DmgMax"] = 1;
        caster["MoodType"] = 2;
        caster["PathQualifer"] = 2;
        caster["Grow"] = false;
        caster["CastSpeed"] = 500;
        caster["SpellScripts"] = new JsonArray("Monster_마레노");

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "pack599-monster-spell-effect.json"), caster.ToJsonString());
    }
}
