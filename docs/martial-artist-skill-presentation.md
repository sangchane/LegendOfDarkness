# 무도가 1~10 기술 연출 — 모션·이펙트·사운드 근거와 구현 계약

- 조사일: 2026-09-15
- 범위: `Kick` · `High Kick` · `Double Punch` · `Poison Punch` · `Sting`
- 상태: 자료 조사 완료. **모바일 구현됨(2026-09-17)** — 몸동작(`BodyMotion`·부위별 `d` 시트) · 이펙트(`Flash`) ·
  소리(`0x19`·`0x13`). 아래 "현재 모바일은 세 채널을 모두 잃는다" 표는 조사 당시의 모습이다
- 원칙: Hades → 원작 아카이브/참고 리더 → 참고 저장소 → 서버팩 순서. 서버팩 숫자는 두 독립 팩이
  일치하고 원작/Hades와 충돌하지 않을 때만 후보로 쓴다.

## 1. 결론

몸동작은 구현할 근거가 충분하다. Hades가 `0x1A`로 보내는 131~133번과 참고 클라이언트의 동작 표,
`skill.tbl`의 무도가 `d` 시트 구간이 정확히 연결된다. **프레임을 파일 크기로 반분할 이유가 없다.**

이펙트는 서버가 보내는 `0x29`를 그대로 그릴 수 있지만, 현재 초반 무도가 템플릿의 기본 타격 효과는
모두 0이다. 예외는 `Poison Punch`의 독 효과 25다. 사운드는 전송·원본 MP3 위치까지 확인했지만 초반
기술별 확정값은 없다. 따라서 구현 순서는 몸동작 → 확정 이펙트(25, Hades의 203 등) → 검증된 사운드다.

현재 모바일은 세 채널을 모두 잃는다.

| 채널 | Hades 패킷 | 현재 모바일 |
|---|---|---|
| 몸동작 | `0x1A`: serial + motion + speed | serial만 보관하고 motion/speed 폐기 |
| 효과 | `0x29`: 시전자/대상 또는 좌표 + effect + speed | 처리기 없음 |
| 피해 사운드 | S→C `0x13`: serial + HP% + sound | HP%만 읽고 sound 폐기 |
| 단독 사운드 | `0x19`: sound 번호 | 처리기 없음 |

근거: `mobile/src/Lod.Mobile.Core/World/WorldClient.cs:159-168,245-253,611-636`,
`sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Network/ServerFormats/ServerFormat1A.cs:13-33`,
`ServerFormat29.cs:18-63`, `ServerFormat13.cs:13-33`, `ServerFormat19.cs:9-21`.

## 2. 실제 평타와 기술 실행 경로

Hades에서 C→S `0x13` 평타 입력은 `GameServerHandlers.Assail()`로 들어간다. 여기서는 캐릭터가 배운
기술 중 `Type == Assail`인 것을 실행한다. 다섯 무도가 템플릿은 `Type`을 생략했고 enum 기본값이
`Assail(0)`이므로 평타 묶음에 들어간다. 기술창 C→S `0x3E`는 선택한 슬롯의 스크립트만 실제로
실행한다.

```
평타 0x13 → Assail() → GetAssails() → ExecuteAbility()
기술 0x3E → 슬롯 조회 → ScriptManager.Load(ScriptName) → OnUse()
피해       → ApplyDamage(sound) → S→C 0x13(HP%, sound)
몸동작     → ServerFormat1A
이펙트     → ServerFormat29
```

근거: `GameServerHandlers.cs:779-807,1787-1838`, `Types/Aisling.cs:531-535`,
`GameClient.cs:549-556`, `Skill.cs:67-76`, `Scripting/ScriptManager.cs:21-25,112-154`,
`Types/Sprite.cs:923-950`.

## 3. 무도가 1~10 연출 현황

| 기술 | 요구 수준 | Hades 몸동작 | 원작/참고 이름 | `d` 시트 등/앞 구간 | 기본 효과 | 피해 사운드 |
|---|---:|---:|---|---|---:|---:|
| Kick | 1 | 131 (`0x83`) | Kick / MonkHighKick | 0–2 / 3–5 | 0 | 0 |
| High Kick | 4 | 133 (`0x85`) | RoundHouseKick / MonkSideKick | 10–13 / 14–17 | 0 | 0 |
| Double Punch | 6 | 132 (`0x84`) | Punch / MonkPunch | 6–7 / 8–9 | 0 | 0 |
| Poison Punch | 10 | 132 (`0x84`) | Punch / MonkPunch | 6–7 / 8–9 | **독 25** | 0 |
| Sting | 조건 없음 | 132 (`0x84`) | Punch / MonkPunch | 6–7 / 8–9 | 0 | 0 |

이 연결은 숫자 모양만 보고 추정한 것이 아니다.

- Hades: `Kick.cs:19`, `HighKick.cs:19`, `DoublePunch.cs:39-43`, `PoisonPunch.cs:21`, `Sting.cs:19`.
- 패킷 번호의 이름: `sources/FallenDev/Arbiter/Arbiter.Net/Types/BodyAnimation.cs:41-58`에서
  131=Kick, 132=Punch, 133=RoundHouseKick. ETDA도 131~133을 무도가 세 동작으로 둔다
  (`sources/wren11/ETDA/BotCore/Types/Action.cs:11-18`).
- 번호→시트/구간: `sources/wren11/DADataViewer/DADataViewer/MotionsForm.cs:27-46`에서
  131=(file 3,start 0,count 3), 132=(3,6,2), 133=(3,10,4)로 직접 연다.
- 원작 표: `data/legend-tables/skill.tbl`의 NO 3/4/5와 동일하다.

`Poison Punch`는 타격 템플릿 효과가 아니라 `Debuff_poison(... Animation=25 ...)`가 적용·주기 갱신 때
효과 25를 보낸다. ETDA의 관찰 이름표도 25를 `poisonpunch`/`venom`으로 기록한다
(`sources/wren11/ETDA/BotCore/Types/Animation.cs:62,77`).

## 4. 걷기 때의 프레임 절단 오류를 반복하지 않는 규칙

### 4.1 파일을 반으로 나누지 않는다

`01`과 `02`가 우연히 앞/뒤 두 덩어리처럼 보일 뿐, 일반 규칙이 아니다.

| 동작 | 실제 규칙 |
|---|---|
| 서기 | `01`: 등 0, 앞 5 |
| 걷기 | `01`: 등 1–4, 앞 6–9 |
| 평타 | `02`: 등 0–1, 앞 2–3 |
| 기술 | `skill.tbl`: 등 `SI..SI+FC-1`, 앞 `SI+FC..SI+2FC-1` |

무도가 `d`는 선언 용량이 20칸이어도 실제 그림은 18칸뿐이다. 18·19번을 패딩처럼 요청해서는 안 된다.
실제 EPF 프레임 수를 먼저 읽고 모든 인덱스가 그 안인지 검사한다.

### 4.2 방향을 고른 뒤 왼쪽만 뒤집는다

- 북/서: 등 구간. 서만 X 반전.
- 동/남: 앞 구간. 남만 X 반전.
- X 반전 축은 셀 중앙이 아니라 기존 `FeetX/FeetY` 발 기준점을 유지한다.
- 방향이 바뀌면 방패·무기·망토의 앞뒤 레이어 순서도 기존 규칙대로 바뀌어야 한다.

### 4.3 생성된 manifest를 단일 진실원으로 쓴다

런타임에서 파일명을 조합하거나 프레임 수를 추측하지 않는다. 추출기가 다음과 같은 불변 manifest를
만들고 Core와 Godot가 함께 읽는다.

```text
motion=131, file=d, start=0, frames=3, back=0..2, front=3..5, physical=18
motion=132, file=d, start=6, frames=2, back=6..7, front=8..9, physical=18
motion=133, file=d, start=10, frames=4, back=10..13, front=14..17, physical=18
```

필수 자동 검사:

1. `physical >= SI + 2*FC`, 모든 요청 인덱스 `< physical`.
2. 각 행의 등/앞 길이가 각각 정확히 `FC`이고 서로 겹치지 않음.
3. 무도가 세 행의 합이 실제 18칸을 빈틈없이 정확히 한 번 덮음.
4. `01`은 `{0},{1..4},{5},{6..9}`, `02`는 `{0..1},{2..3}`으로 별도 고정 시험.
5. 네 방향의 side/mirror 조합과 발 기준점·레이어 순서를 스냅샷으로 검증.
6. 없는 부위별 `d` 파일은 조용히 평타로 대체하지 않고 자산 생성 단계에서 실패.

## 5. 효과 자료

`roh.dat` 실측 목록은 EPF 278개, EFA 133개, TBL 282개다. 효과 재생 순서는 개별
`efct###.tbl`이 아니라 **단일 `effect.tbl`**에 있다. 첫 줄은 항목 수, 이후 `ID - 1`번째 줄이 해당
효과의 프레임 순서다. 효과 203은 `0 1 1`이다. 개별 `efct203.tbl`은 8바이트 바이너리 메타데이터이며
프레임 순서표가 아니다.

근거: `sources/wren11/da-lib/DALib/Drawing/EffectTable.cs:24-62,140-151,193-199`와 실제 아카이브:

```powershell
dotnet tools/dat-extract/bin/Debug/net8.0/dat-extract.dll list `
  sources/wren11/Dark-Ages-Private-Server/database/archives/roh/roh.dat efct203
# efct203.epf 998 bytes / efct203.tbl 8 bytes
```

Hades 기준으로 지금 바로 확정된 것은 `Assail.TargetAnimation=203`과 `Poison Punch` 독 효과 25다.
나머지 네 무도가 기술은 템플릿에 `TargetAnimation`이 없어 0이다. `MonkStrike`가 0도 조건 없이
`ServerFormat29`로 보내므로 클라이언트는 effect 0을 **재생 없음**으로 취급해야 한다.

참고 효과 목록에는 `25=Classic poison`, `27=Classic rescue/kick`, `274=Modern double punch hit`가
기록돼 있다(`sources/FallenDev/Arbiter/docs/src/effects/spells.md:29-37,265-284`). 프록시의 호환 표는
다시 `274 → classic 27`을 modern kick/rescue로 설명한다
(`Arbiter.App/ViewModels/Proxy/ProxyViewModel.EffectFilters.cs:12-47`). 즉 25는 Hades 독 코드와도
교차 확인되지만 27/274의 정확한 기술 이름은 참고 자료끼리도 모호하다. 자산 카탈로그와 실기 캡처의
후보로만 쓰고 서버 값을 자동 변경하지 않는다.

효과 플레이어는 EPF/EFA 셀을 순서대로 늘어놓지 않는다. `effect.tbl` 순서와 각 프레임의
left/top/right/bottom 오프셋을 보존하고, 대상형은 actor 발 위치에, 좌표형은 map tile에 붙인다.

## 6. 사운드 자료

피해 사운드는 S→C `0x13` 마지막 1바이트, 단독 사운드는 `0x19`의 2바이트 번호다. 현재 모바일은 둘 다
버린다. `Legend.dat`에는 숫자 이름 MP3 165개가 있고 ID 범위는 0~167, 결번은 3·118·119다.
`DADataViewer/SoundsForm.cs:21-35,71-92`가 그 파일들을 실제 MP3로 열어 재생한다.

하지만 **Hades/원작 메타파일에는 초반 무도가 기술명→MP3 번호의 권위 매핑이 없다.** Hades는 Assail만
Sound 1이고 다섯 무도가 템플릿은 모두 0이다. 서버팩의 `game_sound` 숫자는 후보일 뿐이다.

| 서버팩 후보 | motion | effect | sound | 판정 |
|---|---:|---:|---:|---|
| 5.99 정권 | 132 | 27 | 18 | Punch 계열 후보, 혼든/Nova와 충돌 |
| 5.99 단각 | 131 | 249 | 14 | Kick 후보, 혼든 기본값과 충돌 |
| 5.99 붕각 | 133 | 249 | 14 | High Kick 후보 |
| 혼든 붕각 | 133 | 249 | 14 | 5.99와 일치하지만 개조 분기 존재 |
| Nova 정권/단각/붕각 | 131 중심 | 166/69 | 18/14 | ~~채택 안 함~~ → **이펙트만 채택**(2026-09-26, 아래) |

> **결정 뒤집음 (2026-09-26, 사용자): 「노바 것이 원작 이펙트다」.** 기술·마법의 **이펙트 번호만** 노바 팩 값으로
> 쓴다 — 정권 27→166 · 단각·붕각·선풍각 249→69 · 연천단각 69→67 · 붕신선각 69→186 · 파천각 69→185 ·
> 마구때리기 158→69 · 다라밀공 288→47 · 허공답보 68→없음(노바에 이펙트가 없다) 등. 동작(motion)·소리(sound)는
> 위 표의 5.99 값 그대로다. AGENTS.md 의 「팩은 3개가 일치할 때만」 규칙의 명시적 예외다.
> 생성기 `scripts/build-nova-effects.py` 가 5.99 를 옮긴 스크립트(`scripts/Pack599`)와 무도가 템플릿의
> `TargetAnimation` 을 바꾼다(`build-pack-abilities.py`·`build-monk-skills.py` 가 끝에 부른다). 노바에만 있는
> 갈래(40% 빗나감의 33 · 데빌크래셔 둘레 131)는 동작이라 옮기지 않았다.

5.99와 혼든이 일치하는 붕각의 `effect=249,sound=14`만 **실기 캡처 후보**로 남긴다. High Kick과
붕각의 이름 동일성까지 원작 자료가 증명한 것은 아니므로 아직 Hades 템플릿에는 넣지 않는다. Kick,
Double Punch, Sting의 값은 팩끼리 불일치하므로 버린다. Honden의 `포이즌어택`은 후대 개조 기술이라
원작 `Poison Punch`의 근거로 쓰지 않는다.

## 7. Graphify를 쓰는 곳과 믿지 않는 곳

Hades 실행 경로를 찾을 때는 유용하다.

```powershell
graphify explain ServerFormat1A --graph graphify-out/graph.json
graphify explain ServerFormat29 --graph graphify-out/graph.json
graphify path HurricaneKick ServerFormat1A --graph graphify-out/graph.json
graphify query "MonkStrike ApplyDamage" --graph graphify-out/graph.json
```

`HurricaneKick → OnSuccess → ServerFormat1A/29`가 두 hop으로 이어지고 `ApplyDamage`의 실제 구현까지
좁혀 준다. 다만 다음은 직접 코드로 재확인한다.

- 주 그래프는 2026-09-11 빌드라 현재 Hades HEAD와 모바일 변경보다 오래됐다.
- `MonkStrike` 정적 클래스는 그래프 노드에서 빠졌다.
- 여러 저장소의 같은 `.OnSuccess()` 이름이 충돌한다. 경로/클래스 노드로 한정한다.
- 서버팩 그래프 일부는 5.99 디렉터리에 Nova 스크립트가 교차 생성돼 있다. 그래프는 호출 후보만 찾고
  숫자는 `data/server-packs/<팩>/db/script/` 원문에서 확인한다.

## 8. 구현 순서와 완료 기준

1. **프로토콜 RED 시험**: `0x1A`의 motion/speed, 두 형태의 `0x29`, `0x13` sound, `0x19`를 불변
   record 이벤트로 읽는 실패 시험을 먼저 쓴다. 패킷 도착 순서를 보존한다.
2. **모션 manifest/추출기**: `skill.tbl` + 실제 EPF 수로 위 불변식을 검사하고, 남녀 및 착용 부위의
   `d` 시트를 같은 80×88 발 기준 캔버스에 추출한다. 오류가 하나면 생성 전체를 실패시킨다.
3. **Godot 몸동작**: 서버 `0x1A`를 권위 입력으로 삼아 actor의 모든 레이어를 같은 motion/프레임으로
   교체한다. 기술 버튼의 낙관적 공통 평타는 제거하거나, 서버 응답 전 임시 큐로만 취급한다.
4. **효과**: 먼저 확정 ID 25와 203으로 `effect.tbl`의 반복 순서·오프셋·대상/좌표 부착을 검증한다.
   effect 0은 아무 노드도 만들지 않는다. EFA 지원 전에는 EFA ID를 명시적으로 미지원 처리한다.
5. **사운드**: `0x13`/`0x19`를 `AudioStreamPlayer`로 연결하되 0은 무음이다. Assail 1을 기준으로
   번호→MP3 직접 대응을 실기에서 확인한 뒤 14/18 후보를 판단한다.
6. **실전 회귀**: 10레벨 무도가가 다섯 기술을 쓸 때 서버 피해 + 올바른 motion 번호 + effect/sound
   이벤트 + Godot 프레임 범위를 함께 기록한다. 앞/등과 좌우 반전 네 방향을 모두 캡처한다.

완료는 “움직여 보인다”가 아니다. 잘못된 프레임을 요청하면 테스트가 실패하고, 다섯 기술의 패킷 번호와
실제 렌더 프레임·효과·사운드가 한 로그에서 연결되어야 한다.

## 9. 아직 확정하지 않은 것

- High Kick ↔ 서버팩 붕각의 이름 동일성 및 effect 249 / sound 14 채택 여부.
- Kick·Double Punch·Sting의 기술별 효과/사운드. 현재 값 0을 임의의 팩 값으로 덮지 않는다.
- 참고 효과표의 classic 27 / modern 274가 각각 어느 초반 기술에 해당하는지.
- 개별 `efct###.tbl` 8바이트 필드와 EFA 형식. 재생 순서는 이미 `effect.tbl`로 확정됐다.
- `Legend.dat` 숫자 MP3가 `0x13`/`0x19` 번호와 정확히 1:1인지 실기 패킷+청음 대조.
