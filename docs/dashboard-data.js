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
  return Object.freeze({ updatedAt: "2026-09-11", knowledge: Object.freeze(knowledge) });
});
