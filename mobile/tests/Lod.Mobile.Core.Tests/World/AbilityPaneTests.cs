using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>The two ability-pane records Hades sends as a character enters the world.</summary>
public sealed class AbilityPaneTests
{
    [Fact]
    public void A_learned_skill_keeps_its_slot_icon_and_name()
    {
        LearnedSkill skill = WorldClient.ReadSkill([
            0x01,
            0x00, 0x01,
            0x06, (byte)'A', (byte)'s', (byte)'s', (byte)'a', (byte)'i', (byte)'l'
        ]);

        Assert.Equal(1, skill.Slot);
        Assert.Equal(1, skill.Icon);
        Assert.Equal("Assail", skill.Name);
    }

    [Fact]
    public void A_learned_spell_keeps_target_prompt_and_chant_lines()
    {
        LearnedSpell spell = WorldClient.ReadSpell([
            0x03,
            0x00, 0x15,
            (byte)SpellTargetType.ChooseTarget,
            0x08, (byte)'b', (byte)'e', (byte)'a', (byte)'g', (byte)' ', (byte)'i', (byte)'o', (byte)'c',
            0x06, (byte)'T', (byte)'a', (byte)'r', (byte)'g', (byte)'e', (byte)'t',
            0x02
        ]);

        Assert.Equal(3, spell.Slot);
        Assert.Equal(21, spell.Icon);
        Assert.Equal(SpellTargetType.ChooseTarget, spell.TargetType);
        Assert.Equal("beag ioc", spell.Name);
        Assert.Equal("Target", spell.Prompt);
        Assert.Equal(2, spell.Lines);
    }

    [Fact]
    public void Truncated_ability_rows_are_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadSkill(new byte[] { 1, 0, 1 }));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadSpell(new byte[] { 1, 0, 1, 2 }));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadSpell(new byte[] { 1, 0, 1, 2, 0 }));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadAbilitySlot([]));
    }

    [Fact]
    public void Removing_an_ability_names_the_server_owned_slot()
    {
        Assert.Equal(37, WorldClient.ReadAbilitySlot([37]));
    }
}
