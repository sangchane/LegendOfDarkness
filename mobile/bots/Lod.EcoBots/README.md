# Lod.EcoBots — 생태계 봇

서버 설정 `EcoBots` 의 이름들로 접속해 **혼자 사냥·장사하며 99레벨까지 자라는** 캐릭터들(전사·도적·무도가). 설계 `autopilot/eco-bots/`.
봇마다 프로그램을 띄우지 않는다 — 이 프로그램 하나가 봇 여럿을 접속시킨다(실측: 봇 220개에 0.5코어, `01-recon.md` §3).

## 돌리기

```bash
cp eco-bots.example.json eco-bots.json   # 비밀번호를 적는다 — 커밋하지 않는다(.gitignore)
DOTNET_ROOT=../../../.tools/dotnet-9.0.317 ../../../.tools/dotnet-9.0.317/dotnet run -- eco-bots.json
```

- 서버 설정 `EcoBots` 에 같은 이름이 있어야 한다. 이 이름은 **같은 기계(루프백)에서만** 로그인·만들기가 된다.
- 계정이 없으면 설정의 `Path`(1 전사 · 2 도적 · 5 무도가)로 만든다.
- `MapFolder` — 앱의 맵 자료(`mobile/client/assets/world`): 벽 `map*.txt` · 가게 `guide.txt` · 사냥터 `eco-grounds.txt`(`scripts/gen/eco/build-eco-grounds.py`) · 직업 기술 `class-kit.txt`.
- `MaxOnline` — 함께 접속할 수(목록 앞에서부터). 클라우드(2 vCPU)는 시작 30, 상한 50(`01-recon.md` §4).

## 판단 (알맹이 `Automation/EcoLife.cs`·`EcoPlan.cs`, 시험 `EcoTests`)

사냥(대신 사냥과 같은 `HuntProxyRunner` 한 틱) → 물약 5 밑 · 가방 빈칸 5 밑 · 60분 · 사냥 멈춤이면 마을 → 물약 가게(체력 물약을 가장 많이 파는 곳)에서
팔기·물약 30개까지 → 장비 가게마다 직업·성별·레벨 제한(서클)이 맞고 지금보다 좋은 것 → 체력 50% 밑이면 쉼 → 레벨 맞는 사냥터(맵당 봇 4까지,
사람이 30초 보이면 비킴). 이동은 봇 전용 순간이동(0xF1 8). 죽으면 뮤레칸에게 살아나고, 3번 죽을 때마다 사냥터 한 층 아래. 능력치는 `StatPlan`.
숫자는 `Tuning.cs` 의 `Eco*`.

## 기록

- 프로그램 기록 `logs/eco-bots.log`(1MB×5) — 5분마다 요약(접속 수 · 하는 일별 수 · 레벨 평균·최고).
- 사건 기록(머신러닝 재료) `eco/YYYY-MM-DD.jsonl`(한국 날짜, 지난 날은 gzip, 365일) — 한 줄 JSON, 숫자는 숫자 칸(`EcoLog`).
- 클라우드: `scripts/ops/cloud-server.sh eco-config`(처음 한 번) · `eco` · `eco-logs` · `ml-pull`.
