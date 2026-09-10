# 원작이 어떻게 하는지 막혔을 때 — 어디를 보나

- 기준일: 2026-09-10
- 왜 있나: **같은 자리에서 반복해서 막혔다.** "원작은 이걸 어떻게 했지"를 서버 코드에서만 찾다가
  "이 저장소엔 없다"로 접은 적이 여러 번이다. 세 번 중 세 번 다 **다른 곳에 이미 있었다.**

---

## 1. 먼저 이 한 줄

```powershell
./scripts/find-in-sources.ps1 <찾을 말> [-Include *.cs] [-Limit 40]
```

참고 저장소 16개를 한 번에 뒤진다. `git grep` 이라 **커밋된 파일만** 보므로 `node_modules`·`bin`·`obj`
는 애초에 대상이 아니고, 열여섯 개를 다 뒤져도 1초 안쪽이다. 보통 검색이 `dark-ages-ts` 에서 멎는
이유가 그 폴더들이다.

- 정규식이다(`-E`). `.` `(` 는 `\.` `\(` 로 적는다.
- `Binary file … matches` 가 나오면 **아카이브 안에 그 이름이 들어 있다**는 뜻이다. 그러면
  `dat-extract list <그 .dat>` 로 열어 본다.

---

## 2. 무엇을 찾느냐에 따라

| 알고 싶은 것 | 어디 | 무엇이 나오나 |
|---|---|---|
| 원본 파일을 **읽는 법**(EPF·MPF·SPF·HPF·PAL·TBL·MAP) | `sources/wren11/da-lib/DALib/Drawing/` | 형식마다 클래스 하나. 머리말 순서가 여기 있다 |
| 사람을 **어떻게 겹쳐 그리나**(부위 순서·방향·염색) | `sources/FallenDev/dark-ages-ts/apps/client/src/game-objects/paper-doll/` | 부위 글자, 겹치는 차례, 방향별 앞뒤 |
| 부위가 **화면 어디에 놓이나** | `sources/FallenDev/dark-ages-ts/apps/client/public/aislings/<이름>/<이름>.atlas` | 조각마다 `bounds`(그림 자리)와 `offsets`(놓을 자리 + **바탕 크기**) |
| **원작 서버가 보내는 패킷**의 진짜 생김새 | `sources/FallenDev/dark-ages-ts/packages/network/src/packets/` | 필드 이름·크기·차례. **우리 서버(Hades)와 다르다** — 4절 |
| 우리 서버가 보내는 것 | `sources/wren11/Dark-Ages-Private-Server/src/.../Network/ServerFormats/` | 실제로 우리 클라이언트가 받는 바이트 |
| 동작 구간·몬스터 행동·색표 | `data/legend-tables/` (원본은 아래 4절) | `skill.tbl` `MobTile.tbl` `color.tbl` `color0.tbl` `itempal.tbl` … |
| 그림·소리 원본 | `sources/Dark-Ages-Private-Server-master/game/*.dat` | `khan`(남) `khan2`(여) `hades`(몬스터) `roh`(효과) `seo`(타일) |
| 이야기 삽화·음악·규칙표 원본 | `sources/wren11/Dark-Ages-Private-Server/database/archives/legend/Legend.dat` | 13MB. `.epf` 186 · `.mp3` 165 · `.tbl` 16 |

---

## 3. 순서 — 데이터가 코드보다 정직하다

1. **원본 파일 자체**(`.dat` 안의 머리말·표)
2. **참고 구현 코드**
3. **그 저장소가 커밋해 둔 변환 산출물** — 아틀라스·표·에셋

**③이 가장 자주 답이었다.** 코드는 "무엇을 한다"만 말하고, 산출물에는 "실제 숫자"가 적혀 있다.
무기 오프셋이 그랬다 — 코드 어디에도 없었고 `.atlas` 의 `offsets:` 넷째 칸에 있었다.

---

## 4. 함정 세 개 (전부 실제로 밟았다)

**"여기 없다"고 적힌 문서를 그대로 믿지 마라.** `Legend.dat` 은 "설치 클라이언트에만 있다"고
적어 뒀었는데 **저장소 안에 있었다**(`.../database/archives/legend/Legend.dat`, 13MB). 표 16개는
이미 `data/legend-tables/` 에 뽑혀 있어 결과는 같았지만, 그 문장 때문에 한 번 접었다.

**우리 서버의 패킷은 원작과 다르다.** `0x33`(사람 표시)에서 Hades 는 무기를 1바이트로 쓰고
(원작은 2바이트), 장신구 색·장신구 셋째·도포 색·몸 색·투명·얼굴형·무리 이름을 아예 안 보낸다.
원작 형식으로 읽으면 자리가 어긋난다. **우리 클라이언트는 Hades 기준으로 맞춘다.**

**`sources/FallenDev/DAGL` 은 근거로 쓰지 않는다.** 7.41 클라이언트를 옮긴 것처럼 보이지만
README 가 스스로 AI 로 대량 생성한 것이라고 밝히고 있고, 실제로 사람 그리는 코드에 무기가 없다.
이름만 그럴듯한 파일이 많다.

---

## 5. 자동으로 막아 주는 것들 (훅)

문서에 적어 두는 것만으로는 안 읽힌다. **기계가 판정할 수 있는 규칙은 훅으로 옮겼다** —
`.claude/settings.json` 에 걸려 있고 스크립트는 `tools/hooks/` 에 있다.

| 언제 | 무엇을 | 어디 |
|---|---|---|
| `git ...` 실행 전 | 잠금 없는 루트 상태 조회는 **막는다**. submodule 17개를 스캔하다 멈추면 `index.lock` 이 남아 git 전체가 마비된다. `--ignore-submodules=all` 이나 `-C sources/...` 면 통과 | `guard_shell.py` |
| `cp`·`mv`·`rsync`·PowerShell 실행 전 | `LOD_` 에서 복사해 오면 **알린다**(막지 않음) | 〃 |
| Artifact 호출 전 | 산출물은 `docs/` 마크다운으로 남기라고 **알린다** | `prefer_docs_over_artifact.py` |
| 세션이 끝날 때 | 포트 2610·2615·2620 이 열려 있으면 서버를 내리라고 **알린다** | `check_hades_ports.py` |

막는 것은 첫 줄 하나뿐이다. 나머지는 한 줄 알리고 지나간다 — 일부러 그렇게 할 때도 있기 때문이다.
훅을 보거나 끄려면 `/hooks`. 무엇을 막고 무엇을 통과시키는지는 `python tools/hooks/test_guard_shell.py`
로 확인한다.

**훅은 조용히 틀린다.** 안 막아도 아무 일이 없고, 잘못 막으면 엉뚱한 명령이 죽는다. 실제로 이 훅을
넣는 커밋이 **자기 자신에게 막혔다** — 커밋 메시지 본문에 그 두 낱말이 들어 있었기 때문이다. 그래서
지금은 토막의 맨 앞이 `git` 일 때만, 그리고 따옴표가 열리기 전까지만 명령으로 본다. 규칙을 손대면
위 시험을 돌린다.

**훅으로 옮기지 않은 것들.** "그린을 믿지 마라"·"class_name 파스에러"는 출력을 해석해야 해서 오판하기
쉽고, "쉽게 설명해라"·"작업에 맞는 모델을 써라"는 사람이 판정할 몫이다. 그런 것은 `CLAUDE.md` 와
기억에 남겨 둔다.
