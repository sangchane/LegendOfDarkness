using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// NPC 가 하는 일의 아이콘 — 어두운 원판에 계열 색 테두리, 안에 그 일의 모양(칼·방패·반지·물약병·자루·동전·망치 …). 미니맵·길 찾기
/// 창·머리 위 이름표·건물 문 표지가 같은 것을 쓴다(사용자 2026-10-08 「마을마다 어느 npc가 뭐하는지 잘 모르겠던데 직관적으로」,
/// autopilot/npc-roles/SPEC.md). 4.51 창에는 색이 없어(<c>docs/original-ui-451.md</c>) 색은 금빛·원작 구슬 두 색·흙빛 몇 가지만 쓰고
/// 구분은 모양이 한다.
/// </summary>
public static class RoleIcon
{
    private static readonly Color Shop = new("#e8c46a");
    private static readonly Color Make = new("#c8783c");
    private static readonly Color Grow = new("#8fa3e0");
    private static readonly Color Ask = new("#ffd75a");
    private static readonly Color Plain = new("#e8e2c8");
    private static readonly Color Plate = new("#16140f");

    /// <summary>계열 색 — 상점·은행 금빛 · 제작 체력 구슬(출구 강조색과 같은 주황) · 성장(전직·기술·체력·능력치) 마력 구슬을 밝게 · 퀘스트 노랑 · 나머지 흙빛.</summary>
    public static Color Paint(NpcRole role) => role switch
    {
        NpcRole.Weapon or NpcRole.Armor or NpcRole.Accessory or NpcRole.Potion or NpcRole.Goods or NpcRole.Bank => Shop,
        NpcRole.Craft => Make,
        NpcRole.Quest => Ask,
        NpcRole.Class or NpcRole.Teach or NpcRole.Vitality or NpcRole.Stats => Grow,
        _ => Plain
    };

    /// <summary>
    /// 원판 하나를 <paramref name="on" /> 위 <paramref name="centre" /> 에 반지름 <paramref name="radius" /> 로 — 계열 색으로 채운 원판에
    /// 어두운 모양. 미니맵(반지름 8)처럼 작을 때 테두리와 가는 선은 엉겨 뭉개졌다(2026-10-08 크기별 사진).
    /// </summary>
    public static void Draw(CanvasItem on, NpcRole role, Vector2 centre, float radius)
    {
        Color disc = Paint(role);
        Color c = Plate;
        on.DrawCircle(centre, radius + 1, new Color(0, 0, 0, 0.85f));
        on.DrawCircle(centre, radius, disc);

        // 모양은 원판 안 [-1,1] 칸에 — 반지름의 0.66 배까지.
        float s = radius * 0.66f;
        Vector2 P(float x, float y) => centre + (new Vector2(x, y) * s);
        float w = Mathf.Max(1.4f, radius / 4.5f);

        switch (role)
        {
            case NpcRole.Weapon:
                on.DrawLine(P(-0.55f, 0.55f), P(0.8f, -0.8f), c, w, true);
                on.DrawLine(P(-0.75f, 0.15f), P(-0.15f, 0.75f), c, w, true);
                on.DrawLine(P(-0.55f, 0.55f), P(-0.9f, 0.9f), c, w * 1.3f, true);
                break;

            case NpcRole.Armor:
                on.DrawColoredPolygon([P(-0.75f, -0.8f), P(0.75f, -0.8f), P(0.75f, 0f), P(0f, 0.95f), P(-0.75f, 0f)], c);
                on.DrawLine(P(0f, -0.55f), P(0f, 0.55f), disc, w * 0.7f, true);
                break;

            case NpcRole.Accessory:
                on.DrawArc(P(0f, 0.25f), 0.6f * s, 0, Mathf.Tau, 18, c, w, true);
                on.DrawColoredPolygon([P(0f, -0.95f), P(0.3f, -0.6f), P(0f, -0.3f), P(-0.3f, -0.6f)], c);
                break;

            case NpcRole.Potion:
                on.DrawCircle(P(0f, 0.35f), 0.6f * s, c);
                on.DrawLine(P(0f, -0.3f), P(0f, -0.8f), c, w * 1.8f, true);
                on.DrawLine(P(-0.3f, -0.85f), P(0.3f, -0.85f), c, w, true);
                break;

            case NpcRole.Goods:
                on.DrawColoredPolygon([P(-0.65f, -0.2f), P(0.65f, -0.2f), P(0.8f, 0.85f), P(-0.8f, 0.85f)], c);
                on.DrawArc(P(0f, -0.2f), 0.35f * s, Mathf.Pi, Mathf.Tau, 10, c, w, true);
                break;

            case NpcRole.Bank:
                on.DrawCircle(centre, 0.85f * s, c);
                on.DrawCircle(centre, 0.45f * s, disc);
                break;

            case NpcRole.Craft:
                on.DrawLine(P(-0.75f, 0.85f), P(0.15f, -0.05f), c, w * 1.2f, true);
                on.DrawColoredPolygon([P(-0.15f, -0.75f), P(0.75f, 0.15f), P(0.4f, 0.5f), P(-0.5f, -0.4f)], c);
                break;

            case NpcRole.Quest:
                on.DrawLine(P(0f, -0.85f), P(0f, 0.25f), c, w * 1.6f, true);
                on.DrawCircle(P(0f, 0.7f), w, c);
                break;

            case NpcRole.Class:
                Star(on, centre, 0.95f * s, c);
                break;

            case NpcRole.Teach:
                on.DrawPolyline([P(0f, -0.45f), P(-0.85f, -0.65f), P(-0.85f, 0.6f), P(0f, 0.8f), P(0.85f, 0.6f), P(0.85f, -0.65f), P(0f, -0.45f), P(0f, 0.8f)], c, w, true);
                break;

            case NpcRole.Vitality:
                on.DrawColoredPolygon([P(0f, 0.85f), P(-0.85f, -0.05f), P(-0.75f, -0.6f), P(-0.3f, -0.75f), P(0f, -0.4f), P(0.3f, -0.75f), P(0.75f, -0.6f), P(0.85f, -0.05f)], c);
                break;

            case NpcRole.Stats:
                on.DrawLine(P(0f, -0.8f), P(0f, 0.8f), c, w * 1.5f, true);
                on.DrawLine(P(-0.8f, 0f), P(0.8f, 0f), c, w * 1.5f, true);
                break;

            case NpcRole.Beauty:
                on.DrawArc(P(-0.45f, 0.55f), 0.28f * s, 0, Mathf.Tau, 10, c, w, true);
                on.DrawArc(P(0.45f, 0.55f), 0.28f * s, 0, Mathf.Tau, 10, c, w, true);
                on.DrawLine(P(-0.3f, 0.3f), P(0.55f, -0.9f), c, w, true);
                on.DrawLine(P(0.3f, 0.3f), P(-0.55f, -0.9f), c, w, true);
                break;

            case NpcRole.Board:
                on.DrawRect(new Rect2(P(-0.8f, -0.75f), new Vector2(1.6f, 1.1f) * s), c, false, w);
                on.DrawLine(P(-0.45f, -0.35f), P(0.45f, -0.35f), c, w * 0.8f, true);
                on.DrawLine(P(-0.45f, 0f), P(0.25f, 0f), c, w * 0.8f, true);
                on.DrawLine(P(0f, 0.35f), P(0f, 0.9f), c, w, true);
                break;

            case NpcRole.Travel:
                on.DrawArc(centre, 0.65f * s, -0.4f, Mathf.Pi * 1.5f, 16, c, w, true);
                on.DrawColoredPolygon([P(0.95f, -0.55f), P(0.9f, 0.1f), P(0.35f, -0.25f)], c);
                break;

            default:
                on.DrawRect(new Rect2(P(-0.85f, -0.7f), new Vector2(1.7f, 1.15f) * s), c, false, w);
                on.DrawColoredPolygon([P(-0.4f, 0.45f), P(-0.1f, 0.45f), P(-0.45f, 0.9f)], c);
                foreach (float x in new[] { -0.4f, 0f, 0.4f })
                {
                    on.DrawCircle(P(x, -0.12f), w * 0.7f, c);
                }

                break;
        }
    }

    /// <summary>
    /// 한 자리의 역할 몇 개(건물 문 안 NPC 들) — 가운데를 <paramref name="centre" /> 에 두고 옆으로 나란히, 셋까지. 하나면 <see cref="Draw" /> 와 같다.
    /// </summary>
    public static void Row(CanvasItem on, IReadOnlyList<NpcRole> roles, Vector2 centre, float radius)
    {
        int count = Mathf.Min(roles.Count, 3);

        for (int at = 0; at < count; at++)
        {
            Draw(on, roles[at], centre + new Vector2((at - ((count - 1) / 2f)) * ((radius * 2) + 1), 0), radius);
        }
    }

    private static void Star(CanvasItem on, Vector2 centre, float radius, Color paint)
    {
        Vector2[] points = new Vector2[10];

        for (int at = 0; at < 10; at++)
        {
            float angle = (-Mathf.Pi / 2) + (at * Mathf.Pi / 5);
            points[at] = centre + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (at % 2 == 0 ? radius : radius * 0.45f));
        }

        on.DrawColoredPolygon(points, paint);
    }
}

/// <summary>역할 아이콘 하나를 컨트롤로 — 길 찾기 창 칩·머리 위 이름표·문 표지 안에 넣는다.</summary>
public sealed partial class RoleBadge : Control
{
    private readonly NpcRole _role;

    public RoleBadge(NpcRole role, float size = 16)
    {
        _role = role;
        CustomMinimumSize = new Vector2(size, size);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public override void _Draw() => RoleIcon.Draw(this, _role, Size / 2, (Mathf.Min(Size.X, Size.Y) / 2) - 1);
}
