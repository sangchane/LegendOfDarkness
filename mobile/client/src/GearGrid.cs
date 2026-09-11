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
    private static readonly int Gap = Main.Portrait ? Main.Gutter / 2 : 0;

    /// <summary>
    /// The ring itself: four cells of places, the doll between them, and the four gaps in between. This is
    /// the ring, not the panel — the panel adds its own plate margins on top (see <see cref="PanelWidth" />).
    /// </summary>
    public static int RingWidth => (Main.TouchMinimum * 4) + DollWidth + (Gap * 4);

    /// <summary>
    /// How wide a panel has to be to hold the ring: the ring, the plate's own margins, and the scroll bar.
    /// A panel narrower than this cuts the outer column off the screen — which happened twice, first from
    /// counting the gaps at the full gutter instead of the half the grid uses, then from forgetting the
    /// bar. In landscape the six rows are taller than the panel, so the bar is always there.
    /// </summary>
    public static int PanelWidth => RingWidth + Plate() + ScrollBar();

    private static int Plate()
    {
        StyleBox plate = Greybox.Plate();

        return (int)(plate.ContentMarginLeft + plate.ContentMarginRight);
    }

    private static int ScrollBar()
    {
        VScrollBar bar = new();
        int width = (int)bar.GetCombinedMinimumSize().X;

        bar.Free();

        return width;
    }

    private static readonly Vector2 Cell = new(Main.TouchMinimum, Main.TouchMinimum);

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

    public GearGrid()
    {
        Name = "Gear";
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        _ring.Columns = GearLayout.Columns;
        _ring.AddThemeConstantOverride("h_separation", Gap);
        _ring.AddThemeConstantOverride("v_separation", Gap);

        Build();

        AddChild(_ring);

        // 종이인형은 가운데 세 줄을 한꺼번에 덮는다. GridContainer 에는 칸 합치기가 없으므로 격자 위에
        // 따로 얹는다 — 격자 안에서 칸을 건너뛰면 그 뒤의 칸이 전부 한 자리씩 밀린다(실제로 그랬다).
        _doll.Position = new Vector2(
            (Main.TouchMinimum * 2) + (Gap * 2),
            (Main.TouchMinimum * GearLayout.DollFirstRow) + (Gap * GearLayout.DollFirstRow));

        _doll.Size = new Vector2(DollWidth, DollRows());
        _stage.Size = new Vector2I(DollWidth, DollRows());

        _doll.AddChild(_stage);
        AddChild(_doll);

        CustomMinimumSize = new Vector2(
            RingWidth,
            (Main.TouchMinimum * GearLayout.Rows) + (Gap * (GearLayout.Rows - 1)));
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

        _figure = new Actor(string.Empty, WorldView.Dress(who));
        _stage.AddChild(_figure);

        // 발이 무대 아래쪽에 닿게. 칸 높이가 원작 종이인형보다 커서 남는 만큼만 내린다.
        _figure.Position = new Vector2(DollWidth / 2f, DollRows() - Main.Gutter);
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

            // 걸친 것은 또렷하게, 빈 자리는 흐리게. 고른 칸만 테두리가 남는다.
            cell.Modulate = filled ? Colors.White : new Color(1, 1, 1, 0.35f);
            cell.Flat = slot != -chosen;
            cell.TooltipText = filled ? gear!.Called : WornPlace.Of(slot);
        }
    }

    /// <summary>The drawing the original shows while a place is empty.</summary>
    private static Texture2D? Empty(int slot) => GearSlotArt.For(GearLayout.Of(slot).Drawing);

    private static int DollRows()
    {
        int rows = (GearLayout.DollLastRow - GearLayout.DollFirstRow) + 1;

        return (Main.TouchMinimum * rows) + (Gap * (rows - 1));
    }

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
                bool middle = column == GearLayout.DollColumn;

                _ring.AddChild(byCell.TryGetValue((column, row), out int slot)
                    ? MakeCell(slot, middle)
                    : Blank(middle));
            }
        }
    }

    private Button MakeCell(int slot, bool middle)
    {
        Button cell = new()
        {
            Name = $"Slot{slot}",
            CustomMinimumSize = middle ? new Vector2(DollWidth, Main.TouchMinimum) : Cell,
            ExpandIcon = true,
            Flat = true
        };

        cell.Pressed += () => Chosen?.Invoke(slot);
        _cells[slot] = cell;

        return cell;
    }

    // 가운데 열은 종이인형 폭을 지켜야 링이 원작처럼 벌어진다.
    private static Control Blank(bool middle) =>
        new() { CustomMinimumSize = middle ? new Vector2(DollWidth, Main.TouchMinimum) : Cell };
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
