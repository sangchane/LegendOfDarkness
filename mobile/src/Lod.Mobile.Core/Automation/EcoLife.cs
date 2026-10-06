using System.Text.Json;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Automation;

/// <summary>생태계 봇이 이번에 할 일.</summary>
public enum EcoAct
{
    /// <summary>사냥터에서 자동 사냥 한 틱.</summary>
    Hunt,

    /// <summary>(다른) 사냥터로 간다.</summary>
    GoHunt,

    /// <summary>마을(가게)로 간다.</summary>
    GoTown,

    /// <summary>가게를 돈다 — 입기·팔기·물약·장비.</summary>
    Shop,

    /// <summary>유령 — 뮤레칸에게 살려 달라고 한다.</summary>
    Revive,

    /// <summary>가만히(혼수, 마을에서 쉬기).</summary>
    Wait,
}

/// <summary>봇이 지금 있는 곳 — 봇 프로그램이 옮긴 뒤 <see cref="EcoLife.Arrived" /> 로 알린다.</summary>
public enum EcoPlace
{
    Nowhere,
    Hunting,
    Town,
}

/// <param name="HuntStopped">자동 사냥이 멈췄다(위험·끝) — <c>HuntProxyRunner.Stopped</c>.</param>
/// <param name="PersonSince">이 맵에 사람(봇 아닌)이 처음 보인 때, 안 보이면 null.</param>
/// <param name="HealthPercent">체력 %.</param>
public sealed record EcoSight(
    TimeSpan Now, bool Comatose, bool Ghost, int Potions, int FreeSlots, bool HuntStopped, TimeSpan? PersonSince, int HealthPercent);

/// <summary>
/// 생태계 봇의 한살이(FR-004·008, 설계 <c>autopilot/eco-bots/</c>): 사냥 → (물약·가방·시간·멈춤) → 마을에서 장보기 → (쉬기) → 사냥.
/// 유령이면 살아나기, 너무 자주 죽으면 사냥터를 한 층 낮춘다(<see cref="Lower" />). 옮기기·장보기 자체는 봇 프로그램이 한다.
/// </summary>
public sealed class EcoLife
{
    private TimeSpan _arrivedAt;
    private bool _shopped;
    private int _deaths;

    /// <summary>마지막 장보기 뒤 물약 수 — 이것도 못 채웠으면(금화 없음) 물약 때문에 바로 다시 마을로 가지 않는다.</summary>
    private int _potionsAfterShop = int.MaxValue;

    public EcoPlace Where { get; private set; } = EcoPlace.Nowhere;

    /// <summary>사냥터를 몇 층 낮출지 — <see cref="Tuning.EcoDeathLoop" /> 번 죽을 때마다 한 층.</summary>
    public int Lower => _deaths / Tuning.EcoDeathLoop;

    public EcoAct Next(EcoSight sight)
    {
        if (sight.Ghost)
        {
            return EcoAct.Revive;
        }

        if (sight.Comatose)
        {
            return EcoAct.Wait;
        }

        switch (Where)
        {
            case EcoPlace.Town when !_shopped:
                return EcoAct.Shop;
            case EcoPlace.Town:
                return sight.HealthPercent < Tuning.EcoRestPercent ? EcoAct.Wait : EcoAct.GoHunt;
            case EcoPlace.Nowhere:
                return sight.Potions < Tuning.EcoPotionLow ? EcoAct.GoTown : EcoAct.GoHunt;
        }

        bool needTown = sight.Potions < Math.Min(Tuning.EcoPotionLow, _potionsAfterShop)
                        || sight.FreeSlots < Tuning.EcoBagLow
                        || sight.Now - _arrivedAt >= Tuning.EcoTownEvery
                        || sight.HuntStopped;

        if (needTown)
        {
            return EcoAct.GoTown;
        }

        return sight.PersonSince is { } seen && sight.Now - seen >= Tuning.EcoYield ? EcoAct.GoHunt : EcoAct.Hunt;
    }

    /// <summary>봇 프로그램이 옮긴 뒤.</summary>
    public void Arrived(EcoPlace place, TimeSpan now)
    {
        if (place == EcoPlace.Town)
        {
            _shopped = false;
        }

        Where = place;
        _arrivedAt = now;
    }

    public void Shopped(int potionsNow)
    {
        _shopped = true;
        _potionsAfterShop = potionsNow;
    }

    /// <summary>뮤레칸에게 살아났다 — 서버가 레벨 맞는 마을로 보낸다. 봇 프로그램이 이어서 <see cref="Arrived" />(마을).</summary>
    public void Revived() => _deaths++;

    /// <summary>레벨이 올랐다 — 낮춘 사냥터를 다시 제 층으로(낮춘 채로는 레벨이 오를 때까지 둔다).</summary>
    public void LeveledUp() => _deaths = 0;
}

/// <summary>
/// 생태계 봇 사건 한 줄(설계 05 E7) — 머신러닝 재료라 숫자는 숫자 칸, 한 줄에 JSON 하나. 파일 쓰기는 봇 프로그램.
/// </summary>
public static class EcoLog
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Line(DateTime at, string bot, int? path, Vitals? vitals, int map, Tile where, string state, string ev, object? data) =>
        JsonSerializer.Serialize(new
        {
            v = 1,
            at = at.ToUniversalTime().ToString("O"),
            bot,
            cls = path ?? 0,
            lvl = vitals?.Level ?? 0,
            exp = vitals?.Experience ?? 0,
            gold = vitals?.Gold ?? 0,
            hp = vitals?.Health ?? 0,
            mhp = vitals?.MaximumHealth ?? 0,
            mp = vitals?.Mana ?? 0,
            mmp = vitals?.MaximumMana ?? 0,
            map,
            x = where.X,
            y = where.Y,
            state,
            ev,
            data = data ?? new { },
        }, Options);
}
