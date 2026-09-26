namespace Lod.Mobile.Core.World;

/// <summary>
/// Which drawings make up a person. The server sends numbers, not file names; the name is the gender
/// letter, the part letter, and the number padded to three digits — docs/original-sprite-animation.md
/// section 7 has the letters and where each one was confirmed.
/// </summary>
/// <summary>
/// One drawing and the colour to dye it. Only the head, the boots and the trousers are dyed — the
/// server sends a colour for those and for nothing else, and the reference client dyes the same three.
/// </summary>
public sealed record Piece(string Name, int Colour);

public static class Wardrobe
{
    // The body byte holds the kind of body in its top half and the trousers' colour in its bottom half.
    private const int Kind = 0xF0;
    private const int Trousers = 0x0F;

    // BodySprite on the server: 2 woman, 4 her ghost, 6 her unseen, 9 her head alone, 11 her blank.
    private static readonly int[] Women = [2, 4, 6, 9, 11];

    // BodySprite 3 남자 유령(0x30) · 4 여자 유령(0x40).
    private const int Ghost = 3;

    /// <summary>
    /// The pieces to draw, furthest back first. Anything the server left at zero is not worn and so is not
    /// in the list; a caller with no picture for a piece should skip that piece rather than the person.
    /// </summary>
    /// <remarks>
    /// The order is the one the reference client adds its pieces in
    /// (sources/FallenDev/dark-ages-ts/.../paper-doll-container.ts): the shield behind everything, then the
    /// body and what covers it, then the weapon, then the head.
    /// </remarks>
    public static IReadOnlyList<Piece> Pieces(Appearance worn)
    {
        char gender = Women.Contains((worn.Body & Kind) >> 4) ? 'w' : 'm';
        List<Piece> pieces = [];

        // 유령(3 남 · 4 여) — 5.99 아카이브의 몸 002 가 머리 위 고리와 날개를 단 유령이다(khan.dat mb00201 · khan2.dat
        // wb00201, 두 파일이 같다). 서버는 유령에게 옷을 보내지 않고(ServerFormat33) 원작도 유령 한 장만 그린다.
        if ((worn.Body & Kind) >> 4 is Ghost or Ghost + 1)
        {
            return [new Piece($"{gender}b002", 0)];
        }

        Add('s', worn.Shield);

        // The body is always the same drawing; which archive it comes out of is what the gender decides.
        pieces.Add(new Piece($"{gender}b001", 0));

        // 바지(mn001)는 몸 바이트 아래 반쪽이 0 이 아닐 때만, 그 반쪽 색으로 입는다 — 5.99 Legend.exe 0x54ffaa..0x55006b
        // (0 이면 바지 번호 0 → 그리지 않음, 0x4e85d1). 참고 클라이언트는 늘 입혀 0 일 때 보라 쫄바지가 됐다. 도복처럼
        // 윗도리만 있는 옷의 하의가 이 바지다. 여자 아카이브에는 바지 그림이 없다.
        if (gender == 'm' && (worn.Body & Trousers) != 0)
        {
            pieces.Add(new Piece("mn001", worn.Body & Trousers));
        }

        Add('l', worn.Boots, worn.BootColor);
        Add('u', worn.Armor);

        // The 5.99 client also asks for these, by the same numbers (Legend.exe 0x4e8514): the arms that go with
        // an armour, a front piece of a weapon, and a front and a back piece of a head. Most numbers have none,
        // and a piece with no drawing is simply left out by the caller.
        Add('a', worn.Armor);
        Add('i', worn.OverCoat);
        Add('w', worn.Weapon);
        Add('p', worn.Weapon);
        Add('h', worn.Head, worn.HairColor);
        Add('e', worn.Head, worn.HairColor);
        Add('f', worn.Head, worn.HairColor);
        Add('c', worn.HeadAccessory1);
        Add('c', worn.HeadAccessory2);

        return pieces;

        void Add(char part, int number, int colour = 0)
        {
            if (number > 0)
            {
                pieces.Add(new Piece($"{gender}{part}{number:000}", colour));
            }
        }
    }

    // Legend.exe 0x69c200: the order a figure is stacked in, bottom first, as part letters. Facing us (east, south)
    // the weapon is lowest so the body and arms cover the hand that holds it; from behind (north, west) it goes over
    // the body but under the arms and head. The shield is lowest from behind and near the top facing us. The table
    // has no overcoat — that client never draws one — so it sits with the armour it is worn over.
    private const string FromBehind = "sbnluidfwahepc";
    private const string FacingUs = "wfbnlhuidaepsc";

    /// <summary>Where a piece with this part letter goes in the stack, bottom first. Unknown letters go on top.</summary>
    public static int Rank(char part, Art.Side side)
    {
        int at = (side == Art.Side.Back ? FromBehind : FacingUs).IndexOf(char.ToLowerInvariant(part));
        return at < 0 ? int.MaxValue : at;
    }
}
