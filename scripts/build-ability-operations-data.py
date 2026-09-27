#!/usr/bin/env python3
"""현재 Hades 기술·마법과 모바일 운영용 이펙트·사운드 목록을 만든다.

원작 613개 계보가 아니라 서버가 지금 읽는 templates/skills·spells 전체가 운영 대상이다.
브라우저는 이 자료와 `/api/ability-overrides`를 합쳐 현재 운영값을 보여 준다.
"""
import importlib.util
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "sources/wren11/Dark-Ages-Private-Server"
TEMPLATES = SERVER / "database/server/templates"
CLIENT_EFFECTS = ROOT / "mobile/client/assets/effect"
CLIENT_SOUNDS = ROOT / "mobile/client/assets/sound"
DOC_EFFECTS = ROOT / "docs/ui/assets/ability-effects"
DOC_SOUNDS = ROOT / "docs/ui/assets/ability-sounds"
OUT_JSON = ROOT / "data/game-data/ability-operations.json"
OUT_JS = ROOT / "docs/ability-operations-data.js"
MEDIA_JS = ROOT / "docs/ability-media-catalog.js"
AUTO = ROOT / "mobile/client/assets/world/auto-learn.txt"

CLASS = {0: "공통", 1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "무도가", 6: "평민"}
PACK_EFFECT = re.compile(
    r'p\.Call\("effect",\s*[^,]+,\s*\(V\)(\d+)L,\s*\(V\)(\d+)L,\s*\(V\)(\d+)L\)')


def ability_builder():
    path = Path(__file__).with_name("build-ability-page-data.py")
    spec = importlib.util.spec_from_file_location("build_ability_page_data", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def auto_levels():
    out = {}
    if not AUTO.exists():
        return out
    for line in AUTO.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        path, level, kind, name, _ = line.split("\t")
        out[(int(path), kind, name)] = int(level)
    return out


def class_of(template):
    required = (template.get("Prerequisites") or {}).get("Class_Required")
    if isinstance(required, int):
        return required
    group = template.get("Group") or ""
    for number, label in CLASS.items():
        if label in group:
            return number
    return 0


def speed_of(bodies, template):
    if template.get("TargetAnimation") and template.get("TargetAnimationSpeed"):
        return int(template["TargetAnimationSpeed"])
    for body in bodies:
        found = PACK_EFFECT.search(body)
        if found:
            return int(found.group(3))
    return 100


def build_abilities():
    source = ability_builder()
    scripts = source.scripted()
    levels = auto_levels()
    rows = []
    for folder, kind, label, script_field in (
        ("skills", "skill", "기술", "ScriptName"),
        ("spells", "spell", "마법", "ScriptKey"),
    ):
        for path in sorted((TEMPLATES / folder).glob("*.json")):
            template = json.loads(path.read_text(encoding="utf-8-sig"))
            name = template.get("Name")
            if not name:
                continue
            script = template.get(script_field) or ""
            bodies = scripts.get(script, [])
            shaped = dict(template, 갈래=folder)
            sent = source.sent_by(bodies, shaped) if bodies else {"이펙트": [], "소리": [], "몸동작": []}
            class_number = class_of(template)
            level = levels.get((class_number, kind, name))
            if level is None:
                level = int((template.get("Prerequisites") or {}).get("ExpLevel_Required") or 0)
            rows.append({
                "운영키": f"{kind}:{name}",
                "갈래": label,
                "이름": name,
                "직업": CLASS.get(class_number, str(class_number)),
                "레벨": level,
                "아이콘": int(template.get("Icon") or 0),
                "그룹": template.get("Group") or "",
                "구현": bool(script and bodies),
                "게임": sent,
                "기본": {
                    "effect": sent["이펙트"][0] if sent["이펙트"] else None,
                    "speed": speed_of(bodies, template),
                    "sound": sent["소리"][0] if sent["소리"] else None,
                },
                "반영가능": {
                    "effect": bool(sent["이펙트"]),
                    "speed": bool(sent["이펙트"]),
                    "sound": bool(sent["소리"]),
                },
            })
    rows.sort(key=lambda row: (0 if row["갈래"] == "기술" else 1, row["직업"], row["레벨"], row["이름"]))
    return {
        "생성": "scripts/build-ability-operations-data.py",
        "설명": "현재 Hades templates 전체. 운영 override는 이펙트·속도·사운드만 바꾼다.",
        "목록": rows,
        "셈": {
            "전체": len(rows),
            "기술": sum(row["갈래"] == "기술" for row in rows),
            "마법": sum(row["갈래"] == "마법" for row in rows),
            "이펙트수정": sum(row["반영가능"]["effect"] for row in rows),
            "사운드수정": sum(row["반영가능"]["sound"] for row in rows),
        },
    }


def build_media():
    DOC_EFFECTS.mkdir(parents=True, exist_ok=True)
    DOC_SOUNDS.mkdir(parents=True, exist_ok=True)
    effects = []
    source = CLIENT_EFFECTS / "effects.txt"
    for line in source.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        parts = [int(value) for value in line.split()]
        if len(parts) < 6:
            continue
        number, frames, wide, tall, x, y, *order = parts
        file = f"efct{number:03d}.png"
        image = CLIENT_EFFECTS / file
        if not image.exists():
            continue
        # 관리 화면이 이미 가진 그림은 원작 바탕·기준점으로 다시 자른 것이다. 클라이언트 사본으로
        # 덮으면 팔레트나 프레임 배치가 달라질 수 있으므로, 카탈로그에 새로 필요한 파일만 더한다.
        target = DOC_EFFECTS / file
        if not target.exists():
            shutil.copyfile(image, target)
        effects.append({"번호": number, "파일": file, "프레임": frames,
                        "바탕": [wide, tall], "기준": [x, y], "순서": order})

    sounds = []
    for source in sorted(CLIENT_SOUNDS.glob("*.mp3"), key=lambda path: int(path.stem)):
        target = DOC_SOUNDS / source.name
        if not target.exists():
            shutil.copyfile(source, target)
        sounds.append({"번호": int(source.stem), "파일": source.name})
    return {"생성": "scripts/build-ability-operations-data.py", "이펙트": effects, "소리": sounds}


def main():
    abilities = build_abilities()
    media = build_media()
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(json.dumps(abilities, ensure_ascii=False, indent=2), encoding="utf-8")
    OUT_JS.write_text("window.LOD_ABILITY_OPERATIONS = " + json.dumps(abilities, ensure_ascii=False) + ";\n",
                      encoding="utf-8")
    MEDIA_JS.write_text("window.LOD_ABILITY_MEDIA = " + json.dumps(media, ensure_ascii=False) + ";\n",
                        encoding="utf-8")
    print(f"현재 기술·마법 {abilities['셈']['전체']}개 → {OUT_JS.relative_to(ROOT)}")
    print(f"이펙트 {len(media['이펙트'])}개 · 소리 {len(media['소리'])}개 → {MEDIA_JS.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
