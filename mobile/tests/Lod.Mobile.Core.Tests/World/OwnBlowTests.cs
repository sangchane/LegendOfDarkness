using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

public sealed class OwnBlowTests
{
    private static readonly TimeSpan Pressed = TimeSpan.FromSeconds(10);

    [Fact]
    public void The_answer_to_our_own_swing_is_remembered()
    {
        OwnBlow blow = new();

        blow.Swung(Pressed);
        blow.Heard(132, 30, Pressed + TimeSpan.FromMilliseconds(200));

        Assert.Equal(132, blow.Number);
        Assert.Equal(30, blow.Speed);
    }

    [Fact]
    public void A_spell_cast_is_not_taken_for_the_plain_blow()
    {
        OwnBlow blow = new();

        // A priest casts: the server sends 128 (the casting pose) without the attack button being pressed.
        blow.Heard(128, 20, Pressed);

        Assert.Null(blow.Number);
    }

    [Fact]
    public void A_skill_long_after_the_swing_is_not_taken_for_the_plain_blow()
    {
        OwnBlow blow = new();

        blow.Swung(Pressed);
        blow.Heard(131, 20, Pressed + TimeSpan.FromSeconds(2));

        Assert.Null(blow.Number);
    }

    [Fact]
    public void Only_the_first_answer_after_a_swing_counts()
    {
        OwnBlow blow = new();

        blow.Swung(Pressed);
        blow.Heard(132, 20, Pressed + TimeSpan.FromMilliseconds(100));
        blow.Heard(133, 20, Pressed + TimeSpan.FromMilliseconds(300));

        Assert.Equal(132, blow.Number);
    }

    [Fact]
    public void A_plain_answer_after_changing_weapons_forgets_the_old_motion()
    {
        OwnBlow blow = new();

        blow.Swung(Pressed);
        blow.Heard(129, 20, Pressed);
        blow.Swung(Pressed + TimeSpan.FromSeconds(5));
        blow.Heard(1, 20, Pressed + TimeSpan.FromSeconds(5.1));

        Assert.Null(blow.Number);
    }
}
