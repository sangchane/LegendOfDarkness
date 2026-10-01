# 위 판(체력창)·위 메뉴 다시 만들기 — 레퍼런스 (2026-10-02)

사용자 요청: 체력창 테두리가 촌스럽다 → 롤(LoL) 같은 스타일, 지금 어두운 배경 테마는 유지. 메뉴 단추는 그림 + 아래 글자,
뒤가 비치게. 지도는 마이소시아 지도 심벌, 장비는 아이템 모양, 인벤은 가방 모양 — 가진 에셋에서.

시안: `shots/hud-ref/concept.png`(지금 ↔ 시안) · 원작 후보: `shots/hud-ref/original-candidates.png`.

## 롤 색 (LoL 클라이언트 CSS 토큰 — https://leonkraim.github.io/rcp-fe-lol-documentation-26.09/css/patterns/)
- 바탕 `#010A13` → `#0A1428` · 금 `#C89B3C` / 밝은 금 `#C8AA6E` / 어두운 금 `#785A28` · 밝은 글자 `#F0E6D2` · 흐린 글자 `#A09B8C`
- 경계선 `#3B3B3B`·`#5B5B5B` · 오류 `#E84057` · 성공 `#00B74F`

## 판 만드는 법 (시안이 쓴 값 — 막대 그라데이션·눈금·테두리 두께는 추정)
- 판: `#010A13` 85% 불투명, 모서리 45° 깎기 7px(둥근 모서리 아님), 테두리 바깥 3px `#785A28` + 안쪽 1px `#C8AA6E`
- 레벨: 왼쪽 위 둥근 배지(금 테두리 2px, 숫자 `#F0E6D2`) · 이름 `#C8AA6E` · 금화 오른쪽 `#C89B3C`
- 막대: 홈 `#050D18` + 1px `#3B3B3B`, 채움은 위→아래 그라데이션 + 맨 위 1px 흰 광택, 14px 마다 얇은 검은 눈금(롤 체력바처럼),
  숫자는 막대 가운데 `현재 / 최대` 밝은 글자 + 검은 외곽선. EXP 는 얇은 금 막대 + 아래 `EXP n%`
- 메뉴: 그림 + 아래 11px 글자, 판은 없거나 35% 검은 원 + 1px `#785A28` 테두리(뒤가 비친다)

## 웹 레퍼런스
- 와일드 리프트 UI 연구(ArtStation) https://www.artstation.com/artwork/YK1YWP · V5.0 HUD 투명도 조절 https://leagueoflegends.fandom.com/wiki/V5.0_(Wild_Rift)
- 롤 UI 리디자인 https://www.behance.net/gallery/222601555/League-of-Legends-UI-Redesign-Concept · https://www.artstation.com/artwork/el1vOP · 모바일 각색 https://www.artstation.com/artwork/WKaXDQ
- 오딘 UI(리니지2M 계열 메뉴) https://www.behance.net/gallery/141098539/Odin-Valhalla-Rising-UI?locale=en_US · 검은사막 https://interfaceingame.com/games/black-desert-online/
- 무료 에셋: game-icons.net(CC BY 3.0, 출처 표기 — 지도·가방·갑옷 단색 아이콘) · Kenney UI Pack RPG(CC0) https://kenney.nl/assets/ui-pack-rpg-expansion ·
  Moon Tribe(무료판 CC0) https://moon-tribe.itch.io/fantasy-rpg-ui-pack · Dark Fantasy RPG UI Kit https://vill8tion.itch.io/dark-fantasy-rpg-ui-kit-high-resolution-4k(조건 확인)

## 원작 에셋 후보 (`mobile/client/assets/item/<번호>.png`, 늘릴 땐 정수배·가장 가까운 화소)
- 지도: **원작에 월드맵 그림·마이소시아 심벌이 없다**(`data/ui-vault/인게임/월드맵.md`). 후보 — 두루마리 `35974`(32×22, 붉은 두루마리),
  `docs/ui/original-451/townBtn.png`(돌 단추), 아니면 game-icons.net 지도 아이콘을 금색으로
- 장비: 갑옷 `46328`(24×35) · 투구 `32786`(21×25) · 검 `33019`
- 인벤토리: 가죽가방 `40999`(29×30, 주황 배낭) · 복주머니 `40792`
- 원작 구슬 `docs/ui/original-451/orb001.png`(체력)·`orb002.png`(마력) — 87×85 16장, 「통째로」 규칙(451.json)
