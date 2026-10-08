# DESIGN.md 레퍼런스 (awesome-design-md)

출처: [VoltAgent/awesome-design-md](https://github.com/VoltAgent/awesome-design-md)(MIT, 2026-10-08 기준 ★약 12만, 74개 사이트).
실제 사이트에서 뽑은 디자인 시스템 문서다. [Google Stitch DESIGN.md 형식](https://stitch.withgoogle.com/docs/design-md/overview/)을 따른다.
- 앞부분 YAML: 색·서체·모서리 반경·간격 토큰
- 본문: 분위기, 색 역할, 타이포 위계, 컴포넌트 상태, 레이아웃·간격, 깊이, Do/Don't, 반응형, 에이전트 프롬프트

`AGENTS.md`가 "어떻게 만드나"를 정하고, `DESIGN.md`가 "어떻게 보이나"를 정한다.

## 받기

```bash
# <slug>는 아래 표의 폴더 이름. 프로젝트 루트에 저장한다.
curl -fsSL https://raw.githubusercontent.com/VoltAgent/awesome-design-md/main/design-md/<slug>/DESIGN.md -o DESIGN.md
```
- 받은 뒤 DESIGN.md **맨 끝**에 `출처: <URL> (받은 날 YYYY-MM-DD)`를 한 줄 남긴다. 맨 위는 YAML이라 건드리지 않는다.
- 프로젝트 `AGENTS.md` 네비게이션에 `디자인 기준: DESIGN.md` 한 줄을 넣는다.
- 미리보기(색·서체·버튼·카드): `https://getdesign.md/<slug>/design-md`. 저장소 폴더에는 `preview.html`·`preview-dark.html`도 있다.
  사용자가 고르기 어려워하면 후보 2~3개의 이 주소를 보여 준다.
- 파일은 약 20~25KB다. YAML 토큰 → Do's and Don'ts → Responsive Behavior → Components 순으로 읽으면 충분하다.

## 프로젝트에 맞게 고치기 (받은 그대로 쓰지 않는다)

1. **브랜드를 지운다.** 로고, 브랜드 이름, 브랜드 전용 서체 이름(LamboType, SoDoSans 등)을 빼고, 문서의 "Font Substitutes"나 비슷한 공개 서체로 바꾼다.
   남의 정체성을 그대로 베끼면 공개 서비스에서 문제가 된다. 가져오는 것은 색 체계·간격·위계·컴포넌트 문법이다.
2. **한글을 붙인다.** DESIGN.md 서체는 라틴 전용이다. 한글은 `Pretendard`(산세리프)나 `Noto Sans KR`과 짝짓고, 라틴 서체는 영문·숫자에만 쓴다.
   숫자 정렬(`tabular-nums` 또는 모노)은 하드룰대로 유지한다.
3. **테마를 토큰으로 옮긴다.** YAML 색을 CSS 변수(`--color-*`)나 Tailwind 테마로 옮긴다. 라이트·다크가 둘 다 필요하면, 문서에 없는 쪽은 같은 역할 이름으로 만든다
   (canvas·surface 단계·ink 단계·hairline·primary·semantic).
4. **데이터 화면 보정.** 대부분 마케팅 페이지에서 뽑은 문서라 큰 디스플레이 서체와 여백이 기본이다. 관제·대시보드(DENSITY ≥ 8)에서는 색·선·반경·컴포넌트 문법만 가져오고,
   크기·여백·카드 규칙은 Cockpit 모드를 따른다.

## 프로파일별 추천 (먼저 여기서 1개)

| 프로파일 | 추천 slug | 성격 |
|---|---|---|
| **관제/대시보드** | `sentry` | 다크 대시보드, 데이터 밀집, 핑크·보라 강조 |
| | `linear.app` | 극단적 미니멀, 정밀, 표면 단계로 위계, 강조색 하나 |
| | `vercel` | 흑백 정밀, Geist |
| | `ibm` | Carbon 디자인 시스템, 구조적 파랑 |
| | `clickhouse` · `supabase` · `posthog` | 기술 문서형(노랑) · 다크 에메랄드 · 친근한 다크 |
| **제품/앱 UI** | `linear.app` · `notion` · `cal` | 미니멀 정밀 · 따뜻한 미니멀 · 중립 단정 |
| | `airtable` · `intercom` · `mintlify` | 구조화 데이터 · 대화형 파랑 · 읽기 최적 |
| | `stripe` · `wise` · `revolut` | 핀테크(보라 그라디언트 · 밝은 초록·친근 · 다크 카드). "토스처럼"은 `wise`부터 |
| **마케팅/랜딩** | `stripe` · `apple` · `framer` | 고급 그라디언트 · 여백·사진 · 모션 우선 |
| | `airbnb` · `spotify` · `webflow` · `clay` | 사진·둥근 UI · 대담한 다크 · 정돈된 마케팅 · 유기적 아트 디렉션 |
| | `claude` · `runwayml` · `notion` | 따뜻한 에디토리얼 · 시네마틱 · 세리프 헤드라인 |

## 전체 slug (74)

AI: `claude` `cohere` `elevenlabs` `minimax` `mistral.ai` `ollama` `opencode.ai` `replicate` `runwayml` `together.ai` `voltagent` `x.ai`
개발 도구: `cursor` `expo` `lovable` `raycast` `superhuman` `vercel` `warp`
백엔드·DB·DevOps: `clickhouse` `composio` `hashicorp` `mongodb` `posthog` `sanity` `sentry` `supabase`
생산성·SaaS: `cal` `intercom` `linear.app` `mintlify` `notion` `resend` `slack` `zapier`
디자인 도구: `airtable` `clay` `figma` `framer` `miro` `webflow`
핀테크: `binance` `coinbase` `kraken` `mastercard` `revolut` `stripe` `wise`
커머스: `airbnb` `meta` `nike` `shopify` `starbucks`
미디어·소비자: `apple` `hp` `ibm` `nvidia` `pinterest` `playstation` `spacex` `spotify` `theverge` `uber` `vodafone` `wired`
자동차: `bmw` `bmw-m` `bugatti` `ferrari` `lamborghini` `renault` `tesla`
레트로: `dell-1996` `nintendo-2001`

목록이 바뀌었을 수 있다. 없는 slug면 `https://api.github.com/repos/VoltAgent/awesome-design-md/contents/design-md`로 확인한다.
