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

        _ = SaveAfterFirstFrames(host, path, SecondsFromCommandLine());
    }

    /// <summary>Whether this run is taking its own picture rather than being looked at.</summary>
    public static bool Requested() => PathFromCommandLine() is not null;

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
