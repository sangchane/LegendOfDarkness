namespace Lod.Mobile.Core.World;

/// <summary>
/// Telling the map's music from a sound effect. The server sends both as 0x19 and the number itself says which:
/// under 128 it is an effect from the original archive, 128 and over is a song (number − 128), and 228 stops the
/// music (원작 <c>Legend.exe 0x54c440</c>).
/// </summary>
public static class Music
{
    /// <summary>The number at which music begins.</summary>
    public const int First = 128;

    /// <summary>What the server sends to stop the music — a song number nobody plays.</summary>
    public const int Silence = 100;

    /// <summary>The song this number asks for, or nothing when it is a sound effect.</summary>
    public static int? Song(int number) => number >= First ? number - First : null;

    /// <summary>The sound effect this number asks for, or nothing when it is music.</summary>
    public static int? Effect(int number) => number < First ? number : null;
}
