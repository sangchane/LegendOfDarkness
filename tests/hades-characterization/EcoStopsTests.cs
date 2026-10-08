using Lod.CompanionBot;
using Lod.EcoBots;
using Lod.Mobile.Core.Model;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 생태계 봇이 배포되는 <c>guide.txt</c> 로 가게를 찾는다 — 사람이 읽는 about 줄이 「판매: 무기 67종 · 레벨 1~99」로 줄어든 뒤(2026-10-08)
/// 봇은 <c>stock</c> 줄을 읽는다. 장비 가게는 마을마다 서클이 달라(<c>autopilot/circle-shops/SPEC.md</c>) 제 서클 가게만 들른다.
/// </summary>
public sealed class EcoStopsTests
{
    private static EcoWorld World() =>
        EcoWorld.Load(Path.Combine(HadesWorkspace.RepositoryRoot, "mobile", "client", "assets", "world"));

    [Fact]
    public void Bots_find_the_potion_shop_from_the_stock_lines()
    {
        EcoStop potions = World().PotionStop ?? throw new Xunit.Sdk.XunitException("물약 가게를 못 찾았습니다.");

        Assert.True(potions.Healing >= 4, $"체력 물약을 {potions.Healing}가지만 파는 가게를 골랐습니다.");
    }

    [Fact]
    public void Bots_visit_only_the_gear_shops_of_their_circle()
    {
        IReadOnlyList<EcoStop> gear = World().GearStops;

        Assert.Equal(2, gear.Count(stop => stop.Fits(5)));    // 노비스 둘
        Assert.Equal(4, gear.Count(stop => stop.Fits(30)));   // 노비스 1~40 둘 · 수오미 11~40 둘
        Assert.Equal(2, gear.Count(stop => stop.Fits(45)));   // 아벨무기점·방어구점
        Assert.All(gear.Where(stop => stop.Fits(45)), stop => Assert.Contains(stop.Map, new[] { 20031, 20032 }));
        Assert.Equal(2, gear.Count(stop => stop.Fits(80)));   // 뤼케시온해안대기실 둘
        Assert.Equal(2, gear.Count(stop => stop.Fits(99)));   // 구광산대기실 둘
        Assert.All(gear.Where(stop => stop.Fits(99)), stop => Assert.Equal(20832, stop.Map));
    }

    /// <summary>세오·칸·뮤레칸도 자리를 박지 않고 이름표(npc 줄)로 — 앱 지도가 없는 신전도 적힌다.</summary>
    [Fact]
    public void Bots_find_the_temples_and_murekan_by_their_name_labels()
    {
        EcoWorld world = World();

        Assert.Equal(20299, world.Npc("세오")?.Map);
        Assert.Equal(20302, world.Npc("칸")?.Map);
        Assert.Equal((20138, new Lod.Mobile.Core.Model.Tile(12, 5)), (world.Npc("뮤레칸")!.Map, world.Npc("뮤레칸")!.Where));
        Assert.Null(world.Npc("없는사람"));
    }

    /// <summary>
    /// 꼬리표가 동선을 정한다(사용자 2026-10-08 「데이터 수정하면 거기에 맞게 적용되도록」) — 가게 줄의 맵·칸·역할·서클만 바꾸면 봇이
    /// 들르는 곳이 바뀐다. 역할이 물약이 아니면 체력 물약을 팔아도 물약 가게가 아니고, 역할이 장비가 아니면 장비 가게가 아니다.
    /// </summary>
    [Fact]
    public void Moving_a_shop_line_moves_where_bots_go()
    {
        IReadOnlyList<EcoStop> stops = EcoWorld.Stops("""
            stock 30001 5 6 무기 41 70 액스, 광단검
            stock 30002 7 8 물약 0 0 하급체력포션, 중급체력포션
            stock 30003 9 9 잡화 0 0 하급체력포션, 가위
            """);
        EcoWorld world = new(new MapWalls(""), [], stops, ClassKit.Read(""));

        EcoStop gear = Assert.Single(world.GearStops);
        Assert.Equal((30001, new Lod.Mobile.Core.Model.Tile(5, 6), "무기"), (gear.Map, gear.Where, gear.Role));
        Assert.True(gear.Fits(41) && gear.Fits(70) && !gear.Fits(40) && !gear.Fits(71));
        Assert.Equal(30002, world.PotionStop?.Map);
    }
}
