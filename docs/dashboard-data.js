(function (root, factory) {
  "use strict";
  var data = factory();
  if (typeof module === "object" && module.exports) { module.exports = data; }
  root.LOD_DASHBOARD_DATA = data;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";
  var knowledge = [
    { category: "characters", label: "캐릭터", title: "캐릭터·계정", status: "verified", statusLabel: "확인됨", summary: "런타임 캐릭터는 JSON으로 생성·저장됩니다. 모바일에서는 성장·장비·상태 UI가 새로 필요합니다.", source: "database/server/aislings/<username>.json", next: "원자 저장과 복구(P0-15)" },
    { category: "characters", label: "캐릭터", title: "직업·성장 규칙", status: "partial", statusLabel: "부분 확인", summary: "Hades 도메인 모델과 설정에 규칙이 있으나 모바일용 성장 정보 구조는 아직 정리되지 않았습니다.", source: "Hades Aisling / ServerConstants", next: "직업·스탯·승급 규칙 목록화" },
    { category: "skills", label: "스킬", title: "기본 공격 Assail", status: "active", statusLabel: "검증 예정", summary: "첫 모바일 전투의 기준 스킬입니다. 서버 권위 판정과 피해 공식의 정확성을 P0-16에서 고정합니다.", source: "scripts/Skills/Assail.cs", next: "P0-16 기본 전투 정확성" },
    { category: "skills", label: "스킬", title: "스킬 템플릿·스크립트", status: "verified", statusLabel: "확인됨", summary: "스킬 정의 JSON과 실행 C# 스크립트가 분리되어 있어 서버 규칙을 유지할 수 있습니다.", source: "templates/skills/*.json · scripts/Skills/*.cs", next: "전체 목록·쿨다운·소모값 색인" },
    { category: "spells", label: "마법", title: "마법 템플릿·스크립트", status: "partial", statusLabel: "부분 확인", summary: "마법 실행 스크립트는 있으나 실제 데이터 완전성을 확인해야 합니다.", source: "templates/spells/*.json · scripts/Spells/**/*.cs", next: "효과·시전 조건 대조" },
    { category: "quests", label: "퀘스트", title: "퀘스트 권위 데이터", status: "unknown", statusLabel: "미확인", summary: "독립된 퀘스트 저장소는 확인되지 않았습니다. NPC 메뉴·대화·스크립트에 흩어졌는지 조사해야 합니다.", source: "미확인", next: "NPC 스크립트에서 퀘스트 흐름 추출" },
    { category: "world", label: "월드", title: "맵·지역·워프", status: "verified", statusLabel: "확인됨", summary: "맵 5개, 지역 4개와 워프·월드맵 템플릿이 서버 권위 데이터로 존재합니다.", source: "maps/lod<ID>.map · areas · templates/warps", next: "Godot 좌표·충돌·이동 패킷 연결" },
    { category: "world", label: "월드", title: "NPC·몬스터", status: "partial", statusLabel: "부분 확인", summary: "몬스터 템플릿과 스크립트는 존재합니다. NPC 메뉴 경로는 두 갈래라 런타임 확인이 필요합니다.", source: "templates/monsters · scripts/Monsters · scripts/Mundanes", next: "NPC 클릭→메뉴→대화 검증" },
    { category: "world", label: "아이템", title: "아이템·장비", status: "partial", statusLabel: "부분 확인", summary: "서버 템플릿과 장착 로직은 재사용 후보입니다. 원작 아이콘 변환은 남아 있습니다.", source: "templates/items/*.json · scripts/Items/*.cs", next: "ia.dat 아이콘과 인벤토리 연결" },
    { category: "operations", label: "운영", title: "계정·보안 운영", status: "risk", statusLabel: "통제 미구현", summary: "레거시 인증과 프로토콜 암호화는 존재하지만 비밀번호 해시·TLS·세션 보호·RBAC 같은 공개 운영 통제는 미구현입니다.", source: "docs/operations/security-maintenance.md", next: "비밀번호 해시·TLS·세션·RBAC 구현" },
    { category: "operations", label: "운영", title: "로그·지표·백업", status: "partial", statusLabel: "기준 문서화", summary: "장애·복구 기준은 마련됐지만 중앙 관측, 불변 백업과 복원 자동화는 구현되지 않았습니다.", source: "docs/operations/backup-restore.md", next: "감사 로그·불변 백업·복원 리허설 구현" }
  ].map(Object.freeze);
  var components = [
    { id: "mobile", name: "Godot 모바일 클라이언트", kind: "application", lifecycle: "experimental", status: "active", responsibility: "화면·입력·월드 표현과 모바일 생명주기", source: "mobile/client + mobile/src/Lod.Mobile.Core", dependsOn: ["protocol", "assets"] },
    { id: "protocol", name: "레거시 프로토콜 계층", kind: "library", lifecycle: "stabilizing", status: "partial", responsibility: "0xAA 프레임, opcode, 암호화, 세션 통신", source: "Hades.Server.Base/Network + docs/current-system-analysis/06-network-protocol.md", dependsOn: ["server"] },
    { id: "server", name: "Hades/Lorule 서버", kind: "service", lifecycle: "baseline", status: "active", responsibility: "인증·월드·전투·게임 규칙의 서버 권위", source: "sources/wren11/Dark-Ages-Private-Server", dependsOn: ["content"] },
    { id: "content", name: "게임 콘텐츠 저장소", kind: "resource", lifecycle: "baseline", status: "partial", responsibility: "캐릭터·맵·아이템·스킬·마법·스크립트", source: "Dark-Ages-Private-Server/database/server", dependsOn: [] },
    { id: "assets", name: "원작 자산 변환", kind: "pipeline", lifecycle: "experimental", status: "partial", responsibility: "DAT/EPF/BMP를 모바일용 리소스로 변환", source: "D:/_personal/LOD_ + 변환 스크립트", dependsOn: ["archive"] },
    { id: "archive", name: "레거시 자료 아카이브", kind: "external", lifecycle: "reference", status: "verified", responsibility: "클라이언트 7.41·DAT·추출 리소스의 보존 원본", source: "D:/_personal/LOD_", dependsOn: [] },
    { id: "delivery", name: "검증·지식 그래프", kind: "tooling", lifecycle: "active", status: "active", responsibility: "특성화 테스트, 변경 근거, 코드·문서 연결 탐색", source: "tests + graphify-out/graph.json", dependsOn: ["mobile", "server"] }
  ].map(function (entry) { return Object.freeze(Object.assign({}, entry, { dependsOn: Object.freeze(entry.dependsOn.slice()) })); });
  var flows = [
    { id: "login", label: "로그인→월드 입장", status: "verified", summary: "두 TCP 포트를 거쳐 캐릭터를 다시 적재하고 월드 상태를 전송합니다.", steps: [
      { component: "모바일 클라이언트", action: "버전·로그인 요청 전송", source: "Godot WorldSession / 로그인 UI", evidence: "모바일 smoke 및 로그인 시나리오", signal: "로그인 성공률·지연: 미계측" },
      { component: "LoginServer", action: "버전 확인, 계정 JSON 조회, redirect 발급", source: "Network/Login/LoginServer.cs", evidence: "docs/current-system-analysis/04-login-to-game-flow.md", signal: "인증 실패 사유·rate limit: 미계측" },
      { component: "GameServer", action: "redirect 검증 후 캐릭터·장비·스킬 적재", source: "Network/Game/GameServerHandlers.cs", evidence: "Format10Handler / EnterGame 코드 확인", signal: "입장 실패율·적재 시간: 미계측" },
      { component: "GameClient", action: "맵과 주변 객체를 보내 월드 입장 확정", source: "Network/Game/GameClient.cs", evidence: "Refresh / LoggedIn 코드 확인", signal: "입장 완료율·세션 수: 미계측" }
    ]},
    { id: "movement", label: "이동→주변 동기화", status: "active", summary: "현재 작업의 중심 흐름입니다. 좌표·가시 범위·화면 표현을 하나의 검증 단위로 묶습니다.", steps: [
      { component: "Godot 입력", action: "터치/방향 입력을 이동 의도로 변환", source: "mobile/client + mobile/src/Lod.Mobile.Core", evidence: "실기기 입력 gate 필요", signal: "입력→표시 지연: 미계측" },
      { component: "프로토콜 계층", action: "이동 opcode와 좌표를 인코딩", source: "NetworkPacketWriter + ClientFormats", evidence: "06-network-protocol.md", signal: "잘못된 패킷 수: 미계측" },
      { component: "Hades 월드", action: "서버 좌표와 주변 객체 상태 갱신", source: "GameServerHandlers / Map / Aisling", evidence: "P0 특성화 범위 확장 필요", signal: "거부 이동·처리 지연: 미계측" },
      { component: "Godot 월드", action: "맵·사람·이동을 실제 화면 좌표로 반영", source: "WorldSession", evidence: "NEXT-ACTION 현재 작업", signal: "동기화 불일치 수: 미계측" }
    ]},
    { id: "combat", label: "공격→전투 결과", status: "partial", summary: "서버 권위 판정과 클라이언트 표현의 경계를 검증해야 합니다.", steps: [
      { component: "모바일 HUD", action: "대상과 기본 공격을 선택", source: "index.html 화면 실험실", evidence: "조작 시안만 존재", signal: "입력 실패·취소율: 미계측" },
      { component: "프로토콜 계층", action: "공격 요청을 opcode로 전달", source: "ClientFormats / ServerFormats", evidence: "패킷 교차 확인 필요", signal: "요청량·거부율: 미계측" },
      { component: "Hades 전투 규칙", action: "Assail 판정·피해·상태 변경", source: "database/server/scripts/Skills/Assail.cs", evidence: "P0-16 기본 전투 정확성", signal: "판정 오류·처리 시간: 미계측" },
      { component: "모바일 표현", action: "체력·효과·전투 로그 반영", source: "Godot 전투 UI 예정", evidence: "E2E 시나리오 미작성", signal: "서버/화면 불일치: 미계측" }
    ]},
    { id: "content", label: "콘텐츠 변경→배포", status: "unknown", summary: "운영자 변경이 검토·검증·버전 기록을 거쳐 배포되는 경로가 아직 없습니다.", steps: [
      { component: "콘텐츠 원본", action: "아이템·스킬·퀘스트 정의 변경", source: "database/server/templates + scripts", evidence: "변경 스키마·소유자 미정", signal: "변경량·검증 실패: 미계측" },
      { component: "자동 검증", action: "스키마·참조·게임 규칙 테스트", source: "tests 예정", evidence: "콘텐츠 계약 테스트 미작성", signal: "검증 통과율: 미계측" },
      { component: "검증/CI", action: "테스트와 빌드 근거 생성", source: "tests + CI 예정", evidence: "로컬 검증은 존재, CI는 미연결", signal: "lead time·change fail rate: 미계측" },
      { component: "운영 배포", action: "버전 호환 확인 후 적용·롤백", source: "docs/operations/release-rollback.md", evidence: "릴리스·롤백 기준 문서화, 자동화 미구현", signal: "배포 빈도·복구 시간: 미계측" }
    ]}
  ].map(function (flow) { return Object.freeze(Object.assign({}, flow, { steps: Object.freeze(flow.steps.map(Object.freeze)) })); });
  return Object.freeze({ updatedAt: "2026-09-11", knowledge: Object.freeze(knowledge), components: Object.freeze(components), flows: Object.freeze(flows) });
});
