# 서버팩 자료 — 방법과 규칙

원작 자료(`docs/game-data.md`)와는 **다른 것**이다. 그쪽은 원작이 배포한 표, 이쪽은 남이 원작을
고쳐 돌리던 서버의 데이터베이스다. 섞지 않는다.

**세부는 문서에 없다. vault 에 있다** — `data/server-packs/vault/<팩>/` 를 Obsidian 으로 열면
아이템·맵·퀘스트·엔진 명령이 한 장씩 있고 서로 이어져 있다. 여기 있는 것은 그것을 어떻게
만들었고 무엇을 믿을 수 있는가다.

- 팩별 요약: **[5.99 서버팩](server-packs/5.99-server.md)** · **[혼든커뮤니티팩2](server-packs/honden-community.md)** · **[NovaOnline 서버팩](server-packs/novaonline.md)**
- 단일 출처: `data/server-packs/extracted/<팩>/*.json` (커밋됨). vault·graph 는 커밋하지 않는다

## 만드는 순서

```
python3 scripts/build-server-pack-data.py                        # db/ → JSON
python3 scripts/build-server-pack-quests.py                      # 스크립트 → 퀘스트·이벤트
python3 scripts/extract-script-commands.py <서버.exe> <팩>        # 실행파일 → 명령표
python3 scripts/classify-script-names.py                         # 부르는 이름 갈래짓기
.venv/bin/python scripts/disasm-script-commands.py <서버.exe> <팩>  # 기계어 → 증거
python3 scripts/build-server-pack-vault.py                       # → Obsidian (팩마다 따로)
python3 scripts/build-server-pack-graph.py                       # → 지식 그래프 (Windows도 자동 탐지)
```

마지막 증거 단계만 `capstone`·`pefile` 이 필요하다 (시스템 파이썬 금지, PEP 668):
`python3 -m venv .venv && .venv/bin/pip install capstone pefile`

## 팩마다 따로 두는 이유

세 서버팩은 따로 둔다. 5.99와 혼든만 보아도 괴물이 242종과 821종인데 **이름이 겹치는 것이
35종뿐**이다. 아이템도 989 대 3,138 에 겹침 449.
엔진까지 다르다 — `Novaonline.exe` 401개 명령, `Yuki.exe` 500개. `last_npc` 는 Nova 에 아예 없다.
묶으면 어느 쪽 수치인지 알 수 없게 된다.

## 여섯 가지 규칙

1. **매니페스트가 정답지다.** `*_db.txt` 가 서버가 무엇을 어떤 갈래로 읽을지 직접 적어 둔다
   (`spawn:db/npc/시장_spawn.txt`). 파일 이름으로 짐작하면 틀린다 — 같은 "NPC 배치"가 팩마다
   `Spawn.txt` 이고 `노점_spawn.txt` 다. 이름으로 찾다가 혼든의 NPC 배치 517개를 통째로 놓쳤다.
   선언되지 않은 파일은 서버가 읽지 않는다 → `매니페스트에없는파일.json`.
2. **간선은 자료 안에 있다.** 워프가 맵→맵, 젠이 맵→괴물, 상점이 상점→아이템, 스크립트 본문의
   `warp "…"`·`item_add "…"` 가 대상을 직접 부른다. 짐작으로 잇지 않는다.
3. **퀘스트는 표가 아니라 스크립트에 있다.** `#` 로 시작하는 영속변수 하나가 퀘스트 하나,
   정수값이 단계다. 보상의 귀속은 그 `set` 이 어느 `if(#변수==n)` 안에 있는지로 정한다.
4. **명령의 뜻은 실행파일에 있다.** `sources/` 16개에는 없다(`buildin_` 검색 0건 — Dark Ages
   계열이라 혈통이 다르다). eAthena 식 `{ 함수, 이름, 인자서명 }` 12바이트 표가 `.rdata` 에 남는다.
5. **칸 이름은 원본 그대로.** `내구력`·`콘변화` 는 db 에 그렇게 적혀 있다. 확인 못 한 칸은
   `fields` 에 원문대로 남긴다.
6. **모르는 것은 모른다고 적는다.** 불렸는데 정의가 없는 이름은 vault 에 `정의없음: true` 로,
   갈래를 못 가린 이름은 `가려내지못함` 으로 남긴다. 기계어에서 뽑은 것은 "이 함수가 `+0xCC` 에
   쓴다" 까지다 — `+0xCC` 가 체력인지는 코드가 말해 주지 않는다.

## 네 번 헤맸다

- **색상 코드가 중괄호 깊이를 망가뜨린다.** 대사에 `{=q` 가 박혀 있다. 그냥 세면 깊이가
  어긋나고, 한번 어긋나면 조건이 파일 끝까지 쌓여 한 퀘스트에 보상 46종이 붙는다.
  문자열을 먼저 걷어내야 한다.
- **정수가 아닌 값을 넣는 변수를 놓쳤다.** `set #X, localtime(3)` 같은 것 59종.
- **인자 0개 명령은 서명이 문자열로 안 풀리는 팩이 있다**(Novaonline). 확실한 것만 주우면
  그런 명령이 연달아 나오는 구간에서 표가 끊긴다. 칸이 이어지는 동안 양쪽으로 늘려 잡아야 한다.
- **`@name` 은 변수지 명령이 아니다.** 느슨하게 세어 "설명 안 되는 이름" 이 93개 나왔는데
  전부 변수·대사·주석이었다. 제대로 세니 0 이다.

## 함정

- **macOS 는 파일이름의 대소문자를 구분하지 않는다.** 혼든에 `루어스은행A` 와 `루어스은행a` 가
  둘 다 있다 — 서로 다른 맵이다. vault 는 겹치면 `~2` 를 붙이고 빌드 때 출력 폴더를 먼저 비운다.
- **원본은 CP949 다.** 서버를 실제로 돌릴 때는 원본 zip 을 쓴다 (`data/server-packs/README.md`).
- **빌드 스크립트는 자기 산출물만 지운다.** 한 폴더에 여러 스크립트가 쓴다.
