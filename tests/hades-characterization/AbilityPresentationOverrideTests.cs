using System.Text.Json;
using Darkages.Network.ServerFormats;
using Darkages.Types;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

[Collection(TimedCollection.Name)]
public sealed class AbilityPresentationOverrideTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"lod-ability-overrides-{Guid.NewGuid():N}.json");
    private readonly string? _before = Environment.GetEnvironmentVariable("LOD_ABILITY_OVERRIDES");

    public AbilityPresentationOverrideTests()
    {
        Environment.SetEnvironmentVariable("LOD_ABILITY_OVERRIDES", _file);
        AbilityPresentationOverrides.ReloadNow();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOD_ABILITY_OVERRIDES", _before);
        if (File.Exists(_file)) File.Delete(_file);
        AbilityPresentationOverrides.ReloadNow();
    }

    [Fact]
    public void A_scoped_skill_replaces_effect_speed_and_both_sound_packet_shapes()
    {
        Write("skill:단각", effect: 42, speed: 75, sound: 16);

        using (AbilityPresentationOverrides.Begin("skill", "단각"))
        {
            var effect = new ServerFormat29(1, 2, 0, 69, 100);
            var healthSound = new ServerFormat13(2, 80, 14);
            var plainSound = new ServerFormat19 { Number = 14 };

            AbilityPresentationOverrides.Apply(effect);
            AbilityPresentationOverrides.Apply(healthSound);
            AbilityPresentationOverrides.Apply(plainSound);

            Assert.Equal((ushort) 42, effect.TargetEffect);
            Assert.Equal((ushort) 0, effect.CasterEffect);
            Assert.Equal((ushort) 75, effect.Speed);
            Assert.Equal((byte) 16, healthSound.Sound);
            Assert.Equal((short) 16, plainSound.Number);
        }
    }

    [Fact]
    public void No_scope_or_a_different_kind_keeps_the_original_packet()
    {
        Write("spell:단각", effect: 42, speed: 75, sound: 16);
        var outside = new ServerFormat29(1, 2, 69, 0, 100);
        AbilityPresentationOverrides.Apply(outside);

        using (AbilityPresentationOverrides.Begin("skill", "단각"))
        {
            var wrongKind = new ServerFormat13(2, 80, 14);
            AbilityPresentationOverrides.Apply(wrongKind);
            Assert.Equal((byte) 14, wrongKind.Sound);
        }

        Assert.Equal((ushort) 69, outside.CasterEffect);
        Assert.Equal((ushort) 100, outside.Speed);
    }

    [Fact]
    public void A_rewritten_file_is_used_without_restarting_the_server()
    {
        Write("spell:쿠로토", effect: 4, speed: 75, sound: 8);
        using (AbilityPresentationOverrides.Begin("spell", "쿠로토"))
        {
            var first = new ServerFormat29(1, 2, 4, 0, 117);
            AbilityPresentationOverrides.Apply(first);
            Assert.Equal((ushort) 4, first.CasterEffect);
            Assert.Equal((ushort) 75, first.Speed);
        }

        Write("spell:쿠로토", effect: 33, speed: 60, sound: 9);
        using (AbilityPresentationOverrides.Begin("spell", "쿠로토"))
        {
            var next = new ServerFormat29(1, 2, 4, 0, 117);
            AbilityPresentationOverrides.Apply(next);
            Assert.Equal((ushort) 33, next.CasterEffect);
            Assert.Equal((ushort) 60, next.Speed);
        }
    }

    [Fact]
    public void Invalid_or_out_of_range_values_are_ignored()
    {
        File.WriteAllText(_file, "{not json");
        AbilityPresentationOverrides.ReloadNow();
        using (AbilityPresentationOverrides.Begin("skill", "단각"))
        {
            var invalidJson = new ServerFormat29(1, 2, 0, 69, 100);
            AbilityPresentationOverrides.Apply(invalidJson);
            Assert.Equal((ushort) 69, invalidJson.TargetEffect);
        }

        File.WriteAllText(_file, JsonSerializer.Serialize(new
        {
            version = 1,
            abilities = new Dictionary<string, object>
            {
                ["skill:단각"] = new { effect = 1000, speed = 0, sound = 256 },
            },
        }));
        AbilityPresentationOverrides.ReloadNow();
        using (AbilityPresentationOverrides.Begin("skill", "단각"))
        {
            var outOfRange = new ServerFormat29(1, 2, 0, 69, 100);
            AbilityPresentationOverrides.Apply(outOfRange);
            Assert.Equal((ushort) 69, outOfRange.TargetEffect);
            Assert.Equal((ushort) 100, outOfRange.Speed);
        }
    }

    private void Write(string key, int effect, int speed, int sound)
    {
        File.WriteAllText(_file, JsonSerializer.Serialize(new
        {
            version = 1,
            abilities = new Dictionary<string, object>
            {
                [key] = new { effect, speed, sound },
            },
        }));
        AbilityPresentationOverrides.ReloadNow();
    }
}
