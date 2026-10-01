#!/usr/bin/env python3
"""5.99 서버팩의 장비(무기·갑옷·방패·투구·장신구·장갑·허리띠·각반·신발·장식)를 하데스 아이템 템플릿으로 옮긴다.

2026-09-17 무기·갑옷에 나머지 장비 칸을 더했다 — 상점 판매 목록 중 서버에 없던 103종의 대부분(장식 56 · 목걸이 8 …)이
이 칸들이었다. 칸마다 하데스가 이미 쓰는 스크립트·자리를 따른다(하데스표 템플릿의 ScriptName·EquipmentSlot 짝):
방패 Shield 3 · 투구 Helmet 4 · 귀걸이 Earring 5 · 목걸이 Necklace 6 · 반지 Generic 7(왼손, 차 있으면 오른손) ·
장갑 Generic 9(왼팔, 차 있으면 오른팔) · 허리띠 Belt 11 · 각반 Generic 12 · 신발 Boot 13 · 장식 Generic 14.
**장식은 입은 모습으로 그리지 않는다** — 7.18 겉모습(0x33)의 OverCoat 칸을 채우는 스크립트가 하데스에 없다. 능력치만 붙는다.

사용자 결정(2026-09-17): 5.99 무기를, 이어서 갑옷을 서버에 들인다. 하데스 무기 템플릿은 남겨 둔다(사용자 결정). 상점·드롭은 나중에
5.99 NPC·상점과 함께 옮긴다 — 이번에는 템플릿만.

그림 주의: 우리가 가진 5.99 한국 클라이언트는 무기 27개 번호의 그림이 다른 무기로 바뀌어 있지만, 5.99 팩 아이템의
착용 번호는 하데스(영문) 아카이브 그림과 맞는다(3 커틀라스 = 칼, 6 단검 = 단검, 11 매스케이드 = Masquerade 칼).
그래서 옷장 생성기는 무기만 하데스 아카이브를 먼저 본다(`build-client-wardrobe.py` 의 `archives`).

**어느 것이 무기·갑옷인가:** 5.99 아이템의 `속성` 칸이 장비 자리다 — 0·12·13 무기, 1 갑옷, 2 방패, 3 투구, 4 귀걸이,
5 목걸이, 6 반지, 7 장갑, 8 허리띠, 9 각반, 10 신발, 11 장식. 폴더 이름은 믿을 수 없다(`전사방어구.txt` 에 도끼·곤봉이
있다). 속성 0 에는 재료도 섞여 있어 공격력(`최소공격력1`)과 착용 그림이 있는 것만 무기로 본다. 갑옷은 속성 1 이면서
착용 그림이 있는 것 — 치장옷·드레스·길드옷까지 들어간다. 도복(공격모션 132)을 입으면 무기 없이 주먹이 나가고
(`Assail.BlowMotion`), 신발과 함께 못 입는다(`scripts/Items/Armor.cs`·`Boot.cs`, Novaonline.exe 0x41cc49·0x41d387).

**칸을 잇는 법:**
  이름 → Name · 착용이미지 → Image(입은 그림, `Aisling.Weapon`) · 이미지 → DisplayImage = 0x8000 + 이미지
    (아이콘 칸 번호 체계가 같다 — 5.99 설단검 91 = 하데스 Dirk 91, 에페 87 = Eppe 87)
  직업제한 → Class (1 전사 · 2 도적 · 3 마법사 · 4 성직자 · 5 도가 — 하데스 `Class` 와 번호가 같다)
  성별제한 0 → Gender 255(둘 다) · 1 남 · 2 여(마스터아머1 남 ↔ 마스터아머2 여) · 레벨제한 → LevelRequired(없으면 1) · 승급제한 1 → StageRequired Master
  최소/최대공격력1 → DmgMin/DmgMax(`Pack599.AttackPower` 가 둘의 평균을 쓴다) · 내구력 → MaxDurability
  판매가격 → Value · 무게 → CarryWeight
  방어력·마법방어·명중수정·공격수정·체력변화·마력변화·힘/덱스/인트/위즈/콘변화 → *Modifer
    (부호가 같은 뜻이다 — 5.99 레더튜닉 방어력 −10 = 하데스 Leather Tunic `AcModifer` 빼기 10)
  Flags: 장착·거래·보관·판매 + 떨굼여부 0 이 아니면 버리기 + 수리여부 1 이면 수리 + (무기만) 공격모션 129 면 양손

공격모션·공격속도 → AttackMotion·AttackSpeed(평타 몸 동작, `Assail.BlowMotion`)

**옮기지 못한 칸:** 최소/최대공격력2(마법 공격력 — 하데스 템플릿에 칸이 없다), 공격속도가 뜻하는 평타 잠금(속도×10ms), 사운드1·2, 속성(원소), 어빌제한·전직제한, 장착펄숫·장착해제펄숫(끼고 벗을 때 스크립트).

  쓰는 법: python3 scripts/build-pack-equipment.py [--쓰기]
"""
import json
import sys
from collections import Counter
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

from lib._paths import ROOT
from lib._pack_equipment import text, classify, kind_of, template
ITEMS = ROOT / "data" / "server-packs" / "extracted" / "5.99-server" / "items.json"
OUT = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"

configure_utf8_stdio(sys.stdout, sys.stderr)

def main():
    write = "--쓰기" in sys.argv
    items = json.loads(ITEMS.read_text(encoding="utf-8"))
    chosen, skipped = [], Counter()
    for item in items:
        kind, why = classify(item["fields"])
        if kind:
            chosen.append(item)
        else:
            skipped[why] += 1

    # 파일 이름이 곧 이름이라 같은 이름이면 앞의 것이 말없이 사라진다. 지금 자료에는 없으니 생기면 멈춘다.
    names = Counter(text(item["fields"], "이름") for item in chosen)
    twice = sorted(name for name, count in names.items() if count > 1)
    if twice:
        raise SystemExit(f"팩에 같은 이름의 장비가 둘 이상이다 — 하나가 덮여 사라진다. 쓰기 전에 가려라: {', '.join(twice)}")

    replaced, made, unsendable = [], 0, []
    for item in chosen:
        body = template(item)
        path = OUT / f"{body['Name']}.json"
        if path.exists():
            replaced.append(body["Name"])
        if body["ScriptName"] == "Weapon" and body["Image"] > 255:
            unsendable.append(f"{body['Name']}({body['Image']})")
        if write:
            path.write_text(json.dumps(body, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        made += 1

    print(f"장비 {made}종 {'썼다' if write else '(미리보기 — --쓰기 로 쓴다)'} → {OUT.relative_to(ROOT)}")
    if skipped:
        print(f"  뺀 것 {sum(skipped.values())}: " + ", ".join(f"{why} {n}" for why, n in skipped.most_common()))
    print("  갈래별", dict(Counter(kind_of(item["fields"]) for item in chosen)))
    print("  파일별", dict(Counter(Path(item["출처"]).stem for item in chosen)))
    if replaced:
        print(f"  이미 있던 같은 이름 {len(replaced)}개를 덮는다: {', '.join(replaced)}")
    if unsendable:
        print(f"  착용 번호가 255 를 넘어 서버가 보내지 못한다(ServerFormat33 이 무기를 1바이트로 쓴다): {', '.join(unsendable)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
