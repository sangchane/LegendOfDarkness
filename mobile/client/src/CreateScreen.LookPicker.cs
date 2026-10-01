using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;

namespace LodClient;

/// <summary>캐릭터 만들기 — 머리(HAIR)·색(COLOR)을 눈으로 보고 고르는 두 격자.</summary>
public sealed partial class CreateScreen : Control
{
    // 머리 그림 한 칸의 크기와, 미리 서 있는 자세(앞모습)가 있는 칸 — WalkMotion.Stand(Side.Front) 그대로.
    private const int CellWidth = 120;
    private const int CellHeight = 96;

    // COLOR 조각 하나 — 원작 소지품 칸(PackPanel.cs)과 같은 최소 터치 크기를 그대로 쓴다. 새 치수를
    // 만들지 않는다. 색은 판판한 사각형이라 이 크기로도 잘 보인다.
    private static readonly Vector2 ColorTileSize = new(Main.TouchMinimum, Main.TouchMinimum);
    private static int ColorGridColumns => Main.Portrait ? 4 : 3;

    // 터치 높이는 유지하면서 머리·색 목록의 여백을 줄인다. 세로 네 열, 짧은 가로는 세 열.
    private static readonly Vector2 HairTileSize = new(48, 72);
    private static int HairGridColumns => Main.Portrait ? 4 : 3;

    private const int ColorCount = 72;

    // 머리 그림칸에서 위 빈 12px만 덜어낸다. 여자 긴 머리의 아래쪽(y60)은 남긴다.
    private static readonly Rect2 HairThumbRegion =
        new(WalkMotion.Stand(Lod.Mobile.Core.Art.Side.Front) * CellWidth + 14, 12, 34, 48);

    private Control _hairRow = null!;
    private GridContainer _hairGrid = null!;
    private readonly Dictionary<int, Button> _hairTiles = new();

    private Control _colorRow = null!;
    private readonly Dictionary<int, Button> _colorTiles = new();

    /// <summary>HAIR 격자의 틀 — 자리는 <see cref="PopulateHairGrid"/> 가 채운다(성별이 바뀔 때 다시).</summary>
    private Control BuildHairGrid()
    {
        VBoxContainer section = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", Main.Gutter / 2);
        if (Main.Portrait) section.AddChild(Aux("HAIR"));

        _hairGrid = new GridContainer { Columns = HairGridColumns };
        _hairGrid.AddThemeConstantOverride("h_separation", Main.Gutter);
        _hairGrid.AddThemeConstantOverride("v_separation", Main.Gutter);

        // 짧은 화면은 한 줄의 터치 크기를 보장하고, 남는 높이를 머리·색 목록에 나눠 준다.
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, HairTileSize.Y),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(_hairGrid);
        section.AddChild(scroll);

        _hairRow = section;
        return section;
    }

    /// <summary>
    /// HAIR 격자를 지금 성별의 머리 번호로, 지금 고른 COLOR 로 다시 채운다. 그림 조각 하나가 곧
    /// 단추다 — 원작 소지품 칸(PackPanel.cs)과 같은 방식으로, 고른 것만 <c>Flat=false</c> 를 둬
    /// 테두리가 남게 한다. COLOR 를 바꿀 때는 이 함수를 통째로 다시 부르지 않고
    /// <see cref="RefreshHairIcons"/> 만 불러 그림만 새로 물들인다(단추를 다시 짓지 않음).
    /// </summary>
    private void PopulateHairGrid()
    {
        foreach (Node child in _hairGrid.GetChildren())
        {
            child.QueueFree();
        }

        _hairTiles.Clear();

        foreach (int number in HairStyles.For(_gender))
        {
            Button tile = new()
            {
                CustomMinimumSize = HairTileSize,
                TextureFilter = TextureFilterEnum.Nearest,
                Icon = HairIcon(number),
                ExpandIcon = true,
                Flat = number != _hairStyle
            };

            int picked = number;
            tile.Pressed += () => SelectHair(picked);

            _hairTiles[number] = tile;
            _hairGrid.AddChild(tile);
        }
    }

    /// <summary>
    /// 이 머리 번호를, 지금 성별·지금 고른 COLOR 로 물들여 자른 그림. 그림이 없으면(있을 리 없음) null.
    /// </summary>
    private AtlasTexture? HairIcon(int number)
    {
        char genderLetter = _gender == 2 ? 'w' : 'm';
        string path = $"{PartsFolder}{genderLetter}h{number:000}.png";

        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        Texture2D dyed = Palettes.Load(path, _hairColor);
        // Crop only transparent thumb padding: the actual hairstyle gets the whole existing touch tile.
        Rect2I frame = new((int)HairThumbRegion.Position.X, (int)HairThumbRegion.Position.Y,
            (int)HairThumbRegion.Size.X, (int)HairThumbRegion.Size.Y);
        Rect2I ink = dyed.GetImage().GetRegion(frame).GetUsedRect();
        Rect2 region = ink.Size == Vector2I.Zero ? HairThumbRegion
            : new Rect2(frame.Position + ink.Position, ink.Size).Grow(2).Intersection(HairThumbRegion);
        return new AtlasTexture { Atlas = dyed, Region = region };
    }

    /// <summary>
    /// COLOR 를 바꿨을 때 HAIR 격자의 그림만 새로 물들인다 — 단추를 다시 짓지 않아 고른 표시
    /// (Flat)가 흔들리지 않는다. <see cref="Palettes.Load"/> 가 (그림, 색) 별로 캐시하므로 같은
    /// 색을 다시 고르면 다시 물들이지 않는다.
    /// </summary>
    private void RefreshHairIcons()
    {
        foreach ((int number, Button tile) in _hairTiles)
        {
            tile.Icon = HairIcon(number);
        }
    }

    /// <summary>COLOR 격자 — 72가지 색 조각. 한 번만 짓는다(성별과 무관).</summary>
    private Control BuildColorGrid()
    {
        VBoxContainer section = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", Main.Gutter / 2);
        if (Main.Portrait) section.AddChild(Aux("COLOR"));

        GridContainer grid = new() { Columns = ColorGridColumns };
        grid.AddThemeConstantOverride("h_separation", Main.Gutter);
        grid.AddThemeConstantOverride("v_separation", Main.Gutter);

        IReadOnlyDictionary<int, IReadOnlyList<Colour>> table = Palettes.AllColours();

        for (int number = 0; number < ColorCount; number++)
        {
            // 색 하나는 여섯 톤을 가진다(밝은 것부터 어두운 것까지, data/character-creation/hair-colours.json).
            // 조각 하나로는 하나만 보여 줄 수 있으니 가운데 톤(6개 중 3번째, 0-기준 인덱스 2)을 쓴다 — 가장
            // 밝은 톤은 바래 보이고 가장 어두운 톤은 칙칙해, 그 사이가 "이 색"이라고 봤을 때 가장 무난했다.
            Colour shade = table.TryGetValue(number, out IReadOnlyList<Colour>? shades) && shades.Count > 0
                ? shades[Mathf.Min(2, shades.Count - 1)]
                : new Colour(120, 120, 120);

            Button tile = new()
            {
                CustomMinimumSize = ColorTileSize,
                Icon = SwatchIcon(shade),
                ExpandIcon = true,
                Flat = number != _hairColor
            };

            int picked = number;
            tile.Pressed += () => SelectColor(picked);

            _colorTiles[number] = tile;
            grid.AddChild(tile);
        }

        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(grid);
        section.AddChild(scroll);

        _colorRow = section;
        return section;
    }

    /// <summary>한 색으로 칠한 1x1 그림 — 단추 안을 그 색으로 채우는 가장 짧은 길(ExpandIcon 이 늘려 준다).</summary>
    private static ImageTexture SwatchIcon(Colour colour)
    {
        Image pixel = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
        pixel.SetPixel(0, 0, Color.Color8(colour.R, colour.G, colour.B));

        return ImageTexture.CreateFromImage(pixel);
    }

    private void SelectHair(int number)
    {
        _hairStyle = number;
        RefreshHairSelection();
        RefreshPreview();
    }

    private void SelectColor(int number)
    {
        _hairColor = number;
        RefreshColorSelection();
        RefreshHairIcons();
        RefreshPreview();
    }

    private void RefreshHairSelection()
    {
        foreach ((int number, Button tile) in _hairTiles)
        {
            tile.Flat = number != _hairStyle;
        }
    }

    private void RefreshColorSelection()
    {
        foreach ((int number, Button tile) in _colorTiles)
        {
            tile.Flat = number != _hairColor;
        }
    }
}
