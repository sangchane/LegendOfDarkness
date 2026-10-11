"""클라우드 봇 이름 「사제」→「성직자」 옮기기(사용자 2026-10-11, 명세 autopilot/death-penalty-gear/SPEC.md SC-5).

scripts/ops/rename-bots.sh 가 서버·봇이 멈춘 것을 확인하고 클라우드에서 `python3 - [--apply]` 로 돌린다. 기본은 미리보기.
옮길 이름은 봇 설정(eco-bots.json Bots[].Name · companion-bot-N.json Name) 중 「사제」가 든 것 — 사제봇N → 성직자봇N,
동료사제N → 동료성직자N. 바꾸는 곳: 캐릭터 파일 aislings/<소문자 이름>.json(+ .json.backup) 이름과 그 안의 Username,
봇 설정 두 곳의 Name, 경매장 auction.json(+ .backup)의 Seller·Bidder·Owner, 생태계 봇 유찰 목록 eco-unlisted.json 의 키.
값은 글자 그대로 바꾼다(파일의 BOM·들여쓰기·다른 칸은 그대로). 새 이름 파일과 옛 이름 파일이 둘 다 있으면 그 봇은 통째로 건너뛴다.
중간에 멈춘 뒤 다시 돌리면 이어서 끝낸다 — 새 이름 파일만 있으면 「이미 옮김」으로 보고 경매·유찰 목록·봇 설정만 마저 바꾼다.
봇 설정이 옮길 이름을 찾는 곳이라 맨 나중에 바꾼다(그 전에 멈춰도 다음에 다시 찾는다). 끝에 백업 폴더와 되돌리는 명령을 출력한다.
서버 설정 LoruleConfig.json 은 deploy 가 틀(server-config/LoruleConfig.template.json)로 다시 만들므로 여기서 안 건드린다.
"""
import re
import shutil
import sys
from datetime import datetime
from pathlib import Path

OLD, NEW = "사제", "성직자"
HOME = Path("/home/ubuntu")
AISLINGS = HOME / "lod/database/server/aislings"
AUCTION = HOME / "lod/database/server/auction"
ECO = HOME / "lod-eco/eco-bots.json"
UNLISTED = HOME / "lod-eco/eco-unlisted.json"
BOT_DIR = HOME / "lod-bot"


def value_of(keys):
    """`"키": "이름"` 의 이름 — 키는 keys 중 하나."""
    return lambda name: re.compile(rb'("(?:' + rb"|".join(k.encode() for k in keys) + rb')"\s*:\s*")' + re.escape(name.encode()) + rb'"')


def key_of(name):
    """`"이름":` — eco-unlisted.json 의 키."""
    return re.compile(rb'(")' + re.escape(name.encode()) + rb'("\s*:)')


USERNAME, NAME, PEOPLE = value_of(["Username"]), value_of(["Name"]), value_of(["Seller", "Bidder", "Owner"])


def swap(path, pattern, new, apply):
    """path 안에서 pattern 을 new 로 — 몇 군데였는지. apply 때만 쓴다(같은 파일에 써 권한·소유자 유지)."""
    if not path.exists():
        return 0
    data = path.read_bytes()
    out, n = pattern.subn(lambda m: m.group(1) + new.encode() + (m.group(2) if m.lastindex == 2 else b'"'), data)
    if n and apply:
        path.write_bytes(out)
    return n


def names(eco, companions):
    found = []
    for path in [eco, *companions]:
        if path.exists():
            found += re.findall(r'"Name"\s*:\s*"([^"]*)"', path.read_text(encoding="utf-8-sig"))
    return [n for n in dict.fromkeys(found) if OLD in n]


def backup(to, aislings, eco, companions, auction, unlisted):
    """바꾸기 전 모습을 to 에 — 되돌리는 명령(줄 목록)을 돌려준다."""
    to.mkdir(parents=True)
    shutil.copytree(aislings, to / "aislings")
    undo = [f"rm -rf '{aislings}' && cp -a '{to / 'aislings'}' '{aislings}'"]
    for path in [eco, unlisted, *companions, *sorted(auction.glob("auction.json*"))]:
        if path.exists():
            shutil.copy2(path, to / path.name)
            undo.append(f"cp -a '{to / path.name}' '{path}'")
    return undo


def run(apply, aislings=AISLINGS, eco=ECO, bot_dir=BOT_DIR, auction=AUCTION, unlisted=UNLISTED, backup_to=None, log=print):
    companions = sorted(bot_dir.glob("companion-bot-*.json"))
    todo = names(eco, companions)
    if not todo:
        log("옮길 이름이 없습니다(봇 설정에 「사제」가 든 이름 없음).")
        return []
    undo = []
    if apply and backup_to:
        undo = backup(backup_to, aislings, eco, companions, auction, unlisted)
        log(f"백업 → {backup_to}")
    done = []
    for old in todo:
        new = old.replace(OLD, NEW)
        src, dst = aislings / f"{old.lower()}.json", aislings / f"{new.lower()}.json"
        olds = src.exists() or src.with_name(src.name + ".backup").exists()
        news = dst.exists() or dst.with_name(dst.name + ".backup").exists()
        if olds and news:
            log(f"경고: {dst.name}(또는 .backup) 이 이미 있어 {old} 는 건너뜁니다(설정·경매도 그대로).")
            continue
        what = []
        if news:
            what.append("이미 옮김")
        elif src.exists():
            if USERNAME(old).search(src.read_bytes()) is None:
                log(f"경고: {src.name} 안에 Username \"{old}\" 가 없어 {old} 는 건너뜁니다.")
                continue
            for path in [src, src.with_name(src.name + ".backup")]:
                if path.exists():
                    swap(path, USERNAME(old), new, apply)
                    if apply:
                        path.rename(aislings / (dst.name + path.name[len(src.name):]))
                    what.append(path.name)
        else:
            what.append("캐릭터 파일 없음")
        trades = sum(swap(path, PEOPLE(old), new, apply) for path in sorted(auction.glob("auction.json*")))
        skipped = swap(unlisted, key_of(old), new, apply)
        settings = sum(swap(path, NAME(old), new, apply) for path in [eco, *companions])
        log(f"{old} → {new}: {' · '.join(what)} · 봇 설정 {settings} · 경매 {trades} · 유찰 목록 {skipped}")
        done.append((old, new))
    log(("바꿨습니다" if apply else "미리보기 — --apply 로 바꿉니다") + f" ({len(done)}/{len(todo)})")
    if undo:
        log(f"되돌리기 — 서버·봇을 멈춘 뒤 클라우드에서(백업 {backup_to}):")
        for line in undo:
            log(f"  {line}")
    return done


if __name__ == "__main__":
    apply = "--apply" in sys.argv[1:]
    run(apply, backup_to=HOME / "lod-backups" / f"rename-{datetime.now():%Y%m%d-%H%M%S}")
