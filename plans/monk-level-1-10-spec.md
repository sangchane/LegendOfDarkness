# 무도가 1–10레벨 기술 구현 명세

## 배경(Context)

Hades에는 무도가 기술의 학습 조건과 클라이언트 표시 자료가 있지만, `Kick`, `High Kick`,
`Poison Punch`, `Sting`은 실행 스크립트가 연결되지 않아 눌러도 서버가 즉시 되돌아간다.
1–10레벨 진행을 실제 전투로 검증하려면 이 빈 동작을 먼저 채워야 한다.

## 현재 상태(Current State)

| 파일 | 현재 상태 |
|---|---|
| `database/server/scripts/Skills/DoublePunch.cs` | 앞칸 공격, 훈련, 모션, 피해 보고의 기준 구현 |
| `database/server/templates/skills/Kick.json` | 무도가·레벨 1 조건, `ScriptName` 없음 |
| `database/server/templates/skills/High Kick.json` | 레벨 4·Kick 5 조건, `ScriptName` 없음 |
| `database/server/templates/skills/Double Punch.json` | 레벨 6, 기존 스크립트 연결됨 |
| `database/server/templates/skills/Poison Punch.json` | 레벨 10 조건, `ScriptName` 없음 |
| `database/server/templates/skills/Sting.json` | 무도가 조건, `ScriptName` 없음 |
| `tests/hades-characterization/ScriptCompilationTests.cs` | 런타임 스크립트 전체 컴파일 검증 |
| `tests/hades-characterization/CombatSmokeTests.cs` | 실제 서버·모바일 프로토콜 전투 검증 패턴 |

## 제안 변경(Proposed Change)

### 구현 세부(Implementation Details)

- `Skills/Monk/MonkStrike.cs`에 앞칸 단일 대상, 공격 가능 여부, 피해·모션·피격 표시를 공통 처리한다.
- 배율은 Hades 기존 범위만 사용한다.
  - Kick: `Str×4 + Dex×2` (Assail 기준)
  - High Kick: `Str×5 + Dex×3` (Wallop 기준)
  - Poison Punch: `Str×4 + Con×2` (Double Punch 기준) + Hades `Debuff_poison`
  - Sting: `Str×4 + Dex×2` (Assail 기준)
- 네 스크립트 모두 기술 레벨에 따라 `10 + Skill.Level`%를 더하고, 사용 시 훈련·은신 해제를 수행한다.
- 모션은 원본 관찰 코드의 `Kick=0x83`, `Punch=0x84`, `RoundHouseKick=0x85`를 사용한다.
- 각 템플릿의 `ScriptName`만 연결하며 학습 조건은 바꾸지 않는다.
- 팩의 피해식·기술 구현은 사용하지 않는다.

## 수용 기준(Acceptance Criteria)

- 레벨 1, 4, 6, 10 진행선의 네 템플릿과 Sting이 실행 스크립트에 연결된다.
- 새 스크립트를 포함한 Hades 런타임 스크립트 전체가 컴파일된다.
- 우드랜드1-1에서 Kick, High Kick, Double Punch, Poison Punch, Sting 각각이 앞칸 몬스터에 체력 보고를 발생시킨다.
- Poison Punch는 Hades의 독 디버프를 대상에 적용한다.
- 기존 Hades characterization 및 모바일 코어 테스트가 회귀 없이 통과한다.

## 테스트 계획(Testing Plan)

| 종류 | 검증 |
|---|---|
| 구조 | 레벨·선행 조건과 `ScriptName` 연결값 |
| 통합 | 격리 Hades 서버 시작 및 전체 스크립트 컴파일 |
| E2E | 우드랜드1-1 고정 표적에 모바일 프로토콜로 기술 5개 사용 |
| 회귀 | 전체 characterization·모바일 코어 테스트 |

## 검증 방법(Verification)

1. Hades 솔루션 Debug 빌드
2. `MonkLevelTenSkillTests` 실행
3. `ScriptCompilationTests` 실행
4. 전체 Hades characterization 및 모바일 코어 테스트 실행

## 롤백 계획(Rollback)

추가한 무도가 스크립트와 템플릿 `ScriptName` 변경, 새 테스트만 되돌린다. 학습 조건과 기존
`Double Punch`는 변경하지 않는다.

## 범위 밖(Out of Scope)

- 레벨 10을 넘는 무도가 기술
- 학습 NPC·아이템·쿨다운 밸런스
- 클라이언트 기술창 재설계
- 서버팩 기술식 이식

## 참조 파일(Files Reference)

- `docs/what-hades-already-has.md`
- `docs/monster-behaviour.md`
- `sources/FallenDev/DAGL/src/741/World/WorldObject_Human.cs` (모션 관찰)
- `sources/FallenDev/SleepHunter4/data/Skills.xml` (쿨다운 참고만, 이번 범위 밖)

명세 실행 가능성 자체 평가: **9/10**. 수치와 테스트 환경은 확정됐으며 장기 밸런스만 범위 밖이다.
