using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The little badges over somebody's health bar saying what is on them — a curse, poison, sleep. The original
/// sends a picture number and a grade for how much time is left (<c>0x3A</c>), and draws each as it does here: the
/// picture, and beside it a thin bar as tall as the time left, in that grade's colour.
/// </summary>
/// <remarks>
/// <para>
/// Evidence, 5.99/2005 Legend.exe: <c>SSpelled</c> (0x3A) fills the own status bar <c>SpelledViewPane</c>, whose
/// draw at <c>0x605ab0</c> loads each picture as kind 2 (<c>0x4e9980</c> → <c>spell%03d.epf</c>, frame = number %
/// 266) — the same sheet the spell pane uses, so <c>assets/ability/spell.png</c> is reused — and fills a 2-pixel bar
/// right of it from y 4 to <c>grade × 2 + 6</c>, coloured by palette entry 88 · 137 · 69 · 151 · 40 · 255 for
/// grades 1…6 (<c>legend.pal</c>: light blue · green · yellow · orange · red · entry 255).
/// </para>
/// <para>
/// A number outside the sheet keeps a shape of its own, so it is still told apart. Badges are drawn rather than
/// built out of nodes, for the same reason the bar above them is.
/// </para>
/// </remarks>
public sealed partial class StatusRow : Node2D
{
    /// <summary>How big one picture is, the time bar beside it, and how far apart they sit.</summary>
    private const int Side = 10;

    /// <summary>How tall the row is with the badges' edges, so whoever places it can keep it clear of the bar.</summary>
    public const int Height = Side + 2;

    private const int Bar = 2;

    private const int Gap = 2;

    /// <summary>One badge's width: the picture, a pixel, the bar.</summary>
    private const int Slot = Side + 1 + Bar;

    /// <summary>The spell sheet: 266 pictures, sixteen to a row of 35-pixel cells (<c>AbilityBar</c>).</summary>
    private const int Pictures = 266;

    private const int Cell = 35;

    private const int Columns = 16;

    private static Texture2D? _sheet;

    /// <summary>
    /// The time bar's colour for grades 1…6 — <c>legend.pal</c> entries 88 · 137 · 69 · 151 · 40 · 255, which
    /// <c>SpelledViewPane</c> (<c>0x605b8e</c>) picks by grade.
    /// </summary>
    private static readonly Color[] Grades =
    [
        Color.Color8(127, 167, 243),
        Color.Color8(0, 99, 0),
        Color.Color8(255, 231, 59),
        Color.Color8(243, 143, 27),
        Color.Color8(203, 0, 23),
        Color.Color8(27, 127, 127)
    ];

    private static readonly Color Edge = new(0f, 0f, 0f, 0.85f);
    private static readonly Color Fading = new(0.95f, 0.85f, 0.35f);

    /// <summary>The size of the "+N" figures — as tall as a badge.</summary>
    private const int RestSize = 10;

    /// <summary>What is drawn now, the ones running out soonest first, and how many more there are ("+N").</summary>
    private readonly List<Ailment> _showing = [];

    private int _more;

    private double _blink;

    /// <summary>
    /// Says what is on somebody now. The ones running out soonest come first, so the badge a player must act on
    /// is never the one pushed off the end — which is the complaint every game with a capped status row collects.
    /// In a coma none are drawn: the coma owns the head (<see cref="Overhead.Badges" />).
    /// </summary>
    public void Show(IEnumerable<Ailment> ailments)
    {
        (IReadOnlyList<Ailment> shown, int more) = Overhead.Badges(ailments);

        if (more == _more && shown.Count == _showing.Count && !shown.Where((one, at) => one != _showing[at]).Any())
        {
            return;
        }

        _showing.Clear();
        _showing.AddRange(shown);
        _more = more;
        Visible = _showing.Count > 0;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        // 몸이 좌우로 뒤집혀도(Actor.Face) 그림과 막대는 뒤집히지 않는다 — 부모의 뒤집기를 되돌린다.
        if (GetParent() is Node2D body)
        {
            Scale = new Vector2(body.Scale.X < 0 ? -1 : 1, 1);
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
        int shown = _showing.Count;
        string rest = $"+{_more}";
        Font font = ThemeDB.FallbackFont;
        float restWide = _more > 0 ? font.GetStringSize(rest, HorizontalAlignment.Left, -1, RestSize).X + 2 : 0;
        float wide = shown * (Slot + Gap) - Gap + (_more > 0 ? Gap + restWide : 0);
        float left = Mathf.Round(-wide / 2f);

        for (int at = 0; at < shown; at++)
        {
            Ailment one = _showing[at];
            Rect2 box = new(left + at * (Slot + Gap), 1, Side, Side);

            // 1등급(10초 미만)은 깜빡인다.
            if (one.Left == 1 && Time.GetTicksMsec() / 300 % 2 == 1)
            {
                continue;
            }

            DrawRect(box.Grow(1), Edge);

            if (!Picture(box, one.Icon))
            {
                Badge(box, one.Icon);
            }

            // 원작처럼 그림 오른쪽에 남은 시간만큼 선 막대 — 등급 × 2 + 2 칸(6등급이면 그림 높이).
            if (one.Left is >= 1 and <= 6)
            {
                float tall = Side * (one.Left * 2 + 2) / 14f;
                Rect2 bar = new(box.End.X + 1, box.Position.Y, Bar, tall);

                DrawRect(bar.Grow(0.5f), Edge);
                DrawRect(bar, Grades[one.Left - 1]);
            }
        }

        // 다섯을 넘으면 나머지는 「+N」 한 칸으로 — 가장 늦게 풀리는 것들이다.
        if (_more > 0)
        {
            Rect2 box = new(left + shown * (Slot + Gap), 1, restWide, Side);

            DrawRect(box.Grow(1), Edge);
            DrawRect(box, Fading);
            DrawString(font, new Vector2(box.Position.X + 1, box.End.Y - 1), rest, HorizontalAlignment.Left, -1, RestSize, Edge);
        }
    }

    /// <summary>The original picture for this number, or false when the sheet has none.</summary>
    private bool Picture(Rect2 box, int icon)
    {
        if (icon is < 0 or >= Pictures)
        {
            return false;
        }

        _sheet ??= ResourceLoader.Exists(AbilityBar.SpellSheet) ? GD.Load<Texture2D>(AbilityBar.SpellSheet) : null;

        if (_sheet is null)
        {
            return false;
        }

        DrawTextureRectRegion(_sheet, box, new Rect2(icon % Columns * Cell, icon / Columns * Cell, Cell, Cell));
        return true;
    }

    /// <summary>
    /// A badge for a number the sheet does not have. Its shape comes from the picture number, so two different things never look the same even when
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
