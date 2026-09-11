using Godot;

namespace LodClient;

/// <summary>
/// Saves one frame and quits when the run asks for it, so a greybox can be reviewed without a device or an
/// editor. Pass it after a double dash: <c>godot --path . -- --shot out.png</c>.
/// </summary>
public static class Screenshot
{
    private const string Flag = "--shot";
    private const string DelayFlag = "--shot-after";

    public static void CaptureIfRequested(Node host)
    {
        string? path = PathFromCommandLine();

        if (path is null)
        {
            return;
        }

        StayOutOfTheWay();

        _ = SaveAfterFirstFrames(host, path, SecondsFromCommandLine());
    }

    /// <summary>
    /// A run that takes its own picture is not for looking at. It still has to draw — a headless
    /// window renders nothing — so the window is moved off the screen and told not to take the
    /// keyboard, rather than popping up over whatever somebody is doing.
    /// </summary>
    private static void StayOutOfTheWay()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        DisplayServer.WindowSetPosition(new Vector2I(-4000, -4000));
    }

    private static string? PathFromCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == Flag)
            {
                return args[index + 1];
            }
        }

        return null;
    }

    /// <summary>Seconds to let the screen settle first, for shots that wait on something slower than a frame.</summary>
    private static double SecondsFromCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == DelayFlag && double.TryParse(args[index + 1], out double seconds))
            {
                return seconds;
            }
        }

        return 0;
    }

    private static async System.Threading.Tasks.Task SaveAfterFirstFrames(Node host, string path, double seconds)
    {
        // Two frames: the first builds the tree, the second has it drawn.
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        if (seconds > 0)
        {
            await host.ToSignal(host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }

        Image image = host.GetViewport().GetTexture().GetImage();
        Error saved = image.SavePng(path);

        GD.Print(saved == Error.Ok ? $"GREYBOX_SHOT_OK {path}" : $"GREYBOX_SHOT_FAILED {saved}");

        host.GetTree().Quit(saved == Error.Ok ? 0 : 1);
    }
}
