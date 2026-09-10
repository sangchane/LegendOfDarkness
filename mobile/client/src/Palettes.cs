using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// Puts the colour the server asked for into a wardrobe sheet.
/// </summary>
/// <remarks>
/// The original recolours a hat without drawing it twice: six entries of the palette it was drawn with are
/// overwritten. Our sheets are cut with those six left as a marker colour, so here that means finding those
/// exact colours and painting the wanted ones over them. Both lists are written by
/// scripts/build-client-assets.ps1 — the markers by the extractor itself, so nobody has to be told them
/// twice, and the colours straight out of Legend.dat.
///
/// Done once per piece and colour and kept, because a street of people wearing the same boots should cost
/// one picture, not one each.
/// </remarks>
public static class Palettes
{
    private const string MarkerList = "res://assets/actor/parts/dye-slots.txt";
    private const string ColourList = "res://assets/actor/parts/dye-colours.txt";

    private static readonly Dictionary<(string Path, int Colour), Texture2D> Dressed = [];

    private static IReadOnlyList<Colour>? _markers;
    private static IReadOnlyDictionary<int, IReadOnlyList<Colour>>? _colours;

    /// <summary>The sheet in the wanted colour, or the sheet as it was cut if it cannot be dyed.</summary>
    public static Texture2D Load(string path, int colour)
    {
        if (Dressed.TryGetValue((path, colour), out Texture2D? already))
        {
            return already;
        }

        Texture2D sheet = GD.Load<Texture2D>(path);
        Texture2D dyed = Recolour(sheet, colour) ?? sheet;

        Dressed[(path, colour)] = dyed;

        return dyed;
    }

    private static Texture2D? Recolour(Texture2D sheet, int colour)
    {
        IReadOnlyList<Colour> markers = Markers();

        if (markers.Count == 0 || !Colours().TryGetValue(colour, out IReadOnlyList<Colour>? wanted))
        {
            return null;
        }

        Image image = sheet.GetImage();
        image.Convert(Image.Format.Rgba8);

        bool painted = false;

        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);

                if (pixel.A8 == 0)
                {
                    continue;
                }

                for (int slot = 0; slot < markers.Count && slot < wanted.Count; slot++)
                {
                    if (pixel.R8 != markers[slot].R || pixel.G8 != markers[slot].G || pixel.B8 != markers[slot].B)
                    {
                        continue;
                    }

                    image.SetPixel(x, y, Color.Color8(wanted[slot].R, wanted[slot].G, wanted[slot].B));
                    painted = true;
                    break;
                }
            }
        }

        return painted ? ImageTexture.CreateFromImage(image) : null;
    }

    private static IReadOnlyList<Colour> Markers() => _markers ??= DyeTable.ReadColours(Read(MarkerList));

    private static IReadOnlyDictionary<int, IReadOnlyList<Colour>> Colours() =>
        _colours ??= DyeTable.Read(Read(ColourList));

    private static string Read(string path) =>
        Godot.FileAccess.FileExists(path) ? Godot.FileAccess.GetFileAsString(path) : string.Empty;
}
