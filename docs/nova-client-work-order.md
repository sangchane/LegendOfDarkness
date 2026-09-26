# 작업지시서 — 노바 클라이언트의 이펙트 그림 가져오기

- 작성: 2026-09-26 (맥 세션)
- 받는 쪽: **Novaonline 원본 팩(`sources/novaonline/`)과 노바로 접속하던 클라이언트가 있는 윈도우 PC 의 세션**
- 끝나면: 브랜치 `data/nova-client` 로 **푸시**한다. 맥 세션이 받아 합친다.

## 1. 왜 하는가

사용자(원작을 기억함): **"노바 것이 원작 이펙트다"**, 그런데 맥에서 노바 번호로 바꿔 보니 **"이펙트는 매칭이 다르다"**.

맥에서 확인한 것:
- 기술 스크립트의 `effect` 번호는 서버가 그대로 0x29 패킷에 싣는다(5.99 계열 `Novaonline.exe` 역어셈블 — 명령 `effect` 0x448576 → 0x46b66a).
  번호 N = 클라이언트 `roh.dat` 의 `efct{N:03d}` 그림이다.
- 노바 스크립트는 기술마다 **옛 번호(200 이하)** 를 쓴다. 5.99·혼든은 같은 기술에 **새 번호(232 이상)** 를 쓴다
  (예: 단각 5.99 249 / 노바 69, 크래셔 299 / 50, 쿠라노 267 / 21).
- **맥에는 노바 클라이언트가 없다.** 맥에 있는 클라이언트 4벌은 `efct` 그림이 번호마다 바이트까지 같다.

그래서 남은 물음은 하나다: **노바 클라이언트의 `roh.dat` 는 같은 번호에 다른 그림을 담고 있나?**
(노바 서버 `Legend.exe` 도 윈도우에만 있다 — `effect` 명령이 번호를 바꾸는지도 여기서 본다.)

## 2. 할 일 (위에서부터)

1. **노바 클라이언트 찾기** — 노바온라인에 접속하던 클라이언트 폴더(`roh.dat`·`setoa.dat`·`legend.dat` 와 실행 파일이 함께 있는 곳).
   `sources/novaonline/` 은 서버 팩이다. 찾은 폴더 경로와 실행 파일 이름·크기·md5 를 적는다. **없으면 "노바 클라이언트 없음"** 이라고 적고 4번으로.
2. **목록 만들기** — 저장소 루트에서(파이썬 3, 표준 라이브러리만):
   ```
   python scripts/dat-manifest.py "<노바클라이언트>\roh.dat"   --out data/client-manifests/nova-roh.json
   python scripts/dat-manifest.py "<노바클라이언트>\setoa.dat" --out data/client-manifests/nova-setoa.json
   python scripts/dat-manifest.py "<노바클라이언트>\legend.dat" --out data/client-manifests/nova-legend.json
   python scripts/dat-manifest.py --compare data/client-manifests/5.99-roh.json data/client-manifests/nova-roh.json
   ```
   맥의 5.99 클라이언트 목록(`data/client-manifests/5.99-*.json`)은 이미 들어 있다. 비교 결과(같음·다름·한쪽에만)를 아래 5번에 붙인다.
3. **다른 것만 꺼내기** — 비교에서 `efct*`·`effect*.tbl`·`effpal.tbl`·`eff*.pal` 중 **다름 또는 노바에만** 인 것이 있으면:
   ```
   python scripts/dat-manifest.py "<노바클라이언트>\roh.dat" --extract "efct*" "effect*.tbl" "effpal.tbl" "eff*.pal" --to data/nova-client/roh
   ```
   (roh.dat 통째는 35MB 라 올리지 않는다. 이펙트 쪽 파일만 — 모두 합쳐도 수십 MB 아래.)
   아이콘 시트(`setoa.dat` 의 `skill001.epf`·`spell001.epf`·`gui06.pal`)가 다르면 그것도 `data/nova-client/setoa/` 로 꺼낸다.
4. **노바 서버 `Legend.exe` 의 `effect` 명령** (여유가 있으면) — 명령표에서 `effect` 처리 함수를 찾아, 스크립트 번호를 그대로 보내는지
   (더하기·표 거치기가 있는지) 주소와 함께 적는다. `docs/server-packs/novaonline.md` 에 명령표 322개를 복원한 기록이 있다.
5. **결과 적기** — 이 파일 끝에 「결과」 절을 더한다: 클라이언트 경로·md5, 비교 셈, 다른 파일 목록, 4번 결론.

## 3. 올리는 것

- `data/client-manifests/nova-*.json`
- (다른 것이 있으면) `data/nova-client/roh/` · `data/nova-client/setoa/`
- 이 파일(결과 절)

커밋: `docs(nova-client): 노바 클라이언트 이펙트 그림 목록·비교` · 브랜치 `data/nova-client` · 푸시.
**`sources/` 아래는 건드리지 않는다**(원본 submodule).

## 4. 맥이 받은 뒤 할 일 (참고)

- 다른 그림이 없으면: 번호는 같은 그림이므로 **이펙트는 5.99 번호로 되돌린다**(`scripts/build-nova-effects.py` 가 바꾼 줄 끝의
  `// 노바 이펙트(5.99: …)` 에 원래 번호가 있다). 아이콘은 노바 번호 그대로(사용자 2026-09-26 "아이콘은 노바가 맞다").
- 다른 그림이 있으면: 앱 이펙트 그림을 노바 클라이언트 것으로 뽑고(`scripts/build-client-effects.py` 입력) 번호는 노바 것을 둔다.

---

# 결과 (2026-09-26, 윈도우 세션)

## 1. 노바 클라이언트 — **없다**

이 PC 를 다 뒤졌다(`roh.dat`·`setoa.dat`·`Legend.exe` 로 C:·D: 전체). 나온 것은 넷이고, **셋이 같은 파일**이다.

| 어디 | roh.dat | 무엇 |
|---|---|---|
| `C:\Users\dev\Downloads\darkness-legend-resources\fixtures\dat` | `f8b88a2b…` 14,298,568B | 2005 원작 클라이언트 한 벌 |
| `D:\_personal\LOD_\raw_data` | `f8b88a2b…` 같은 파일 | 같은 것 |
| `sources/lodr.exe`(2005 설치본) 을 푼 것 | `f8b88a2b…` 같은 파일 | 같은 것 |
| 하데스 `game/roh.dat` · `database/archives/roh/roh.dat` | `c4f2c216…` · `e1d86e88…` | 하데스가 다시 꾸린 것 |

`C:\Users\dev\OneDrive\novaonline\` 이 있지만 **서버 팩이다**(`sources/novaonline/` 과 같은 내용 — `Legend.exe` 544,768B, `db/`, `save/`).
클라이언트 아카이브가 없다. **노바로 접속하던 클라이언트는 이 PC 에 없다.**

## 2. 목록 비교 — 이펙트 그림은 **번호마다 같다**

노바 클라이언트가 없으므로 `nova-*.json` 은 못 만들었다. 대신 **2005 원작 클라이언트**의 목록을 만들어
맥의 5.99 클라이언트와 맞댔다 (`data/client-manifests/orig2005-*.json`).

| 아카이브 | 같음 | 다름 | 5.99 에만 | 2005 에만 |
|---|---:|---:|---:|---:|
| `roh.dat` | 598 | **2** (`effect.tbl` · `effpal.tbl`) | 125 | 0 |
| `setoa.dat` | **480** | 0 | 0 | 0 |
| `legend.dat` | 390 | 6 (아이템 아이콘 시트) | 48 | 0 |

- **`efct*` 그림은 하나도 안 다르다.** 표본으로 `efct010`·`efct086`·`efct143` 을 2005·5.99·하데스 셋에서 재 보니 md5 가 같다.
- 5.99 에만 있는 125개는 **새로 늘어난 것**이다(`efct308`~`efct387`, `eff023~029.pal`, `mefc*.spf`). 2005 에만 있는 것은 없다.
- `effect.tbl` 은 커지기만 했다: 2005 5,920B(첫 줄 `307`) → 5.99 6,749B → 하데스 6,871B.
- **`setoa.dat` 은 480개가 전부 같다** — 아이콘 시트(`skill001.epf`·`spell001.epf`)도 같은 그림이다.

**그러므로 번호 N 은 어느 클라이언트에서나 같은 그림이다.** 노바 전용 그림은 없다.

## 3. 꺼낸 것 — 없음

다른 `efct*`·`eff*.pal` 이 하나도 없으므로 `data/nova-client/` 에 올릴 것이 없다.

## 4. 노바 서버의 `effect` 명령 — **번호를 안 바꾼다**

`sources/novaonline/Legend.exe`(md5 `b909a155…`, sha256 `be1f7686…`)를 역어셈블했다. **노바 실행 파일에 기계어 근거가 붙은 것은 이것이 처음이다**
(`docs/server-packs/novaonline.md`: "Nova에는 아직 기계어 disassembly 증거가 없어").

- **명령표 `0x47c73c`** — 한 칸 12바이트 `{핸들러, 이름, 인자서명}`.
  `effect` = `{0x442430, 0x47d8fc "effect", 0x47d904 "iiii"}`, 그 다음이 `effect2 "iiiii"`.
- **`0x442430` (effect)** — 인자 넷을 `0x4390c4` 로 꺼내 **손대지 않고** `0x45cd18` 로 넘긴다. 더하기도 표 거치기도 없다.
- **`0x45cd18` (0x29 만들기)** — `AA 00 0E 29 00 00 00 | idHi idLo | 대상 4바이트 | 번호 1바이트 | ? | 속도 2바이트`.
  **번호는 `0x48c3ed` 에 1바이트로 그대로 들어간다**(`0x45cdc2`).

스크립트 꼴은 `effect @myid, <번호>, 0, <속도>` 이고, 노바 `db/` 전체에서 쓰인 번호는 **42가지, 0~195 뿐**이다(256 이상 없음) — 1바이트 칸과 맞는다.

근거 발췌: `data/disassembly/excerpts/nova-effect-442430.asm` · 단일 출처 `data/disassembly/findings.json`(`nova-server`).

## 5. 결론

**노바 클라이언트의 `roh.dat` 가 같은 번호에 다른 그림을 담고 있나?** → **아니다(확인할 수 있는 범위에서).**
노바 전용 클라이언트는 찾지 못했고, 손에 있는 클라이언트 넷은 `efct*` 가 번호마다 바이트까지 같다.
노바 서버도 번호를 바꾸지 않는다.

그러니 노바 69 와 5.99 249 는 **정말 서로 다른 그림**이다. 서버·클라이언트 탓이 아니라 팩을 만든 사람이 고른 값이 다른 것이다.
지시서 4절의 갈림길에서 **"다른 그림이 없으면"** 쪽이다.

### 맥이 더 봐야 할 것 하나

노바 엔진의 이펙트 번호 칸은 **1바이트**다. 그런데 5.99 는 `299`(크래셔) 같은 값을 쓴다 — 1바이트에 안 들어간다.
5.99 엔진의 칸 너비가 2바이트인지, 아니면 299 가 43 으로 잘려 나가는지는 **확인 못 했다**(5.99 `Novaonline.exe` 가 이 PC 에서 사라졌다).
5.99 번호로 되돌리기 전에 이것부터 보는 편이 좋다.
