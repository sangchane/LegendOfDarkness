using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.World;

/// <summary>
/// Remembers which body motion our own plain blow is drawn with, so the attack button can draw it before the
/// server answers.
/// </summary>
/// <remarks>
/// The server picks it from what is worn (the weapon's attack motion, else the armour's — a monk's robe punches,
/// 132) and says so in the 0x1A that answers the swing. Spells and skills arrive through the same 0x1A, so only
/// the first motion shortly after the attack button counts: learning from any motion made a priest's attack
/// button draw the casting pose (128) after one spell. A plain answer (1) forgets the old motion, so changing a
/// two-handed weapon for a sword stops the two-handed swing.
/// <para>
/// A swing the server refuses (pressed again inside its 500ms) gets no answer at all, so the next motion of ours may
/// belong to something else. Two rules keep that out (사용자 2026-09-25, 「무도가 평타가 가끔 공통 휘두르기로 나간다」):
/// a motion between 2 and 127 is never a blow — the server's blow is 1 or an item's attack motion — so 쿠로토's
/// hands up (6) no longer wipes the punch; and a skill or spell used after the swing ends the wait, so 붕각's kick
/// (133) is not taken for the punch either.
/// </para>
/// <para>
/// Until the first answer nothing is known, and guessing the plain swing is exactly what showed a monk the common
/// blow on the first tap after every login. So the first swing is not drawn ahead; the answer draws it
/// (<see cref="Heard" /> says when).
/// </para>
/// </remarks>
public sealed class OwnBlow
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private TimeSpan? _swungAt;
    private bool _drawnAhead;

    /// <summary>The class motion number (128 and up) the blow is drawn with, or none for the plain swing.</summary>
    public int? Number { get; private set; }

    /// <summary>The speed the server gave with it.</summary>
    public int Speed { get; private set; } = 20;

    /// <summary>Whether the server has answered a swing yet, so <see cref="Number" /> can be drawn ahead of it.</summary>
    public bool Learned { get; private set; }

    /// <summary>The attack button was pressed. True when the blow should be drawn now, ahead of the answer.</summary>
    public bool Swung(TimeSpan now)
    {
        _swungAt = now;
        _drawnAhead = Learned;
        return _drawnAhead;
    }

    /// <summary>A skill or spell was used: whatever motion comes next is its, not the blow's.</summary>
    public void Other() => _swungAt = null;

    /// <summary>
    /// The server named a motion for us. True when it is the answer to a swing that was not drawn ahead, so the caller
    /// draws it now.
    /// </summary>
    public bool Heard(int number, int speed, TimeSpan now)
    {
        if (_swungAt is not { } swung || now - swung > Window)
        {
            return false;
        }

        if (number > 1 && number < BodyMotion.FirstSkill)
        {
            return false;
        }

        _swungAt = null;
        Number = number >= BodyMotion.FirstSkill ? number : null;
        Speed = speed;
        Learned = true;
        return !_drawnAhead;
    }
}
