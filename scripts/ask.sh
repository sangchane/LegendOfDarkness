#!/bin/bash
# 게임 자료를 한 번에 묻는다 — 이름이 맞는 볼트 노트를 먼저 보이고, 게임 그래프(graphify)에 물은 답을 잇는다.
#
#   scripts/ask.sh 윙부츠                  아이템 — 어둠템 대 서버 수치 · 누가 떨구나 · 누가 파나
#   scripts/ask.sh 아돌                    NPC — 자리 · 역할 · 하는 일 · 물목 · 가르침
#   scripts/ask.sh 아벨해안4-A             사냥터 — 서클 · 괴물 · 드랍
#   scripts/ask.sh 서클3                   서클 — 그 레벨 띠의 사냥터 · 상점 · 장비
#   scripts/ask.sh --코드 HandleBuy        서버·앱·참고 저장소 코드 그래프(graphify-out, `graphify .` 로 만든다)
#
# 사용자 2026-10-09 「어둠템관련해서 스펙 정리한것 엔피시나 기타 게임데이터 graphify 랑 볼트로 정리한 데이터끼리 연결고리 정리해서
# 작업할때 계속 같은 내용 찾거나 맥락 이해 못 하는 일 없도록 해」 — 게임 자료(아이템·NPC·맵·괴물·드랍·상점·서클·기술)를 다루기 전에
# 이것부터 돌린다(AGENTS.md). 어디에 무엇이 있는지 전체 지도는 docs/knowledge-map.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VAULT="$ROOT/data/game-vault"
GRAPH="$VAULT/graph/graph.json"
BUDGET="${ASK_BUDGET:-1500}"

if [ "${1:-}" = "--코드" ]; then
    shift
    exec graphify query "$*" --graph "$ROOT/graphify-out/graph.json" --budget "$BUDGET"
fi

if [ $# -eq 0 ]; then
    sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'
    exit 2
fi

question="$*"

# 그래프는 무시 목록이라 없을 수 있다 — 볼트와 함께 다시 만든다(1분 안팎).
if [ ! -f "$GRAPH" ]; then
    echo "(게임 그래프가 없어 만든다 — scripts/gen/vault/build-game-vault.py --그래프)" >&2
    python3 "$ROOT/scripts/gen/vault/build-game-vault.py" --그래프 >/dev/null
fi

# 1) 이름이 맞는 노트 — 게임 볼트(아이템·NPC·사냥터·괴물·서클)와 기술·식·자료출처 볼트. 꼭 맞는 이름이 먼저.
found=$(
    for vault in "$VAULT" "$ROOT/data/game-data/vault-abilities" "$ROOT/data/formula-vault" "$ROOT/data/truth-vault"; do
        [ -d "$vault" ] || continue
        find "$vault" -name "${question}.md" -o -name "${question}@*.md"
        find "$vault" -name "*${question}*.md"
    done | awk '!seen[$0]++' | head -6
)
if [ -n "$found" ]; then
    echo "== 노트"
    first=1
    while IFS= read -r note; do
        echo "-- ${note#"$ROOT/"}"
        if [ "$first" = 1 ]; then
            sed -n '1,45p' "$note"
            first=0
        fi
    done <<< "$found"
    echo
fi

# 2) 그래프 — 이름표에 레벨·수치·역할·서클이 실려 있어 이어진 것까지 한 번에 보인다.
echo "== 그래프 (${GRAPH#"$ROOT/"})"
graphify query "$question" --graph "$GRAPH" --budget "$BUDGET"
