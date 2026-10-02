using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 장비창 — 위 메뉴 [장비] 로 연다(사용자, 2026-10-01: 소지품 창의 탭으로는 세로가 모자라 따로 뺐다). 창은 원작 장비 그림
/// 그 자체이고 틀·제목 줄·여백이 없다(<see cref="GearGrid" />). 닫기는 그림 속 원작 Close 단추나 [장비]를 다시 누른다. 그림에
/// 칸이 없는 다섯 자리(겉투구·겉옷·장신구1~3)는 아직 안 보인다 — 따로 추가한다(사용자 2026-10-01).
/// </summary>
/// <remarks>
/// 두 가지로 쓴다(사용자 2026-10-01). 내 것: 옆에 소지품 창이 같이 열려 입고 벗는다 — 칸을 누르면 그 위에 이름·한 줄·[벗기],
/// 빠르게 두 번 누르면 바로 벗는다. 사람 단추는 그룹 신청 받기 켜고 끄기. 남의 것(<see cref="ShowOther" />): 사람을 눌러 서버가
/// 보낸 장비를 보이고, 사람 단추는 그 사람에게 그룹 신청.
/// </remarks>
public sealed partial class GearPanel : PanelContainer
{
    private readonly GearGrid _gear = new();

    private readonly PanelContainer _action = new() { Name = "GearAction", TopLevel = true, Visible = false, ZIndex = 5 };
    private readonly Label _actionName = new();
    private readonly Label _actionLine = new();
    private readonly Label _actionStats = new();
    private readonly GridContainer _actionTable = new();
    private readonly TextureRect _actionIcon = new()
    {
        CustomMinimumSize = new Vector2(40, 40),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
    };
    private readonly Button _off = WindowFrame.IconButton(GlyphKind.TakeOff, "장착 해제", width: 56);
    private readonly Button _shut = WindowFrame.CloseButton();
    private readonly DoubleTap _taps = new();

    // 고른 자리(서버 번호), 없으면 0. 무엇을 그렸는지 — 같으면 다시 그리지 않는다.
    private int _chosen;
    private string? _showing;
    private IReadOnlyList<WornItem> _shownWorn = [];

    // 남의 장비창이면 그 사람. 없으면 내 것.
    private OtherProfile? _other;

    public GearPanel()
    {
        Name = "GearWindow";
        Visible = false;

        // 창은 그림뿐 — 바탕 판도 여백도 없다. 그림 왼쪽 위 귀퉁이는 투명하다.
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _gear.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        _gear.Chosen += slot =>
        {
            if (_other is null && _taps.Tap(slot, Time.GetTicksMsec() / 1000.0))
            {
                _action.Visible = false;
                TakenOff?.Invoke(slot);
                return;
            }

            // 보고 있는 칸을 다시 누르면 정보 상자를 내린다(사용자 2026-10-02).
            _chosen = slot == _chosen && _action.Visible ? 0 : slot;
            _showing = null;
        };

        _shut.Pressed += () =>
        {
            _chosen = 0;
            _showing = null;
            _action.Visible = false;
        };

        _gear.GroupPressed += () =>
        {
            if (_other is null)
            {
                GroupToggled?.Invoke();
            }
            else
            {
                GroupAsked?.Invoke(_other.Name);
            }
        };

        _off.Pressed += () =>
        {
            if (_chosen > 0)
            {
                TakenOff?.Invoke(_chosen);
                _action.Visible = false;
            }
        };

        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color("#0f0f0f");
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(10);
        plate.SetContentMarginAll(6);
        _action.AddThemeStyleboxOverride("panel", plate);
        _actionName.AddThemeColorOverride("font_color", Greybox.Title);
        _actionName.AddThemeFontSizeOverride("font_size", 15);
        _actionLine.AddThemeColorOverride("font_color", Greybox.Muted);
        _actionLine.AddThemeFontSizeOverride("font_size", 12);
        _actionStats.AddThemeColorOverride("font_color", Greybox.Text);
        _actionStats.AddThemeFontSizeOverride("font_size", 12);

        // 소지품 정보 상자와 같은 모양 — 그림 · 이름 · 내구, 그 아래 수치. [장착 해제]는 오른쪽 위(사용자 2026-10-01).
        VBoxContainer words = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddChild(_actionName);
        words.AddChild(_actionLine);
        HBoxContainer top = new();
        top.AddThemeConstantOverride("separation", Main.Gutter);
        top.AddChild(_actionIcon);
        top.AddChild(words);
        top.AddChild(_off);
        top.AddChild(_shut);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(top);
        column.AddChild(_actionTable);
        column.AddChild(_actionStats);
        _action.AddChild(column);

        AddChild(_gear);
        Control actionLayer = new() { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(actionLayer);
        actionLayer.AddChild(_action);
    }

    /// <summary>Somebody asked to take off what is in one worn place. The number is the server's own.</summary>
    public event System.Action<int>? TakenOff;

    /// <summary>Our person button: take group requests, or stop taking them.</summary>
    public event System.Action? GroupToggled;

    /// <summary>Somebody else's person button: ask them to group. The name is theirs.</summary>
    public event System.Action<string>? GroupAsked;

    /// <summary>The original Close button in the picture.</summary>
    public BaseButton Close => _gear.Close;

    /// <summary>Whether this is somebody else's window rather than ours.</summary>
    public bool ShowingOther => _other is not null;

    /// <summary>
    /// Shows our own gear, class and name, fighting figures and whether we take group requests; the squares are redrawn
    /// only when what is worn or picked changes.
    /// </summary>
    public void Show(IReadOnlyList<WornItem> worn, Vitals? mine, int? path, string name, bool groupOpen, Vector2 room)
    {
        ShowMine();
        _gear.ShowFigures(mine);
        _gear.ShowWho(ClassName(path), name);
        _gear.ShowGroup(groupOpen);
        Fill(worn, room);
    }

    /// <summary>
    /// Leaves somebody else's window for ours — the screen fills ours in only while <see cref="ShowingOther" /> is false, so
    /// the 장비 button must clear it first (once a companion's window had opened, the button kept showing that companion).
    /// </summary>
    public void ShowMine()
    {
        if (_other is not null)
        {
            _other = null;
            _chosen = 0;
            _showing = null;
        }
    }

    /// <summary>Shows somebody else's gear as the server sent it (0x34). Their fighting figures are not sent, so the boxes stay empty.</summary>
    public void ShowOther(OtherProfile who, Vector2 room)
    {
        _other = who;
        _chosen = 0;
        _showing = null;
        _gear.ShowFigures(null);
        _gear.ShowWho(ClassName(who.Path), who.Name);
        _gear.ShowGroup(who.GroupOpen);
        Fill(who.Worn, room);
    }

    /// <summary>Hades <c>Class</c> as a number (0x39) or a name (0x34), in the words the game uses.</summary>
    private static string ClassName(int? path) => path switch
    {
        0 => "평민", 1 => "전사", 2 => "도적", 3 => "마법사", 4 => "성직자", 5 => "무도가", _ => string.Empty
    };

    private static string ClassName(string path) => path switch
    {
        "Peasant" => "평민", "Warrior" => "전사", "Rogue" => "도적", "Wizard" => "마법사", "Priest" => "성직자", "Monk" => "무도가", _ => path
    };

    private void Fill(IReadOnlyList<WornItem> worn, Vector2 room)
    {
        _gear.Lay(_gear.ScaleThatFits(room));

        string wanted = $"{_chosen}|" + string.Join(";", worn.Select(gear => $"{gear.Slot}:{gear.Icon}"));

        if (wanted == _showing && _shownWorn.SequenceEqual(worn))
        {
            return;
        }

        _showing = wanted;
        _shownWorn = worn.ToArray();
        _gear.Show(worn, -_chosen);

        WornItem? picked = worn.FirstOrDefault(one => one.Slot == _chosen);
        _action.Visible = picked is not null;

        if (picked is not null)
        {
            // 걸친 것은 바로 버릴 수 없다 — 벗어서 소지품에 든 다음에야. 남의 것은 부위 이름만 온다.
            _actionName.Text = picked.Called;
            _actionIcon.Texture = ItemIcons.For(picked.Icon);
            _actionLine.Text = _other is null ? ItemActions.Line(picked) : string.Empty;
            _actionLine.Visible = _other is null;
            WindowFrame.ShowStats(_actionTable, _actionStats, ItemActions.Stats(picked.Stats));
            _off.Visible = _other is null;
            _action.ResetSize();
        }
    }

    /// <summary>Picks the first worn thing and presses 벗기 — only for a run with no hand on it, through the same events.</summary>
    public bool PressFirst()
    {
        if (_gear.Cells.FirstOrDefault() is not Button cell)
        {
            return false;
        }

        cell.EmitSignal(BaseButton.SignalName.Pressed);
        _off.EmitSignal(BaseButton.SignalName.Pressed);

        return true;
    }

    public override void _Process(double delta)
    {
        if (!_action.Visible)
        {
            return;
        }

        if (_gear.FindChild($"Slot{_chosen}", true, false) is not Control cell || !cell.IsVisibleInTree())
        {
            _action.Visible = false;
            return;
        }

        // 고른 칸 바로 위에, 자리가 없으면 아래에 — 창 좌우 안에서.
        Rect2 window = GetGlobalRect();
        Rect2 at = cell.GetGlobalRect();
        Vector2 size = _action.GetCombinedMinimumSize();
        float x = Mathf.Clamp(at.GetCenter().X - (size.X / 2), window.Position.X, Mathf.Max(window.Position.X, window.End.X - size.X));
        float above = at.Position.Y - size.Y - 4;

        _action.Size = size;
        _action.GlobalPosition = new Vector2(x, above >= window.Position.Y ? above : at.End.Y + 4);
    }
}
