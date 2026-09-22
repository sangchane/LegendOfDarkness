using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 때린 자리에 따라 피해가 달라진다 — 등 뒤 ×2 · 옆 ×1.5 · 정면 ×1.
/// </summary>
/// <remarks>
/// <para>
/// 판정은 기술 스크립트가 아니라 피해가 들어가는 공통 길에 있다(<c>Sprite.BlowFacing</c>, 방어를 거친 뒤
/// 곱해진다). 그래서 이 시험은 <b>평타 하나와 무도가 기술 하나</b>를 같은 잣대로 재서 "기술마다 적은 것이
/// 아니라 한 곳을 지난다"를 보인다.
/// </para>
/// <para>
/// <b>왜 자리를 세 군데로 나누는가.</b> 괴물은 방향 0(북)으로 선다 — 어디서도 정해 주지 않으므로 기본값
/// 그대로다. 그러니 내가 선 자리만 바꾸면 세 각이 다 나온다: 내 북쪽 놈은 등을 보이고, 내 서쪽 놈은 옆을
/// 보이고, 내 남쪽 놈은 나를 마주 본다. 걸어 돌 필요가 없으니 걸음이 만드는 어긋남도 없다.
/// </para>
/// <para>
/// <b>왜 피해를 말로 읽는가.</b> 체력 보고는 백분율이라 잘린다. 표적을 <c>Training Dummy</c> 로 세우면
/// 맞을 때마다 <c>"{이름}'s {기술}: {피해} DMG."</c> 를 주변에 말하므로(<c>scripts/Monsters/TrainingDummy.cs</c>)
/// 들어간 점수를 그대로 읽을 수 있다. 이 인형은 체력이 0 이 되면 스스로 채우므로 죽지도 않는다.
/// </para>
/// </remarks>
public sealed class FacingDamageTests : IDisposable
{
    private const int WoodlandOneOne = 20015;

    /// <summary>내가 서는 칸. 둘레 네 칸이 모두 바닥이다(<c>maps/lod20015.map</c>).</summary>
    private static readonly Tile Start = new(2, 35);

    /// <summary>북쪽 인형. 북을 보므로 남쪽에 선 내가 그 <b>등 뒤</b>다.</summary>
    private static readonly Tile BehindTile = new(2, 34);

    /// <summary>서쪽 인형. 북을 보므로 동쪽에 선 내가 그 <b>옆</b>이다.</summary>
    private static readonly Tile SideTile = new(1, 35);

    /// <summary>남쪽 인형. 북을 보므로 북쪽에 선 내가 그 <b>정면</b>이다.</summary>
    private static readonly Tile FrontTile = new(2, 36);

    /// <summary><c>SpawnQualifer.Defined</c> — 적힌 칸에 세운다.</summary>
    private const int SpawnDefined = 4;

    /// <summary>
    /// 게임마스터가 때리면 <c>Sprite.ApplyDamage</c> 가 <b>알리는 값만</b> 200 배로 부풀린다 — 체력은 이미
    /// <c>DamageTarget</c> 안에서 깎인 뒤다. 기술을 배우려면 <c>/skill</c> 이 필요하고 그건 게임마스터만
    /// 쓸 수 있으므로, 기술 쪽 시험은 이 배수를 걷어내고 읽는다.
    /// </summary>
    private const int GameMasterTellsThisMuchMore = 200;

    /// <summary>
    /// 평타의 기술 수준. <c>Assail</c> 은 휘두를 때마다 오르고 수준이 피해에 들어가므로, 세 각을 같은 세기로
    /// 재려면 천장(<c>templates/skills/Assail.json</c> 의 <c>MaxLevel</c>)에 올려 더 오르지 않게 둔다.
    /// </summary>
    private const int AssailMaxLevel = 100;

    /// <summary>기술 하나가 다시 준비될 때까지. <c>GlobalBaseSkillDelay</c> 는 500ms 다.</summary>
    private static readonly TimeSpan BetweenBlows = TimeSpan.FromMilliseconds(900);

    private static readonly Regex Told = new(@":\s*(\d+)\s*DMG", RegexOptions.Compiled);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));
    private readonly List<IsolatedHadesServer> _servers = [];
    private readonly ITestOutputHelper _say;

    public FacingDamageTests(ITestOutputHelper say) => _say = say;

    public void Dispose()
    {
        _deadline.Dispose();

        foreach (IsolatedHadesServer server in _servers)
        {
            server.Dispose();
        }
    }

    [Fact]
    public async Task A_plain_blow_is_worth_twice_from_behind_and_half_again_from_the_side()
    {
        (WorldClient world, _) = await Enter("facingblow", gameMaster: false, assailAtCeiling: true);

        (uint front, uint side, uint behind) = await ThreeDummies(world);

        int inFront = await Swing(world, Direction.South, front);
        int fromSide = await Swing(world, Direction.West, side);
        int fromBehind = await Swing(world, Direction.North, behind);

        _say.WriteLine($"평타 — 정면 {inFront} · 옆 {fromSide} · 등 뒤 {fromBehind}");

        Assert.True(inFront > 0, "정면으로 친 평타가 아무 점수도 내지 못했습니다.");
        Assert.Equal(inFront * 2, fromBehind);
        Assert.Equal((int)(inFront * 1.5), fromSide);
    }

    /// <summary>
    /// 무도가 기술도 같은 판정을 받는다 — 단각(<c>Kick</c>). 기술 스크립트에는 방향에 관한 줄이 하나도 없다.
    /// </summary>
    /// <remarks>
    /// 천장 수준으로 배운다. <c>MonkStrike.Begin</c> 도 <c>Skill.Level &lt; MaxLevel</c> 일 때만 올리므로
    /// 천장에서는 세 번이 모두 같은 세기다.
    /// </remarks>
    [Fact]
    public async Task A_monk_skill_goes_through_the_same_judgement()
    {
        (WorldClient world, _) = await Enter("facingkick", gameMaster: true, assailAtCeiling: false, monk: true);

        (uint front, uint side, uint behind) = await ThreeDummies(world);

        await world.SayAsync($"/skill \"Kick\" {AssailMaxLevel}", _deadline.Token);
        int slot = await Learned(world, "Kick");

        int inFront = await Swing(world, Direction.South, front, slot);
        int fromSide = await Swing(world, Direction.West, side, slot);
        int fromBehind = await Swing(world, Direction.North, behind, slot);

        _say.WriteLine($"단각 — 정면 {inFront} · 옆 {fromSide} · 등 뒤 {fromBehind}" +
            $" (알리는 값은 {GameMasterTellsThisMuchMore} 배로 부푼 것이다)");

        Assert.True(inFront > 0, "정면으로 쓴 단각이 아무 점수도 내지 못했습니다.");
        Assert.Equal(inFront * 2, fromBehind);

        // 200 배는 알리는 값에만 붙으므로 걷어내고 곱한 뒤 다시 붙인다 — 1.5 는 실제로 들어간 점수에 곱해진다.
        Assert.Equal((int)(inFront / GameMasterTellsThisMuchMore * 1.5) * GameMasterTellsThisMuchMore, fromSide);
    }

    /// <summary>
    /// 마법에는 배수가 걸리지 않는다 — 등 뒤에서 걸어도 정면과 같은 점수다(사용자, 2026-09-23).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 무엇으로 갈랐나: <b>마법이 도는 동안만 표시가 올라간다</b>(<c>Sprite.CastingSpell</c> 이 감싸는 다섯
    /// 군데 — 사람·NPC·괴물·애완·API). 기술은 아무 데서도 감싸지 않으므로 새로 만드는 기술도 따로 적을 것
    /// 없이 배수를 받고, 마법은 그 표시 하나로 빠진다.
    /// </para>
    /// <para>
    /// 플레어(5.99 `Wizard.txt`)를 쓴다 — 굴리는 값이 없다. 피해는 지력 ×10 ×4 ÷2 ×3 이고, 치명타는
    /// 민첩 ÷ 11 퍼센트라 갓 만든 캐릭터(민첩 5)에게는 영영 안 난다. 그래서 두 번의 값이 같아야 한다.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_spell_is_worth_the_same_from_behind()
    {
        (WorldClient world, _) = await Enter(
            "facingspell", gameMaster: true, assailAtCeiling: false, mana: true);

        (uint front, _, uint behind) = await ThreeDummies(world);

        await world.SayAsync("/spell \"플레어\" 1", _deadline.Token);
        int slot = await LearnedSpell(world, "플레어");

        int inFront = await Cast(world, Direction.South, front, slot);
        int fromBehind = await Cast(world, Direction.North, behind, slot);

        _say.WriteLine($"플레어 — 정면 {inFront} · 등 뒤 {fromBehind}" +
            $" (알리는 값은 {GameMasterTellsThisMuchMore} 배로 부푼 것이다)");

        Assert.True(inFront > 0, "정면으로 건 플레어가 아무 점수도 내지 못했습니다.");
        Assert.Equal(inFront, fromBehind);
    }

    /// <summary>
    /// 맞은 괴물은 때린 쪽을 바라본다. 안 그러면 문 앞에 선 놈은 늘 등을 보이고 있어 등 뒤 ×2 가 거저 나온다.
    /// </summary>
    /// <remarks>
    /// 두 가지로 확인한다 — 서버가 말하는 그 놈의 방향이 남쪽으로 바뀌는지, 그리고 <b>바로 다음 한 방이
    /// 정면 값</b>이 되는지. 이 인형은 제자리에 고정돼 있어(<c>Training Dummy</c> 는 돌아가는 차례가 없다)
    /// 걸음과 무관하게 방향만 도는 길이 있는지도 함께 본다.
    /// </remarks>
    [Fact]
    public async Task A_monster_turns_to_face_whoever_hit_it()
    {
        (WorldClient world, _) = await Enter("facingturn", gameMaster: false, assailAtCeiling: true);

        (uint front, _, uint behind) = await ThreeDummies(world);

        Assert.Equal(Direction.North, Standing(world, behind).Facing);

        int inFront = await Swing(world, Direction.South, front);
        int first = await Swing(world, Direction.North, behind);

        await world.RefreshAsync(_deadline.Token);
        await Until(() => Standing(world, behind).Facing == Direction.South,
            "등 뒤에서 맞은 괴물이 때린 쪽으로 돌아서지 않았습니다.");

        int second = await Swing(world, Direction.North, behind);

        _say.WriteLine($"평타 — 정면 {inFront} · 등 뒤 첫 타 {first} · 돌아선 뒤 둘째 타 {second}");

        Assert.Equal(inFront * 2, first);
        Assert.Equal(inFront, second);
    }

    /// <summary>
    /// 한 방 친다: 그 쪽으로 몸을 돌리고, 때리고, 인형이 말한 점수를 읽는다.
    /// </summary>
    /// <remarks>
    /// 칸을 밟지 않고 도는 것은 <c>TurnAsync</c> 다 — 괴물이 선 칸으로 걸으면 거절당하면서 제자리로 되돌리는
    /// 패킷이 온다.
    /// </remarks>
    private Task<int> Swing(WorldClient world, Direction facing, uint dummy, int? skillSlot = null) =>
        Blow(world, facing, dummy, () => skillSlot is { } slot
            ? world.UseSkillAsync(slot, _deadline.Token)
            : world.AttackAsync(_deadline.Token));

    /// <summary>마법을 하나 건다. 고른 표적에 거는 것이라 몸이 어느 쪽을 보든 닿는다.</summary>
    private Task<int> Cast(WorldClient world, Direction facing, uint dummy, int spellSlot) =>
        Blow(world, facing, dummy, () => world.UseSpellAsync(spellSlot, dummy, _deadline.Token));

    private async Task<int> Blow(WorldClient world, Direction facing, uint dummy, Func<Task> use)
    {
        await world.TurnAsync(facing, _deadline.Token);
        await Task.Delay(150, _deadline.Token);

        int before = world.HeardCount;

        await use();

        int told = 0;
        await Until(
            () => (told = Damage(world, dummy, before)) > 0,
            $"{facing} 쪽 인형이 맞았다고 말하지 않았습니다. 마지막으로 들은 말: " +
            $"{string.Join(" / ", world.Heard.TakeLast(4).Select(spoken => spoken.Text))}");

        await Task.Delay(BetweenBlows, _deadline.Token);
        return told;
    }

    /// <summary>이번 한 방 뒤에 들은 말 가운데 그 인형이 알린 점수. 없으면 0.</summary>
    private static int Damage(WorldClient world, uint dummy, int before)
    {
        IReadOnlyList<Spoken> heard = world.Heard;
        int since = Math.Clamp(world.HeardCount - before, 0, heard.Count);

        foreach (Spoken spoken in heard.TakeLast(since))
        {
            if (spoken.Serial == dummy && Told.Match(spoken.Text) is { Success: true } hit)
            {
                return int.Parse(hit.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        return 0;
    }

    /// <summary>세 인형이 제 칸에 설 때까지 기다리고, 각각의 일련번호를 돌려준다.</summary>
    private async Task<(uint Front, uint Side, uint Behind)> ThreeDummies(WorldClient world)
    {
        await Until(
            () =>
            {
                Tile[] standing = [.. world.Creatures.Select(creature => creature.Where)];
                return standing.Contains(FrontTile) && standing.Contains(SideTile) && standing.Contains(BehindTile);
            },
            "세 인형이 다 서지 않았습니다.",
            within: TimeSpan.FromMinutes(2),
            asking: world);

        return (At(world, FrontTile), At(world, SideTile), At(world, BehindTile));
    }

    private static uint At(WorldClient world, Tile tile) =>
        world.Creatures.First(creature => creature.Where == tile).Serial;

    private static Creature Standing(WorldClient world, uint serial) =>
        world.Creatures.First(creature => creature.Serial == serial);

    private Task<int> Learned(WorldClient world, string skill) =>
        Slot(
            () => world.Skills.FirstOrDefault(
                known => known.Name.StartsWith(skill + " (", StringComparison.Ordinal))?.Slot,
            $"기술 창에 {skill} 이 오지 않았습니다.");

    /// <summary>마법 창의 이름에는 수준이 붙지 않는다 — 이름 그대로 시작하는 것을 찾는다.</summary>
    private Task<int> LearnedSpell(WorldClient world, string spell) =>
        Slot(
            () => world.Spells.FirstOrDefault(
                known => known.Name.StartsWith(spell, StringComparison.Ordinal))?.Slot,
            $"마법 창에 {spell} 이 오지 않았습니다.");

    private async Task<int> Slot(Func<int?> find, string complaint)
    {
        int? found = null;
        await Until(() => (found = find()) is not null, complaint);
        return found!.Value;
    }

    private async Task<(WorldClient World, IsolatedHadesServer Server)> Enter(
        string name, bool gameMaster, bool assailAtCeiling, bool monk = false, bool mana = false)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        _servers.Add(server);

        StandThreeDummies(server);

        if (gameMaster)
        {
            MakeGameMaster(server, name);
        }

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);
        Rewrite(server, name, monk, assailAtCeiling, mana);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");

        return (world, server);
    }

    private static void MakeGameMaster(IsolatedHadesServer server, string name)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// 갓 만든 캐릭터를 시험이 쓸 모습으로 고친다 — 무도가로 올리고, 평타를 천장 수준으로 올리고,
    /// 마법에 쓸 마력을 채운다.
    /// </summary>
    private static void Rewrite(
        IsolatedHadesServer server, string name, bool monk, bool assailAtCeiling, bool mana)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;

        if (monk)
        {
            saved["ExpLevel"] = 10;
            saved["Path"] = 5;
        }

        if (mana)
        {
            // 플레어는 620 을 쓰고 620 미만이면 아예 나가지 않는다. 두 번을 걸고도 남게 둔다.
            saved["_MaximumMp"] = 30000;
            saved["CurrentMp"] = 30000;
        }

        if (assailAtCeiling)
        {
            // 빈 칸은 JSON 의 null 로 적혀 있으므로 걸러내고 본다.
            JsonObject skills = saved["SkillBook"]!["Skills"]!.AsObject();
            JsonNode assail = Assert.Single(
                skills.Select(entry => entry.Value).OfType<JsonNode>(),
                skill => skill is JsonObject known
                         && known["Template"] is JsonObject template
                         && (string?)template["ScriptName"] == "Assail");
            assail["Level"] = AssailMaxLevel;
        }

        File.WriteAllText(path, saved.ToJsonString());
    }

    /// <summary>
    /// 내 북·서·남 세 칸에 인형을 하나씩 세우고, 그 방에 다른 놈은 서지 못하게 한다.
    /// </summary>
    /// <remarks>
    /// 방의 정의를 하나 베껴 <c>Training Dummy</c> 로 바꾼다 — 그 대본은 다가오지도 되받아치지도 돌아가지도
    /// 않으므로 방향을 바꾸는 것은 내 한 방뿐이다. 체력은 죽지 않을 만큼 크게 둔다(백분율로 재지 않으므로
    /// 크기는 상관없다). 방어는 0 — 세 값이 같은 방어를 거쳐야 배수만 남는다.
    /// </remarks>
    private static void StandThreeDummies(IsolatedHadesServer server)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex thisRoom = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        JsonNode? sample = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!thisRoom.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            sample ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        Assert.NotNull(sample);

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);

        foreach ((string name, Tile at) in new[]
                 {
                     ("방향시험인형앞", FrontTile),
                     ("방향시험인형옆", SideTile),
                     ("방향시험인형뒤", BehindTile),
                 })
        {
            JsonNode dummy = sample!.DeepClone();
            dummy["Name"] = name;
            dummy["BaseName"] = name;
            dummy["ScriptName"] = "Training Dummy";
            dummy["SpawnType"] = SpawnDefined;
            dummy["SpawnRate"] = 1;
            dummy["SpawnMax"] = 1;
            dummy["DefinedX"] = at.X;
            dummy["DefinedY"] = at.Y;
            dummy["MaximumHP"] = 1_000_000;
            dummy["Ac"] = 0;
            dummy["Grow"] = false;

            File.WriteAllText(Path.Combine(testFolder, $"facing-{at.X}-{at.Y}.json"), dummy.ToJsonString(indented));
        }
    }

    private async Task Until(
        Func<bool> wanted, string complaint, TimeSpan? within = null, WorldClient? asking = null)
    {
        DateTime giveUp = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted())
            {
                return;
            }

            if (asking is not null)
            {
                await asking.RefreshAsync(_deadline.Token);
            }

            await Task.Delay(100, _deadline.Token);
        }

        throw new TimeoutException(complaint);
    }
}
