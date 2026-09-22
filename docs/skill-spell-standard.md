# 기술·마법 자료 표준

아이템처럼 출처와 신원을 나눠 보되, 기술·마법은 한 행으로 합치지 않는다. 현재 정본은 `data/game-data/ability-standard.json`이고, 그 manifest는 아래 네 층의 권한과 연결 상태를 재생성해 검증한다.

| 무엇을 정하는가 | 정본 | 행/대상 | 다른 층을 덮는가 |
|---|---|---:|---|
| 영문 신원·기술/마법·직업·선행 | `data/game-data/abilities.json` (Hades `SClass1~5`) | 613 | 아니오 |
| 한글 이름·일반/어빌리티·비용·재료·배우는 곳 | `data/skill-spell-2023/skills.json` (사용자 제공 워크북) | 539 | 아니오 |
| 지금 서버의 템플릿·스크립트·기본 이펙트/소리 | `database/server/templates/{skills,spells}` · `database/server/scripts` | 실행 자료 | 아니오 |
| 관리 페이지 | `docs/abilities-data.js` | 파생물 | 정본 아님 |

`SClass`와 2023 워크북은 공통 신원 키가 없다. 따라서 영문↔한글 이름, 레벨, 아이콘, 팩의 유사한 값으로 자동 짝짓지 않는다. manifest의 `exact_template_name`은 같은 문자열의 서버 템플릿이 정확히 한 장일 때만 쓰는 **실행 점검**이며, 두 계보를 같은 능력으로 확정하지 않는다. 못 잇는 행과 여러 템플릿에 걸리는 행은 `audit`에 그대로 남는다.

이미 존재하는 Hades 한글 표시의 우선순위도 유지한다: `data/기술마법-한글이름.tsv`의 사용자 수정이 우선이고, 없을 때만 세 서버팩 완전 일치가 표시 후보이다. 이 표시 규칙은 SClass와 2023 워크북의 신원 매칭 규칙이 아니다. 팩의 연출 표도 원작 사실을 덮지 않으며, 관리 페이지의 실제 게임 연출 번호는 서버 템플릿·스크립트에서 읽는다.

## 생성·검증

```bash
python3 scripts/build-skill-spell-2023.py
python3 scripts/build-skill-spell-2023-vault.py
python3 scripts/build-skill-spell-2023-graph.py
python3 scripts/build-ability-page-data.py
python3 scripts/build-ability-standard.py
python3 scripts/build-ability-standard.py --check
```

2023 워크북의 볼트는 `data/skill-spell-2023-vault/`, Hades 613개 볼트는 `data/game-data/vault-abilities/`, 워크북 그래프는 `data/skill-spell-2023/graph/`에 각각 남는다. 관리 페이지의 무도가 표도 더 이상 XLSX를 직접 읽지 않고 `skills.json`의 `도가/ordinary` 행을 읽는다.
