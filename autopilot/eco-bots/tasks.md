# 작업 — 생태계 봇
SPEC: `SPEC.md`. 위에서부터. 첫 셋이 버티컬 슬라이스(서버 이동 → 봇 한 바퀴 → 기록).

- [x] T0 내구도 끔(FR-018) — 서버 `Sprite.ApplyDamage` · `Pack599ArmorTests`. model: opus
- [x] T1 서버 EcoBots: 설정·`IsEcoBot`·0xF1 8·루프백 로그인/만들기·[접속자] AI·활동 bot 칸 + `EcoBotServerTests`. model: opus
- [x] T2 알맹이 순수: `StatPlan` 차례형 · `EcoGrounds` · `EcoShopping` · `EcoLife` · `EcoLog` + 시험. model: opus
- [x] T3 `build-eco-grounds.py` → `eco-grounds.txt`. model: sonnet
- [x] T4 `Lod.EcoBots` 프로그램(EcoHost·EcoRunner·설정) + `EcoBotLoopTests`(SC-001·002). model: opus
- [x] T5 `CheckObjectClients` 이름 집합 + `BotLoadTests` 전·후(SC-004). model: opus
- [x] T6 활동 숫자 칸 + `export-activity.py` + `test_export.py`(FR-013·014·SC-005). model: sonnet
- [x] T7 운영: `cloud-server.sh` eco·eco-config·eco-logs·deploy·cron, `eco-bots.example.json`. model: sonnet
- [ ] T8 회귀·리뷰(정확성 1 + 보안) · 커밋.
