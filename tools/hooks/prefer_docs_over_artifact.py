#!/usr/bin/env python3
"""PreToolUse 훅(Artifact): 이 프로젝트의 산출물은 저장소 문서로 남긴다는 것을 상기시킨다.

Artifact 는 나중에 다시 찾기 어렵다. 읽을거리는 `docs/` 의 마크다운으로, 그림은 파일로 보낸다.
막지는 않는다 — 정말 웹 페이지가 필요한 때도 있다.
"""
import json
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except (AttributeError, ValueError):
    pass

NOTE = (
    "이 프로젝트의 산출물은 Artifact 말고 저장소에 남기기로 했습니다 — 읽을거리는 `docs/` 의 "
    "마크다운으로, 그림·스크린샷은 파일로 보내는 쪽이 나중에 다시 찾힙니다. "
    "웹 페이지 형태가 정말 필요한 경우에만 이대로 진행하세요."
)


def main() -> int:
    json.dump(
        {
            "systemMessage": NOTE,
            "hookSpecificOutput": {"hookEventName": "PreToolUse", "additionalContext": NOTE},
        },
        sys.stdout,
        ensure_ascii=False,
    )

    return 0


if __name__ == "__main__":
    sys.exit(main())
