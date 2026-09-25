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
/// 괴물을 잡았을 때 기록 창에 뜨는 "경험치가 N 올랐습니다" 의 N 이, 왼쪽 위 "EXP 다음까지" 가 실제로 줄어든 양과 같은가.
/// </summary>
/// <remarks>
/// 사용자(아이폰, 2026-09-25): "경험치가 표시되는 것만큼 줄지 않는 것 같은데?" — 서버(<c>monsterexp.cs</c>)는 알림에
/// 괴물 정의의 경험치를 그대로 적고, 실제로는 레벨 차이로 깎은 값(<c>ForLevel</c> — 괴물 정의 567개가 모두 레벨 1
/// 이라 7레벨부터 깎인다)을, 그것도 1,000 단위로 올려 더했다. 둘이 같아야 한다.
/// 레벨이 오르지 않게 다음 레벨까지를 넉넉히 둔다(오르면 "다음까지" 가 새 목표로 바뀌어 뺄셈이 안 된다).
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class ExperienceNoticeTests : IDisposable
{
    private const int SpawnDefined = 4;
    private const int PathFixed = 2;
    private const int MoodIdle = 1;
    private const int MostSwings = 120;

    /// <summary>한 마리로는 절대 닿지 않는 다음 레벨까지.</summary>
    private const long FarAway = 1_000_000;

    private const string Name = "expnote";

    private static readonly Regex Notice = new(@"^경험치가 (\d+) 올랐습니다", RegexOptions.CultureInvariant);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 깎기에 쓰는 괴물 레벨은 괴물의 경험치에서 추정한다(monsterexp.cs <c>CutLevel</c>, 생성기
    /// <c>scripts/build-monster-cut-level.py</c>). 표적은 그 방에서 체력이 가장 낮은 제 정의 — 경험치도 제 값 그대로다.
    /// 표적의 추정 레벨: 우드랜드1-1 니에1(466) 1(우드랜드1 구간 1~10 의 맨 아래) · 우드랜드3-1 녹색말벌3(2,534) 26 ·
    /// 포테의숲1존 그린팜팻(7,671) 22 · 노비스평원A 노비스풀뱀(1,068) 1.
    /// 다섯 레벨 차이까지는 깎지 않는다.
    /// </summary>
    [Theory]
    [InlineData(20015, 2, 35, 1, false)]   // 우드랜드1-1 · 1레벨 — 안 깎인다
    [InlineData(20015, 2, 35, 14, true)]   // 우드랜드1-1 · 14레벨 — 니에1 은 1레벨, 13 차이라 깎인다(466 → 153, 33%)
    [InlineData(20023, 39, 97, 30, false)] // 우드랜드3-1 · 30레벨 — 녹색말벌3 26 과 4 차이라 안 깎인다
    [InlineData(20263, 33, 47, 27, false)] // 포테의숲1존 · 27레벨 — 22 와 5 차이라 안 깎인다
    [InlineData(20263, 33, 47, 90, true)]  // 포테의숲1존 · 90레벨 — 깎인다
    [InlineData(20393, 25, 25, 30, true)]  // 노비스평원A · 30레벨 — 깎인다
    public async Task The_notice_is_what_the_bar_actually_moves(int room, int x, int y, int level, bool cut)
    {
        Tile start = new(x, y);
        Tile target = new(x, y - 1);

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (room, start.X, start.Y));
        int stated = StandOneAtTheDoor(server, room, target);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = level;
        character["ExpNext"] = FarAway;
        File.WriteAllText(saved, character.ToJsonString());

        long toGo;
        long notice;

        using (WorldSession session = await HadesLoginClient.LoginAsync(
                   IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token))
        {
            WorldClient world = new(session);
            _ = world.PumpAsync(_deadline.Token);
            await Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.");
            await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "문 앞에 괴물이 서지 않았습니다.");
            await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

            Vitals before = Mine(world);
            Assert.Equal(level, before.Level);
            Assert.Equal(FarAway, before.ExperienceToGo);

            List<long> notices = [];
            await SwingUntil(world, enough: () => Collect(world, notices) > 0);
            Assert.True(notices.Count == 1, $"알림이 {notices.Count}번 왔습니다. 서버가 한 말: {world.Said}");
            notice = notices[0];
            Assert.True(cut ? notice < stated : notice == stated,
                $"{level}레벨이 {room} 에서 경험치 {stated:N0} 짜리를 잡고 {notice:N0} 받았습니다 — " +
                (cut ? "깎여야 합니다." : "그대로여야 합니다."));

            // 서버는 0x08 을 알림보다 먼저 보낸다. 늦게 오는 몫이 있는지 조금 더 기다려 본다.
            await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);

            Vitals after = Mine(world);
            Assert.Equal(level, after.Level);
            toGo = after.ExperienceToGo;

            Assert.True(
                before.ExperienceToGo - after.ExperienceToGo == notice,
                $"{level}레벨: 알림은 {notice:N0} 인데 '다음까지' 는 {before.ExperienceToGo - after.ExperienceToGo:N0} 줄었습니다 " +
                $"({before.ExperienceToGo:N0} → {after.ExperienceToGo:N0}).");
            Assert.Equal(notice, after.Experience - before.Experience);

            // 나갈 때의 저장은 마지막 저장에서 2초가 지나야 한다(CharacterSaveTests).
            await Task.Delay(TimeSpan.FromSeconds(3), _deadline.Token);
        }

        // 서버가 파일에 적은 값도 앱이 본 값과 같다.
        await Until(
            () => File.Exists(saved) && (long?)JsonNode.Parse(File.ReadAllText(saved))?["ExpNext"] == toGo,
            $"저장된 ExpNext 가 앱이 본 {toGo:N0} 과 다릅니다: {JsonNode.Parse(File.ReadAllText(saved))?["ExpNext"]}");
    }

    private static int Collect(WorldClient world, List<long> notices)
    {
        while (world.TakeTold(out _, out string text))
        {
            if (Notice.Match(text) is { Success: true } match)
            {
                notices.Add(long.Parse(match.Groups[1].Value));
            }
        }

        return notices.Count;
    }

    /// <summary>방의 정의 하나만 문 앞칸에 가만히 세우고 그 경험치를 돌려준다 — <see cref="LevelUpVitalsTests" /> 와 같은 모양.</summary>
    private static int StandOneAtTheDoor(IsolatedHadesServer server, int room, Tile targetTile)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        (string Path, JsonNode Template)[] definitions =
        [
            .. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Select(path => (path, text: File.ReadAllText(path)))
                .Where(file => file.text.Contains($"\"AreaID\": {room}", StringComparison.Ordinal))
                .Select(file => (file.path, JsonNode.Parse(file.text, documentOptions: lenient)!))
        ];

        Assert.NotEmpty(definitions);

        JsonNode target = definitions.MinBy(definition => (int?)definition.Template["MaximumHP"] ?? 0).Template.DeepClone();

        foreach ((string path, JsonNode template) in definitions)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        target["Name"] = "경험치시험표적";
        target["SpawnType"] = SpawnDefined;
        target["SpawnRate"] = 60;
        target["SpawnMax"] = 1;
        target["DefinedX"] = targetTile.X;
        target["DefinedY"] = targetTile.Y;
        target["PathQualifer"] = PathFixed;
        target["MoodType"] = MoodIdle;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "exp-notice-target.json"), target.ToJsonString(indented));
        return (int)target["Exp"]!;
    }

    private async Task SwingUntil(WorldClient world, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough();)
        {
            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(200, _deadline.Token);
                continue;
            }

            int dx = goal.X - before.Where.X, dy = goal.Y - before.Where.Y;
            Direction step = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            await world.WalkAsync(step, _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(120, _deadline.Token);

            if (world.State is not { } after || !IsThere(world, Ahead(after.Where, step)))
            {
                continue;
            }

            await world.AttackAsync(_deadline.Token);
            swings++;
            await Task.Delay(600, _deadline.Token);
        }
    }

    private static Tile Ahead(Tile from, Direction facing) => facing switch
    {
        Direction.North => new Tile(from.X, from.Y - 1),
        Direction.South => new Tile(from.X, from.Y + 1),
        Direction.East => new Tile(from.X + 1, from.Y),
        _ => new Tile(from.X - 1, from.Y)
    };

    private static bool IsThere(WorldClient world, Tile tile) =>
        world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == tile);

    private static Tile? Nearest(WorldClient world)
    {
        if (world.State is not { } me)
        {
            return null;
        }

        return world.Creatures
            .Where(c => c.Kind == CreatureKind.Hostile)
            .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y))
            .Select(c => (Tile?)c.Where)
            .FirstOrDefault();
    }

    private static Vitals Mine(WorldClient world) =>
        world.Vitals ?? throw new InvalidOperationException("서버가 내 수치를 말하지 않았습니다.");

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
