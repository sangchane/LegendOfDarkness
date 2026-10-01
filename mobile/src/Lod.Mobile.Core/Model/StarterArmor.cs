namespace Lod.Mobile.Core.Model;

/// <summary>
/// 직업마다 레벨 1 에 입는 갑옷 그림 번호(5.99) — 캐릭터 만들기 미리보기가 입힌다. 직업 번호는 Hades <c>Class</c>
/// (1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 무도가).
/// </summary>
public static class StarterArmor
{
    /// <summary>5.99 level-one class armour image: warrior 2, rogue 4, wizard 6, priest 5, monk 3. 직업을 아직 안 골랐으면 0(맨몸).</summary>
    public static int For(byte? path) => path switch
    {
        1 => 2,
        2 => 4,
        3 => 6,
        4 => 5,
        5 => 3,
        _ => 0
    };
}
