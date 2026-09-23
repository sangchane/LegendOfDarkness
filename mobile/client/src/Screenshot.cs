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

    private static double? QuitAfterFromCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--quit-after" && double.TryParse(args[index + 1], out double seconds))
            {
                return seconds;
            }
        }

        return null;
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

        // 창이 가려지면(화면 밖으로 내보낸 창, 다른 앱이 전체 화면일 때) macOS 의 Godot 는 그리기를 멈춘다 — 접속처럼
        // 오래 기다린 사진이 로그인 화면으로 남았다(2026-09-23). 찍기 직전에 한 번 그리게 한다.
        RenderingServer.ForceDraw(swapBuffers: false);

        Image image = host.GetViewport().GetTexture().GetImage();
        Error saved = image.SavePng(path);

        GD.Print(saved == Error.Ok ? $"GREYBOX_SHOT_OK {path}" : $"GREYBOX_SHOT_FAILED {saved}");

        // 두 클라이언트를 함께 찍을 때 — 먼저 찍은 쪽이 바로 나가면 상대 화면에서 그 사람이 사라진다(파티가 흩어진다).
        // --quit-after 초만큼(시작부터 센다) 더 머문다.
        if (QuitAfterFromCommandLine() is { } stay && stay > seconds)
        {
            await host.ToSignal(host.GetTree().CreateTimer(stay - seconds), SceneTreeTimer.SignalName.Timeout);
        }

        host.GetTree().Quit(saved == Error.Ok ? 0 : 1);
    }
}
