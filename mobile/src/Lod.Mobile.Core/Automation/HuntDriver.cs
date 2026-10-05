using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 자동 사냥이 한 틱에 보는 세상(<see cref="HuntSight" />)을 접속(<see cref="WorldClient" />)에서 모은다 — 앱(WorldView)과
/// 대신 사냥 대리 프로그램(Lod.HuntProxy)이 같은 것을 써서 같은 판단을 한다.
/// </summary>
public static class HuntDriver
{
    /// <summary>다른 사람이 이만큼 안에 친 괴물은 "남이 치는 것"으로 본다.</summary>
    public static readonly TimeSpan ContestedFor = TimeSpan.FromSeconds(5);

    /// <param name="skills">기술 막대에 놓인 기술.</param>
    /// <param name="spells">기술 막대에 놓인 마법(회복 찾기).</param>
    /// <param name="enemySpells">그 가운데 괴물에 쓰는 마법.</param>
    /// <param name="healthPotion">자동 체력 포션 — 켜져 있고 가방에 있으면 포션을 믿고 버틴다.</param>
    public static HuntSight Sight(
        WorldClient world,
        Tile standing,
        Direction facing,
        int mapId,
        bool comatose,
        IReadOnlyList<LearnedSkill> skills,
        IReadOnlyList<LearnedSpell> spells,
        IReadOnlyList<LearnedSpell> enemySpells,
        PotionRule healthPotion,
        bool autoLoot,
        Func<Tile, bool> blocked,
        IReadOnlyCollection<Tile> exits,
        TimeSpan now)
    {
        uint me = world.Serial;

        return new HuntSight
        {
            Standing = standing,
            Facing = facing,
            MapId = mapId,
            Vitals = world.Vitals,
            Comatose = comatose,
            Creatures = world.Creatures,
            HealthOf = world.Health,
            FoughtByOthers = serial => world.StruckByOthers(serial, ContestedFor),
            Skills = skills,
            Spells = spells,
            EnemySpells = enemySpells,
            Cooling = world.CoolingFor,
            PotionReady = healthPotion.Enabled && AutoPotion.Count(world.Pack, healthPotion.Potion) > 0,
            AutoLoot = autoLoot,
            Blocked = blocked,
            People = [.. world.Others.Where(one => one.Serial != me).Select(one => one.Where)],
            Exits = exits,
            Now = now,
        };
    }
}
