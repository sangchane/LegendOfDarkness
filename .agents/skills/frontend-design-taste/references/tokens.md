# 구체 토큰 (복붙용)

MengTo/Skills(`beautiful-shadows`·`minimalist-ui`·`tailwindcss`)에서 이식. 프로젝트에 CSS 변수
테마가 있으면(예: NEUROS `var(--color-*)`) 하드코드 색은 그 토큰으로 치환해 다크/라이트를 지원한다.

---

## 1. 그림자 — 무채색 레이어드 3종 (colored glow 금지)

한 컴포넌트/상태에 **한 종류만**. Tailwind 기본 `shadow-md/lg`와 섞지 말 것.

**md (기본 카드/패널)** — 원본 검증 값:
```
shadow-[0px_0px_0px_1px_rgba(0,0,0,0.06),0px_1px_1px_-0.5px_rgba(0,0,0,0.06),0px_3px_3px_-1.5px_rgba(0,0,0,0.06),0px_6px_6px_-3px_rgba(0,0,0,0.06),0px_12px_12px_-6px_rgba(0,0,0,0.06),0px_24px_24px_-12px_rgba(0,0,0,0.06)]
```

**sm (작은 카드/컨트롤)** — 같은 원리의 얕은 변형:
```
shadow-[0px_0px_0px_1px_rgba(0,0,0,0.05),0px_1px_1px_-0.5px_rgba(0,0,0,0.05),0px_2px_2px_-1px_rgba(0,0,0,0.05),0px_4px_4px_-2px_rgba(0,0,0,0.05)]
```

**lg (모달/히어로)** — 같은 원리의 깊은 변형:
```
shadow-[0px_0px_0px_1px_rgba(0,0,0,0.06),0px_2px_2px_-1px_rgba(0,0,0,0.06),0px_5px_5px_-2.5px_rgba(0,0,0,0.06),0px_10px_10px_-5px_rgba(0,0,0,0.06),0px_20px_20px_-10px_rgba(0,0,0,0.06),0px_40px_40px_-20px_rgba(0,0,0,0.06)]
```

> 테마 토큰화 권장: 위 값을 `--shadow-sm/md/lg` CSS 변수로 등록해 `shadow-[var(--shadow-md)]`로 사용.
> (sm/lg는 md의 레이어드 원리를 따른 스케일 변형 — 필요 시 프로젝트 톤에 맞게 미세조정.)

---

## 2. 의미 상태색쌍 (배경 / 텍스트) — 창백한 파스텔

`minimalist-ui`의 pale pastel 방식. 관제의 정상/경고/위험/정보 칩·배지에 매핑.

| 의미 | 배경 | 텍스트 | 출처 |
|---|---|---|---|
| 정상(normal/ok) | `#EDF3EC` | `#346538` | 원본(Pale Green) |
| 위험(danger/alarm) | `#FDEBEC` | `#9F2F2D` | 원본(Pale Red) |
| 경고(warning) | `#FBF3E6` | `#8A5A12` | 원본 원리 확장(Pale Amber) |
| 정보(info) | `#EAF1FB` | `#2B5597` | 원본 원리 확장(Pale Blue) |
| 중립(neutral/off) | `#F2F2F0` | `#4B4B4B` | 원본 원리 확장 |

캔버스/보더 중립 토큰(라이트): 캔버스 `#FFFFFF`/`#F7F6F3`, 보더 `#EAEAEA`/`rgba(0,0,0,0.06)`.
다크 테마는 프로젝트 `var(--color-*)`로 대응(순수 #000 대신 Zinc-950 계열).

---

## 3. 동적 클래스명 → 룩업맵 [필수 패턴]

`"bg-" + tone` 처럼 문자열로 조립하면 Tailwind 퍼지에서 **사라진다**. 반드시 정적 룩업맵.

```ts
// 알람 심각도 → 정적 클래스 (관제에 직결)
const alarmTone: Record<AlarmLevel, string> = {
  normal:  "bg-[#EDF3EC] text-[#346538]",
  info:    "bg-[#EAF1FB] text-[#2B5597]",
  warning: "bg-[#FBF3E6] text-[#8A5A12]",
  danger:  "bg-[#FDEBEC] text-[#9F2F2D]",
};
// 사용: <span className={alarmTone[level]}>…</span>
```

- 클래스 목록이 길어지면 `clsx`/`cva` 사용(있는지 `package.json` 확인 후).
- 임의값(`w-[42rem]`)은 아껴 쓰고, 반복되면 테마 토큰/컴포넌트로 승격.

---

## 4. 폰트 페어링 (대시보드/소프트웨어 UI)

세리프 금지. 산세리프 본문 + 모노 숫자.

| 본문 | 숫자/코드 |
|---|---|
| `Geist` | `Geist Mono` |
| `Satoshi` | `JetBrains Mono` |
| `Inter Tight`(대안) | `IBM Plex Mono` |

- 숫자·측정값·ID·시각은 항상 모노(`font-mono` 또는 위 모노 페이스).
- 폰트는 로컬/셀프호스트로 반입(폐쇄망·오프라인 대비 — 외부 CDN 의존 금지).

---

## 5. 밀도 높은 그룹화 (Cockpit, DENSITY ≥ 8)

카드 대신 선·여백으로 그룹:
```html
<section class="divide-y divide-[rgba(0,0,0,0.06)]">
  <div class="grid grid-cols-4 gap-4 py-2">…행…</div>
</section>
```
- 지표 라벨은 작게·저대비, 값은 크게·모노.
- z-index로 실제로 띄울 이유(드롭다운·모달)가 있을 때만 카드+그림자.
