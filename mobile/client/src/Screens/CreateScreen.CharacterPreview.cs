using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;

namespace LodClient;

/// <summary>캐릭터 만들기 — 가운데 미리보기: 고른 성별·머리·색·직업 갑옷으로 서 있는 몸, 양옆 화살표로 돌리기.</summary>
public sealed partial class CreateScreen : Control
{
    // 미리보기 칸 — 이 화면의 주인공(사용자, 2026-09-19: "몸이 가운데·크게"). 세로가 가로보다 큰
    // 것은 사람이 가로보다 세로로 긴 그림이기 때문이다. 이름·비밀번호·확인 세 칸을 한 줄로 모으고
    // (아래 AuthRow) 옆 캡션을 힌트 글자로 바꿔 세로 96px 을 돌려받아, 그 값으로 배율을 3배까지
    // 올렸다(예전엔 격자 둘 자리가 안 나 2배에 머물렀다).
    private static int PreviewWidth => Main.Portrait ? 168 : 144;
    private const int PreviewHeight = 168;

    // 정수 배율로 키운다 — 3배씩이면 원작 그림 한 칸(1px)이 화면에서도 칼같이 3px 로 남는다(흐려지지
    // 않음). 고도 프로젝트 설정(project.godot: default_texture_filter=0=Nearest)이 이미 전역으로
    // 이렇게 그리고 있어 Actor.cs·WorldView.cs 를 보니 텍스처마다 따로 필터를 거는 코드가 없었다 — 여기도
    // 새로 걸지 않고 그 설정에 얹힌다.
    private int PreviewScale => Main.Portrait && _previewHeight < 160 ? 2 : 3;

    // 원작 그림칸(120x96)의 발 기준점(FeetX=31.5, FeetY=83, Actor.cs)은 실제 그려진 그림의 한가운데가
    // 아니다 — 옆으로는 무기를 휘두를 자리를, 위로는 머리 위 여백을 남겨 두기 때문이다. 몸(mb001·wb001)과
    // 머리 그림 전부(서기 프레임: 뒷모습 0번 · 앞모습 5번, WalkMotion.Stand)를 실측한 테두리 한가운데는
    // (약 29.25, 41) 이었다 — 기준점과의 차이만큼 미리보기를 옮겨 기준점이 아니라 실제 그림이 칸
    // 한가운데 오게 한다. 뒷모습·앞모습이 이 차이가 서로 거의 같아(값이 다르지 않음) 방향이 바뀌어도
    // 이 보정은 그대로 쓴다 — 그래서 돌아도 들썩이지 않는다.
    private const float BodyCentreOffsetX = 2.25f;
    private const float BodyCentreOffsetY = 36f;

    /// <summary>맨몸·머리 그림이 하나도 없을 때만 쓰는 마지막 대안(있을 리 없음).</summary>
    private const string HeroSheet = "res://assets/actor/hero-walk.png";

    /// <summary>겹쳐 입힐 그림들이 있는 자리 — <c>WorldView.Dress</c> 와 같은 값(부위별로 따로 못 나눈다).</summary>
    private const string PartsFolder = "res://assets/actor/parts/";

    private Control _preview = null!;
    private Control _stage = null!;
    private Actor? _previewActor = null!;
    private int _previewHeight = PreviewHeight;

    // 화살표로 고른 방향은 머리·색·성별 변경과 화면 회전 뒤에도 유지한다.
    private Direction _facing = Direction.South;

    /// <summary>
    /// 가운데 미리보기 자리. 고른 직업이 있으면 실제 5.99 레벨 1 직업 갑옷을 입히고, 아직 안 골랐을
    /// 때만 맨몸이다. 실제 그림은 <see cref="RefreshPreview"/> 가 채운다.
    /// </summary>
    private Control BuildPreview()
    {
        // On the compact 360x640 portrait drawable the decorative preview yields height, never input
        // fields or the two final actions.
        // The five job choices add one 48px thumb row.  At 360x640 reserve that room from the
        // decorative preview, rather than letting the first fields and the final actions fall offscreen.
        float available = GetViewportRect().Size.Y - Main.SafeInsets.Top - Main.SafeInsets.Bottom;
        int height = Main.Portrait ? (int)Mathf.Clamp(available - 628, 112, PreviewHeight) : 144;
        _previewHeight = height;
        PanelContainer box = new() { CustomMinimumSize = new Vector2(PreviewWidth, height) };
        StyleBoxFlat background = Greybox.Sheet();
        background.SetBorderWidthAll(0);
        box.AddThemeStyleboxOverride("panel", background);

        _stage = new Control { ClipContents = true };
        TextureRect circle = new()
        {
            Texture = new AtlasTexture
            {
                Atlas = GD.Load<Texture2D>("res://assets/ui/create-circle-cutout.png"),
                Region = new Rect2(171, 152, 952, 947)
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore
        };
        circle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.AddChild(circle);


        _stage.Resized += RefreshPreview;
        box.AddChild(_stage);
        Control turns = new() { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        foreach (bool right in new[] { false, true })
        {
            Button arrow = new()
            {
                Name = right ? "TurnRight" : "TurnLeft",
                Text = right ? "›" : "‹",
                TooltipText = right ? "오른쪽으로 회전" : "왼쪽으로 회전",
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
                AnchorLeft = right ? 1 : 0,
                AnchorRight = right ? 1 : 0,
                AnchorTop = 0.5f,
                AnchorBottom = 0.5f,
                OffsetLeft = right ? -Main.TouchMinimum : 0,
                OffsetRight = right ? 0 : Main.TouchMinimum,
                OffsetTop = -Main.TouchMinimum / 2f,
                OffsetBottom = Main.TouchMinimum / 2f
            };
            arrow.AddThemeFontSizeOverride("font_size", 32);
            arrow.AddThemeColorOverride("font_outline_color", Colors.Black);
            arrow.AddThemeConstantOverride("outline_size", 4);
            StyleButton(arrow);
            foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
                arrow.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            arrow.Pressed += () =>
            {
                _facing = NextFacing(_facing);
                if (!right) _facing = NextFacing(NextFacing(_facing));
                _previewActor?.Face(_facing);
            };
            turns.AddChild(arrow);
        }
        box.AddChild(turns);
        _preview = box;
        return box;
    }

    /// <summary>
    /// 미리보기를 지금 고른 성별·머리·색으로, 화살표로 고른 방향으로 다시 그린다. 머리나 색을 넘길
    /// 때마다 다시 불린다 — 팔레트 교체(<see cref="Palettes"/>)는 (그림, 색) 별로 캐시돼 있어 이미
    /// 그려 본 조합은 다시 읽지 않는다.
    /// </summary>
    private void RefreshPreview()
    {
        foreach (Node child in _stage.GetChildren())
        {
            child.QueueFree();
        }

        // 키우는 것과 가운데에 놓는 것은 Actor 가 아니라 이 겉 노드가 한다 — Actor.Face() 가 자기
        // Scale.X 를 좌우 뒤집기(거울)에 쓰고 있어(Actor.cs), 거기 손대지 않고 배율을 얹으려면 한 칸
        // 밖에서 씌워야 한다.
        Node2D wrapper = new()
        {
            Position = new Vector2(
                _stage.Size.X / 2f + BodyCentreOffsetX * PreviewScale,
                _previewHeight / 2f + BodyCentreOffsetY * PreviewScale),
            Scale = new Vector2(PreviewScale, PreviewScale)
        };

        Actor figure = new("미리보기", BareBodySheet());
        wrapper.AddChild(figure);
        _stage.AddChild(wrapper);

        figure.Face(_facing);
        _previewActor = figure;
    }

    /// <summary>
    /// 고른 머리와 실제 레벨 1 직업 갑옷을 그리는 겹 목록. 실제 게임이 쓰는 <see cref="Wardrobe.Pieces"/> 를 그대로 쓴다 —
    /// 갑옷·무기·신발·방패를 전부 0으로 주면 그 부위들은 스스로 빠진다(<c>Wardrobe.cs</c>). 다만 바지
    /// (part 'n')는 여기서 따로 뺀다: 몸 그림(mb001.png) 을 실제로 뽑아 보니 이미 흰/회색 팬티 차림의
    /// 맨몸이었다 — 바지는 그 위에 게임 화면(WorldView)이 항상 덧입히는 것이라 미리보기에서는 필요
    /// 없다(Wardrobe.cs 자체는 게임 화면도 같이 쓰므로 고치지 않는다). 그림이 없는 겹은
    /// <c>WorldView.Dress</c> 와 같은 규칙으로 건너뛴다. 머리색은 <see cref="Palettes"/> 가 팔레트
    /// 98번부터 6칸을 갈아 끼워 그린다(plans/character-creation.md).
    /// </summary>
    private Actor.Sheet BareBodySheet()
    {
        Appearance appearance = new(
            Head: _hairStyle, Body: _gender * 16, Armor: StarterArmor.For(_path), Boots: 0, Shield: 0, Weapon: 0,
            HairColor: _hairColor, BootColor: 0, HeadAccessory1: 0, Lantern: 0, HeadAccessory2: 0,
            Resting: 0, OverCoat: 0);

        List<string> paths = [];
        List<int> colours = [];
        List<char> parts = [];

        foreach (Piece piece in Wardrobe.Pieces(appearance))
        {
            if (piece.Name[1] == 'n')
            {
                continue;
            }

            string path = $"{PartsFolder}{piece.Name}.png";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            paths.Add(path);
            colours.Add(piece.Colour);
            parts.Add(piece.Name[1]);
        }

        return paths.Count > 0 ? Actor.Sheet.Walk(paths, colours, [], parts) : Actor.Sheet.Walk(HeroSheet);
    }

    /// <summary>한 바퀴 도는 차례 — 북·동·남·서(시계 방향). 두 벌만 있는 그림을 돌아가며 거울에 비추므로
    /// (Facing.Of, Wardrobe.Rank 와 같은 원작 규칙) 넷 다 자연스럽게 이어진다.</summary>
    private static Direction NextFacing(Direction facing) => facing switch
    {
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        _ => Direction.North
    };
}
