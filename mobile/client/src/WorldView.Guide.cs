using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>월드 화면 — 길 안내(경로 찾기·따라 걷기·발자국).</summary>
public sealed partial class WorldView
{
    /// <summary>Where we are being walked to, by its name — empty for a bare tile — or null when we are not.</summary>
    public string? Guiding => _guide?.Label;

    /// <summary>The tiles still to walk, the goal last. Empty when not guiding.</summary>
    public IReadOnlyList<Tile> Route => _route;

    /// <summary>
    /// Walks us to one of these tiles, round the walls, a step at a time — what a tap on the 길 찾기 map asks for.
    /// False when there is no way there from here.
    /// </summary>
    public bool Guide(TabGoal goal)
    {
        if (TabMap.WayToAny(_tile, goal.Goals, Walled) is not { } way)
        {
            return false;
        }

        _guide = goal;
        _guideMap = MapId;
        _guideSteps = 0;
        _guideBudget = (3 * way.Count) + 20;
        _route = way;
        _trail.QueueRedraw();

        return true;
    }

    /// <summary>Stops walking to the goal — the pad was pressed, we got there, or the way closed.</summary>
    public void StopGuiding()
    {
        if (_guide is null)
        {
            return;
        }

        _guide = null;
        _route = [];
        _trail.QueueRedraw();
    }

    /// <summary>Whether a tile cannot be stood on, as the pathing sees it.</summary>
    public bool Blocked(Tile tile) => Walled(tile);

    /// <summary>
    /// Takes the next step to the goal once the last one has finished. The way is measured again every step from the
    /// tile we are on, so a step the server put back, or somebody standing in the lane, is walked round rather than
    /// into. Stops on arriving, when the map changes under us (we went through the exit), or when the way is gone.
    /// </summary>
    private void FollowGuide()
    {
        if (_guide is not { } goal || _walked >= 0 || Frozen || Comatose)
        {
            return;
        }

        if (MapId != _guideMap)
        {
            StopGuiding();
            return;
        }

        // 사람이 길목에 서 있으면 서버가 걸음마다 되돌린다. 처음 길의 세 배 넘게 걸었으면 그만둔다.
        IReadOnlyList<Tile>? way = TabMap.WayToAny(_tile, goal.Goals, Walled);

        if (way is not { Count: > 0 } || ++_guideSteps > _guideBudget)
        {
            StopGuiding();
            return;
        }

        _route = way;
        _trail.QueueRedraw();
        Walk(TabMap.StepOf(_tile, way[0]));
    }

    /// <summary>Dots on the floor along the way still to walk, and a ring on where it ends — so the way can be seen.</summary>
    private void DrawTrail()
    {
        if (_route.Count == 0)
        {
            return;
        }

        Color dot = Greybox.Accent with { A = 0.9f };

        for (int at = 0; at < _route.Count - 1; at++)
        {
            Vector2 middle = Ground(_route[at]) - new Vector2(0, 9);
            _trail.DrawCircle(middle, 6, Colors.Black with { A = 0.55f });
            _trail.DrawCircle(middle, 4.5f, dot);
        }

        Vector2 end = Ground(_route[^1]) - new Vector2(0, 9);
        _trail.DrawArc(end, 12, 0, Mathf.Tau, 32, Greybox.Accent, 3);
    }
}
