"""괴물 템플릿의 드랍 목록 — 드랍 생성기들이 같이 쓴다."""


def drops_of(monster, items_only=False):
    """`Drops`(그냥 목록이거나 `{"$values": [...]}`) 의 이름들. `items_only` 면 빈 이름과 "random" 을 뺀다."""
    listed = monster.get("Drops")
    values = listed.get("$values") if isinstance(listed, dict) else listed
    names = [name for name in (values or []) if isinstance(name, str)]
    return [name for name in names if name and name != "random"] if items_only else names
