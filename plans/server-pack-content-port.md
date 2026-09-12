# 서버팩 콘텐츠를 Hades 에 이식한다

- 다시 쓴 날: 2026-09-12 (2판 — 적대적 검토에서 나온 결함을 반영)
- 작업 브랜치: `test/hades-characterization` · 기본 브랜치 `main` · CI 없음(검증은 손으로 실행)
- 서버는 submodule fork (`kimsangchan/Dark-Ages-Private-Server`, 브랜치 `fix/run-on-macos`).
  **원본에 직접 push 하지 않는다** — fork 에서 고치고 루트는 포인터만 옮긴다 (`WORKFLOW.md`).
- 배경 사실은 `docs/what-hades-already-has.md`. **단계를 시작하기 전에 그것부터 읽는다.**
- `data/server-packs/` 아래는 다른 사람이 맡는다. **쓰지 않는다.** 읽기만 한다.

---

## 한 줄

**Hades 는 규칙이 있고 내용이 없다.** C# 스크립트 115장이 이미 돈다. 이 작업은 시스템을
만드는 일이 아니라 **자료를 붓는 일**이다. 팩 스크립트는 옮기는 게 아니라 **읽는 참고서**다.

---

## 넣을 수 있는 것 — 실측 (이 표가 모든 완료 기준의 근거다)

| 갈래 | 팩에 있는 것 | **넣을 수 있는 것** | 왜 줄어드나 | 기존 | 로그 목표 |
|---|---|---|---|---|---|
| 맵 | 807 | **804** | 크기 모순 3 (아래 A) | 4 | `Map Templates Loaded: 808` |
| 워프 | 1,109 | **886** | 끝이 안 풀림 222 · 완전 중복 1 | 4 | `Warp Templates Loaded: 890` |
| 아이템 | 989 | **989** | — (이름 전부 유일) | 3 | `Item Templates Loaded: 992` |
| 괴물 배치 | 570쌍 | **565** | 괴물 정의 없음 5 | 3 | `Monster Templates Loaded: 568` |
| NPC 배치 | 179 | **84** | 정의 없음 95 (스크립트가 만듦) | 0 | `Mundane Templates Loaded: 84` |
| 기술 | 82 | **82** | — | 1 | `Skill Templates Loaded: 83` |
| 마법 | 71 | **71** | — | 0 | `Spell Templates Loaded: 71` |
| 월드맵 | 1 (27노드) | **1** | — | 1 | `World Map Templates Loaded: 2` |
| 문 | 1 | **1** | — | 0 | (로그 없음 — 게임 안 확인) |
| 상점 | 47 | **미정** | NPC 결합이 자료에 없다 (아래 B) | 0 | (9b 에서 정한다) |
| 함정·배우기 | **0** | — | 팩에 없다. 뺀 게 맞다 | — | — |

### A. 맵 3건은 원리적으로 못 넣는다 — 미리 이름을 적어 둔다

한 파일이 서로 다른 두 크기로 선언돼 있다. 한 파일이 두 크기일 수 없으므로 한쪽은 반드시 걸린다.

| 파일 | 선언 1 | 선언 2 | 버리는 것 |
|---|---|---|---|
| `default/maps/lod3713.map` | 집털 1-1~1-4 (25×25) | **해안가 3** (50×50) | 해안가 3 |
| `default/maps/lod3716.map` | 집털 1-5 (25×25) | **해안가 5** (50×50) | 해안가 5 |
| `서쪽대륙/maps/lod10287.map` | 흉가1층 (29×45) | **흉가2층** (29×50) | 흉가2층@lod10287 |

`흉가2층` 은 `서쪽대륙/maps/lod10293.map`(29×45) 짜리가 **따로 또 있다.** lod10287 쪽이 팩의 오기다.
그것만 버리면 **크기 모순과 이름 중복이 동시에 풀린다.**

남는 이름 중복은 둘: `연습장`(default/lod1956 · 노비스마을/lod4666),
`뤼케시온필드`(rucesion/lod505 · rucesionfield/lod505). **이 이름들을 부르는 워프·젠은 0건**이라
개명해도 참조가 안 깨진다.

### B. 상점은 NPC 에 붙일 수가 없다 — 결합이 스크립트 안에 있다

```
상점 이름 47종 ∩ NPC 정의 31종 = 0
상점 이름 47종 ∩ 8단계가 놓는 84 배치 = 0
상점 갈래: 물건사기 40 · 물건팔기 7
```
상점 이름은 `각반사기`·`귀걸이사기`·`기능성아이템` 처럼 **목록 이름**이지 NPC 이름이 아니다.
"어느 NPC 가 어느 목록을 연다"는 **팩 스크립트 안에** 있다.

또 `shop1.cs` 의 `DefaultMerchantStock` 은 **파는 목록**이다. 사는 쪽(`0x0002`)은 목록을 안 쓰고
인벤토리를 받는다. **`물건팔기` 7개를 `DefaultMerchantStock` 에 넣으면 뜻이 뒤집힌다.**

→ 상점은 2단계(스크립트 읽기)가 끝나야 시작할 수 있다. 9단계를 둘로 나눈다.

---

## 전체 그림

```
0 관문: 맵 파일 521개 ──┬── 3 전역 번호 ── 4 맵 ──┬── 5 워프 ── 5b 월드맵·문
                        │                         ├── 7 괴물 배치
1 적재기 ───────────────┴── 6 아이템 ─────────────┴── 8 NPC 배치 ──┬── 9a 기술·마법
                                                                    └── 9b 상점 ── 10 스모크
2 명령·스크립트 읽기 ───────────────────────────────────────────────┘
```

동시에 할 수 있는 것: **{0, 1, 2}** → **{3, 6}** → **4** → **{5, 7, 8}** → **{5b, 9a, 9b}** → **10**

자료가 fork 안이므로 **모든 단계가 fork 커밋 + 루트 포인터 커밋 한 쌍**으로 끝난다.

---

## 모든 단계가 지키는 것

1. **완료 기준은 위 표의 수다.** "넣으려던 수"가 아니라 **"넣을 수 있는 수"** 로 잰다.
2. **끊긴 참조는 0건이 아니라 기준선이다.** 3단계가 `plans/5.99-기준선.json` 에 박아 커밋한다:
   워프 미해결 이름 **30종** · NPC젠 미정의 **95건** · 상점 미정의 아이템 **23종** · 괴물젠 미정의 **5건**.
   **기준선과 같으면 통과, 늘면 실패.**
3. **파일 이름은 소문자로 쓴다.** `TemplateStorage.Load` 가 디렉터리에서 찾은 경로를 안 쓰고
   `StoragePath/{name.ToLower()}.json` 을 **다시 조립한다**(`TemplateStorage.cs:211`).
   대문자가 섞이면 대소문자 구분 파일시스템에서 **조용히 null → 건너뜀**이다.
   (괴물만 예외 — `fixedPath` 라 하위 폴더·임의 파일명이 된다.)
4. **파일명 금지문자를 정화한다.** 맵 `뮤레칸의역습::밀레스` 하나가 `:` 를 가졌다.
   macOS 는 넘어가지만 Windows 에서 깨진다.
5. **시험을 같이 고친다.** `MapIntegrityTests.cs:16` 이 `AreasShipped = 4` 를 하드코딩하고
   `IsolatedHadesServer.cs:61` 이 실제 `database/server` 를 통째로 복사한다.
   **4단계가 맵을 넣는 순간 이 시험 2개는 반드시 깨진다.**
6. **되돌리기는 폴더 삭제 + 재기동.**

---

## 0. 관문 — 맵 파일 521개를 가져온다

**이게 없으면 3·4·5·7·8·9 를 못 한다. 통과 못 하면 계획을 멈춘다.**

### 맥락
맵 바이너리는 이 Mac 에 없다. `5.99 서버팩.zip`(3,270 엔트리, Windows PC)에서 텍스트 238개만
꺼내 두었다. 저장소의 `.map` 2,241개는 **7.18 계보**라 쓸 수 없다 — 팩 이름 448개 중
이름·크기가 맞는 것 131개, 이름만 같은 다른 맵 93개, 없는 것 224개.

### 할 일
1. Windows PC 에서 `5.99 서버팩.zip` 을 **CP949 그대로** 푼다(그냥 풀면 폴더 이름이 깨진다).
2. `plans/5.99-필요한-맵파일.tsv` 의 1열 경로 **521개**를 꺼낸다.
3. 2열 기대 바이트와 대조한다.
4. 이 Mac 으로 옮긴다. 최종 목적지는 4단계가 넣는다.

### 검증
```bash
python3 - <<'PY'
from pathlib import Path
src = Path("<가져온 폴더>")
rows = [l.split("\t") for l in Path("plans/5.99-필요한-맵파일.tsv").read_text(encoding="utf-8").splitlines()
        if l and not l.startswith("#")]
missing = [r[0] for r in rows if not (src / r[0]).exists()]
wrong = [(r[0], (src / r[0]).stat().st_size, int(r[1])) for r in rows
         if (src / r[0]).exists() and (src / r[0]).stat().st_size != int(r[1])]
print(f"필요 {len(rows)} · 없음 {len(missing)} · 크기 다름 {len(wrong)}")
for w in wrong[:15]: print("  ", w)
PY
```

### 완료 기준
- 필요 **521** · 없음 **0** · 크기 다름 **0**
- **크기가 다른 것이 20개를 넘으면 zip 이 다른 판이다.** 3단계 전에 zip 을 다시 확인한다.
- TSV 는 A 의 세 파일에서 이미 작은 쪽 크기를 골라 두었다. **그 선택이 여기 통과의 조건이다.**

### 되돌리기
가져온 폴더를 지운다. 저장소를 건드리지 않는다.

---

## 1. 적재기 — 되풀이 가능한 한 길 (0 없이 가능)

**갈래마다 손으로 여덟 번 하지 않기 위한 단계다. 이 계획의 중심이다.**

### 맥락
넣을 것이 9갈래 3,400건이 넘는다. 갈래마다 따로 스크립트를 쓰면 아홉 번 같은 실수를 한다.
입력은 전부 `data/server-packs/extracted/<팩>/*.json` 한 모양, 출력은 전부
`templates/<갈래>/<이름>.json` 한 모양이다.

### 할 일
`tools/pack-import/` 를 만든다 (Python, `scripts/build-server-pack-data.py` 와 같은 결).

- 입력: `extracted/<팩>/<갈래>.json` + 3단계 번호 표 · 출력: fork 의 `templates/` (맵은 `areas/`)
- **갈래마다 다른 것은 칸 대응표 하나뿐.** 읽기·검사·쓰기·보고는 공용.
- `--dry-run` 이 기본. 쓰기는 `--write`.
- 파일명은 **소문자 + 금지문자 정화**(공통 규칙 3·4).
- 늘 보고서를 낸다: **넣을 수 · 못 넣는 수와 그 이유 · 이름 겹침 · 미해결 참조(이름을 다 적는다)**.
- **뜻이 확인된 칸만 대응시킨다.** 모르는 칸은 옮기지 않고 보고서에 남긴다.

### 검증
```bash
python3 tools/pack-import/import.py --pack 5.99-server --kind items --dry-run | tail -20
```

### 완료 기준
- 9갈래 전부 `--dry-run` 이 돌고, **"넣을 수"가 위 실측 표와 정확히 같다**
  (맵 804 · 워프 886 · 아이템 989 · 괴물 565 · NPC 84 · 기술 82 · 마법 71 · 월드맵 1 · 문 1)
- 못 넣는 것의 수와 이유도 표와 같다 (맵 3 · 워프 223 · 괴물 5 · NPC 95)
- 아직 **아무것도 쓰지 않는다**
- 같은 명령을 두 번 돌리면 보고서가 똑같다(결정적)

### 되돌리기
`tools/pack-import/` 를 지운다.

---

## 2. 명령·스크립트 읽기 (독립 — 8·9 의 선행조건)

### 맥락
팩 스크립트 289개는 Novaonline/Yuki 네이티브 엔진 전용 언어다. Hades 는 C# 을 컴파일한다.
**번역기를 만드는 일이 아니다.** 엔진 명령 401개는 `vault/5.99-server/명령/` 에 이미 정리돼 있고,
Hades 는 C# 스크립트 115장을 가지고 있다(`SpellScript` 40 · `SkillScript` 26 · `MundaneScript` 25 ·
`ItemScript` 11 · `MonsterScript` 4).

### 할 일
1. 명령 401개를 갈래로 나눈 표 → `docs/pack-command-map.md`
   **있다** / **없다, 만들 수 있다** / **불가능·무의미** / **모르겠다(수를 적는다)**
2. **상점 ↔ NPC 결합을 복원한다** → `plans/5.99-상점결합.tsv`
   (`상점이름 <TAB> 여는 NPC <TAB> 놓인 맵 <TAB> 갈래(사기/팔기) <TAB> 근거 스크립트`)
   스크립트 본문은 `data/server-packs/5.99-server/db/script/**/*.txt` 에 있다.
   **9b 가 이것 없이는 시작할 수 없다.**
3. 8단계가 쓸 `NPC → ScriptKey` 대응도 여기서 뽑는다. 확실하지 않으면 **비워 둔다.**

### 검증
```bash
ls data/server-packs/vault/5.99-server/명령/*.md | wc -l     # 401
grep -c "^| " docs/pack-command-map.md
wc -l plans/5.99-상점결합.tsv
```

### 완료 기준
- 401개가 모두 네 갈래 중 하나에 들어갔다
- 상점 47종 각각에 대해 "여는 NPC 를 찾았다 / 못 찾았다" 가 적혔고 **찾은 수가 명시됐다**
- `물건팔기` 7개가 **따로 표시**됐다 (`DefaultMerchantStock` 이 아니다)

### 되돌리기
문서·표를 지운다.

---

## 3. 전역 맵 번호 표 (0 필요)

### 맥락
Hades 는 맵을 **전역 정수 하나**로 센다(`GlobalMapCache` 는 `Dictionary<int, Area>`,
`AreaStorage` 는 `maps/lod{Id}.map` 을 평평한 폴더에서 찾는다). 팩은 **폴더마다** 번호를 매긴다 —
맵 807개가 정수 번호 **445개**를 나눠 쓰고, 번호 1 하나를 **11개 맵**이 쓴다.
**번호를 열쇠로 삼으면 맵이 사라진다.** 팩 번호 1·2·3 은 Hades 것과도 겹친다.

### 할 일
1. `plans/5.99-맵번호표.tsv`: `출처<TAB>이름<TAB>새번호<TAB>맵파일경로<TAB>넣나(Y/N)<TAB>사유`
   - 신원은 **`맵파일` 경로 + `이름`**. 팩 번호는 쓰지 않는다.
   - 새 번호는 **100000 부터** (1~99999 는 Hades 것).
   - **A 의 3건은 `N` 으로 표시하고 사유를 적는다.**
   - 맵마다 다른 번호다(맵마다 워프·젠·이름이 다르므로). 파일은 번호마다 복사한다.
2. 이름 중복 둘(`연습장`·`뤼케시온필드`)을 개명한다. 이 이름을 부르는 워프·젠은 0건이라 안전하다.
3. 파일명 금지문자를 정화한다(`뮤레칸의역습::밀레스`).
4. **기준선을 박는다** → `plans/5.99-기준선.json`
   워프 미해결 이름 **30종(이름을 다 적는다)** · NPC젠 미정의 **95건** ·
   상점 미정의 아이템 **23종** · 괴물젠 미정의 **5건**.

### 검증
```bash
python3 - <<'PY'
import collections
rows=[l.split("\t") for l in open("plans/5.99-맵번호표.tsv",encoding="utf-8").read().splitlines()
      if l and not l.startswith("#")]
put=[r for r in rows if r[4]=="Y"]
ids=[int(r[2]) for r in put]
print("전체",len(rows),"· 넣는 것",len(put),"· 번호 유일",len(set(ids))==len(ids))
print("100000 미만",[i for i in ids if i<100000])
n=collections.Counter(r[1].lower() for r in put)
print("이름 겹침",[k for k,c in n.items() if c>1])
print("금지문자",[r[1] for r in put if any(c in r[1] for c in ':\\/*?\"<>|')])
PY
```

### 완료 기준
- 전체 **807** · 넣는 것 **804** · 번호 유일 · 100000 미만 **0** · 이름 겹침 **0** · 금지문자 **0**
- `plans/5.99-기준선.json` 의 네 수가 **30 / 95 / 23 / 5**

### 되돌리기
표 두 개를 지운다.

---

## 4. 맵 804 — 세계의 바닥 (0·1·3 필요)

### 맥락
Hades 에 맵이 4개뿐이다. 여기부터 워프·젠·NPC 가 설 자리가 생긴다.
맵 적재는 길이를 검사한다(`AreaStorage.LoadMap`). 안 맞으면 이름을 대고 거른다.

### 할 일
1. `--kind maps --write` — `areas/<이름>.json` 804장 (`Id` 는 3단계 표에서, 파일명 소문자)
2. `maps/lod{번호}.map` 804개 — 원본을 번호마다 복사
3. **시험을 고친다** (공통 규칙 5):
   - `MapIntegrityTests.AreasShipped` 를 하드코딩에서 **실제 `areas/*.json` 수를 세는 것**으로 바꾼다
   - 그래야 다음 단계에서 또 안 깨진다
4. **지역(폴더) 단위로 3~4개 PR 로 쪼갠다.** 807 영역 + 11.7MB 바이너리를 한 번에 넣지 않는다.
   abel / rucesion / 서쪽대륙 / 나머지 식으로. 사고가 나도 그 지역에 갇힌다.

### 검증
```bash
grep -E "Map Templates Loaded|Not loaded" <서버로그>
ls sources/.../database/server/areas/*.json | wc -l    # 808
python3 -c "
import json,glob,collections
ids=[json.load(open(f,encoding='utf-8-sig'))['Id'] for f in glob.glob('sources/.../areas/*.json')]
print('영역', len(ids), '· Id 중복', [k for k,c in collections.Counter(ids).items() if c>1])"
```

### 완료 기준
- `Map Templates Loaded: 808` (팩 804 + 기존 4) · `Not loaded` **0건**
- **`areas/*.json` 파일 수 == 808 이고 `Id` 중복 0.**
  (서버가 기동마다 `areas/` 를 `Name.ToLower()` 로 다시 쓰므로 — `AreaStorage.cs:82,125` —
  이름이 겹치면 **첫 기동에 이미** 파일이 덮인다. 캐시는 `Id` 열쇠라 수가 안 줄어
  "두 번째 기동에 수가 준다" 로는 **절대 못 잡는다.** 파일 수와 Id 중복으로 잡는다.)
- 기동 시간과 메모리를 적는다
- 시험 62개가 다시 전부 통과한다

### 되돌리기
`areas/`·`maps/` 에서 번호 100000 이상인 것과 그 이름의 json 을 지우고 재기동.

---

## 5. 워프 886 — 맵을 잇는다 (4 필요)

### 맥락
워프 템플릿은 이름이 아니라 **번호**로 맵을 가리킨다
(`ActivationMapId`, `Activations[].AreaID`, `To.AreaID`). 그래서 3단계 표가 있어야 한다.

1,109줄 중 **222줄은 한쪽 끝이 `maps.json` 에 없다**(출발맵 없음 93 · 도착맵 없음 201).
끝을 번호로 못 바꾸니 템플릿을 만들 수 없다. 완전 중복 1줄까지 빼면 **886**.

**개수로는 검증할 수 없다.** `WarpStorage.cs:35` 가 `GlobalWarpTemplateCache.Add(obj)` 를
**null 검사 없이** 한다 — 개수는 언제나 파일 수와 같다. 게다가 `WarpStorage` 에는
**try/catch 가 없어** 깨진 JSON 은 null 이 아니라 **기동을 터뜨린다**(`AreaStorage.Load` 에는 있다).

### 할 일
1. **먼저 fork 를 고친다**: `WarpStorage.Load` 에 try/catch 를 넣고, `Add` 앞에 null 을 거르고,
   못 읽은 파일 이름을 찍는다. 재현 시험을 붙인다 — **고치기 전에 실패하는 것을 먼저 확인한다**
   (지금은 "개수가 준다"가 아니라 "기동이 터진다"가 정답이다).
2. **워프 이름 규칙을 정한다.** `WarpStorage.Save` 가 `Name.ToLower()` 로 파일을 쓴다(`:52`).
   `warp {출발맵}({x},{y}) to {도착맵}` 꼴로 짓고 소문자·금지문자 정화를 건다.
3. `--kind warps --write` — 886장.

### 검증
```bash
grep -E "Warp Templates Loaded|not loaded" <서버로그>
```
서버가 뜬 뒤 `GlobalWarpTemplateCache` 를 훑어 `ActivationMapId`·`To.AreaID` 가
`GlobalMapCache` 에 있는지 센다.

### 완료 기준
- `Warp Templates Loaded: 890` (팩 886 + 기존 4) · 못 읽은 워프 **0** · 캐시 안 null **0**
- **런타임에 없는 AreaID 를 가리키는 끝 0건.** (이름 30종은 런타임에 셀 수 없다 — 이름이 번호로
  안 바뀐 끝은 애초에 AreaID 가 없다. 30종은 **적재기 보고서**에서 대조한다.)
- 적재기 보고서의 미해결 이름이 **기준선 30종과 정확히 같다**
- 게임 안에서 한 번 넘어가 본다

### 되돌리기
`templates/warps/` 에서 넣은 파일을 지우고 재기동. **try/catch 와 null 가드는 남긴다**(버그 수정).

---

## 5b. 월드맵 1 · 문 1 (5 필요)

### 맥락
**빼면 안 된다.** 팩 월드맵 `마이소시아`(27노드)는 매달린 이름 30종 안에 들어 있다.
`우드랜드대기실`·`야외배틀필드` 도 월드맵 노드 쪽이다. 이 단계를 빼면 그 워프들은
**영원히** 못 붙고, 기준선 30이 그 영구 고장에 "통과" 도장을 찍는다.

Hades 는 월드맵 워프를 이미 지원한다 — `WarpTemplate.WorldResetWarpId`/`WorldTransionWarpId`,
기존 `warp from 2 to world map..json`, `GlobalWorldMapTemplateCache`(열쇠는 `FieldNumber`).

### 할 일
`--kind worldmaps --write` · `--kind doors --write`

### 완료 기준
- `World Map Templates Loaded: 2` (팩 1 + 기존 1)
- 월드맵 노드 27개가 가리키는 맵이 전부 `GlobalMapCache` 에 있다
- **기준선 30종 중 월드맵으로 풀린 이름이 몇 개인지 적는다.** 30이 줄어든다
- 게임 안에서 월드맵을 열어 한 곳으로 간다

### 되돌리기
넣은 파일을 지우고 재기동.

---

## 6. 아이템 989 (1 필요 — 0·3·4 없이 가능, 병렬)

### 맥락
맵과 무관한 유일한 갈래다. 캐시가 `GlobalItemTemplateCache[Name]` 이라 이름이 겹치면 덮이는데,
**989개는 전부 서로 다른 이름이고 대소문자 충돌·금지문자도 0**이라 안전하다.
그림 번호는 갈래마다 기준이 다르다(`Image`/`DisplayImage`, `ItemImageFlag = 0x8000`,
`FramesPerItemFile = 266` — `tools/dat-extract/Program.cs:19-20`). **뜻이 확인된 칸만 옮긴다.**

### 할 일
`--kind items --write` — `templates/items/<소문자 이름>.json` 989장

### 완료 기준
- `Item Templates Loaded: 992` · **파일 수 == 서로 다른 Name 수 == 989**
- 게임 안에서 GM 명령으로 하나 만들어 들어 본다(`give <이름>`)

### 되돌리기
`templates/items/` 에서 넣은 파일을 지우고 재기동.

---

## 7. 괴물 배치 565 (4 필요 — 6은 스모크에만 필요)

### 맥락
**Hades 의 괴물 템플릿은 "종류"가 아니라 "배치"다.** `MonsterTemplate` 이 `AreaID`·`SpawnMax` 를
직접 가지고, `MonolithComponent` 가 `templates.Where(i => i.AreaID == map.Id)` 로 골라
`count < SpawnMax` 로 개체 수를 맞춘다. 그래서 파일 수는 **서로 다른 `(맵, 괴물)` 쌍**이다 —
671줄에 중복 101, 그중 **괴물 정의가 없는 5쌍**(`슬러그` 3 · `팜팻` 2)을 빼면 **565**.

**전리품 때문에 6이 먼저일 필요는 없다.** `Drops` 는 서버 코드가 안 읽고 스크립트가 읽으며
(`scripts/Creations/monsters.cs:204`, `Formulas/monsterexp.cs:112`) 둘 다
`GlobalItemTemplateCache.ContainsKey` 로 거른다. 6이 필요한 곳은 10단계 스모크뿐이다.

### 조심할 것
- **`mob_spawns.json` 에 좌표가 없다**(`{맵, 괴물, 마리수}` 뿐). `SpawnType` 을 Defined 로 두면
  전부 (0,0) 에 뭉친다. **Random 계열로 두고 `SpawnMax` 에 마리수를 넣는다.**
- **`레아로4` 는 정의가 2개다**(같은 파일, 이미지 383 vs 815). `{괴물}@{맵}` 파일명은 이걸 못 푼다 —
  **어느 스탯을 쓸지 정하는 규칙을 따로 적고 보고서에 남긴다.**
- 중복 101쌍 중 100쌍은 완전 동일하지만 `포테의숲5존/놀` 한 쌍만 마리수가 12·8 로 다르다. 합칠 때 정한다.
- **`LootType` 에 Random(2)을 켜면서 `Drops` 를 비우면 괴물을 잡을 때마다 예외가 난다**
  (`Formulas/monsterexp.cs:112-115` 가 빈 리스트에 `Drops[0]`). 기존 `minion.json` 은 36(Gold|Table)이라 안 탄다.

### 할 일
`--kind monsters --write` — `templates/monsters/<괴물>@<맵>.json` 565장
(괴물만 `fixedPath` 라 하위 폴더·임의 파일명이 된다 — `TemplateStorage.cs:71,113`)

### 완료 기준
- `Monster Templates Loaded: 568` (배치 565 + 기존 3) · 파일 수도 568
- 모든 `AreaID` 가 `GlobalMapCache` 에 있다
- 정의 없는 젠이 **기준선 5건과 같다**
- `LootType` 에 Random 이 켜졌는데 `Drops` 가 빈 템플릿 **0개**
- 게임 안에서 사냥터 하나에 들어가 한 마리 잡는다 — **예외 없이**

### 되돌리기
`templates/monsters/` 에서 넣은 파일을 지우고 재기동.

---

## 8. NPC 배치 84 — 대화가 열린다 (4 필요)

### 맥락
**NPC 가 세상에 없는 이유는 스크립트가 없어서가 아니다.** `scripts/Mundanes/` 에 25장이 대기 중이고
`templates/mundanes/` 가 비어 있다. JSON 한 장이면 붙는다.

`MundaneTemplate` 도 배치를 직접 가진다(`AreaID`·`X`·`Y`). 캐시는 `[Name]` 이라
이름이 겹치면 배치가 사라진다 → **배치마다 이름을 새로 짓는다**: `{npc}@{맵}#{x},{y}` (소문자).
84건은 **서로 다른 (NPC, 맵, 좌표)** 이고 좌표 중복 0 · 맵 밖 좌표 0 이다.

정의 없는 95건은 스크립트가 만드는 NPC다. 2단계 표를 보고 뒤에 다룬다.

### 할 일
1. `--kind mundanes --write` — 84장. `ScriptKey` 는 **2단계의 `NPC → ScriptKey` 대응**에서.
   확실하지 않으면 **비워 두고 보고서에 남긴다**(엉뚱한 스크립트를 붙이면 더 나쁘다).
2. 최소 한 명은 `simple_generic_npc` 로 확실히 붙여 대화를 먼저 뚫는다.

### 완료 기준
- `Mundane Templates Loaded: 84` · 서로 다른 Name **84**
- 모든 `AreaID` 가 `GlobalMapCache` 에 있다
- **게임 안에서 NPC 를 눌러 대화창이 뜬다.** 진짜 완료 기준은 이것이다
- 정의 없는 95건이 목록으로 남았다

### 되돌리기
`templates/mundanes/` 를 비우고 재기동. 원래 비어 있었다.

---

## 9a. 기술 82 · 마법 71 (6 필요)

### 맥락
캐시가 이름 열쇠다(`GlobalSkillTemplateCache[Name]`, `GlobalSpellTemplateCache[Name]`).
**기술 82·마법 71 은 각각 이름이 유일하고 서로 겹치지도 않는다** — "겹침 0" 이 달성 가능하다.
Hades 에 `SkillScript` 26 · `SpellScript` 40 이 있으니 **이름이 대응되는 것부터** 붙인다.

### 할 일
`--kind skills --write` · `--kind spells --write` (파일명 소문자).
스크립트가 없는 것은 `ScriptName` 을 비우고 목록으로 남긴다.

### 완료 기준
- `Skill Templates Loaded: 83` · `Spell Templates Loaded: 71`
- 각각 파일 수 == 서로 다른 Name == 캐시 수
- 게임 안에서 기술이나 마법을 **하나 쓴다**
- 스크립트 없는 것의 수가 목록으로 남았다

### 되돌리기
넣은 파일을 지우고 재기동.

---

## 9b. 상점 (2·8 필요 — 결합 복원이 선행조건)

### 맥락
B 를 읽어라. **상점 이름은 NPC 이름이 아니다.** 2단계의 `plans/5.99-상점결합.tsv` 없이는
어느 NPC 에 무엇을 붙일지 알 수 없다. 그리고 `물건팔기` 7개는 `DefaultMerchantStock` 이 아니다 —
`shop1.cs` 의 사는 쪽은 목록을 안 쓴다.

**시스템은 이미 있다.** `shop1.cs` 가 `0x0001` 사기 · `0x0002` 팔기 · `0x0003` 수리를 다 하고,
`ScriptKey = "shop1"` 이면 `ScriptManager` 가 `[Script("shop1","Dean")]` 를 찾아 붙인다.
`DefaultMerchantStock` 은 기본값이 빈 리스트라 null 예외도 없다.

### 할 일
1. 2단계에서 결합이 확인된 **`물건사기` 상점만** 그 NPC 의 `DefaultMerchantStock` 에 넣고
   `ScriptKey` 를 `shop1` 로 둔다.
2. `물건팔기` 7개는 **넣지 않는다.** 무엇이 남았는지 목록으로 남긴다.
3. 결합을 못 찾은 상점도 목록으로 남긴다.

### 완료 기준
- **붙인 상점 수 == 2단계가 결합을 찾은 수.** (47 이 아니다 — 47은 달성 불가다.)
- 없는 아이템을 파는 상점: **기준선 23종과 같다**
- **게임 안에서 상점 하나에서 물건을 하나 산다.** 진짜 완료 기준은 이것이다
- 못 붙인 상점과 `물건팔기` 7개가 목록으로 남았다

### 되돌리기
NPC 의 `DefaultMerchantStock` 을 비우고 `ScriptKey` 를 되돌린다.

---

## 10. 10분 스모크 — 사람이 한 번 돌아본다 (전부 필요)

### 할 일 (순서대로, 한 번에)
1. 접속 → 캐릭터 생성 → 세계 입장
2. **워프를 두 번** 넘는다 · **월드맵으로 한 번** 간다
3. 사냥터에서 **한 마리 잡는다** (전리품이 떨어지는지, 예외가 없는지)
4. **NPC 를 눌러 대화창**을 연다
5. **상점에서 하나 산다**
6. 기술이나 마법을 **하나 쓴다**
7. 끊고 다시 들어와 **위치와 소지품이 남아 있는지** 본다

### 완료 기준
- 일곱 개가 다 된다 · 서버 로그에 예외 **0건**
- 시험 전부 통과
- 10명 30분 동시접속: 성공 ≥ 10,000 · 실패 ≤ 5 · 끝날 때 스레드 ≈ 시작값

---

## 계획을 바꿔야 할 때

| 신호 | 무엇을 한다 |
|---|---|
| 0단계에서 521개를 못 모은다 | **멈춘다.** 부분 이식은 워프가 다 끊긴다 |
| 0단계에서 크기가 다른 것이 20개를 넘는다 | zip 이 다른 판이다. 3단계 전에 zip 을 다시 확인 |
| 1단계 "넣을 수"가 실측 표와 다르다 | **표가 틀렸거나 적재기가 틀렸다.** 진행 전에 어느 쪽인지 가린다 |
| 2단계에서 상점 결합을 거의 못 찾는다 | 9b 를 미루고 나머지를 끝낸다. 상점은 스크립트 작업이 된다 |
| 4단계에서 `areas/*.json` 수가 808 이 아니다 | 이름 충돌이 남았다. 3단계로 돌아간다 |
| 5단계에서 미해결 이름이 30종보다 많다 | 번호 표가 틀렸다. 3단계로 돌아간다 |
| 7단계에서 괴물을 잡다 예외가 난다 | `LootType`/`Drops` 조합이다. 위 "조심할 것" |
| 8단계에서 대화창이 안 뜬다 | `ScriptKey` 문제다. 한 명만 손으로 붙여 먼저 뚫는다 |
| 어느 단계든 로그 개수는 맞는데 게임에서 안 보인다 | **개수를 믿지 말고 게임 안 확인을 믿는다** |
