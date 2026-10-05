using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 무도가의 쿠로토가 화면에 무엇을 보내는지. 5.99 `SPELL_쿠로토` 는 <c>motion 136, 75</c> · <c>effect @target, 4, 0, 75</c>
/// 인데, 136(마법사 시전)은 원작 클라이언트가 마법사 옷에만 그려 도복 무도가는 몸이 안 움직였다(사용자: "도복 입어도
/// 쿠로토 모션 있어"). 그래서 무도가에게는 혼든 팩·하데스 Aisling.Cast 처럼 손 들기(6)를 쓴다. 이펙트 속도는 노바 값 75다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class KurotoTests(ITestOutputHelper output) : IDisposable
{
    private const int WoodlandOneOne = 20015;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 쿠로토를 누르고 곧바로 평타 — 평타가 마법 대기 줄을 비워 쿠로토가 말없이 사라졌다(사용자 2026-10-05 「입력이 안 먹는 느낌」).
    /// 이제 외우는 중인 마법만 끊는다. 마력 3% 조건도 원작에 없어 뺐다 — 마력 0 에서도 나간다.
    /// </summary>
    [Fact]
    public async Task Kuroto_pressed_right_before_a_blow_still_goes_off_even_with_no_mana()
    {
        const string name = "kurotoblow";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, 2, 35));
        server.Start(TimeSpan.FromMinutes(2));

        using WorldSession session = await HadesLoginClient.CreateCharacterAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret,
            hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.Spells.Any(spell => spell.Name.StartsWith("쿠로토", StringComparison.Ordinal)),
            "쿠로토가 오지 않았습니다.", _deadline.Token);
        await Task.Delay(1500, _deadline.Token);
        LearnedSpell kuroto = world.Spells.First(spell => spell.Name.StartsWith("쿠로토", StringComparison.Ordinal));

        int casts = 0;
        for (int round = 0; round < 5; round++)
        {
            await Task.Delay(400, _deadline.Token); // 서버 마법 딜레이(0.25초) 밖에서
            while (world.TakeEffect(out _))
            {
            }

            await world.UseSpellAsync(kuroto.Slot, 0, _deadline.Token);
            await world.AttackAsync(_deadline.Token);
            DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(1.5);
            bool seen = false;
            while (!seen && DateTime.UtcNow < giveUp)
            {
                while (world.TakeEffect(out Effect? effect))
                {
                    seen |= effect!.Target == world.Serial && effect.Source == world.Serial && effect.SourceAnimation == 4;
                }

                await Task.Delay(50, _deadline.Token);
            }

            casts += seen ? 1 : 0;
        }

        Assert.Equal(5, casts);
    }

    [Fact]
    public async Task A_monk_in_the_robe_raises_hands_for_kuroto_under_ring_four_both_slowed()
    {
        const string name = "kuroto";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, 2, 35));
        server.Start(TimeSpan.FromMinutes(2));

        using WorldSession session = await HadesLoginClient.CreateCharacterAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret,
            hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.Spells.Any(spell => spell.Name.StartsWith("쿠로토", StringComparison.Ordinal))
                                  && world.Self?.Wearing is not null,
            "쿠로토나 내 옷차림이 오지 않았습니다.", _deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        while (world.TakeEffect(out _) || world.TakeMotion(out _))
        {
        }

        LearnedSpell kuroto = world.Spells.First(spell => spell.Name.StartsWith("쿠로토", StringComparison.Ordinal));
        await world.UseSpellAsync(kuroto.Slot, 0, _deadline.Token);

        Effect? ring = null;
        Motion? cast = null;
        await Waiting.Until(() =>
        {
            while (ring is null && world.TakeEffect(out Effect? effect))
            {
                ring = effect;
            }

            while (cast is null && world.TakeMotion(out Motion? motion))
            {
                cast = motion;
            }

            return ring is not null && cast is not null;
        }, "쿠로토의 이펙트나 몸동작이 오지 않았습니다.", _deadline.Token);

        int armour = world.Self!.Wearing!.Armor;
        output.WriteLine($"effect {ring}; motion {cast}; armour {armour}");

        Assert.Equal(world.Serial, ring!.Source);
        Assert.Equal(world.Serial, ring.Target);
        Assert.Equal(4, ring.SourceAnimation);
        Assert.Equal(0, ring.TargetAnimation);
        Assert.Equal(75, ring.Speed);
        // 몸 동작은 도복에서 분명히 보이도록 정한 90을 유지하고, 이펙트만 노바 원값 75를 쓴다.
        Assert.Equal((world.Serial, 6, 90), (cast!.Serial, cast.Number, cast.Speed));

        // 6 은 03 파일의 손 들기 — 옷을 가리지 않는다. 136 은 도복(3)이 못 한다(skill.tbl 8번 줄).
        Assert.NotNull(BodyMotion.Of(6));
        Assert.True(BodyMotion.Fits(6, armour));
        Assert.False(BodyMotion.Fits(136, armour));
    }
}
