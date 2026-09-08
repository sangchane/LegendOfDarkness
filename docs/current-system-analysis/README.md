# 현행 프로젝트 분석서

- 분석 기준일: 2026-09-08
- 조사 대상: 제공된 GitHub URL 18개 중 중복 1개를 제외한 17개 저장소
- 조사 방식: 기본 브랜치 전체 이력 클론 후 정적 소스 분석
- 신뢰도: 구조·경로·클래스는 높음, 실제 실행 가능성은 빌드/실행을 하지 않았으므로 중간

## 결론 요약

이 자료는 하나의 완성된 MMORPG 프로젝트가 아니라 서로 다른 시기의 **서버, 실험적 클라이언트, 프로토콜 분석기, 자동화 도구, 데이터 변환기** 모음이다.

- 가장 완성도 높은 전통형 서버 기준선은 `wren11/Dark-Ages-Private-Server`의 Hades/Lorule이다. C# 서버, 파일 기반 JSON 저장소, TCP 패킷 프로토콜, 맵·아이템·몬스터·스킬·스크립트를 함께 포함한다.
- 모바일 전환의 가장 가까운 기술 실험은 `FallenDev/dark-ages-ts`다. TypeScript 기반 서버와 Phaser/Svelte 브라우저 클라이언트가 한 저장소에 있지만, 아이템·스킬·NPC·전투는 아직 일부 자리표시자 수준이다.
- `Arbiter`, `DAGL`, `da-lib`, `Decipher`는 프로토콜과 데이터 형식을 이해하고 옮길 때 유용한 참고 자산이다.
- `Spark`, `SleepHunter4`, `da`, `ETDA`, `Dark-Ages-AI-Bot`은 Windows 프로세스 패치·메모리 접근·후킹에 강하게 묶여 있어 모바일 런타임 코드로 직접 재사용하기 어렵다.
- `DAMapEditor`, `PalMake`, `bmp2epf`, `DADataViewer`는 콘텐츠 제작 파이프라인의 참고 도구다. `Archivist`는 현재 실질 기능이 없는 골격이다.
- `DungMunkey/Dark-Ages`는 네트워크 MMORPG가 아니라 C++/SDL2 오프라인 게임 재현물이다.

## 문서 구성

1. [저장소 목록](01-repository-inventory.md)
2. [시스템 구성과 역할](02-system-architecture.md)
3. [빌드 방식과 실행 시작점](03-entrypoints-and-builds.md)
4. [로그인부터 게임 입장까지](04-login-to-game-flow.md)
5. [게임 데이터 저장 위치](05-game-data-locations.md)
6. [클라이언트·서버 통신](06-network-protocol.md)
7. [외부 DLL과 라이브러리](07-external-dependencies.md)
8. [모바일 전환 재사용성](08-mobile-port-assessment.md)
9. [추가 분석에 필요한 정보](09-gaps-and-required-inputs.md)

## 표기 규칙

- **확인됨**: 현재 클론의 코드나 설정에서 직접 확인했다.
- **추정**: 확인된 구조를 바탕으로 한 설계 판단이다.
- **미확인**: 실행, 외부 바이너리, 운영 데이터가 필요하다.

