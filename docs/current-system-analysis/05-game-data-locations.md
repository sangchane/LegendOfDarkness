# 05. 게임 데이터 저장 위치

## Hades/Lorule

| 데이터 | 저장 위치 | 읽는 코드 |
|---|---|---|
| 캐릭터/계정 | 런타임 `database/server/aislings/<username>.json` | `AislingStorage` |
| 맵 설명 | `database/server/areas/*.json` | `AreaStorage` |
| 맵 타일 | `database/server/maps/lod<ID>.map` | `AreaStorage`, `Map` |
| NPC | `database/server/templates/mundanes/*.json` 생성 예정/런타임 + `scripts/Mundanes/*.cs` | `TemplateStorage<MundaneTemplate>`, `MundaneScript` |
| 몬스터 | `database/server/templates/monsters/**/*.json` + `scripts/Monsters/*.cs` | `MonsterTemplate`, `MonsterScript` |
| 아이템 | `database/server/templates/items/*.json` + `scripts/Items/*.cs` | `ItemTemplate`, `ItemScript` |
| 스킬 | `database/server/templates/skills/*.json` + `scripts/Skills/*.cs` | `SkillTemplate`, `SkillScript` |
| 마법 | `database/server/templates/spells/*.json` 생성 예정/런타임 + `scripts/Spells/**/*.cs` | `SpellTemplate`, `SpellScript` |
| 전투 공식 | `database/server/scripts/Formulas/*.cs` 및 `LoruleConfig.json` 수치 | `DamageFormulaScript` 등 |
| 워프/월드맵 | `database/server/templates/warps/*.json`, `worldmaps/*.json` | `WarpStorage`, `WorldMapTemplate` |

**확인됨:** 저장 방식은 관계형 DB가 아니라 JSON/바이너리 맵/스크립트 파일이다. 저장소에는 샘플 캐릭터 파일이 없으며 실행 중 생성된다.

## Medenia

- 계정/캐릭터 상태: 런타임 `db.sqlite`, TypeORM `AislingEntity` (`apps/server/src/database/entities/aisling.entity.ts`)
- 맵 설정과 배치: `apps/server/data/maps/*.json`
- 원본 타일: `apps/server/data/map-data/lod<ID>.map`
- 몬스터: `apps/server/data/mobs/*.json`
- 충돌 표: `apps/server/data/sotp.dat`
- 렌더링 자산: `apps/client/public/`의 캐릭터·몬스터·아이템·타일·벽 PNG/atlas

**확인됨:** NPC는 독립 모델보다 맵 JSON `entities`에서 `CreatureType.Merchant`인 `Monster`로 만들어진다. 아이템·스킬·마법의 서버 권위 템플릿은 아직 없고 `PlayerCache`가 `test`, `shirt`, 빈 객체 같은 자리표시자를 넣는다. 전투도 기본 공격 시 대상 HP를 1 줄이는 초기 구현이다.

## 기타 자료 저장소

- `SleepHunter4/data/{Versions,Skills,Spells,Staves}.xml`: 자동화용 주소와 메타데이터이며 서버 원본 데이터가 아니다.
- `DAGL/src/741/GameLogic`, `UI/SkillDatabase.cs`, `UI/SpellDatabase.cs`, `IO/MapFile.cs`: 7.41 동작과 자료 구조 참고.
- `da-lib/DALib/{Data,Drawing}`: DAT/MAP/팔레트/EPF/MPF/SPF/HPF 파서.
- `DADataViewer`: 설치된 DA 데이터 파일을 읽어 아이템·스킬·몬스터·효과·음원을 표시한다.
