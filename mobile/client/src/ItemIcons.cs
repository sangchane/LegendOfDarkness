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
/// no file receives the shared grey tile so every screen shows the same placeholder.
/// </remarks>
public static class ItemIcons
{
    private const string Folder = "res://assets/item/";

    private static readonly Dictionary<int, Texture2D?> Known = [];

    private static Texture2D? _missing;
    private static Texture2D Missing
    {
        get
        {
            if (_missing is not null) return _missing;
            Image tile = Image.CreateEmpty(24, 24, false, Image.Format.Rgba8);
            tile.Fill(Greybox.Muted with { A = 0.5f });
            _missing = ImageTexture.CreateFromImage(tile);
            return _missing;
        }
    }

    /// <summary>The picture for an item number, or nothing when none has been cut — where a grey tile would stand out.</summary>
    public static Texture2D? Found(int number) => ResourceLoader.Exists($"{Folder}{number}.png") ? For(number) : null;

    /// <summary>The picture for an item number, or a grey tile when none has been cut.</summary>
    public static Texture2D? For(int number)
    {
        if (Known.TryGetValue(number, out Texture2D? found))
        {
            return found;
        }

        string path = $"{Folder}{number}.png";
        found = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : Missing;
        Known[number] = found;

        return found;
    }
}
