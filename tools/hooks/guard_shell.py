#!/usr/bin/env python3
"""PreToolUse 훅(Bash·PowerShell): 이 저장소에서 반복해서 밟은 두 지뢰를 명령 실행 전에 잡는다.

1. **루트 `git status` 는 막는다.** submodule 17개를 스캔하다 멈추면 `index.lock` 이 남아 git 전체가
   마비된다. `--ignore-submodules=all` 을 붙이거나 `-C sources/...` 로 특정 저장소만 보면 통과.
2. **`LOD_` 에서 파일을 복사해 오면 경고한다.** 자료 쪽 저장소의 산출물을 들여오지 않기로 했다 —
   추출 *방법*만 참고하고 자산은 이 저장소의 원본 `.dat` 에서 직접 뽑는다. 막지는 않는다.

표준입력으로 훅 JSON 을 받고, 할 말이 있을 때만 표준출력에 JSON 을 낸다. 판단이 서지 않으면 조용히
통과시킨다(exit 0) — 훅이 일을 막는 것보다 놓치는 편이 낫다.
"""
import json
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except (AttributeError, ValueError):
    pass

# 한 줄에 여러 명령이 이어 붙는다: 파이프·세미콜론·&& 단위로 끊어서 본다.
SEPARATORS = re.compile(r"\|\||&&|[|;\n]")

# 토막의 **맨 앞**이 git 일 때만 명령으로 본다. 그러지 않으면 그 두 낱말을 본문에 적은 커밋
# 메시지나 문서까지 명령으로 오인한다 — 이 훅을 넣는 커밋이 실제로 자기 자신에게 막혔다.
GIT_STATUS = re.compile(r"^\s*(?:sudo\s+|&\s+)?(?:\S*[/\\])?git(?:\.exe)?\b[^\n]*?\bstatus\b")

# 명령이 끝나고 인자의 내용이 시작되는 자리.
QUOTED = re.compile(r"[\"']")

SUBMODULE_SAFE = ("--ignore-submodules", "-C sources", "-C ./sources", "-C sources/")

COPYING = re.compile(r"\b(cp|copy|xcopy|robocopy|mv|move|rsync|Copy-Item|Move-Item)\b", re.IGNORECASE)
LOD_UNDERSCORE = re.compile(r"LOD_(?![A-Za-z0-9])")


def deny(reason: str) -> None:
    json.dump(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": reason,
            }
        },
        sys.stdout,
        ensure_ascii=False,
    )


def warn(note: str) -> None:
    json.dump(
        {
            "systemMessage": note,
            "hookSpecificOutput": {"hookEventName": "PreToolUse", "additionalContext": note},
        },
        sys.stdout,
        ensure_ascii=False,
    )


def main() -> int:
    try:
        command = json.load(sys.stdin).get("tool_input", {}).get("command", "")
    except (json.JSONDecodeError, AttributeError, ValueError):
        return 0

    if not isinstance(command, str) or not command:
        return 0

    for part in SEPARATORS.split(command):
        # 따옴표가 열리면 거기서부터는 인자의 내용이지 명령이 아니다. 그러지 않으면
        # `git commit -m "... 두 낱말 ..."` 처럼 메시지 안에 적힌 것까지 명령으로 오인한다.
        head = QUOTED.split(part, 1)[0]

        if GIT_STATUS.search(head) and not any(safe in head for safe in SUBMODULE_SAFE):
            deny(
                "이 저장소에서 그냥 `git status` 는 submodule 17개를 스캔하다 멈추고, 멈추면 "
                "`index.lock` 이 남아 git 전체가 마비됩니다. "
                "`git status --ignore-submodules=all` 을 쓰거나, 특정 저장소만 볼 때는 "
                "`git -C sources/<소유자>/<저장소> status` 를 쓰세요."
            )
            return 0

    if COPYING.search(command) and LOD_UNDERSCORE.search(command):
        warn(
            "`LOD_` 의 산출물은 이 저장소로 들여오지 않기로 했습니다 — 추출 *방법*만 참고하고, "
            "자산은 이 저장소 원본 `.dat` 에서 `tools/dat-extract` 로 직접 뽑습니다. "
            "정말 필요하면 그대로 진행하되, 어디서 가져왔는지 남기세요."
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
