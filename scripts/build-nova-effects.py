#!/usr/bin/env python3
"""기술·마법의 이펙트(그림) 번호를 Novaonline 팩 값으로 바꾼다.

사용자 결정(2026-09-26): **"노바 것이 원작 이펙트다."** 5.99 팩과 노바 팩에 같은 이름으로 있는 기술·마법은
이펙트 번호를 노바 스크립트의 것으로 쓴다. 팩 3개가 일치할 때만 쓴다는 규칙(AGENTS.md)의 예외다.
이펙트 번호와 **이펙트 속도**(`effect` 의 넷째 인자)를 바꾼다 — 동작(motion)·소리(sound)·피해식·스크립트 짜임은
5.99 그대로다. 속도는 사용자 결정(2026-09-27): 「이펙트 속도도 노바 값으로」(예: 데빌크래셔 5.99 130 → 노바 70).

바꾸는 곳은 두 갈래다.

- `scripts/Pack599/Skills|Spells/<이름>.cs` (`build-pack-abilities.py` 가 5.99 문장을 옮긴 것) —
  `p.Call("effect", 대상, 쓴쪽그림, 대상그림, 속도)` 줄. 5.99 블록의 i 번째 `effect` 에 노바 블록의 i 번째
  (노바가 더 짧으면 마지막) 것을 짝지어 **두 그림 칸을 통째로** 옮기고, 속도는 그 짝의 속도로 바꾼다(노바 속도가
  숫자가 아니면 5.99 그대로). 짝의 대상(`@target`·`@myid` …)이
  다르면 통째로 옮기지 않고 번호끼리만 바꾼다(5.99 번호 → 같은 자리 노바 번호). **줄을 더하지 않는다** —
  노바에만 있는 `effect` 는 노바에만 있는 갈래다: 데프레코·렌토·바르도·프라보·어둠의각인의 33 은 노바가 새로 둔
  「몬스터가 마법을 피했다」(40%) 쪽 빗나감 그림이고, 데빌크래셔의 131 은 노바가 둘레 네 칸까지 치게 바꾼 쪽이다.
  그 갈래(확률·범위)는 동작이지 이펙트가 아니라 옮기지 않는다.
- `templates/skills/<이름>.json` 의 `TargetAnimation` — `Skills/Monk/<이름>.cs`(`build-monk-skills.py`)는
  이펙트를 템플릿에서 읽는다. 노바 첫 `effect` 의 대상그림을 넣고, **노바에 이펙트가 없으면 5.99 번호를 둔다**
  (0 으로 지우지 않는다 — 사용자 2026-09-27: 허공답보는 5.99 의 68 이 원작).
  템플릿의 `TargetAnimationSpeed` 에 노바 속도를 적고 `MonkStrike` 가 0x29 에 그 값을 보낸다.

다시 돌려도 같다: 바꾼 줄 끝에 `// 노바 이펙트(5.99: 쓴쪽, 대상, 속도 N)` 로 원래 값을 남겨 두고 그것에서 다시
계산하며(속도가 없는 옛 꼬리표는 줄의 속도가 곧 5.99 값이다),
`build-pack-abilities.py`·`build-monk-skills.py`
가 `--쓰기` 끝에 이것을 부르므로 그쪽을 다시 돌려도 5.99 번호로 되돌아가지 않는다.

노바 팩이 읽는 파일은 `db/script/script_db.txt` 에 적힌 순서다(같은 이름이 두 번 있으면 먼저 것).

  쓰는 법: python3 scripts/build-nova-effects.py [--쓰기]
"""
import sys

from graphify_runtime import configure_utf8_stdio

from lib._nova_effects import main

configure_utf8_stdio(sys.stdout, sys.stderr)

if __name__ == "__main__":
    raise SystemExit(main())
