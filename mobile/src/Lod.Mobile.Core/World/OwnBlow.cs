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
/// </remarks>
public sealed class OwnBlow
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private TimeSpan? _swungAt;

    /// <summary>The class motion number (128 and up) the blow is drawn with, or none for the plain swing.</summary>
    public int? Number { get; private set; }

    /// <summary>The speed the server gave with it.</summary>
    public int Speed { get; private set; } = 20;

    /// <summary>The attack button was pressed.</summary>
    public void Swung(TimeSpan now) => _swungAt = now;

    /// <summary>The server named a motion for us.</summary>
    public void Heard(int number, int speed, TimeSpan now)
    {
        if (_swungAt is not { } swung || now - swung > Window)
        {
            return;
        }

        _swungAt = null;
        Number = number >= BodyMotion.FirstSkill ? number : null;
        Speed = speed;
    }
}
