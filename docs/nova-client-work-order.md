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
