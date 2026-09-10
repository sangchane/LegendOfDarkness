#!/usr/bin/env python3
"""`guard_shell.py` 가 막을 것만 막는지 확인한다. `python tools/hooks/test_guard_shell.py`

훅은 조용히 틀린다 — 안 막아도 아무 일이 없고, 잘못 막으면 엉뚱한 명령이 죽는다. 그래서 어느 쪽으로
틀렸는지 여기서 본다. 두 낱말이 본문에 적힌 커밋 메시지가 막히는 것을 실제로 겪어서 생긴 파일이다.
"""
import json
import subprocess
import sys

# 'git ' + 'status' 로 쪼개 적는 것은 이 파일 자체가 훅에 걸리지 않게 하려는 것이 아니라,
# 사람이 읽을 때 '여기 적힌 것은 명령이 아니라 시험 자료'임이 드러나게 하려는 것이다.
CASES = [
    ("git " + "status", "DENY"),
    ("  git " + "status", "DENY"),
    ("git add . && git " + "status", "DENY"),
    ("git " + "status --ignore-submodules=all", "PASS"),
    ("git -C sources/wren11/da-lib " + "status", "PASS"),
    ('echo "그냥 git ' + 'status 는 쓰지 마라"', "PASS"),
    ('git commit -m "a bare git ' + 'status walks submodules"', "PASS"),
    ("git log --oneline -1", "PASS"),
    ("cp D:/_personal/LOD_/extract/hero.png mobile/client/assets/", "WARN"),
    ("ls -la", "PASS"),
]


def verdict(command: str) -> str:
    out = subprocess.run(
        [sys.executable, "tools/hooks/guard_shell.py"],
        input=json.dumps({"tool_input": {"command": command}}),
        capture_output=True,
        text=True,
        encoding="utf-8",
    ).stdout

    return "DENY" if '"deny"' in out else ("WARN" if out.strip() else "PASS")


def main() -> int:
    wrong = 0

    for command, wanted in CASES:
        got = verdict(command)

        if got != wanted:
            wrong += 1

        print(f"{'ok ' if got == wanted else 'BAD'} {got:5} (기대 {wanted:5}) {command[:60]}")

    print(f"\n어긋난 것 {wrong}건")

    return 1 if wrong else 0


if __name__ == "__main__":
    sys.exit(main())
