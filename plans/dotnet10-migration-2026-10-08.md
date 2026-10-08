# .NET 9 → .NET 10 전환 계획 (2026-10-08)

리뷰 2026-10-08 #14 의 「지원 런타임 전환 계획」. 이번 작업에서는 바꾸지 않았다 — 클라우드 런타임 설치와 앱(Godot·iOS)이 함께 걸린다.

## 왜
- .NET 9(STS) 지원 종료 **2026-11-10**. 그 뒤로는 보안 패치가 없다. .NET 10 은 LTS(2028-11 까지). [지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- 지금 net9.0: 서버(`Lorule.GameServer`, `Hades.Server.Base`), 모바일 알맹이·봇(`mobile/src`, `mobile/bots`), 앱(`mobile/client/LodClient.csproj`), 시험 둘.
- 저장소 SDK `.tools/dotnet-9.0.317` 하나, 클라우드 런타임 `/opt/dotnet`(`cloud-server.sh setup` 이 `--channel 9.0` 으로 깜).

## 순서 (서버·봇 먼저, 앱은 따로)
1. **SDK** — `.tools/dotnet-10.0.x` 를 나란히 둔다(9 는 지우지 않음). 스크립트가 쓰는 SDK 경로(`cloud-server.sh`, `ios-build.sh`, 시험 안내)를 한 변수로 바꿀 곳만 적어 둔다.
2. **서버·봇·알맹이** — TargetFramework `net10.0`. 빌드 경고·오류, 서버 격리 시험 전체, Core 시험 전체를 9 때와 비교(리뷰 때 실패 6건 외 새 실패 0).
   - 확인할 것: ServiceStack 5.10·옛 Microsoft.Extensions 5.0·Roslyn 스크립팅 3.8 이 .NET 10 에서 그대로 도는지(서버는 켤 때 `database/server/scripts` 를 컴파일한다).
3. **클라우드** — `/opt/dotnet` 에 **10 런타임을 9 와 나란히** 설치(`dotnet-install.sh --runtime dotnet --channel 10.0`). 9 는 되돌림용으로 남긴다.
   배포 전환·되돌림(리뷰 #4 의 `.prev`)으로 올리고, 확인 실패면 옛 net9 산출물로 돌아간다. 캐릭터·경매 자료 형식은 바뀌지 않는다.
4. **앱** — Godot 4.6 C# 가 net10.0 을 대상으로 내보낼 수 있는지(특히 iOS NativeAOT)를 **먼저 확인**한다. 안 되면 앱은 Godot 이 지원할 때까지 net9 로 두고,
   알맹이(`Lod.Mobile.Core`)는 `net9.0;net10.0` 둘 다로 빌드한다. 앱은 사용자 기기 안에서만 돌아 서버만큼 급하지 않다.

## 완료 기준
- 서버·봇이 클라우드에서 net10 으로 돌고, `cloud-server.sh status` 의 dotnet 프로세스가 10 런타임.
- `dotnet list package --vulnerable --include-transitive` 0건 유지.
- 서버 격리 시험·Core 시험이 9 때와 같은 결과.

## 하지 않을 것
- 오래된 패키지(ServiceStack 5, Extensions 5) 대규모 교체 — 10 에서 실제로 깨질 때만.
