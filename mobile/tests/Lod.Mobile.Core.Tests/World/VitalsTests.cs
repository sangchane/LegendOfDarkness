using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Our own numbers, which arrive in four independent pieces behind one flag byte. The server sends all four
/// as a character enters and single pieces afterwards — what is left of health and mana every time anything
/// is struck — so reading one piece must leave the other three alone.
/// </summary>
public sealed class VitalsTests
{
    private const byte Standing = 0x20;
    private const byte Remaining = 0x10;
    private const byte Earned = 0x08;
    private const byte Fighting = 0x04;

    /// <summary>Two flags the server always sets. They say nothing about the body.</summary>
    private const byte AlwaysOn = 0x40 | 0x80;

    [Fact]
    public void All_four_pieces_read_as_one_character()
    {
        Vitals me = WorldClient.ReadVitals([
            Standing | Remaining | Earned | Fighting | AlwaysOn,

            // Standing: three bytes of nothing, level, ability level, the two maxima, the five attributes,
            // a flag and a count for unspent points, then carried and carryable weight, then four of nothing.
            0x01, 0x00, 0x00,
            0x07, 0x02,
            0x00, 0x00, 0x00, 0x96,
            0x00, 0x00, 0x00, 0xC8,
            10, 5, 6, 7, 8,
            0x01, 0x04,
            0x00, 0x2C, 0x00, 0x0B,
            0x00, 0x00, 0x00, 0x00,

            // Remaining: health, then mana.
            0x00, 0x00, 0x00, 0x3C,
            0x00, 0x00, 0x00, 0x1E,

            // Earned: experience, what is left of this level, the same two for abilities, points, gold.
            0x00, 0x00, 0x02, 0x58,
            0x00, 0x00, 0x01, 0x2C,
            0x00, 0x00, 0x00, 0x0A,
            0x00, 0x00, 0x00, 0x14,
            0x00, 0x00, 0x00, 0x05,
            0x00, 0x00, 0x02, 0xAA,

            // Fighting: four of nothing, blindness, one of nothing, the two elements, magic resistance,
            // one of nothing, armour, damage, hit.
            0x00, 0x00, 0x00, 0x00,
            0x01,
            0x00,
            (byte)Element.Fire, (byte)Element.Water,
            0x03,
            0x00,
            0x64, 0x0C, 0x09
        ]);

        Assert.Equal(7, me.Level);
        Assert.Equal(2, me.AbilityLevel);
        Assert.Equal(60, me.Health);
        Assert.Equal(150, me.MaximumHealth);
        Assert.Equal(30, me.Mana);
        Assert.Equal(200, me.MaximumMana);
        Assert.Equal(10, me.Str);
        Assert.Equal(5, me.Int);
        Assert.Equal(6, me.Wis);
        Assert.Equal(7, me.Con);
        Assert.Equal(8, me.Dex);
        Assert.Equal(4, me.Unspent);
        Assert.Equal(44, me.MaximumWeight);
        Assert.Equal(11, me.Weight);
        Assert.Equal(600, me.Experience);
        Assert.Equal(300, me.ExperienceToGo);
        Assert.Equal(10, me.AbilityExperience);
        Assert.Equal(20, me.AbilityExperienceToGo);
        Assert.Equal(5, me.GamePoints);
        Assert.Equal(682, me.Gold);
        Assert.True(me.Blind);
        Assert.Equal(Element.Fire, me.Offense);
        Assert.Equal(Element.Water, me.Defense);
        Assert.Equal(3, me.MagicResistance);
        Assert.Equal(100, me.Armor);
        Assert.Equal(12, me.Damage);
        Assert.Equal(9, me.Hit);
    }

    [Fact]
    public void A_piece_the_server_left_out_keeps_what_it_last_said()
    {
        Vitals before = Vitals.Unknown with { Level = 7, Str = 10, MaximumHealth = 150, Health = 60, Armor = 100 };

        // What arrives every time anything is struck: health and mana, and nothing else.
        Vitals after = WorldClient.ReadVitals(
            [Remaining | AlwaysOn, 0x00, 0x00, 0x00, 0x32, 0x00, 0x00, 0x00, 0x1E],
            before);

        Assert.Equal(50, after.Health);
        Assert.Equal(30, after.Mana);

        // 보내지 않은 것은 그대로 있어야 한다 — 안 그러면 한 대 맞을 때마다 힘이 0 이 된다.
        Assert.Equal(7, after.Level);
        Assert.Equal(10, after.Str);
        Assert.Equal(150, after.MaximumHealth);
        Assert.Equal(100, after.Armor);
    }

    [Fact]
    public void Armour_worth_having_is_below_zero()
    {
        // The server writes it as a signed byte, because gear takes it down and past zero.
        Vitals me = WorldClient.ReadVitals([
            Fighting | AlwaysOn,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            (byte)Element.None, (byte)Element.None,
            0x00, 0x00,
            0xBA, 0x00, 0x00
        ]);

        Assert.Equal(-70, me.Armor);
    }

    [Fact]
    public void A_piece_that_is_not_all_there_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadVitals([]));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadVitals([Standing | AlwaysOn, 0x01, 0x00, 0x00]));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadVitals([Remaining | AlwaysOn, 0x00, 0x00, 0x00]));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadVitals([Fighting | AlwaysOn, 0x00, 0x00]));
    }
}
