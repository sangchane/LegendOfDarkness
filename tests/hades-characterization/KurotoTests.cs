using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 무도가의 쿠로토가 화면에 무엇을 보내는지. 5.99 `SPELL_쿠로토` 는 <c>motion 136, 75</c> · <c>effect @target, 4, 0, 75</c>
/// 인데, 136(마법사 시전)은 원작 클라이언트가 마법사 옷에만 그려 도복 무도가는 몸이 안 움직였다(사용자: "도복 입어도
/// 쿠로토 모션 있어"). 그래서 무도가에게는 혼든 팩·하데스 Aisling.Cast 처럼 손 들기(6)를, 둘 다 20%씩 두 번 느리게(117).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class KurotoTests(ITestOutputHelper output) : IDisposable
{
    private const int WoodlandOneOne = 20015;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

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
        Assert.Equal(117, ring.Speed);
        // 몸은 앱이 30% 를 더 걸어 링과 같은 빠르기가 되도록 117 / 1.3 ≈ 90(사용자: "모션이 이펙트에 비해 느리다").
        Assert.Equal((world.Serial, 6, 90), (cast!.Serial, cast.Number, cast.Speed));

        // 6 은 03 파일의 손 들기 — 옷을 가리지 않는다. 136 은 도복(3)이 못 한다(skill.tbl 8번 줄).
        Assert.NotNull(BodyMotion.Of(6));
        Assert.True(BodyMotion.Fits(6, armour));
        Assert.False(BodyMotion.Fits(136, armour));
    }
}
