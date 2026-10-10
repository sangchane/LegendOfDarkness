# SPEC: 봇 기록 정리 — 의미 있는 것만 남기고, 처음부터 묶어 쓰기 (M · 2026-10-11)

## 배경
사용자: 「의미있는 데이터만 정리하고 제안대로해 — 사냥을 하는데 끊기는 구간이 있으면 가만히 서있는 경우 같은게 발생하거나
몬스터를 제대로 인식하지 못 한다던가 아이템이나 금전 관련해서 더 이상 이벤트가 발생하지 않는 경우를 정리해두란거지 ·
머신러닝 데이터를 어떻게 처리하는지 내가 모르는 부분까지 작업」 + 「애초에 엄청나게 많은 반복 데이터를 최대한 압축시켜」.
제안(승인): 봇 기록만 줄인다 — 원본 3일, 조회용 7일. 사람 기록은 89일 그대로.

## 조사 결과 (2026-10-11, 클라우드 실측)
- 게임 서버 활동 기록(`/home/ubuntu/lod-activity/날짜.jsonl`)이 10-07 부터 하루 100만~158만 줄(0.55~0.89GB), 99.9% 가 봇.
  대시보드가 같은 줄을 `activity.sqlite`(3GB·540만 줄)에 또 담는다. 둘이 하루 약 1.5GB → 남은 32GB 가 약 3주.
- 봇 줄의 58% 가 ledger — 킬마다 `xp/Monsterexp.HandleExp`·`gold/GroupLoot.ShareGold`, 물약마다 `item/Format1CHandler` 한 줄씩(이 셋이 97%).
  state·heartbeat 는 60초마다 한 쌍, request_* 는 이미 60초 단위 합.
- **kill 은 아무도 안 남는다**(사람 포함 0줄) — `Area.cs:353-358` 이 `OnDeath()`(Target=null) 뒤에 `UpdateKillCounters` 를 불러
  `:314` 에서 늘 돌아간다. `MonsterKillCounters` 도 안 오른다(대시보드 「저장된 캐릭터」 처치 수 0).
- 이상 구간 규칙(3시간 표본 근거): 정상 봇은 제자리 p90 2분, 10분에 xp 53건(p50). 동료사제 5명은 대기 전용(제외).
- 머신러닝: 학습 코드는 없다 — 하루 04:30 cron `scripts/ml/export-activity.py` 가 재료만 `/home/ubuntu/lod-ml` 로 모은다.
  ① `--activity` 경로가 없는 곳(`/home/ubuntu/lod/Staging/net9.0/activity`)이라 게임 기록 내보내기가 한 번도 안 돌았다
  ② 봇 사건 gz 를 `lod-eco/eco` 와 `lod-ml/eco` 에 똑같이 두 벌 ③ `lod-ml` 은 지우는 규칙이 없다 ④ 봇 표준출력이 journald 에도 가서 journal 1.1GB.
  진짜 재료는 봇이 따로 쓰는 `lod-eco/eco`(킬·이동·사고팔기·죽음, 하루 약 10MB gz, 365일) — 이것은 줄이지 않는다.

## 변경
1. **처음부터 묶어 쓰기 — 서버 `ActivitySession.cs`** (봇만, 사람은 지금 그대로)
   - 봇은 flush 간격 60초 → 300초(state·heartbeat·request_* 가 5분에 한 번).
   - ledger 를 5분 동안 (asset, reason, item, from, to) 별로 모아 한 줄 — `count`=건수, meta `delta`=합, `balance`=마지막, `quantity`=합.
   - kill 도 (괴물) 별 `count` 로 묶는다. 어림: 봇 줄 하루 120만 → 약 10만.
2. **kill 버그** — `Area.cs` 에서 처치 기록·카운터를 `OnDeath()` 앞으로(대상이 지워지기 전에). 사람·봇 모두 kill 과 처치 수가 남는다.
3. **의미 있는 것만 남기기 — `activity_store.py`**: 새 표 `anomalies(player, rule, start, end, minutes, map, x, y, detail)`, 89일.
   봇 줄(동료 봇 제외)을 10분 칸으로 보고, 이어진 칸은 한 구간으로 합친다:
   - `stand` 가만히 서 있음 — 칸 안 위치(맵·x·y) 그대로 + xp 0 + request 0
   - `blind` 몬스터를 못 잡음 — skill+spell 20번 이상인데 xp 0
   - `income` 수입 끊김 — xp·gold·item 늘어남이 0 (그 칸에 맵 바뀜·상점이 있으면 빼고) · 20분 이상만
   - 건수는 `sum(amount)` 로 센다(묶기 전 한 줄 한 건 / 묶은 뒤 count) — 옛 기록과 새 기록 모두. 위치는 `detail` 앞 「맵 N (x,y)」.
   - 지난 칸만, 한 번만 계산(어디까지 했는지 기록). 보고서에 `anomalies`, 접속 기록 화면에 「봇 이상 구간」 표.
4. **보관** — 봇 줄 7일(sqlite), 원본 jsonl 3일(머신러닝 사본을 만든 날만 지운다), 사람 89일 그대로.
   봇 줄을 지우기 전에 그 날의 이상 구간 계산을 먼저 끝낸다. 배포 때 한 번 VACUUM(3GB → 작게).
5. **머신러닝 사본** — cron `--activity /home/ubuntu/lod-activity` 로 고침 · `lod-ml/eco` 는 복사 대신 하드링크(실패하면 복사) ·
   `lod-ml` 365일 지나면 지움 · journald 상한 500MB(`/etc/systemd/journald.conf.d/lod.conf`).

## 완료 기준
- [ ] SC-1 서버: 봇 ledger·kill 이 5분 묶음 한 줄(count·delta 합), 사람은 그대로 — 시험
- [ ] SC-2 서버: 괴물을 잡으면 kill 줄과 `MonsterKillCounters` 가 오른다 — 시험
- [ ] SC-3 이상 구간: 서 있음·못 잡음·수입 끊김을 옛 꼴(한 줄 한 건)과 새 꼴(count) 모두에서 찾고, 동료 봇·상점·맵 이동은 빼고, 다시 돌려도 두 번 안 남김 — 시험
- [ ] SC-4 보관: 7일 지난 봇 줄은 이상 구간을 남긴 뒤 지워지고 사람 줄은 남음, 원본은 사본이 있는 3일 지난 것만 지움 — 시험
- [ ] SC-5 머신러닝 사본: 경로 고침·하드링크·365일 — 시험(`tests/test_ml_export.py`)
- [ ] SC-6 클라우드: 배포 뒤 하루치 줄 수·디스크 확인, 접속 기록 화면에 이상 구간 표

## 안 할 것
학습 코드 만들기 · 봇 자체 기록(`lod-eco/eco`) 줄이기 · 사람 기록 보관 바꾸기 · 실패 중인 `lod-bot.service`(따로).

## 작업 순서
P1(파이썬: 3·4·5 + 시험) ∥ S1(서버: 1·2 + 시험) → 리뷰 → 배포(머신러닝 사본을 먼저 한 번 만든 뒤 보관 줄이기 · 서버는 알린 뒤) → SC-6.
