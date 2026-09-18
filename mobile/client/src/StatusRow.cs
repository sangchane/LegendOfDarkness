using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The little badges under somebody's health bar saying what is on them — a curse, poison, sleep. The original
/// sends a picture number and a grade for how much time is left (<c>0x3A</c>), and until those pictures are cut
/// out of the archive each one is drawn as a shape of its own.
/// </summary>
/// <remarks>
/// Shapes, not only colours. Somebody who cannot tell red from green still reads a triangle from a circle, and
/// that is what the accessibility guidance every console maker publishes asks for. Badges are drawn rather than
/// built out of nodes, for the same reason the bar above them is.
/// </remarks>
public sealed partial class StatusRow : Node2D
{
    /// <summary>How big one badge is, and how far apart they sit.</summary>
    private const int Side = 9;

    private const int Gap = 2;

    /// <summary>How many fit before the rest are summed up. Five is as many as a tile's width allows.</summary>
    private const int Most = 5;

    private static readonly Color Edge = new(0f, 0f, 0f, 0.85f);
    private static readonly Color Fading = new(0.95f, 0.85f, 0.35f);

    /// <summary>What is on us now, the ones running out soonest first.</summary>
    private readonly List<Ailment> _showing = [];

    private double _blink;

    /// <summary>
    /// Says what is on somebody now. The ones running out soonest come first, so the badge a player must act on
    /// is never the one pushed off the end — which is the complaint every game with a capped status row collects.
    /// </summary>
    public void Show(IEnumerable<Ailment> ailments)
    {
        List<Ailment> sorted = [.. ailments.OrderBy(one => one.Left).ThenBy(one => one.Icon)];

        if (sorted.Count == _showing.Count && !sorted.Where((one, at) => one != _showing[at]).Any())
        {
            return;
        }

        _showing.Clear();
        _showing.AddRange(sorted);
        Visible = _showing.Count > 0;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        // 곧 풀리는 것은 깜빡인다 — 남은 시간을 색으로만 말하지 않기 위한 두 번째 신호다.
        _blink += delta;

        if (_blink > 0.2)
        {
            _blink = 0;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        int shown = Mathf.Min(_showing.Count, Most);
        int badges = _showing.Count > Most ? shown + 1 : shown;
        float left = -(badges * (Side + Gap) - Gap) / 2f;

        for (int at = 0; at < shown; at++)
        {
            Ailment one = _showing[at];
            Rect2 box = new(left + at * (Side + Gap), 0, Side, Side);

            // 1등급(10초 미만)은 깜빡인다.
            if (one.Left == 1 && Time.GetTicksMsec() / 300 % 2 == 1)
            {
                continue;
            }

            DrawRect(box.Grow(1), Edge);
            Badge(box, one.Icon);
        }

        if (_showing.Count > Most)
        {
            Rect2 rest = new(left + shown * (Side + Gap), 0, Side, Side);

            DrawRect(rest.Grow(1), Edge);
            DrawRect(rest, Fading);
            DrawRect(new Rect2(rest.Position + new Vector2(2, 4), new Vector2(Side - 4, 1)), Edge);
        }
    }

    /// <summary>
    /// One badge. Its shape comes from the picture number, so two different things never look the same even when
    /// their colours are close — the original numbers its statuses and that numbering is kept.
    /// </summary>
    private void Badge(Rect2 box, int icon)
    {
        (Color paint, int shape) = Look(icon);

        switch (shape)
        {
            case 0:
                DrawRect(box, paint);
                break;

            case 1:
                DrawCircle(box.Position + box.Size / 2, Side / 2f, paint);
                break;

            case 2:
                DrawColoredPolygon(
                    [
                        box.Position + new Vector2(Side / 2f, 0),
                        box.Position + new Vector2(Side, Side),
                        box.Position + new Vector2(0, Side)
                    ],
                    paint);
                break;

            default:
                DrawColoredPolygon(
                    [
                        box.Position + new Vector2(Side / 2f, 0),
                        box.Position + new Vector2(Side, Side / 2f),
                        box.Position + new Vector2(Side / 2f, Side),
                        box.Position + new Vector2(0, Side / 2f)
                    ],
                    paint);
                break;
        }
    }

    /// <summary>
    /// What a picture number looks like. The known ones are named; anything else is given a shape and a colour
    /// out of its own number, so that it is at least told apart from its neighbours.
    /// </summary>
    private static (Color Paint, int Shape) Look(int icon) => icon switch
    {
        // 저주 — Pack599.Curse 가 쓰는 번호.
        82 => (new Color(0.78f, 0.30f, 0.82f), 2),
        _ => (Color.FromHsv(icon % 12 / 12f, 0.55f, 0.95f), icon % 4)
    };
}
