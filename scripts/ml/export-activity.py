#!/usr/bin/env python3
"""머신러닝용 사본 — 게임 활동 기록과 생태계 봇 사건을 날짜별 gzip JSONL 로 모은다(설계 autopilot/eco-bots/05 E8).
원본 활동 기록(서버 activity/YYYY-MM-DD.jsonl)은 IP 가 있어 90일 뒤 지워진다. 이 사본은 IP·detail(한국어 문장)을 빼고
사람 이름은 비밀 소금으로 HMAC-SHA256 한 앞 16글자(p)로 바꿔 오래 둔다. 이름 그대로 두는 것은 서버 설정의 봇 이름(EcoBots·CompanionBots)뿐 —
bot=true 는 대신 사냥 중인 사람에게도 서므로 그것만 믿지 않는다. meta 의 거래 상대(counterparty)도 같이 바꾸고 기기 번호(install)는 뺀다.
오늘 파일은 아직 자라니 건너뛴다.
  쓰는 법: python3 scripts/ml/export-activity.py --activity <서버 activity 폴더> --eco <봇 eco 폴더> --out <ml 폴더> --salt <소금 파일> --config <LoruleConfig.json>
  산출물:  <out>/activity/YYYY-MM-DD.jsonl.gz · <out>/eco/YYYY-MM-DD.jsonl.gz (이미 있으면 그대로)
"""
from __future__ import annotations

import argparse
import gzip
import hashlib
import hmac
import json
import re
import shutil
from datetime import datetime, timedelta, timezone
from pathlib import Path

DROP = ("ip", "detail")


def pseudonym(salt: bytes, name: str) -> str:
    return hmac.new(salt, name.lower().encode("utf-8"), hashlib.sha256).hexdigest()[:16]


def bot_names(config: str) -> set:
    """서버 설정 글(주석 섞인 JSON)의 EcoBots·CompanionBots 이름, 소문자로."""
    names = set()
    for key in ("EcoBots", "CompanionBots"):
        found = re.search(r'"%s"\s*:\s*\[([^\]]*)\]' % key, config)
        if found:
            names |= {name.lower() for name in re.findall(r'"([^"]+)"', found.group(1))}
    return names


def scrub(event: dict, salt: bytes, bots: set) -> dict:
    clean = {k: v for k, v in event.items() if k not in DROP}
    player = clean.pop("player", "") or ""
    if player.lower() in bots:
        clean["bot_name"] = player
    elif player:
        clean["p"] = pseudonym(salt, player)
    meta = clean.get("meta")
    if isinstance(meta, dict):
        meta = {k: v for k, v in meta.items() if k != "install"}
        other = meta.get("counterparty")
        if isinstance(other, str) and other and other.lower() not in bots:
            meta["counterparty"] = pseudonym(salt, other)
        clean["meta"] = meta
    return clean


def export(activity: Path, eco: Path, out: Path, salt: bytes, today: str, bots: set = frozenset()) -> list[str]:
    done = []
    for source in sorted(activity.glob("????-??-??.jsonl")) if activity.is_dir() else []:
        day = source.stem
        target = out / "activity" / f"{day}.jsonl.gz"
        if day >= today or target.exists():
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_suffix(".tmp")
        with source.open(encoding="utf-8") as read, gzip.open(temporary, "wt", encoding="utf-8") as write:
            for line in read:
                line = line.strip()
                if not line:
                    continue
                try:
                    event = json.loads(line)
                except json.JSONDecodeError:
                    continue
                write.write(json.dumps(scrub(event, salt, bots), ensure_ascii=False) + "\n")
        temporary.replace(target)
        done.append(str(target))

    for source in sorted(eco.glob("????-??-??.jsonl.gz")) if eco.is_dir() else []:
        target = out / "eco" / source.name
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            done.append(str(target))
    return done


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--activity", type=Path, required=True)
    parser.add_argument("--eco", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--salt", type=Path, required=True, help="비밀 소금 파일(저장소 밖, 권한 600)")
    parser.add_argument("--config", type=Path, required=True, help="서버 LoruleConfig.json — 이름을 남길 봇 이름")
    args = parser.parse_args()
    salt = args.salt.read_bytes().strip()
    if len(salt) < 16:
        raise SystemExit(f"소금이 너무 짧습니다(16바이트 이상): {args.salt}")
    today = (datetime.now(timezone.utc) + timedelta(hours=9)).strftime("%Y-%m-%d")
    done = export(args.activity, args.eco, args.out, salt, today, bot_names(args.config.read_text(encoding="utf-8-sig")))
    print(f"학습용 사본 {len(done)}개")


if __name__ == "__main__":
    main()
