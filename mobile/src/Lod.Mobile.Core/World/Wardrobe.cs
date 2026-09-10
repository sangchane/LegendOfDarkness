namespace Lod.Mobile.Core.World;

/// <summary>
/// Which drawings make up a person. The server sends numbers, not file names; the name is the gender
/// letter, the part letter, and the number padded to three digits — docs/original-sprite-animation.md
/// section 7 has the letters and where each one was confirmed.
/// </summary>
public static class Wardrobe
{
    // The body byte holds the kind of body in its top half and the trousers in its bottom half.
    private const int Kind = 0xF0;
    private const int Trousers = 0x0F;

    // BodySprite on the server: 2 woman, 4 her ghost, 6 her unseen, 9 her head alone, 11 her blank.
    private static readonly int[] Women = [2, 4, 6, 9, 11];

    /// <summary>
    /// The pieces to draw, furthest back first. Anything the server left at zero is not worn and so is not
    /// in the list; a caller with no picture for a piece should skip that piece rather than the person.
    /// </summary>
    /// <remarks>
    /// The weapon is left out on purpose. Its drawings do not sit on the same spot as the rest — a sword
    /// lands well to the right of the body — and nobody on this server carries one yet, so there is
    /// nothing to check a guess against.
    /// </remarks>
    public static IReadOnlyList<string> Pieces(Appearance worn)
    {
        char gender = Women.Contains((worn.Body & Kind) >> 4) ? 'w' : 'm';
        List<string> pieces = [];

        Add('s', worn.Shield);

        // The body is always the same drawing; which archive it comes out of is what the gender decides.
        pieces.Add($"{gender}b001");

        Add('n', worn.Body & Trousers);
        Add('l', worn.Boots);
        Add('u', worn.Armor);
        Add('i', worn.OverCoat);
        Add('h', worn.Head);

        return pieces;

        void Add(char part, int number)
        {
            if (number > 0)
            {
                pieces.Add($"{gender}{part}{number:000}");
            }
        }
    }
}
