using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Protocol.World;

namespace LodClient;

/// <summary>
/// 접속자 창 — 원작 451 사람 목록 창(<c>users01</c>, 466×308)을 통째로 화면에 맞게 줄여 쓴다. 왼쪽 어두운 두 칸에 사람마다
/// 한 줄 — 왼쪽 칸에 직업 아이콘(<c>legends</c>) + 아이디, 오른쪽 칸에 길드명(사용자 2026-10-04). 서버가 정렬한 차례(레벨, 그다음
/// 체력 + 마력×2)대로, 넘치면 손으로 굴린다.
/// 오른쪽 칸에는 Total 과 직업별 수 — Country·Master·Guild 는 서버가 주지 않아 비워 둔다.
/// </summary>
public sealed partial class UsersPanel : Control
{
    private static readonly Vector2 Art = new(466, 308);

    // 원작 그림 안 자리(화소). 목록 두 칸 26~159 · 166~250, 오른쪽 칸 x 374~437 — Total 46, 직업 줄은 72 부터 20 간격.
    private const int ListLeft = 26, ListTop = 23, ListBottom = 276, LeftWide = 133, RightWide = 85, Gap = 7;
    private const int CountLeft = 376, CountWide = 58, TotalTop = 45, FirstRowTop = 71, RowStep = 20;

    // 오른쪽 칸 줄 차례(Country 0 · Master 1 · Warrior 2 …)와 그 줄이 세는 직업(Hades Class).
    private static readonly (int Row, int Path)[] ClassRows = [(2, 1), (3, 2), (4, 3), (5, 4), (6, 5), (7, 0)];

    private readonly Control _inside;
    private readonly GridContainer _rows;
    private readonly Label _total;
    private readonly Dictionary<int, Label> _counts = new();
    private readonly Texture2D? _icons = GD.Load<Texture2D>("res://assets/ui/legends.png");

    public UsersPanel()
    {
        Name = "Users";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        // 원작 크기 그대로 짓고 통째로 줄인다(Scale) — 바깥(this)의 최소 크기만 줄인 크기로 둔다.
        _inside = new Control { Size = Art, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_inside);
        _inside.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/ui/users01.png"), Size = Art, MouseFilter = MouseFilterEnum.Ignore });

        Label title = Text("접속자", HorizontalAlignment.Center);
        title.Position = new Vector2(306, 11);
        title.Size = new Vector2(124, 20);
        _inside.AddChild(title);

        ScrollContainer scroll = new()
        {
            Position = new Vector2(ListLeft, ListTop),
            Size = new Vector2(LeftWide + Gap + RightWide, ListBottom - ListTop),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever
        };
        _inside.AddChild(scroll);
        _rows = new GridContainer { Columns = 2 };
        _rows.AddThemeConstantOverride("h_separation", Gap);
        _rows.AddThemeConstantOverride("v_separation", 1);
        scroll.AddChild(_rows);

        _total = Count(TotalTop);

        foreach ((int row, int path) in ClassRows)
        {
            _counts[path] = Count(FirstRowTop + (row * RowStep));
        }

        Close = WindowFrame.CloseButton();
        AddChild(Close);
    }

    public Button Close { get; }

    /// <summary>The names now listed, in order — for the hands-free check.</summary>
    public List<string> Names { get; } = [];

    /// <summary>Fits the window to the room it stands in — never bigger than the original.</summary>
    public void Fit(Vector2 room)
    {
        float scale = Math.Min(1f, Math.Min(room.X / Art.X, room.Y / Art.Y));
        _inside.Scale = new Vector2(scale, scale);
        CustomMinimumSize = Art * scale;
        Close.Position = new Vector2((Art.X * scale) - Main.TouchMinimum, 0);
    }

    /// <summary>Fills the list with who is on, in the order the server sent.</summary>
    public void Show(IReadOnlyList<OnlineUser> users)
    {
        // 빼고 나서 버린다 — QueueFree 만 하면 이 프레임 동안 옛 칸이 남아 새 칸이 다른 열로 밀린다.
        foreach (Node old in _rows.GetChildren())
        {
            _rows.RemoveChild(old);
            old.QueueFree();
        }

        Names.Clear();
        Names.AddRange(users.Select(one => one.Name));

        foreach (OnlineUser user in users)
        {
            _rows.AddChild(Row(user));
            Label guild = Text(user.Guild, HorizontalAlignment.Left);
            guild.CustomMinimumSize = new Vector2(RightWide, 18);
            guild.ClipText = true;
            guild.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _rows.AddChild(guild);
        }

        _total.Text = users.Count.ToString();

        foreach ((int path, Label count) in _counts)
        {
            count.Text = users.Count(one => one.Path == path).ToString();
        }
    }

    private Control Row(OnlineUser user)
    {
        HBoxContainer row = new() { CustomMinimumSize = new Vector2(LeftWide, 18), MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 3);

        row.AddChild(new TextureRect
        {
            Texture = _icons is null ? null : new AtlasTexture { Atlas = _icons, Region = new Rect2(user.Path * 24, 0, 20, 20) },
            CustomMinimumSize = new Vector2(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        });

        Label name = Text(user.Name, HorizontalAlignment.Left);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(name);

        return row;
    }

    private Label Count(int top)
    {
        Label count = Text(string.Empty, HorizontalAlignment.Right);
        count.Position = new Vector2(CountLeft, top);
        count.Size = new Vector2(CountWide, 16);
        _inside.AddChild(count);

        return count;
    }

    private static Label Text(string text, HorizontalAlignment align)
    {
        Label label = new()
        {
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Greybox.Text);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);

        return label;
    }
}
