#!/usr/bin/env python3
"""Stop 훅: Hades 서버를 띄워 놓고 세션을 끝내려 하면 알려 준다.

포트가 코드에 박혀 있어(2620) 남은 프로세스 하나가 다음 세션의 시험을 전부 막는다. 막지는 않고
한 줄 알리기만 한다 — 서버를 일부러 띄워 둘 때도 있다.
"""
import json
import socket
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except (AttributeError, ValueError):
    pass

PORTS = (2610, 2615, 2620)


def listening(port: int) -> bool:
    """무언가 받아 주면 열려 있는 것. 붙었다 바로 끊으므로 서버에 남기는 것이 없다."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
        probe.settimeout(0.2)
        try:
            return probe.connect_ex(("127.0.0.1", port)) == 0
        except OSError:
            return False


def main() -> int:
    busy = [str(port) for port in PORTS if listening(port)]

    if busy:
        json.dump(
            {
                "systemMessage": (
                    f"Hades 서버가 아직 떠 있습니다 (포트 {', '.join(busy)}). "
                    "`./scripts/stop-hades.ps1` 로 내리지 않으면 다음 세션의 격리 시험이 막힙니다."
                )
            },
            sys.stdout,
            ensure_ascii=False,
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
