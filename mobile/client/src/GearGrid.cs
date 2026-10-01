using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character has on, on the original's own equipment picture: <c>equip01.epf</c> cut to its paper doll and
/// the squares round it, shown whole at its own size or a whole multiple of it, with what is worn laid into the
/// picture's squares and the fighting figures into its number boxes. Which square is which is <see cref="GearLayout" />'s
/// to say. The five places the picture has no square for are not shown yet (사용자 2026-10-01: 따로 추가한다).
/// </summary>
/// <remarks>
/// The picture is never stretched to fit (data/original-ui/451.json 「통째로」) — on a phone its squares are about twenty
/// wide, less than a finger, so a touch goes to the nearest square rather than only the one it lands in.
/// </remarks>
public sealed partial class GearGrid : VBoxContainer
{
    private const string PicturePath = "res://assets/ui/equip-panel.png";
    private const string CloseArt = "res://assets/ui/close.png";
    private const string ClosePressedArt = "res://assets/ui/close-pressed.png";
    private const string GroupArt = "res://assets/ui/equip-group.png";

    // 손가락이 칸 밖에 떨어져도 이만큼 안이면 가장 가까운 칸으로 친다.
    private const int Reach = 14;

    private readonly TextureRect _picture = new()
    {
        Name = "Picture",
        StretchMode = TextureRect.StretchModeEnum.Scale,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        TextureFilter = TextureFilterEnum.Nearest,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private readonly Dictionary<int, Button> _cells = [];

    private readonly Label _armor = Figure("Armor");
    private readonly Label _damage = Figure("Damage");
    private readonly Label _hit = Figure("Hit");
    private readonly Label _nextLevel = Figure("NextLevel");
    private readonly Label _class = Figure("Class");
    private readonly Label _name = Figure("Name");

    // 사람 단추 위에 얹는 원작 그림(equip05) — 사람 하나 = 그룹 신청 안 받음, 둘 = 받음.
    private readonly TextureRect _group = new()
    {
        Name = "Group",
        StretchMode = TextureRect.StretchModeEnum.Scale,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        TextureFilter = TextureFilterEnum.Nearest,
        MouseFilter = MouseFilterEnum.Ignore
    };

    // 그림을 몇 배로 보이나 — 늘리지 않으므로 1 이상의 정수다.
    private int _scale;

    public GearGrid()
    {
        Name = "Gear";
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Stop;

        // 그림은 제 크기(정수배)만 — 크기는 컨테이너가 잰다.
        _picture.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        if (ResourceLoader.Exists(PicturePath))
        {
            _picture.Texture = GD.Load<Texture2D>(PicturePath);
        }

        AddChild(_picture);

        foreach (int slot in GearLayout.Slots)
        {
            if (GearLayout.Square(slot) is not null)
            {
                _picture.AddChild(MakeCell(slot));
            }
        }

        foreach (Label figure in Figures)
        {
            _picture.AddChild(figure);
        }

        _picture.AddChild(_group);
        ShowGroup(false);

        // 원작 Close 단추(butt001) — 누른 모양까지 원작 그림.
        Close = new TextureButton
        {
            Name = "Close",
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest,
            TooltipText = "닫기"
        };

        if (ResourceLoader.Exists(CloseArt))
        {
            Close.TextureNormal = GD.Load<Texture2D>(CloseArt);
            Close.TexturePressed = GD.Load<Texture2D>(ClosePressedArt);
        }

        _picture.AddChild(Close);



        Lay(1);
    }

    /// <summary>
    /// Shows the picture at a whole multiple of its own size and lays the squares and number boxes over it to match.
    /// Nothing moves when the size is the one already laid.
    /// </summary>
    public void Lay(int scale)
    {
        scale = Mathf.Max(1, scale);

        if (scale == _scale)
        {
            return;
        }

        _scale = scale;

        Vector2 picture = new Vector2(GearLayout.PictureWidth, GearLayout.PictureHeight) * scale;
        _picture.CustomMinimumSize = picture;

        foreach ((int slot, Button cell) in _cells)
        {
            if (GearLayout.Square(slot) is { } square)
            {
                Place(cell, square);
            }
        }

        foreach (Label figure in Figures)
        {
            figure.AddThemeFontSizeOverride("font_size", 10 * scale);
        }

        Place(Close, GearLayout.Close);

        // 그룹 그림(34x27)은 단추(36x30) 가운데에.
        _group.Position = new Vector2(GearLayout.Group.X + 1, GearLayout.Group.Y + 2) * scale;
        _group.Size = new Vector2(34, 27) * scale;
        Centre();
    }

    /// <summary>The largest whole multiple of the picture that fits the room, and never less than its own size.</summary>
    public int ScaleThatFits(Vector2 room)
    {
        int across = (int)(room.X / GearLayout.PictureWidth);
        int down = (int)(room.Y / GearLayout.PictureHeight);

        return Mathf.Max(1, Mathf.Min(across, down));
    }

    /// <summary>Somebody asked about one of the places. The number is the server's own.</summary>
    public event System.Action<int>? Chosen;

    /// <summary>
    /// Writes the fighting figures and what is left to the next level into the picture's boxes. Nothing for a
    /// character whose figures the server does not send us — a bot's.
    /// </summary>
    public void ShowFigures(Vitals? mine)
    {
        _armor.Text = mine is null ? string.Empty : $"{mine.Armor}";
        _damage.Text = mine is null ? string.Empty : $"{mine.Damage}";
        _hit.Text = mine is null ? string.Empty : $"{mine.Hit}";
        _nextLevel.Text = mine is null ? string.Empty : ExperienceGauge.Short(mine.ExperienceToGo);
        Centre();
    }

    /// <summary>Writes the class and the name into the picture's top boxes.</summary>
    public void ShowWho(string called, string name)
    {
        _class.Text = called;
        _name.Text = name;
        Centre();
    }

    /// <summary>Draws the person button as the original does: one figure while requests are refused, two while taken.</summary>
    public void ShowGroup(bool open)
    {
        if (!ResourceLoader.Exists(GroupArt))
        {
            return;
        }

        _group.Texture = new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>(GroupArt),
            Region = new Rect2(open ? 76 : 0, 0, 34, 27)
        };
    }

    /// <summary>The original Close button under the person button.</summary>
    public TextureButton Close { get; }

    /// <summary>Somebody pressed the person button.</summary>
    public event System.Action? GroupPressed;

    private Label[] Figures => [_armor, _damage, _hit, _nextLevel, _class, _name];

    /// <summary>
    /// Stands every line of text in the middle of its box. A label is never shorter than its font, which is taller than
    /// the picture's boxes — laid from the box's top it sank to the bottom right (사용자 2026-10-01). So each is laid at
    /// its own size around the box's centre instead.
    /// </summary>
    private void Centre()
    {
        foreach ((Label figure, (int X, int Y, int Width, int Height) box) in new[]
        {
            (_armor, GearLayout.Armor), (_damage, GearLayout.Damage), (_hit, GearLayout.Hit),
            (_nextLevel, GearLayout.NextLevel), (_class, GearLayout.Class), (_name, GearLayout.Name)
        })
        {
            Vector2 size = figure.GetCombinedMinimumSize();
            Vector2 middle = new Vector2(box.X + (box.Width / 2f), box.Y + (box.Height / 2f)) * _scale;

            figure.Size = size;
            figure.Position = (middle - (size / 2)).Floor();
        }
    }

    /// <summary>Puts what is worn into the squares. An empty square, or one whose picture has not been cut, stays the picture's own dark square.</summary>
    public void Show(IReadOnlyList<WornItem> worn, int chosen)
    {
        Dictionary<int, WornItem> onNow = [];

        foreach (WornItem gear in worn)
        {
            onNow[gear.Slot] = gear;
        }

        foreach ((int slot, Button cell) in _cells)
        {
            bool filled = onNow.TryGetValue(slot, out WornItem? gear);

            // 칸 바탕은 늘 원작 그림의 어두운 칸 — 빈 자리 그림·회색 타일을 얹으면 칸마다 색이 달라진다(사용자 2026-10-01).
            cell.Icon = filled ? ItemIcons.Found(gear!.Icon) : null;

            // 고른 칸만 밝고 굵은 테두리. 그림 속 칸은 제 테두리가 있다.
            StyleBoxFlat edge = Outline();

            if (slot == -chosen)
            {
                edge.BorderColor = Greybox.Title;
                edge.SetBorderWidthAll(2);
            }

            cell.AddThemeStyleboxOverride("normal", edge);
            cell.TooltipText = filled ? gear!.Called : WornPlace.Of(slot);
        }
    }

    /// <summary>The places on the picture. Only a run with no hand on it asks.</summary>
    public IEnumerable<Node> Cells => _cells.Values;

    /// <summary>
    /// A finger on the picture picks the square nearest to it, so long as it landed within reach of one — the squares
    /// are smaller than a finger and a miss by a few points should not pick nothing.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release)
        {
            return;
        }

        (int X, int Y, int Width, int Height) person = GearLayout.Group;

        if (new Rect2(_picture.Position + (new Vector2(person.X, person.Y) * _scale), new Vector2(person.Width, person.Height) * _scale)
            .HasPoint(release.Position))
        {
            AcceptEvent();
            GroupPressed?.Invoke();
            return;
        }

        int nearest = 0;
        float best = Reach * _scale;

        foreach ((int slot, Button cell) in _cells)
        {
            if (GearLayout.Square(slot) is null)
            {
                continue;
            }

            Rect2 at = new(_picture.Position + cell.Position, cell.Size);
            float away = release.Position.DistanceTo(release.Position.Clamp(at.Position, at.End));

            if (away <= best)
            {
                best = away;
                nearest = slot;
            }
        }

        if (nearest != 0)
        {
            AcceptEvent();
            _cells[nearest].EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    private void Place(Control control, (int X, int Y, int Width, int Height) box)
    {
        control.Position = new Vector2(box.X, box.Y) * _scale;
        control.Size = new Vector2(box.Width, box.Height) * _scale;
    }

    private static StyleBoxFlat Outline() => new() { DrawCenter = false };

    private static Label Figure(string name)
    {
        Label figure = new()
        {
            Name = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };

        figure.AddThemeColorOverride("font_color", Greybox.Text);
        figure.AddThemeConstantOverride("line_spacing", 0);

        return figure;
    }

    private Button MakeCell(int slot)
    {
        Button cell = new()
        {
            Name = $"Slot{slot}",
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            FocusMode = FocusModeEnum.None,

            // 누르는 곳이 칸보다 넓다 — 손가락은 GearGrid 가 받아 가장 가까운 칸에 준다(_GuiInput).
            MouseFilter = MouseFilterEnum.Ignore
        };

        // 칸 바탕은 원작 그림의 어두운 칸이다.
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            StyleBoxFlat box = Outline();
            box.SetContentMarginAll(1);
            cell.AddThemeStyleboxOverride(state, box);
        }

        cell.Pressed += () => Chosen?.Invoke(slot);
        _cells[slot] = cell;

        return cell;
    }
}
