namespace Lod.Mobile.Core.World;

/// <summary>
/// Who a pressed spell goes to. The original client let a target spell be aimed at anyone, our own feet included; a
/// phone has no pointer, so a target spell with nobody picked goes to ourselves — a heal or a buff is what one casts
/// with nobody picked (2026-09-26: 호르라마 on oneself was refused with "마법 대상을 먼저 누르세요").
/// </summary>
public static class SpellAim
{
    /// <summary>The serial to send with the cast (0x0F) — 0 for a spell that takes none.</summary>
    public static uint Target(SpellTargetType type, uint picked, uint self) =>
        type == SpellTargetType.ChooseTarget ? (picked != 0 ? picked : self) : 0;
}
