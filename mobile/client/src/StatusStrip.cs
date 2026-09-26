using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// A row of small status icons for a plate — mine (beside health and mana), the bot's slot, and a party member's later
/// (사용자, 2026-09-26: 버프·디버프 아이콘을 체력·마력 표시한 곳에). Each is the original picture from the spell sheet
/// (as the head badges, <see cref="StatusRow" />) with the original's thin time bar beside it in its grade's colour; a
/// harmful one gets a red edge. What to show comes from <see cref="StatusBadges" />.
/// </summary>
/// <remarks>Drawn, not built out of nodes; redraws only when what it shows changes, and to blink the last ten seconds.</remarks>
public sealed partial class StatusStrip : Control
{
    private const int Pictures = 266;
    private const int Cell = 35;
    private const int Columns = 16;
    private const int Bar = 2;
    private const int Gap = 3;

    private static readonly Color[] Grades =
    [
        Color.Color8(127, 167, 243), Color.Color8(0, 160, 0), Color.Color8(255, 231, 59),
        Color.Color8(243, 143, 27), Color.Color8(203, 0, 23), Color.Color8(27, 127, 127)
    ];

    private static readonly Color Edge = new(0f, 0f, 0f, 0.85f);
    private static readonly Color Harm = new("#a33f36");
    private static Texture2D? _sheet;

    private readonly int _side;
    private readonly int _most;
    private readonly bool _timed;
    private IReadOnlyList<StatusBadge> _shown = [];
    private double _blink;

    /// <param name="side">One picture's size.</param>
    /// <param name="most">How many fit; the rest are counted as "+N".</param>
    /// <param name="timed">
    /// Whether to show how long is left — the grade bar and the last-ten-seconds blink. Mine only; the bot's and a party
    /// member's show icons alone (사용자, 2026-09-26: 봇이나 그룹원 버프는 남은 시간 표시 안 해도 된다). The red edge of a
    /// harmful one stays either way — that is what one acts on.
    /// </param>
    public StatusStrip(int side = 12, int most = 6, bool timed = true)
    {
        _side = side;
        _most = most;
        _timed = timed;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0, side + 2);
        Visible = false;
    }

    /// <summary>Says what to show; nothing hides the strip (so an empty one takes no room).</summary>
    public void Show(IReadOnlyList<StatusBadge> badges)
    {
        if (badges.Count == _shown.Count && badges.SequenceEqual(_shown))
        {
            return;
        }

        _shown = badges;
        Visible = badges.Count > 0;

        // 판이 폭을 재도록 — 다섯 칸과 "+N" 까지.
        CustomMinimumSize = new Vector2(WidthFor(badges.Count) + (badges.Count > _most ? 22 : 0), _side + 2);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_timed && Visible && _shown.Any(one => one.Grade == 1) && (_blink += delta) > 0.3)
        {
            _blink = 0;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        int step = Step;
        int shown = System.Math.Min(_shown.Count, _most);

        for (int at = 0; at < shown; at++)
        {
            StatusBadge one = _shown[at];
            Rect2 box = new(1 + (at * step), 1, _side, _side);

            // 10초 미만은 깜빡인다 — 곧 풀린다.
            if (_timed && one.Grade == 1 && Time.GetTicksMsec() / 300 % 2 == 1)
            {
                continue;
            }

            DrawRect(box.Grow(1), one.Harmful ? Harm : Edge);

            if (!Picture(box, one.Icon))
            {
                DrawRect(box, Color.FromHsv(one.Icon % 12 / 12f, 0.55f, 0.95f));
            }

            if (!_timed)
            {
                continue;
            }

            float tall = _side * ((one.Grade * 2) + 2) / 14f;
            Rect2 bar = new(box.End.X + 1, box.End.Y - tall, Bar, tall);
            DrawRect(bar.Grow(0.5f), Edge);
            DrawRect(bar, Grades[System.Math.Clamp(one.Grade, 1, 6) - 1]);
        }

        if (_shown.Count > shown)
        {
            Font font = GetThemeDefaultFont();
            DrawString(font, new Vector2(1 + (shown * step), _side), $"+{_shown.Count - shown}", HorizontalAlignment.Left, -1, 10, Greybox.Muted);
        }
    }

    /// <summary>The width the strip wants for this many badges — a plate may size itself by it.</summary>
    public float WidthFor(int count) => (System.Math.Min(count, _most) * Step) + 2;

    /// <summary>One badge and the gap after it — the time bar only takes room when time is shown.</summary>
    private int Step => _side + Gap + (_timed ? 1 + Bar : 0);

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
}
