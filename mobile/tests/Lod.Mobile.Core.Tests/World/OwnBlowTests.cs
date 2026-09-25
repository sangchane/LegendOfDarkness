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

    [Fact]
    public void Hands_up_after_a_refused_swing_does_not_undo_the_punch()
    {
        OwnBlow blow = new();

        blow.Swung(Pressed);
        blow.Heard(132, 20, Pressed + TimeSpan.FromMilliseconds(100));

        // 500ms 안에 다시 누른 평타는 서버가 답하지 않는다. 그 뒤 쿠로토의 손 들기(6)가 온다.
        blow.Swung(Pressed + TimeSpan.FromMilliseconds(300));
        blow.Heard(6, 90, Pressed + TimeSpan.FromMilliseconds(800));

        Assert.Equal(132, blow.Number);
    }

    [Fact]
    public void A_skill_used_after_a_refused_swing_is_not_taken_for_the_punch()
    {
        OwnBlow blow = new();

        blow.Swung(Pressed);
        blow.Heard(132, 20, Pressed + TimeSpan.FromMilliseconds(100));

        blow.Swung(Pressed + TimeSpan.FromMilliseconds(300));
        blow.Other();
        blow.Heard(133, 30, Pressed + TimeSpan.FromMilliseconds(500));

        Assert.Equal(132, blow.Number);
    }

    [Fact]
    public void The_first_swing_is_not_guessed_but_drawn_from_the_answer()
    {
        OwnBlow blow = new();

        // 아직 아무것도 모른다 — 공통 휘두르기를 미리 그리면 무도가가 로그인 뒤 첫 평타를 휘두른다.
        Assert.False(blow.Swung(Pressed));
        Assert.True(blow.Heard(132, 20, Pressed + TimeSpan.FromMilliseconds(100)));

        // 그다음부터는 미리 그리고, 답은 다시 그리지 않는다.
        Assert.True(blow.Swung(Pressed + TimeSpan.FromSeconds(1)));
        Assert.False(blow.Heard(132, 20, Pressed + TimeSpan.FromSeconds(1.1)));
    }
}
