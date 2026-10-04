"""괴물 템플릿의 드랍 목록 — 드랍 생성기들이 같이 쓴다."""


def drops_of(monster, items_only=False):
    """`Drops`(그냥 목록이거나 `{"$values": [...]}`) 의 이름들. `items_only` 면 빈 이름과 "random" 을 뺀다."""
    listed = monster.get("Drops")
    values = listed.get("$values") if isinstance(listed, dict) else listed
    names = [name for name in (values or []) if isinstance(name, str)]
    return [name for name in names if name and name != "random"] if items_only else names



# 서·북의우드랜드(2026-10-04, scripts/gen/world/build-woodland-west-north.py) 구역마다 장비 층 — 원작 표(docs/items/어둠템#1~5.xlsx)
# 접미사 장비 층을 구역이 깊어질수록 차례로 밟는다(사용자 2026-10-04 「순차적으로 — 11 → 21 → 41 → 51 …」). 구역 번호 2~20(열아홉)을
# 11·26·41·56·71·86 여섯 층에 앞에서부터 고르게 나눈다: 2~5 = 11 · 6~8 = 26 · 9~11 = 41 · 12~14 = 56 · 15~17 = 71 · 18~20 = 86.
# 갈래 방(9-2 보스방·17-2·19-2)은 같은 번호의 층. 99 층은 99레벨 사냥터(구광산·카스마늄) 몫으로 남긴다. 1-1 은 장비 없음(EARLY).
WOODLAND_WEST_NORTH_STEPS = (11, 26, 41, 56, 71, 86)


def woodland_west_north_layer(zone):
    """구역 번호(2~20) → 층."""
    return WOODLAND_WEST_NORTH_STEPS[(zone - 2) * len(WOODLAND_WEST_NORTH_STEPS) // 19]


def woodland_west_north_layers(areas_dir):
    """서버 맵 정의에서 서·북의우드랜드 2-1 이상 구역을 찾아 {층: [맵 번호]} — 서·북 같은 층은 한 무리."""
    import json
    import re
    out = {}
    for path in sorted(areas_dir.glob("*.json")):
        area = json.loads(path.read_text(encoding="utf-8-sig"))
        m = re.fullmatch(r"(서의|북의)우드랜드(\d+)-\d+", area["Name"])
        if m and int(m.group(2)) >= 2:
            out.setdefault(woodland_west_north_layer(int(m.group(2))), []).append(area["Id"])
    return {layer: sorted(ids) for layer, ids in sorted(out.items())}
