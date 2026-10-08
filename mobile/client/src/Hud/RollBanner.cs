using Godot;
using Lod.Mobile.Core.Protocol.World;

namespace LodClient;

/// <summary>
/// 그룹 전리품 룰렛 띠(설계 05 E-01) — 위 줄 아래 가운데에 물건 그림·이름과 파티원별 수를 늘어놓는다. 처음 1.5초는 이름 위로
/// 불빛이 돌다가 이긴 이에서 멈추고(이긴 줄은 강조색 + ★), ROLL_SHOW(3초)가 지나면 사라진다. 창이 아니라 손을 받지 않는다.
/// 결과는 서버가 채팅에도 한 줄 적어 준다 — 새 룰렛이 오면 앞의 것을 덮는다.
/// </summary>
public sealed partial class RollBanner : PanelContainer
{
    /// <summary>03 상수 표 ROLL_SHOW — 띠가 떠 있는 시간(초).</summary>
    private const double RollShow = 3;

    private const double Spin = 1.5;
    private const double FadesFor = 0.4;

    private readonly TextureRect _icon = new()
    {
        MouseFilter = MouseFilterEnum.Ignore,
        CustomMinimumSize = new Vector2(32, 32),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
        TextureFilter = TextureFilterEnum.Nearest
    };

    private readonly Label _item = new() { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly GridContainer _names = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<(Label Label, string Text)> _lines = [];
    private int _winner;
    private int _cursor;
    private bool _landed;
    private double _age = double.MaxValue;
    private double _next;

    public RollBanner()
    {
        Name = "RollBanner";
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Main.Portrait ? 210 : 260, 0);

        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = plate.BgColor with { A = 0.82f };
        plate.SetCornerRadiusAll(Greybox.RoundPlate);
        AddThemeStyleboxOverride("panel", plate);

        _item.AddThemeColorOverride("font_color", Greybox.Title);
        _item.AddThemeFontSizeOverride("font_size", 15);

        HBoxContainer head = new() { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", Main.Gutter);
        head.AddChild(_icon);
        head.AddChild(_item);

        _names.AddThemeConstantOverride("h_separation", Main.Gutter / 2);
        _names.AddThemeConstantOverride("v_separation", 2);

        VBoxContainer inside = new() { MouseFilter = MouseFilterEnum.Ignore };
        inside.AddThemeConstantOverride("separation", Main.Gutter / 2);
        inside.AddChild(head);
        inside.AddChild(_names);
        AddChild(inside);
    }

    /// <summary>새 룰렛을 처음부터 돌린다.</summary>
    public void Show(LootRoll roll)
    {
        if (roll.Rolls.Count == 0)
        {
            return;
        }

        foreach (Node old in _names.GetChildren())
        {
            _names.RemoveChild(old);
            old.QueueFree();
        }

        _lines.Clear();
        _winner = 0;
        _icon.Texture = ItemIcons.For(roll.Image);
        _item.Text = roll.Item;
        _names.Columns = roll.Rolls.Count > 3 ? 2 : 1;

        for (int at = 0; at < roll.Rolls.Count; at++)
        {
            (uint serial, string name, byte number) = roll.Rolls[at];
            _winner = serial == roll.Winner ? at : _winner;

            Label label = new()
            {
                Text = $"{name} {number}",
                MouseFilter = MouseFilterEnum.Ignore,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(96, 0)
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            _names.AddChild(label);
            _lines.Add((label, label.Text));
            Light(label, lit: false, final: false);
        }

        _cursor = -1;
        _next = 0;
        _landed = false;
        _age = 0;
        Modulate = Colors.White;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _age += delta;

        if (_age >= RollShow)
        {
            Visible = false;
            return;
        }

        if (_age < Spin)
        {
            // 돌수록 느려져 이긴 이에서 멈추는 맛이 난다.
            if (_age >= _next)
            {
                _cursor = (_cursor + 1) % _lines.Count;
                _next = _age + 0.07 + (0.16 * _age / Spin);

                for (int at = 0; at < _lines.Count; at++)
                {
                    Light(_lines[at].Label, at == _cursor, final: false);
                }
            }
        }
        else if (!_landed)
        {
            _landed = true;

            for (int at = 0; at < _lines.Count; at++)
            {
                _lines[at].Label.Text = at == _winner ? $"{_lines[at].Text} ★" : _lines[at].Text;
                Light(_lines[at].Label, at == _winner, final: true);
            }
        }

        double left = RollShow - _age;
        Modulate = new Color(1, 1, 1, left < FadesFor ? (float)(left / FadesFor) : 1f);
    }

    /// <summary>불이 켜진 줄은 밝은 글씨와 테두리, 나머지는 흐리게. 줄 크기가 안 변하게 테두리는 늘 같은 두께로 둔다.</summary>
    private static void Light(Label label, bool lit, bool final)
    {
        Color edge = lit ? (final ? Greybox.Accent : Greybox.Title) : Colors.Transparent;
        StyleBoxFlat cell = Greybox.Rounded(Greybox.Surface());
        cell.BgColor = lit ? cell.BgColor : Colors.Transparent;
        cell.BorderColor = edge;
        cell.ContentMarginLeft = cell.ContentMarginRight = 6;
        cell.ContentMarginTop = cell.ContentMarginBottom = 2;

        label.AddThemeStyleboxOverride("normal", cell);
        label.AddThemeColorOverride("font_color", lit ? (final ? Greybox.Accent : Greybox.Text) : Greybox.Muted);
    }
}
