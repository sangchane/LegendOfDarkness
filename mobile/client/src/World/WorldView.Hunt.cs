using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace LodClient;

/// <summary>월드 화면 — 자동 사냥(옛 `--hunt` 봇과 알맹이 `AutoHunt`), 자동 줍기·물약.</summary>
public sealed partial class WorldView
{
    /// <summary>
    /// Plays the character by itself: walks to the nearest monster, swings at it, and spends the points a
    /// level hands out. Only runs when the build was told to (<c>--hunt</c>, or a shipped `hunt.cfg`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it lives in the client and not outside it.</b> Skills, motion, sound and the flash a blow
    /// makes are all drawn here. A character driven from another connection leaves this screen showing
    /// somebody standing still, and then the only thing a run can check is the server's arithmetic.
    /// </para>
    /// <para>
    /// It moves at the pace a thumb would. The server rejects steps sent faster than
    /// <c>WalkingSpeedLimitFactor</c> and puts the character back where it was, which on screen reads as
    /// teleporting, and a swing sent before the last one finished is refused in words.
    /// </para>
    /// <para>
    /// 우드랜드1-1 은 60×60 — 3,600칸에 괴물이 50마리다. 화면에 보이는 것은 열두 칸 안이라, 서 있으면
    /// 아무것도 지나가지 않는다. 그래서 보이지 않을 때는 걸어서 찾고, 벽에 막히면 방향을 튼다.
    /// </para>
    /// </remarks>
    private void HuntOnItsOwn()
    {
        if (!Main.Hunting || server is null || Frozen || _walked >= 0 || _guide is not null)
        {
            return;
        }

        _hunted++;

        Tile standing = server.State?.Where ?? _tile;

        if (Nearest() is not { } prey)
        {
            _stuck = 0;
            Roam();
            return;
        }

        int dx = prey.X - standing.X, dy = prey.Y - standing.Y;

        if (Math.Abs(dx) + Math.Abs(dy) <= 1)
        {
            _stuck = 0;
            Face(dx, dy);

            // 한 번 휘두르고 서버가 허락하는 간격만큼 쉰다. GlobalBaseSkillDelay 가 500ms 다.
            if (_hunted % SwingFrames == 0)
            {
                Strike();
            }

            return;
        }

        // 자리가 한참 그대로면 그 한 마리를 잊는다. 길찾기가 벽은 돌아가 주지만, 사람이나 괴물이 길목에
        // 서 있는 것까지는 모른다 — 그럴 때 빠져나오는 마지막 장치다.
        _stuck = standing == _chasedFrom ? _stuck + 1 : 0;
        _chasedFrom = standing;

        if (_stuck > StuckTicks)
        {
            _stuck = 0;
            _heading = (Direction)((((int)_heading) + 1) % 4);
            Roam();
            return;
        }

        // 벽을 뚫고 가려 하지 않는다 — 돌아가는 길의 첫 걸음을 딛는다. 길이 아예 없으면 다른 데를 본다.
        if (Pathing.StepTowards(standing, prey, Walled, ChaseReach) is not { } step)
        {
            _stuck = 0;
            Roam();
            return;
        }

        Walk(step);
    }

    // 자동 사냥 — 판단은 알맹이(AutoHunt), 여기는 그 결정을 걸음·평타·기술로 옮기기만 한다.
    private readonly AutoHunt _autoHunt = new();

    /// <summary>다른 사람이 이만큼 안에 친 괴물은 "남이 치는 것"으로 본다.</summary>
    private static readonly System.TimeSpan ContestedFor = System.TimeSpan.FromSeconds(5);

    /// <summary>자동 사냥이 켜져 있나.</summary>
    public bool AutoHunting => _autoHunt.On;

    /// <summary>손이 잠시 조작 중이라 자동 사냥이 쉬고 있나.</summary>
    public bool AutoHuntPaused => _autoHunt.On && _autoHunt.Paused(Now);

    /// <summary>기술 부채꼴에 놓인 기술 — GameScreen 이 AbilityBar 에서 이어 준다.</summary>
    public System.Func<IReadOnlyList<LearnedSkill>>? BarSkills { get; set; }

    /// <summary>자동 사냥이 스스로 멈췄다 — 한 줄 알림.</summary>
    public event System.Action<string>? AutoHuntStopped;

    private static System.TimeSpan Now => System.TimeSpan.FromMilliseconds(Time.GetTicksMsec());

    /// <summary>켜고 끈다. 켠 자리가 사냥 반경의 중심이다.</summary>
    public void SetAutoHunt(bool on)
    {
        if (on && !_autoHunt.On)
        {
            _autoHunt.Start(_tile, MapId);
        }
        else if (!on && _autoHunt.On)
        {
            _autoHunt.Stop();
        }
    }

    /// <summary>사람이 방향판을 눌렀다 — 잠시 손에 맡기고, 선 자리를 새 중심으로.</summary>
    public void SteeredByHand()
    {
        if (_autoHunt.On)
        {
            _autoHunt.Steered(_tile, Now);
        }
    }

    /// <summary>사람이 공격·기술 단추를 눌렀다 — 잠시 손에 맡긴다.</summary>
    public void FoughtByHand()
    {
        if (_autoHunt.On)
        {
            _autoHunt.Pause(Now);
        }
    }

    private void AutoHuntTick()
    {
        if (!_autoHunt.On || server is not { } world || Frozen || _walked >= 0 || _guide is not null)
        {
            return;
        }

        uint me = world.Serial;
        HuntSight sight = new()
        {
            Standing = _tile,
            Facing = _player.Looking,
            MapId = MapId,
            Vitals = world.Vitals,
            Comatose = Comatose,
            Creatures = world.Creatures,
            HealthOf = world.Health,
            FoughtByOthers = serial => world.StruckByOthers(serial, ContestedFor),
            Skills = BarSkills?.Invoke() ?? [],
            Spells = BarSpells?.Invoke() ?? [],
            EnemySpells = [.. (BarSpells?.Invoke() ?? []).Where(one => AimsAtEnemy?.Invoke(one) == true && Main.Kit.AutoCasts(world.Path, one.Name))],
            Cooling = world.CoolingFor,
            PotionReady = Main.HealthPotion.Enabled && AutoPotion.Count(world.Pack, Main.HealthPotion.Potion) > 0,
            AutoLoot = Main.AutoLoot,
            Blocked = Walled,
            People = [.. world.Others.Where(one => one.Serial != me).Select(one => one.Where)],
            Now = Now,
        };

        HuntStep step = _autoHunt.Next(sight, Main.AutoHuntSettings);

        if (step.Target != 0 && step.Target != _target)
        {
            _target = step.Target;
            Mark();
        }

        switch (step.Act)
        {
            case HuntAct.Stop:
                GD.Print($"GREYBOX_AUTOHUNT 멈춤 {step.Why}");
                AutoHuntStopped?.Invoke(step.Why);
                break;
            case HuntAct.Heal:
                UseSpell(step.Slot, 0);
                break;
            case HuntAct.Walk:
                Walk(step.Toward);
                break;
            case HuntAct.Face:
                _player.Face(step.Toward);
                Main.Fire(world.TurnAsync(step.Toward, _leaving.Token));
                break;
            case HuntAct.Strike:
                Strike();
                break;
            case HuntAct.Skill:
                UseSkill(step.Slot);
                break;
            case HuntAct.Cast:
                UseSpell(step.Slot, step.Target);
                break;
        }
    }

    /// <summary>Faces the neighbouring tile without stepping onto it, so a swing lands the right way.</summary>
    private void Face(int dx, int dy)
    {
        _player.Face(Toward(dx, dy));
        Main.Fire(server?.TurnAsync(Toward(dx, dy), _leaving.Token));
    }

    private static Direction Toward(int dx, int dy) =>
        Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Direction.East : Direction.West)
            : (dy >= 0 ? Direction.South : Direction.North);

    /// <summary>
    /// The nearest monster's tile — nearest by <b>walking</b>, not by how the crow flies. One three tiles off
    /// behind a wall is further away than one six tiles off down an open lane, and picking by the straight line
    /// is what left a figure pressed against that wall for ever.
    /// </summary>
    private Tile? Nearest()
    {
        if (server is null)
        {
            return null;
        }

        Tile? best = null;
        int shortest = int.MaxValue;

        foreach (Creature beast in server.Creatures.Where(one => one.Kind == CreatureKind.Hostile))
        {
            // 멀리 있는 것부터 길을 재면 프레임이 녹는다 — 직선으로도 멀면 아예 보지 않는다.
            if (Math.Abs(beast.Where.X - _tile.X) + Math.Abs(beast.Where.Y - _tile.Y) > ChaseReach)
            {
                continue;
            }

            if (Pathing.Steps(_tile, beast.Where, Walled, ChaseReach) is not { } steps || steps >= shortest)
            {
                continue;
            }

            shortest = steps;
            best = beast.Where;
        }

        return best;
    }

    /// <summary>How far away a monster may be and still be worth walking to.</summary>
    private const int ChaseReach = 20;

    /// <summary>Whether a tile cannot be walked on — the walls this map was drawn with, and everything off it.</summary>
    private bool Walled(Tile tile) =>
        tile.X < 0 || tile.Y < 0
        || (_layout is { } map && (tile.X >= map.Columns || tile.Y >= map.Rows || map.Blocks(tile)));

    /// <summary>Walks on looking for something to fight, turning when the last step did not land.</summary>
    /// <remarks>
    /// Whether the step landed is asked of the <b>server</b>, not of what this client believes. A step is
    /// drawn the moment it is asked for — that is what keeps walking from lagging a third of a second — so
    /// the client's own tile moves even into a wall, and comparing against it says "we moved" every time.
    /// The character then walks into the same wall for ever, which is exactly what it did.
    /// </remarks>
    private void Roam()
    {
        // 걸음 간격은 이미 _walked 가 잰다 — 한 걸음이 끝나기 전에는 여기 오지도 않는다. 그 위에 프레임
        // 제동을 하나 더 걸었더니 막힌 자리에서 한 걸음에 13초가 걸렸다.
        Tile standing = server?.State?.Where ?? _tile;

        if (standing == _roamedFrom)
        {
            _heading = (Direction)((((int)_heading) + 1) % 4);
        }

        _roamedFrom = standing;
        Walk(_heading);
    }

    /// <summary>
    /// 레벨업 점수 한 점을 직업 계획(알맹이 <see cref="StatPlan"/>)대로 찍는다. 계획이 없는 직업은 그대로 둔다.
    /// </summary>
    private void SpendAPoint()
    {
        // 레벨업이 준 점수를 스스로 찍는다. 예전에는 자동 사냥 중에만 돌아, 사람이 놀면 점수가 쌓이기만 했다
        // (2026-09-18 조사). 서버는 한 번에 한 점씩 받으므로 프레임마다 한 점.
        if (server?.Vitals is { } mine && StatPlan.Next(server.Path, mine) is { } which)
        {
            Main.Fire(server.RaiseAsync(which, _leaving.Token));
        }
    }

    /// <summary>
    /// Swings at whatever stands in front of us. Nothing is assumed about the result — the server knows
    /// where everyone is and how recently we last swung, and answers in words when it refuses.
    /// </summary>
    public void Strike()
    {
        if (Frozen || Comatose)
        {
            return;
        }

        // Drawn straight away rather than waiting to be told: the server does not answer an allowed blow,
        // the same as a step, and a swing that lags a third of a second reads as a broken button.
        //
        // 무엇을 그릴지는 **서버가 지난번에 말해 준 것**을 쓴다. 평타 동작은 입은 것이 정하는데(무기의
        // 공격모션, 없으면 갑옷의 것 — 도복은 주먹 132) 클라이언트는 그 칸을 모른다. 그래서 늘 일반
        // 휘두르기만 그렸고, 무도가가 주먹을 안 쥐었다(사용자, 2026-09-18).
        // 직업 동작은 그 동작을 받는 옷(skill.tbl ST)을 입었을 때만 — 아니면 원작처럼 일반 휘두르기다.
        // 서버가 아직 한 번도 답하지 않았으면 미리 그리지 않는다 — 공통 휘두르기를 짐작해 그리면 무도가가 로그인 뒤
        // 첫 평타를 휘둘렀다(2026-09-25). 그 한 번은 답이 오면 Swings 가 그린다.
        if (_ownBlow.Swung(System.TimeSpan.FromMilliseconds(Time.GetTicksMsec())))
        {
            DrawOwnBlow(_ownBlow.Number ?? 1, _ownBlow.Speed);
        }

        Main.Fire(server?.AttackAsync(_leaving.Token));
    }

    /// <summary>Our own blow in the motion given — a class motion only in clothes skill.tbl lists for it, else the plain swing.</summary>
    private void DrawOwnBlow(int number, int speed)
    {
        BodyMotion blow = BodyMotion.Of(number) is { } known
            && server is { } world
            && BodyMotion.Fits(number, ArmourOf(world, world.Serial))
            ? known
            : BodyMotion.Blow;
        _player.Play(blow, blow.SecondsPerFrame(speed));
    }

    /// <summary>
    /// Uses a learned skill. Unlike the plain blow nothing is drawn yet: which motion a skill makes (a kick, a
    /// stab, a cast) only the server says, and it says so to us as well (0x1A) — <see cref="Swings" /> draws it.
    /// </summary>
    public void UseSkill(int slot)
    {
        if (Frozen)
        {
            return;
        }

        _ownBlow.Other();
        Main.Fire(server?.UseSkillAsync(slot, _leaving.Token));
    }

    /// <summary>Casts a learned spell at a chosen serial, or at self when target is zero.</summary>
    public void UseSpell(int slot, uint target)
    {
        if (Frozen)
        {
            return;
        }

        _ownBlow.Other();
        Main.Fire(server?.UseSpellAsync(slot, target, _leaving.Token));
    }

    /// <summary>We threw something at our feet; walk-over loot leaves it lying (<see cref="AutoLootGate.Threw" />).</summary>
    public void Threw(Tile where) => _autoLoot.Threw(where);

    /// <summary>
    /// Picks up whatever we are standing on. The only way to lift something was to tap it, and on a floor with
    /// thirty monsters on it nobody finds a 20-pixel bundle to tap — a character fought all night and came home
    /// with an empty bag (사용자, 2026-09-18). Stepping on it is what players expect now.
    /// </summary>
    /// <remarks>
    /// The server decides whether it may be carried (weight, a full bag) and says so in words; this only asks.
    /// Asking again while the answer is on its way would ask many times for the one bundle, so it waits.
    /// </remarks>
    private void Gather()
    {
        if (server is null || Frozen)
        {
            return;
        }

        // Allowed walks are silent on this protocol.  State.Where is therefore often the tile where we
        // logged in, while _tile is the prediction we just told the server to make.  Pickup must name the
        // latter or the server quite correctly finds no item at the old coordinate.
        if (_autoLoot.Next(Main.AutoLoot, _tile, server.Creatures) is { } where)
        {
            Main.Fire(server.PickUpAsync(where, _leaving.Token));
        }
    }

    /// <summary>Drinks a carried potion when health or mana has fallen to the line chosen on the game screen.</summary>
    private void Drink()
    {
        if (server is null || Frozen || server.Vitals is not { } vitals)
        {
            return;
        }

        System.TimeSpan now = System.TimeSpan.FromMilliseconds(Time.GetTicksMsec());

        if (_potion.Next(vitals, server.Pack, Main.HealthPotion, Main.ManaPotion, now) is { } slot)
        {
            Main.Fire(server.UseAsync(slot, _leaving.Token));
        }
    }
}
