using System.Collections.Generic;
using Godot;

namespace LodClient;

/// <summary>
/// Says whether the screen still fits at the size the run asked for.
/// </summary>
/// <remarks>
/// A layout that overflows does it quietly: the row simply walks off the bottom of the screen and nobody
/// notices until a screenshot looks wrong. That happened when the pack panel was given a minimum height —
/// it pushed the status bar and the movement pad out of the window entirely. So this walks the parts that
/// have to stay reachable and names the first one that does not.
///
/// <c>godot --path mobile/client -- --screen game --size 360x780 --orient portrait --layout</c>
/// </remarks>
public static class LayoutCheck
{
    private const string Flag = "--layout";

    public static bool Requested() => System.Array.IndexOf(OS.GetCmdlineUserArgs(), Flag) >= 0;

    public static void RunIfRequested(Node host, GameScreen screen)
    {
        if (Requested())
        {
            _ = ReportAfterLayout(host, screen);
        }
    }

    private static async System.Threading.Tasks.Task ReportAfterLayout(Node host, GameScreen screen)
    {
        // Containers settle over a couple of frames; asking before that reads sizes nobody will ever see.
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        Vector2 screenSize = host.GetViewport().GetVisibleRect().Size;
        List<string> wrong = [];

        GD.Print($"GREYBOX_LAYOUT size {screenSize.X}x{screenSize.Y}");

        foreach ((string name, Control part) in screen.Parts)
        {
            Rect2 where = part.GetGlobalRect();

            GD.Print(
                $"GREYBOX_LAYOUT {name} {where.Position.X:0},{where.Position.Y:0} "
                + $"{where.Size.X:0}x{where.Size.Y:0}{(part.Visible ? string.Empty : " (숨김)")}");

            if (!part.Visible)
            {
                continue;
            }

            if (where.Position.Y < -1 || where.End.Y > screenSize.Y + 1)
            {
                wrong.Add($"{name} 이(가) 화면 위아래를 벗어납니다");
            }

            if (where.Position.X < -1 || where.End.X > screenSize.X + 1)
            {
                wrong.Add($"{name} 이(가) 화면 좌우를 벗어납니다");
            }
        }

        wrong.AddRange(Overlaps(screen.Parts));

        foreach (string complaint in wrong)
        {
            GD.Print($"GREYBOX_LAYOUT_BAD {complaint}");
        }

        GD.Print(wrong.Count == 0 ? "GREYBOX_LAYOUT_OK" : $"GREYBOX_LAYOUT_BAD {wrong.Count}건");

        host.GetTree().Quit(wrong.Count == 0 ? 0 : 1);
    }

    /// <summary>
    /// The bars must not sit on top of each other. The world is left out — in landscape everything is meant
    /// to float over it, and the pack is meant to cover part of it.
    /// </summary>
    private static IEnumerable<string> Overlaps(IReadOnlyList<(string Name, Control Part)> parts)
    {
        for (int first = 0; first < parts.Count; first++)
        {
            for (int second = first + 1; second < parts.Count; second++)
            {
                (string oneName, Control one) = parts[first];
                (string otherName, Control other) = parts[second];

                if (oneName == "월드" || otherName == "월드" || !one.Visible || !other.Visible)
                {
                    continue;
                }

                if (one.GetGlobalRect().Intersects(other.GetGlobalRect()))
                {
                    yield return $"{oneName} 과(와) {otherName} 이(가) 겹칩니다";
                }
            }
        }
    }
}
