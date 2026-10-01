using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>월드 화면 — 바닥 깔기·워프 이름표·벽과 사물·물결 바닥.</summary>
public sealed partial class WorldView
{
    /// <summary>
    /// Puts the map we are standing on under our feet. One picture per map, drawn ahead of time from the
    /// map file and the original tileset, and named by the map's own number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The floor used to be one fixed picture — the safe house — whatever map the server said we were on.
    /// On a 60×60 hunting zone that picture covers a corner and everything past it is black, which is what
    /// made 우드랜드 look like the room a new character wakes in with a different name on it.
    /// </para>
    /// <para>
    /// The pictures are drawn by <c>tools/dat-extract map &lt;seo.dat&gt; &lt;맵파일&gt; &lt;가로&gt;
    /// &lt;세로&gt; &lt;출력&gt;</c>, which lays tiles out with exactly the arithmetic
    /// <see cref="IsometricFloor" /> uses, so a tile in the picture sits where a figure standing on that
    /// tile is drawn. A map with no picture shows no floor: keeping the last map's floor drew people and NPCs
    /// standing off its edge, since their tiles belong to a different map.
    /// </para>
    /// </remarks>
    private void LayTheFloor(MapInfo map)
    {
        if (map.Id == _floored)
        {
            return;
        }

        _floored = map.Id;
        StandObjects(map);
        TagExits(map);

        if (_floorSheet is not null)
        {
            _floor.Texture = null;
            return;
        }

        string path = $"{FloorFolder}map{map.Id}.png";

        if (!ResourceLoader.Exists(path))
        {
            GD.Print($"바닥 그림이 없습니다: {path} ({map.Name})");
            _floor.Texture = null;
            return;
        }

        _floor.Texture = GD.Load<Texture2D>(path);
        _floorSize = _floor.Texture.GetSize();
    }

    /// <summary>
    /// 출구마다 가운데 칸 위에 「→ 간 곳」 이름표 하나. 붙은 칸들이 한 출구로 묶여 있어(<see cref="MapGuide.ExitsOn" />) 이름표가
    /// 칸마다 겹치지 않는다. 사람·물건보다 위에 그려 가려지지 않게 한다.
    /// </summary>
    private void TagExits(MapInfo map)
    {
        foreach (Control old in _exitTags)
        {
            old.QueueFree();
        }

        _exitTags.Clear();

        foreach (MapExit exit in Exits.ExitsOn(map.Id))
        {
            (int x, int y) = IsometricFloor.Stand(exit.Middle.X, exit.Middle.Y, map.Rows);
            PanelContainer tag = new() { Name = $"Exit{exit.Middle.X}_{exit.Middle.Y}", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 50 };
            StyleBoxFlat plate = new() { BgColor = new Color(0, 0, 0, 0.65f), BorderColor = new Color(1, 0.87f, 0.45f, 0.8f) };
            plate.SetBorderWidthAll(1);
            plate.SetCornerRadiusAll(4);
            plate.ContentMarginLeft = plate.ContentMarginRight = 5;
            plate.ContentMarginTop = plate.ContentMarginBottom = 1;
            tag.AddThemeStyleboxOverride("panel", plate);

            Label words = new() { Text = $"→ {exit.To}", MouseFilter = MouseFilterEnum.Ignore };
            words.AddThemeFontSizeOverride("font_size", 11);
            words.AddThemeColorOverride("font_color", new Color(1, 0.87f, 0.45f));
            tag.AddChild(words);

            _camera.AddChild(tag);
            Vector2 size = tag.GetCombinedMinimumSize();
            tag.Position = new Vector2(x - (size.X / 2), y - size.Y - 6);
            _exitTags.Add(tag);
        }
    }

    /// <summary>
    /// Stands up the map's buildings, trees and lamps, each picture on its own so a figure behind one is drawn under
    /// it and a figure in front over it — the camera sorts everything by height. Also takes the map's walls, so a
    /// step into one is not taken, and its floor tiles (<see cref="LayTiles" />). All of it comes from
    /// <c>map&lt;번호&gt;.txt</c>, <c>-floor.png</c> and <c>-objects.png</c>, which <c>scripts/build-client-maps.py</c>
    /// draws out of the same .map file and sotp.dat the server reads.
    /// </summary>
    private void StandObjects(MapInfo map)
    {
        foreach (Sprite2D standing in _objects)
        {
            standing.QueueFree();
        }

        _objects.Clear();
        _layout = null;
        _floorSheet = null;
        _tiledFloor.QueueRedraw();

        string layoutPath = $"{FloorFolder}map{map.Id}.txt";
        string sheetPath = $"{FloorFolder}map{map.Id}-objects.png";

        if (!Godot.FileAccess.FileExists(layoutPath))
        {
            return;
        }

        _layout = MapLayout.Read(Godot.FileAccess.GetFileAsString(layoutPath));

        string floorPath = $"{FloorFolder}map{map.Id}-floor.png";
        _floorSheet = _layout.Tiles.Count > 0 && ResourceLoader.Exists(floorPath) ? GD.Load<Texture2D>(floorPath) : null;
        _tiledFloor.Material = _floorSheet is not null ? CyclingFloor(map.Id) : null;

        if (!ResourceLoader.Exists(sheetPath))
        {
            return;
        }

        Texture2D sheet = GD.Load<Texture2D>(sheetPath);
        Dictionary<int, AtlasTexture> cut = [];

        foreach (MapObject standing in _layout.Objects)
        {
            if (!_layout.Pictures.TryGetValue(standing.Picture, out MapPicture picture))
            {
                continue;
            }

            if (!cut.TryGetValue(standing.Picture, out AtlasTexture? texture))
            {
                texture = new AtlasTexture { Atlas = sheet, Region = new Rect2(picture.X, picture.Y, MapPicture.Width, picture.Height) };
                cut[standing.Picture] = texture;
            }

            (int x, int y) = IsometricFloor.ObjectFoot(standing.Column, standing.Row, _layout.Rows, standing.Right);
            Sprite2D sprite = new()
            {
                Texture = texture,
                Centered = false,
                Offset = new Vector2(0, -picture.Height),
                Position = new Vector2(x, y),
                Material = picture.Glows ? _glow : null
            };

            _camera.AddChild(sprite);
            _objects.Add(sprite);
        }
    }

    /// <summary>The floor's colour-turning material, or nothing when no pixel of this map's floor turns (<see cref="FloorCycle" />).</summary>
    private static ShaderMaterial? CyclingFloor(int mapId)
    {
        string marks = $"{FloorFolder}map{mapId}-floor-cycle.png";
        string colours = $"{FloorFolder}map{mapId}-floor-cycle-colours.png";

        if (!ResourceLoader.Exists(marks) || !ResourceLoader.Exists(colours))
        {
            return null;
        }

        ShaderMaterial material = new() { Shader = FloorCycle };
        material.SetShaderParameter("marks", GD.Load<Texture2D>(marks));
        material.SetShaderParameter("colours", GD.Load<Texture2D>(colours));
        return material;
    }

    /// <summary>
    /// Lays the floor tile by tile, in the same order tools/dat-extract draws a floor picture (row by row), so a
    /// tile's overlap onto its neighbour comes out the same.
    /// </summary>
    private void LayTiles()
    {
        if (_layout is not { } layout || _floorSheet is null)
        {
            return;
        }

        Vector2 size = new(IsometricFloor.TileWidth, IsometricFloor.TileHeight);

        for (int row = 0; row < layout.Rows; row++)
        {
            for (int column = 0; column < layout.Columns; column++)
            {
                if (!layout.Tiles.TryGetValue(layout.Floor(column, row), out (int X, int Y) at))
                {
                    continue;
                }

                (int x, int y) = IsometricFloor.Corner(column, row, layout.Rows);
                _tiledFloor.DrawTextureRectRegion(_floorSheet, new Rect2(new Vector2(x, y), size), new Rect2(new Vector2(at.X, at.Y), size));
            }
        }
    }
}
