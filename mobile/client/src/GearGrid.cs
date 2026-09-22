using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character has on, laid out the way the original laid it out: a ring of places around a paper
/// doll rather than a row of pictures. Which place sits where is <see cref="GearLayout" />'s to say —
/// this only turns that into cells a finger can hit.
/// </summary>
/// <remarks>
/// The original's cells are 32 wide and a finger is not, so the ring is re-set on a grid of
/// <see cref="Main.TouchMinimum" /> cells rather than scaled up from the original's coordinates. At 360
/// wide that comes to exactly the usable width, which is why the middle is the size it is —
/// docs/mobile-test-v1-wireframes.md 8.1절.
/// </remarks>
public sealed partial class GearGrid : Control
{
    /// <summary>The paper doll's column, wide enough for the original's 110-wide drawing.</summary>
    public const int DollWidth = 120;

    // 원작은 칸이 붙어 있다. 가로에서는 여섯 줄에 머리말까지 넣으면 틈을 둘 높이가 없다.
    internal static readonly int Gap = Main.Portrait ? Main.Gutter / 2 : 0;

    private readonly GridContainer _ring = new() { Name = "Ring" };

    private readonly Dictionary<int, Button> _cells = [];

    // 종이인형은 그림 한 장이 아니라 월드에서 쓰는 것과 같은 겹치기다. 그래서 작은 무대를 하나 두고
    // 거기에 Actor 를 세운다 — 옷 겹치는 규칙을 여기서 다시 쓰지 않으려는 것이다.
    private readonly SubViewport _stage = new()
    {
        Name = "DollStage",
        TransparentBg = true,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always
    };

    private readonly SubViewportContainer _doll = new()
    {
        Name = "Doll",
        Stretch = false,
        MouseFilter = MouseFilterEnum.Ignore
    };

    // 무엇을 입은 모습으로 세워 두었나. 같은 차림이면 다시 세우지 않는다.
    private string? _dressed;

    private Actor? _figure;

    // 칸 한 변. 세로는 손가락 최소치 그대로, 가로는 고리 여섯 줄이 한 화면에 들도록 줄인다(PackPanel.FitRing).
    private int _cell;

    public GearGrid()
    {
        Name = "Gear";
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        _ring.Columns = GearLayout.Columns;
        _ring.AddThemeConstantOverride("h_separation", Gap);
        _ring.AddThemeConstantOverride("v_separation", Gap);

        Build();

        AddChild(_ring);

        _doll.AddChild(_stage);
        AddChild(_doll);

        Lay(Main.TouchMinimum);
    }

    /// <summary>
    /// Sets every cell to one size and lays the doll over the middle three rows to match. Nothing moves when the size is
    /// the one already laid.
    /// </summary>
    public void Lay(int cell)
    {
        if (cell == _cell)
        {
            return;
        }

        _cell = cell;

        // 가운데 열은 종이인형 폭을 지켜야 링이 원작처럼 벌어진다. 격자는 줄 순서로 차므로 몇 번째인지가 곧 열이다.
        int index = 0;

        foreach (Node node in _ring.GetChildren())
        {
            bool middle = index++ % GearLayout.Columns == GearLayout.DollColumn;
            ((Control)node).CustomMinimumSize = new Vector2(middle ? DollWidth : cell, cell);
        }

        // 종이인형은 가운데 세 줄을 한꺼번에 덮는다. GridContainer 에는 칸 합치기가 없으므로 격자 위에
        // 따로 얹는다 — 격자 안에서 칸을 건너뛰면 그 뒤의 칸이 전부 한 자리씩 밀린다(실제로 그랬다).
        _doll.Position = new Vector2((cell * 2) + (Gap * 2), (cell + Gap) * GearLayout.DollFirstRow);
        _doll.Size = new Vector2(DollWidth, DollRows());
        _stage.Size = new Vector2I(DollWidth, DollRows());

        if (_figure is not null)
        {
            _figure.Position = FigureAt();
        }

        CustomMinimumSize = new Vector2(
            (cell * 4) + DollWidth + (Gap * 4),
            (cell * GearLayout.Rows) + (Gap * (GearLayout.Rows - 1)));
    }

    /// <summary>Somebody asked about one of the places. The number is the server's own.</summary>
    public event System.Action<int>? Chosen;

    /// <summary>
    /// Stands the character in the middle of the ring wearing what the cells say, facing the reader. The
    /// figure is only rebuilt when the clothes change, because putting one together throws sprites away.
    /// </summary>
    public void ShowDoll(Character? who)
    {
        string wanted = Describe(who);

        if (wanted == _dressed)
        {
            return;
        }

        _dressed = wanted;
        _figure?.QueueFree();
        _figure = null;

        if (who is null)
        {
            return;
        }

        _figure = new Actor("PaperDoll", WorldView.Dress(who));
        _stage.AddChild(_figure);

        _figure.Position = FigureAt();
        _figure.Face(Direction.South);
        _figure.Rest();
    }

    private static string Describe(Character? who) =>
        who?.Wearing is null ? string.Empty : who.Wearing.ToString();

    /// <summary>Puts what is worn into the ring, and the part's own drawing into every place left empty.</summary>
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

            cell.Icon = filled ? ItemIcons.For(gear!.Icon) ?? Empty(slot) : Empty(slot);

            // 걸친 것은 또렷하게, 빈 자리는 흐리게 — 원작 빈자리 그림을 그대로 쓰되 눈에 덜 걸리게 한다.
            cell.Modulate = filled ? Colors.White : new Color(1, 1, 1, 0.35f);

            // 걸친 칸과 고른 칸은 테두리가 밝아진다. 밝기만으로 말하지 않으려고 굵기도 함께 바꾼다.
            StyleBoxFlat edge = Greybox.Surface();
            edge.SetCornerRadiusAll(8);

            if (slot == -chosen)
            {
                edge.BorderColor = Greybox.Title;
                edge.SetBorderWidthAll(2);
            }
            else if (filled)
            {
                edge.BorderColor = Greybox.Muted;
            }

            cell.AddThemeStyleboxOverride("normal", edge);
            cell.TooltipText = filled ? gear!.Called : WornPlace.Of(slot);
        }
    }

    /// <summary>The places, in the order the ring lays them out. Only a run with no hand on it asks.</summary>
    public IEnumerable<Node> Cells => _ring.GetChildren();

    /// <summary>The drawing the original shows while a place is empty.</summary>
    private static Texture2D? Empty(int slot) => GearSlotArt.For(GearLayout.Of(slot).Drawing);

    private int DollRows()
    {
        int rows = (GearLayout.DollLastRow - GearLayout.DollFirstRow) + 1;

        return (_cell * rows) + (Gap * (rows - 1));
    }

    // 발이 무대 아래쪽에 닿게. 칸 높이가 원작 종이인형보다 커서 남는 만큼만 내린다.
    private Vector2 FigureAt() => new(DollWidth / 2f, DollRows() - Main.Gutter);

    private void Build()
    {
        // A grid fills row by row, so every cell has to exist — the blank ones included.
        Dictionary<(int Column, int Row), int> byCell = [];

        foreach (int slot in GearLayout.Slots)
        {
            (int column, int row, _) = GearLayout.Of(slot);
            byCell[(column, row)] = slot;
        }

        for (int row = 0; row < GearLayout.Rows; row++)
        {
            for (int column = 0; column < GearLayout.Columns; column++)
            {
                _ring.AddChild(byCell.TryGetValue((column, row), out int slot) ? MakeCell(slot) : new Control());
            }
        }
    }

    private Button MakeCell(int slot)
    {
        Button cell = new()
        {
            Name = $"Slot{slot}",
            ExpandIcon = true
        };

        // 칸은 평평한 어둠이다 — 돌은 틀과 확정 단추에만 쓴다(data/ui-vault 안C).
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            StyleBoxFlat box = Greybox.Surface();
            box.SetCornerRadiusAll(8);
            cell.AddThemeStyleboxOverride(state, box);
        }

        cell.Pressed += () => Chosen?.Invoke(slot);
        _cells[slot] = cell;

        return cell;
    }
}

/// <summary>
/// The fourteen drawings the original puts in an empty place, cut from <c>_nui_eqi.spf</c> into one strip
/// of 32-wide cells by <c>build-client-assets.ps1</c>. Eighteen places share them: the overhelm borrows
/// the helmet's and the three trinkets borrow armour and cloak, which is what the original does too.
/// </summary>
internal static class GearSlotArt
{
    private const string Path = "res://assets/ui/gear-slots.png";

    private const int Side = 32;

    private static readonly Dictionary<int, Texture2D?> Cut = [];

    public static Texture2D? For(int drawing)
    {
        if (Cut.TryGetValue(drawing, out Texture2D? found))
        {
            return found;
        }

        found = null;

        if (ResourceLoader.Exists(Path))
        {
            found = new AtlasTexture
            {
                Atlas = GD.Load<Texture2D>(Path),
                Region = new Rect2(drawing * Side, 0, Side, Side)
            };
        }

        Cut[drawing] = found;

        return found;
    }
}
