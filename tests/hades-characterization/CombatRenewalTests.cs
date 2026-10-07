using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Darkages.Types;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Element = Darkages.Types.ElementManager.Element;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 전투 리뉴얼(`plans/combat-renewal-spec-2026-10-04.md`)의 식. 식은 서버가 켤 때 컴파일하는 스크립트 안에 있으므로
/// 여기서도 같은 스크립트 폴더를 통째로 컴파일해 그 정적 함수를 부른다 — 서버를 띄우지 않고 숫자만 본다.
/// 장비 속성은 끼고 벗는 흐름이라 격리 서버에서 본다.
/// </summary>
public sealed class CombatRenewalTests : IDisposable
{
    private static readonly Lazy<Assembly> Scripts = new(CompileScripts);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    /// <summary>원작 5.99 `0x4150f2`: d + trunc(d × AC × k), k = 0.01(AC &gt; 0) · 0.009(AC ≤ 0), 최소 1.</summary>
    [Theory]
    [InlineData(1000, 100, 2000)]
    [InlineData(1000, 30, 1300)]
    [InlineData(1000, 0, 1000)]
    [InlineData(1000, -45, 596)] // 0.009 는 double 로 조금 작아 −405 가 −404.99… → 버림 −404. 원작 x87 도 같은 상수다.
    [InlineData(1000, -70, 370)]
    [InlineData(2, -70, 1)]
    [InlineData(1, -70, 1)]
    public void Armour_is_the_original_formula(int blow, int armour, int expected) =>
        Assert.Equal(expected, Call<int>("Ac", "Apply", blow, armour));

    /// <summary>
    /// 새 상성표(사용자 2026-10-04). 고리 수 &gt; 화 &gt; 풍 &gt; 토 &gt; 수 · 암흑은 넷을 이기고 생명은 암흑을 이긴다 ·
    /// 방어 무속성은 속성 있는 공격을 1.3 으로 받는다(사용자 2026-10-07) · 나머지(같음·무관·공격 무속성)는 1.
    /// </summary>
    [Theory]
    [InlineData(Element.Water, Element.Fire, 1.3)]
    [InlineData(Element.Fire, Element.Wind, 1.3)]
    [InlineData(Element.Wind, Element.Earth, 1.3)]
    [InlineData(Element.Earth, Element.Water, 1.3)]
    [InlineData(Element.Fire, Element.Water, 0.7)]
    [InlineData(Element.Wind, Element.Fire, 0.7)]
    [InlineData(Element.Earth, Element.Wind, 0.7)]
    [InlineData(Element.Water, Element.Earth, 0.7)]
    [InlineData(Element.Water, Element.Water, 1.0)]
    [InlineData(Element.Water, Element.Wind, 1.0)]
    [InlineData(Element.Fire, Element.Earth, 1.0)]
    [InlineData(Element.None, Element.Fire, 1.0)]
    [InlineData(Element.Fire, Element.None, 1.3)]
    [InlineData(Element.Dark, Element.None, 1.3)]
    [InlineData(Element.None, Element.None, 1.0)]
    [InlineData(Element.Dark, Element.Water, 1.3)]
    [InlineData(Element.Dark, Element.Fire, 1.3)]
    [InlineData(Element.Dark, Element.Wind, 1.3)]
    [InlineData(Element.Dark, Element.Earth, 1.3)]
    [InlineData(Element.Water, Element.Dark, 0.7)]
    [InlineData(Element.Fire, Element.Dark, 0.7)]
    [InlineData(Element.Wind, Element.Dark, 0.7)]
    [InlineData(Element.Earth, Element.Dark, 0.7)]
    [InlineData(Element.Light, Element.Dark, 1.3)]
    [InlineData(Element.Dark, Element.Light, 0.7)]
    [InlineData(Element.Light, Element.Water, 1.0)]
    [InlineData(Element.Earth, Element.Light, 1.0)]
    [InlineData(Element.Dark, Element.Dark, 1.0)]
    [InlineData(Element.Light, Element.Light, 1.0)]
    [InlineData(Element.Light, Element.None, 1.3)]
    [InlineData(Element.None, Element.Dark, 1.0)]
    public void Elements_follow_the_new_table(Element attack, Element defense, double expected) =>
        Assert.Equal(expected, Call<double>("Elements", "Multiplier", attack, defense));

    /// <summary>
    /// 평타 = 공격력 + rand%10 − rand%10(−9~+9), rand%100 ≤ 치명타면 ×2(원작 `0x4166b1` · `0x4166eb`).
    /// 치명타 0 이어도 1% 는 두 배가 나고, 99 면 늘 두 배다.
    /// </summary>
    [Fact]
    public void A_plain_blow_is_attack_power_give_or_take_nine_and_doubles_on_a_critical()
    {
        const long power = 1500;
        Random dice = new(20261004);
        int[] rolls = [.. Enumerable.Range(0, 20000).Select(_ => Call<int>("Assail", "Roll", power, 0L, dice))];

        int[] plain = [.. rolls.Where(roll => roll <= power + 9)];
        int[] doubled = [.. rolls.Where(roll => roll > power + 9)];

        Assert.Equal((int)power - 9, plain.Min());
        Assert.Equal((int)power + 9, plain.Max());
        Assert.All(doubled, roll => Assert.InRange(roll, 2 * (power - 9), 2 * (power + 9)));
        Assert.InRange(doubled.Length, 100, 320); // 1% ≈ 200

        Assert.All(
            Enumerable.Range(0, 500).Select(_ => Call<int>("Assail", "Roll", power, 99L, dice)),
            roll => Assert.InRange(roll, 2 * (power - 9), 2 * (power + 9)));
    }

    /// <summary>
    /// 무도가 기술은 공격력 × 배율 + 콘 × 배율 그대로 — 기술 레벨 보정이 없다. 71레벨 무도가 붕각(AD×3.5 + 콘×59)이
    /// 문어(AC −45)에게: 힘 150(10 + 70×2) · 견습자의글러브 110 · 무도가 보너스 210 → 공격력 1820, 콘 5.
    /// 원작은 3666(진단서 4절, 원작 시작 힘 4 라 공격력 1760, 콘 항 없음).
    /// </summary>
    [Fact]
    public void A_level_71_monk_kick_lands_near_the_original()
    {
        int strike = Call<int>("MonkStrike", "Strike", 1820L, 5, 350, 5900);
        int landed = Call<int>("Ac", "Apply", strike, -45);

        Assert.Equal(6665, strike);
        Assert.Equal(3966, landed);
        Assert.InRange(landed, 3666 * 0.9, 3666 * 1.1);
    }

    /// <summary>등 뒤 ×2 · 옆 ×1.5 는 껐다 — 판정은 남고 배수가 모두 1 이다(사용자 2026-10-04).</summary>
    [Fact]
    public void A_blow_from_behind_or_the_side_is_worth_the_same_as_from_in_front()
    {
        foreach (string facing in new[] { "FromBehind", "FromTheSide", "FromInFront" })
        {
            FieldInfo field = typeof(Sprite).GetField(facing, BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.Equal(1.0, (double)field.GetRawConstantValue()!);
        }
    }

    /// <summary>
    /// 장비 속성을 끼고 벗는다 — 공격 = 목걸이, 방어 = 허리띠(사용자 2026-10-07). 광단검화 · 튜닉수의 수·토·풍·화는 수치만
    /// 바꾸고 속성을 주지 않는다. 목걸이(토) · 허리띠(암흑)는 시험용으로 격리 서버에만 하나씩 만든다.
    /// </summary>
    [Fact]
    public async Task Gear_elements_follow_what_is_worn_on_and_off()
    {
        const string name = "gearelement";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server, name);
        ElementalCopy(server, "대지의크리스탈목걸이", "속성시험목걸이", "OffenseElement", Element.Earth);
        ElementalCopy(server, "대지의금벨트", "속성시험허리띠", "DefenseElement", Element.Dark);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, name);
        string path = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        saved["ExpLevel"] = 99;
        saved["_Str"] = 100;
        File.WriteAllText(path, saved.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.Vitals is not null, "처음 수치가 오지 않았습니다.", world);

        await Wear(world, "광단검화");
        await Expect(world, Element.None, Element.None);
        await Wear(world, "속성시험목걸이");
        await Expect(world, Element.Earth, Element.None);
        await world.TakeOffAsync(1, _deadline.Token);
        await Expect(world, Element.Earth, Element.None);
        await world.TakeOffAsync(6, _deadline.Token);
        await Expect(world, Element.None, Element.None);

        await Wear(world, "튜닉수");
        await Expect(world, Element.None, Element.None);
        await Wear(world, "속성시험허리띠");
        await Expect(world, Element.None, Element.Dark);
        await world.TakeOffAsync(2, _deadline.Token);
        await Expect(world, Element.None, Element.Dark);
        await world.TakeOffAsync(11, _deadline.Token);
        await Expect(world, Element.None, Element.None);
    }

    private async Task Wear(WorldClient world, string item)
    {
        await world.SayAsync($"/give \"{item}\" 1", _deadline.Token);
        InventoryItem? given = null;
        await Until(() => (given = world.Pack.FirstOrDefault(carried => carried.Name == item)) is not null,
            $"{item}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}", world);
        await world.UseAsync(given!.Slot, _deadline.Token);
        await Until(() => world.Pack.All(carried => carried.Name != item),
            $"{item}을 걸치지 못했습니다. 서버가 한 말: {world.Said}", world);
    }

    private Task Expect(WorldClient world, Element offense, Element defense) =>
        Until(() => world.Vitals is { } now && (int)now.Offense == (int)offense && (int)now.Defense == (int)defense,
            $"공격·방어 속성이 {offense}·{defense} 이어야 합니다. 지금 {world.Vitals?.Offense}·{world.Vitals?.Defense}", world);

    private async Task Until(Func<bool> wanted, string complaint, WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!wanted())
        {
            Assert.True(DateTime.UtcNow < giveUp, complaint);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(200, _deadline.Token);
        }
    }

    private static void MakeGameMaster(IsolatedHadesServer server, string name)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ElementalCopy(IsolatedHadesServer server, string from, string name, string field, Element element)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "items");
        JsonNode template = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, $"{from}.json")))!;
        template["Name"] = name;
        template[field] = (int)element;
        File.WriteAllText(Path.Combine(folder, $"{name}.json"), template.ToJsonString());
    }

    private static T Call<T>(string type, string method, params object[] args)
    {
        Type found = Scripts.Value.GetTypes().Single(candidate => candidate.Name == type);
        MethodInfo call = found.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        return (T)call.Invoke(null, args)!;
    }

    /// <summary>서버(<c>ScriptManager.LoadAndCacheScripts</c>)처럼 스크립트 폴더 전체를 서버 빌드의 dll 들에 대고 컴파일한다.</summary>
    private static Assembly CompileScripts()
    {
        string[] files = Directory.GetFiles(
            Path.Combine(HadesWorkspace.ServerDataDirectory, "scripts"), "*.cs", SearchOption.AllDirectories);

        List<string> references = [];
        foreach (string dll in Directory.GetFiles(HadesWorkspace.StagingDirectory, "*.dll"))
        {
            try
            {
                AssemblyName.GetAssemblyName(dll);
                references.Add(dll);
            }
            catch (BadImageFormatException)
            {
            }
        }

        HashSet<string> staged = [.. references.Select(dll => Path.GetFileName(dll))];
        references.AddRange(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(dll => !staged.Contains(Path.GetFileName(dll))));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "CombatRenewalScripts",
            files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file))),
            references.Select(dll => MetadataReference.CreateFromFile(dll)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream image = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(image);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(20)));

        return Assembly.Load(image.ToArray());
    }
}
