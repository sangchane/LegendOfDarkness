using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

public sealed class AutoHuntTests
{
    private const int Map = 20015;
    private static readonly AutoHuntSettings Defaults = new();
    private static readonly Tile Home = new(10, 10);

    private static Vitals Life(int health, int maximum = 1000) => Vitals.Unknown with { Health = health, MaximumHealth = maximum };

    private static Creature Beast(uint serial, int x, int y) => new(serial, new Tile(x, y), Direction.South, 1, CreatureKind.Hostile, "괴물");

    private static Creature Loot(uint serial, int x, int y) => new(serial, new Tile(x, y), Direction.South, 1, CreatureKind.Passable, "물건");

    private static HuntSight Sight(
        Tile? standing = null,
        Direction facing = Direction.South,
        IReadOnlyCollection<Creature>? creatures = null,
        int health = 1000,
        double seconds = 10) => new()
    {
        Standing = standing ?? Home,
        Facing = facing,
        MapId = Map,
        Vitals = Life(health),
        Creatures = creatures ?? [],
        Now = TimeSpan.FromSeconds(seconds),
    };

    private static AutoHunt Started(Tile? home = null)
    {
        AutoHunt hunt = new();
        hunt.Start(home ?? Home, Map);
        return hunt;
    }

    [Fact]
    public void Off_does_nothing()
    {
        Assert.Equal(HuntAct.Wait, new AutoHunt().Next(Sight(creatures: [Beast(1, 10, 11)]), Defaults).Act);
    }

    [Fact]
    public void Picks_the_nearest_monster_and_on_a_tie_the_more_hurt_one()
    {
        AutoHunt hunt = Started();
        HuntSight sight = Sight(creatures: [Beast(1, 14, 10), Beast(2, 10, 13), Beast(3, 7, 10)]) with
        {
            HealthOf = serial => serial == 3 ? 40 : 90
        };

        HuntStep step = hunt.Next(sight, Defaults);

        Assert.Equal(HuntAct.Walk, step.Act);
        Assert.Equal(3u, hunt.Target);
        Assert.Equal(Direction.West, step.Toward);
    }

    [Fact]
    public void Avoids_a_monster_someone_else_is_fighting_when_there_is_another()
    {
        AutoHunt hunt = Started();
        HuntSight sight = Sight(creatures: [Beast(1, 12, 10), Beast(2, 10, 15)]) with { FoughtByOthers = serial => serial == 1 };

        hunt.Next(sight, Defaults);

        Assert.Equal(2u, hunt.Target);
    }

    [Fact]
    public void Still_fights_a_contested_monster_when_it_is_the_only_one()
    {
        AutoHunt hunt = Started();
        HuntSight sight = Sight(creatures: [Beast(1, 12, 10)]) with { FoughtByOthers = _ => true };

        hunt.Next(sight, Defaults);

        Assert.Equal(1u, hunt.Target);
    }

    [Fact]
    public void Does_not_chase_outside_the_radius_and_walks_back_home_instead()
    {
        AutoHunt hunt = Started();

        HuntStep step = hunt.Next(Sight(standing: new Tile(10, 14), creatures: [Beast(1, 10, 25)]), Defaults with { Radius = 12 });

        Assert.Equal(0u, hunt.Target);
        Assert.Equal(HuntAct.Walk, step.Act);
        Assert.Equal(Direction.North, step.Toward);
        Assert.Equal("돌아가기", step.Why);
    }

    [Fact]
    public void Waits_at_home_with_nothing_in_sight()
    {
        Assert.Equal(HuntAct.Wait, Started().Next(Sight(), Defaults).Act);
    }

    [Fact]
    public void Faces_an_adjacent_monster_before_hitting_it()
    {
        AutoHunt hunt = Started();

        HuntStep step = hunt.Next(Sight(facing: Direction.North, creatures: [Beast(1, 11, 10)]), Defaults);

        Assert.Equal(HuntAct.Face, step.Act);
        Assert.Equal(Direction.East, step.Toward);
    }

    [Fact]
    public void Uses_a_ready_fighting_skill_then_the_plain_blow_while_it_cools()
    {
        AutoHunt hunt = Started();
        Dictionary<int, int> cooling = [];
        LearnedSkill[] bar = [new(1, 1, "이형환위"), new(2, 2, "붕각")];

        HuntSight At(double seconds) => Sight(facing: Direction.East, creatures: [Beast(1, 11, 10)], seconds: seconds) with
        {
            Skills = bar,
            Cooling = (skill, slot) => skill && cooling.TryGetValue(slot, out int left) ? left : 0,
        };

        HuntStep first = hunt.Next(At(10), Defaults);
        Assert.Equal(HuntAct.Skill, first.Act);
        Assert.Equal(2, first.Slot);

        cooling[2] = 5;
        Assert.Equal(HuntAct.Wait, hunt.Next(At(10.2), Defaults).Act);
        Assert.Equal(HuntAct.Strike, hunt.Next(At(10.6), Defaults).Act);
        Assert.Equal(HuntAct.Wait, hunt.Next(At(11.0), Defaults).Act);
        Assert.Equal(HuntAct.Strike, hunt.Next(At(11.3), Defaults).Act);
    }

    [Fact]
    public void A_skill_the_server_refused_is_rested_so_the_plain_blow_gets_its_turn()
    {
        AutoHunt hunt = Started();

        HuntSight At(double seconds) => Sight(facing: Direction.East, creatures: [Beast(1, 11, 10)], seconds: seconds) with
        {
            Skills = [new LearnedSkill(2, 2, "붕각")],
        };

        Assert.Equal(HuntAct.Skill, hunt.Next(At(10), Defaults).Act);

        // 대기 안내가 오지 않았다 — 거절. 6초 동안은 평타만.
        Assert.Equal(HuntAct.Strike, hunt.Next(At(10.6), Defaults).Act);
        Assert.Equal(HuntAct.Strike, hunt.Next(At(11.3), Defaults).Act);
        Assert.Equal(HuntAct.Strike, hunt.Next(At(15.9), Defaults).Act);
        Assert.Equal(HuntAct.Skill, hunt.Next(At(16.6), Defaults).Act);
    }

    [Fact]
    public void Healing_and_body_moving_skills_are_not_for_fighting()
    {
        Assert.False(AutoHunt.IsForFighting("쿠로토"));
        Assert.False(AutoHunt.IsForFighting("이형환위"));
        Assert.False(AutoHunt.IsForFighting("armor lore"));
        Assert.True(AutoHunt.IsForFighting("붕각"));
        Assert.True(AutoHunt.IsForFighting("단각"));
        Assert.True(AutoHunt.IsHealing("쿠로토"));
        Assert.True(AutoHunt.IsHealing("beag ioc"));
    }

    [Fact]
    public void Heals_below_the_line_before_fighting()
    {
        AutoHunt hunt = Started();
        HuntSight sight = Sight(facing: Direction.East, creatures: [Beast(1, 11, 10)], health: 450) with
        {
            Spells = [new LearnedSpell(3, 21, SpellTargetType.NoTarget, "쿠로토", string.Empty, 0)],
        };

        HuntStep step = hunt.Next(sight, Defaults);

        Assert.Equal(HuntAct.Heal, step.Act);
        Assert.Equal(3, step.Slot);
    }

    [Fact]
    public void Does_not_heal_above_the_line()
    {
        AutoHunt hunt = Started();
        HuntSight sight = Sight(facing: Direction.East, creatures: [Beast(1, 11, 10)], health: 700) with
        {
            Spells = [new LearnedSpell(3, 21, SpellTargetType.NoTarget, "쿠로토", string.Empty, 0)],
        };

        Assert.Equal(HuntAct.Strike, hunt.Next(sight, Defaults).Act);
    }

    [Fact]
    public void Stops_when_very_low_with_no_way_to_heal()
    {
        AutoHunt hunt = Started();

        HuntStep step = hunt.Next(Sight(creatures: [Beast(1, 11, 10)], health: 150), Defaults);

        Assert.Equal(HuntAct.Stop, step.Act);
        Assert.Contains("포션", step.Why);
        Assert.False(hunt.On);
    }

    [Fact]
    public void Keeps_going_when_very_low_but_a_potion_will_be_drunk()
    {
        AutoHunt hunt = Started();

        HuntStep step = hunt.Next(Sight(creatures: [Beast(1, 11, 10)], health: 150) with { PotionReady = true }, Defaults);

        Assert.NotEqual(HuntAct.Stop, step.Act);
    }

    [Fact]
    public void Stops_in_a_coma_and_when_dead()
    {
        AutoHunt coma = Started();
        Assert.Equal(HuntAct.Stop, coma.Next(Sight() with { Comatose = true }, Defaults).Act);

        AutoHunt dead = Started();
        Assert.Equal(HuntAct.Stop, dead.Next(Sight(health: 0), Defaults).Act);
    }

    [Fact]
    public void Stops_when_the_map_changes()
    {
        AutoHunt hunt = Started();

        HuntStep step = hunt.Next(Sight() with { MapId = Map + 1 }, Defaults);

        Assert.Equal(HuntAct.Stop, step.Act);
        Assert.Contains("맵", step.Why);
    }

    [Fact]
    public void Walks_to_where_its_kill_dropped_something_before_the_next_fight()
    {
        AutoHunt hunt = Started();
        HuntSight fighting = Sight(facing: Direction.East, creatures: [Beast(1, 11, 10), Beast(2, 10, 7)]) with { AutoLoot = true };

        Assert.Equal(HuntAct.Strike, hunt.Next(fighting, Defaults).Act);

        HuntSight dropped = fighting with { Creatures = [Loot(50, 11, 10), Beast(2, 10, 7)], Now = TimeSpan.FromSeconds(11) };
        HuntStep step = hunt.Next(dropped, Defaults);

        Assert.Equal(HuntAct.Walk, step.Act);
        Assert.Equal(Direction.East, step.Toward);
        Assert.Equal("줍기", step.Why);
    }

    [Fact]
    public void Does_not_go_for_drops_when_auto_loot_is_off()
    {
        AutoHunt hunt = Started();
        HuntSight fighting = Sight(facing: Direction.East, creatures: [Beast(1, 11, 10)]);

        hunt.Next(fighting, Defaults);
        HuntStep step = hunt.Next(fighting with { Creatures = [Loot(50, 11, 10)], Now = TimeSpan.FromSeconds(11) }, Defaults);

        Assert.Equal(HuntAct.Wait, step.Act);
    }

    [Fact]
    public void Goes_round_a_wall_to_reach_the_monster()
    {
        AutoHunt hunt = Started();
        HashSet<Tile> wall = [new(11, 9), new(11, 10), new(11, 11)];
        HuntSight sight = Sight(creatures: [Beast(1, 13, 10)]) with { Blocked = wall.Contains };

        HuntStep step = hunt.Next(sight, Defaults);

        Assert.Equal(HuntAct.Walk, step.Act);
        Assert.NotEqual(Direction.East, step.Toward);
    }

    [Fact]
    public void Gives_up_on_an_unreachable_monster_for_another()
    {
        AutoHunt hunt = Started();
        HashSet<Tile> box = [new(14, 9), new(15, 10), new(14, 11), new(13, 10)];
        HuntSight sight = Sight(creatures: [Beast(1, 14, 10), Beast(2, 10, 16)]) with { Blocked = box.Contains };

        Assert.Equal(HuntAct.Wait, hunt.Next(sight, Defaults).Act);
        hunt.Next(sight, Defaults);

        Assert.Equal(2u, hunt.Target);
    }

    [Fact]
    public void Gives_up_when_steps_do_not_land()
    {
        AutoHunt hunt = Started();
        HuntSight sight = Sight(creatures: [Beast(1, 10, 15), Beast(2, 5, 10)]) with { HealthOf = serial => serial == 1 ? 10 : 100 };

        for (int tick = 0; tick <= AutoHunt.StuckSteps; tick++)
        {
            hunt.Next(sight, Defaults);
        }

        hunt.Next(sight, Defaults);
        Assert.Equal(2u, hunt.Target);
    }

    [Fact]
    public void A_thumb_on_the_pad_pauses_it_and_moves_the_centre()
    {
        AutoHunt hunt = Started();
        hunt.Steered(new Tile(20, 20), TimeSpan.FromSeconds(10));

        Assert.Equal(HuntAct.Wait, hunt.Next(Sight(standing: new Tile(20, 20), creatures: [Beast(1, 21, 20)], seconds: 11), Defaults).Act);
        Assert.Equal(new Tile(20, 20), hunt.Home);
        Assert.NotEqual(HuntAct.Wait, hunt.Next(Sight(standing: new Tile(20, 20), creatures: [Beast(1, 21, 20)], seconds: 14), Defaults).Act);
        Assert.True(hunt.On);
    }

    [Fact]
    public void Settings_round_trip_and_fall_back_to_defaults()
    {
        Assert.Equal(new AutoHuntSettings(8, 35), AutoHuntSettings.Parse(new AutoHuntSettings(8, 35).ToLine()));
        Assert.Equal(new AutoHuntSettings(), AutoHuntSettings.Parse("junk"));
        Assert.Equal(new AutoHuntSettings(), AutoHuntSettings.Parse(null));
        Assert.Equal(12, new AutoHuntSettings().Radius);
        Assert.Equal(50, new AutoHuntSettings().HealPercent);
    }
}
