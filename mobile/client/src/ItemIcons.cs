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

    /// <summary>The picture for one item number, or null when none has been cut.</summary>
    public static Texture2D? For(int number)
    {
        if (Known.TryGetValue(number, out Texture2D? found))
        {
            return found;
        }

        string path = $"{Folder}{number}.png";
        found = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        Known[number] = found;

        return found;
    }
}
