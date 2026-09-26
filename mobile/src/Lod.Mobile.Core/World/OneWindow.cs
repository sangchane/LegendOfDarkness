namespace Lod.Mobile.Core.World;

/// <summary>The big windows of the game screen. Only one of them is up at a time (<see cref="OneWindow" />).</summary>
public enum GameWindow
{
    Pack,
    Talk,
    Chat,
    WorldMap,
    Settings,
    TabMap,
    BotGear
}

/// <summary>
/// Which big window is open — at most one (사용자, 2026-09-26: 창을 하나 열면 먼저 열려 있던 것은 닫힌다). The screen asks
/// it before showing one and shuts whatever it names; it does not know how each window is shut (an NPC's talk and the
/// world map have to tell the server), so it only says which.
/// </summary>
public sealed class OneWindow
{
    public GameWindow? Current { get; private set; }

    /// <summary>Whether the open window lies over the world, so the floor takes no taps or steps.</summary>
    public bool Freezing => Current is { } open && Freezes(open);

    /// <summary>Makes <paramref name="window" /> the open one, and names the one that has to shut first — none when it was already open.</summary>
    public GameWindow? Open(GameWindow window)
    {
        GameWindow? before = Current;
        Current = window;

        return before == window ? null : before;
    }

    /// <summary>Forgets <paramref name="window" /> — only when it is the open one, so a late close cannot shut its successor.</summary>
    public void Shut(GameWindow window)
    {
        if (Current == window)
        {
            Current = null;
        }
    }

    public bool IsOpen(GameWindow window) => Current == window;

    /// <summary>
    /// The pack, an NPC's talk and the log lie over the world and a thumb aimed at them must not walk the character.
    /// The others leave the pad alive: settings and the bot's gear sit aside, the 길 찾기 map walks us while open, and
    /// the world map is held by the server anyway.
    /// </summary>
    public static bool Freezes(GameWindow window) => window is GameWindow.Pack or GameWindow.Talk or GameWindow.Chat;
}
