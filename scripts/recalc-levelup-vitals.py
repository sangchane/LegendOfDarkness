#!/usr/bin/env python3
"""이미 레벨업한 캐릭터의 기본 최대 체력·마력을 원작 레벨업 식으로 다시 맞춘다.

원작(5.99 Novaonline.exe 0x469a46~0x469a94 · 혼든 Yuki.exe 0x45fa29~0x45fa46): 레벨마다
    기본 최대 체력 += 콘 + 30,  기본 최대 마력 += 위즈 + 25
하데스가 쓰던 식: 레벨마다 int(5*콘*0.65), int(5*위즈*0.45)

파일의 _MaximumHp/_MaximumMp 는 기본값이다(장비 몫 BonusHp/BonusMp 는 저장되지 않는다).
레벨마다의 콘·위즈는 기록에 없으므로, 옛 식으로 쌓인 몫에서 되짚는다:
    옛 몫 H = _MaximumHp - 150,  n = ExpLevel - 1
    int(3.25c) 는 3.25c-0.75 ~ 3.25c 이므로 Σ콘 은 H/3.25 ~ (H+0.75n)/3.25 — 가운데를 쓴다.
    Σ콘 은 n*지금콘 을 넘을 수 없다(능력치는 내려가지 않는다).
    새 _MaximumHp = 150 + Σ콘 + 30n.  마력도 같은 모양(200 · 2.25 · 25).
현재 체력·마력은 새 최대치를 넘지 않게 자른다. 다른 칸은 건드리지 않는다(글자 그대로 둔다).

한 번만 돌린다 — 이미 맞춘 캐릭터는 폴더 안 .levelup-recalc-done 에 적어 두고 다시 하지 않는다.
서버가 접속 중 캐릭터를 10초마다 덮어쓰므로 **서버를 멈춘 뒤** 돌린다.

    python3 scripts/recalc-levelup-vitals.py <aislings 폴더> [--dry-run]
"""
import argparse
import json
import re
import shutil
import sys
import time
from pathlib import Path

START_HP, START_MP = 150, 200          # Aisling.cs 새 캐릭터
OLD_HP_RATE, OLD_MP_RATE = 3.25, 2.25   # 5*0.65, 5*0.45
HP_PER_LEVEL, MP_PER_LEVEL = 30, 25     # 원작 상수
DONE = ".levelup-recalc-done"


def estimate_sum(old_part: int, levels: int, rate: float, current_stat: int) -> tuple[int, bool]:
    """옛 식으로 쌓인 몫에서 레벨업 때마다의 능력치 합을 되짚는다. (합, 잘렸는가)"""
    guess = round((old_part + 0.375 * levels) / rate)
    ceiling = levels * current_stat
    floor = levels  # 능력치는 1 이상
    clipped = max(floor, min(guess, ceiling))
    slack = 0.375 * levels / rate + 1  # 되짚기 자체의 반올림 폭 — 이 안이면 이상한 게 아니다
    return clipped, abs(clipped - guess) > slack


def top_level(text: str, key: str, value: int) -> str:
    pattern = re.compile(rf'^(  "{re.escape(key)}": )-?\d+', re.MULTILINE)
    new, count = pattern.subn(rf'\g<1>{value}', text)
    if count != 1:
        raise ValueError(f"맨 위 '{key}' 칸이 {count}개입니다")
    return new


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folder", type=Path, help="캐릭터 파일 폴더 (database/server/aislings)")
    parser.add_argument("--dry-run", action="store_true", help="바꿀 값만 찍고 파일은 그대로 둔다")
    args = parser.parse_args()

    folder: Path = args.folder
    if not folder.is_dir():
        print(f"폴더가 없습니다: {folder}", file=sys.stderr)
        return 2

    done_file = folder / DONE
    done = set(done_file.read_text(encoding="utf-8").split()) if done_file.exists() else set()
    backup = folder.parent / f"{folder.name}-before-levelup-recalc-{time.strftime('%Y%m%d-%H%M%S')}"

    changed: list[str] = []
    for path in sorted(folder.glob("*.json")):
        name = path.stem
        raw = path.read_bytes()
        bom = b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b""
        text = raw.decode("utf-8-sig")  # 줄바꿈은 그대로 둔다
        c = json.loads(text)
        level = int(c.get("ExpLevel", 1))
        levels = level - 1
        hp, mp = int(c["_MaximumHp"]), int(c["_MaximumMp"])
        con, wis = int(c["_Con"]), int(c["_Wis"])
        cur_hp, cur_mp = int(c["CurrentHp"]), int(c["CurrentMp"])

        if name in done:
            print(f"{name}: 이미 맞춤 — 건너뜀")
            continue
        if levels <= 0:
            print(f"{name}: {level}레벨 — 바꿀 것 없음")
            continue
        if hp < START_HP or mp < START_MP:
            print(f"{name}: 시작값({START_HP}/{START_MP})보다 낮음({hp}/{mp}) — 손으로 확인, 건너뜀")
            continue

        sum_con, con_clip = estimate_sum(hp - START_HP, levels, OLD_HP_RATE, con)
        sum_wis, wis_clip = estimate_sum(mp - START_MP, levels, OLD_MP_RATE, wis)
        new_hp = START_HP + sum_con + HP_PER_LEVEL * levels
        new_mp = START_MP + sum_wis + MP_PER_LEVEL * levels
        new_cur_hp, new_cur_mp = min(cur_hp, new_hp), min(cur_mp, new_mp)

        note = ""
        if con_clip or wis_clip:
            note = " (옛 몫이 능력치로 설명되지 않아 잘랐음 — 죽음 벌칙·아이템·GM 변경이 섞였을 수 있다)"
        print(f"{name}: {level}레벨 콘{con} 위즈{wis} · 체력 {hp} → {new_hp} (Σ콘≈{sum_con})"
              f" · 마력 {mp} → {new_mp} (Σ위즈≈{sum_wis})"
              f" · 현재 {cur_hp}/{cur_mp} → {new_cur_hp}/{new_cur_mp}{note}")

        if args.dry_run:
            continue

        backup.mkdir(exist_ok=True)
        shutil.copy2(path, backup / path.name)
        for key, value in (("_MaximumHp", new_hp), ("_MaximumMp", new_mp),
                           ("CurrentHp", new_cur_hp), ("CurrentMp", new_cur_mp)):
            text = top_level(text, key, value)
        json.loads(text)  # 깨지지 않았는지
        path.write_bytes(bom + text.encode("utf-8"))
        changed.append(name)

    if changed:
        with done_file.open("a", encoding="utf-8") as f:
            f.write("".join(f"{n}\n" for n in changed))
        print(f"\n{len(changed)}명을 바꿨습니다. 원본: {backup}")
    elif args.dry_run:
        print("\n--dry-run: 파일은 그대로입니다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
