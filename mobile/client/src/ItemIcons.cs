using System;
using System.Collections.Generic;
using Godot;

namespace LodClient;

/// <summary>
/// Pictures for the things a character can carry. The server names an item by one number — the same
/// number whether it is lying on the floor or sitting in the pack — and that number is the file name,
/// so nothing here has to know what the item is.
/// </summary>
/// <remarks>
/// <c>build-client-assets.ps1</c> cuts one file per number the server's item templates use. A number with
/// no file is not an error: it means that item has not been cut yet, and the caller draws its placeholder.
/// </remarks>
public static class ItemIcons
{
    private const string Folder = "res://assets/item/";

    private static readonly Dictionary<int, Texture2D?> Known = [];
    private static Texture2D? _fallback;

    /// <summary>The picture for one item number. A small neutral tile keeps rare archive-only items visible.</summary>
    public static Texture2D? For(int number)
    {
        if (Known.TryGetValue(number, out Texture2D? found))
        {
            return found;
        }

        string path = $"{Folder}{number}.png";
        found = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : Fallback();
        Known[number] = found;

        return found;
    }

    private static Texture2D Fallback()
    {
        if (_fallback is not null)
        {
            return _fallback;
        }

        Image image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool edge = x < 2 || y < 2 || x >= 30 || y >= 30;
                bool slash = Math.Abs(x - y) <= 1 || Math.Abs(x + y - 31) <= 1;
                image.SetPixel(x, y, edge ? new Color("#d1a85a") : slash ? new Color("#6b5130") : new Color("#241d18"));
            }
        }

        _fallback = ImageTexture.CreateFromImage(image);
        return _fallback;
    }
}
