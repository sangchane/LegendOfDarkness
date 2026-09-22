# 2023 기술·마법 표

사용자가 제공한 `docs/skill_spell/어둠기술표(2023.01.03).xlsx`의 별도 계보다. Hades `SClass1~5`에서 난 원작 613개 표와 섞지 않는다.

단일 출처는 `data/skill-spell-2023/skills.json`이다. 이 JSON은 표의 일반·어빌리티 행만 보존하며 A:E/R:X 비용 계산기와 수식 보조 영역은 읽지 않는다. 비용은 숫자(`gold`)와 원문 셀(`cost_text`)을 함께 남겨 `기본기술`·`승급기본기술`을 빈 값으로 만들지 않는다.

계보별 정본·실행 자료·매칭 금지 규칙은 [기술·마법 자료 표준](skill-spell-standard.md)에 있다. 관리 페이지의 무도가 표도 이 JSON을 읽으며, XLSX를 별도로 해석하지 않는다.

```bash
python3 scripts/build-skill-spell-2023.py
python3 scripts/build-skill-spell-2023-vault.py
python3 scripts/build-skill-spell-2023-graph.py
python3 scripts/build-ability-page-data.py
python3 scripts/build-ability-standard.py --check
```

볼트는 `data/skill-spell-2023-vault/`이다. 그래프 산출물은 재생성물 `data/skill-spell-2023/graph/`이며 다음처럼 묻는다.

```bash
graphify query "무도가 어빌리티 다음 등급" --graph data/skill-spell-2023/graph/graph.json --budget 700
```

`도적 (2)`와 `도적`은 같은 직업의 대체 표다. 후자가 `설치형트랩` 한 행을 더 가지므로 정본으로 선택했다. 다른 값 충돌은 병합하거나 정정하지 않고 JSON의 `duplicate_sheet_audit`에 기록한다.
