using Godot;

namespace LodClient;

/// <summary>
/// Saves one frame and quits when the run asks for it, so a greybox can be reviewed without a device or an
/// editor. Pass it after a double dash: <c>godot --path . -- --shot out.png</c>.
/// </summary>
public static class Screenshot
{
    private const string Flag = "--shot";

    public static void CaptureIfRequested(Node host)
    {
        string? path = PathFromCommandLine();

        if (path is null)
        {
            return;
        }

        _ = SaveAfterFirstFrames(host, path);
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

    private static async System.Threading.Tasks.Task SaveAfterFirstFrames(Node host, string path)
    {
        // Two frames: the first builds the tree, the second has it drawn.
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        Image image = host.GetViewport().GetTexture().GetImage();
        Error saved = image.SavePng(path);

        GD.Print(saved == Error.Ok ? $"GREYBOX_SHOT_OK {path}" : $"GREYBOX_SHOT_FAILED {saved}");

        host.GetTree().Quit(saved == Error.Ok ? 0 : 1);
    }
}
