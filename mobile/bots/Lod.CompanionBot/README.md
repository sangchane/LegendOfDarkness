# Lod.CompanionBot — 동료 봇(성직자)

성직자 캐릭터로 서버에 접속해 기다리다가, 누가 앱에서 [동료 부르기]를 누르면 서버가 이 봇을 그 사람 곁으로 옮기고
주인으로 정해 준다(우리 확장 `0x5E`). 그때부터 따라다니며 회복·버프를 건다. 끊기면 5초~1분 간격으로 다시 접속한다.

## 돌리기

```bash
cp companion-bot.example.json companion-bot.json   # 비밀번호를 적는다 — 이 파일은 커밋하지 않는다(.gitignore)
DOTNET_ROOT=../../../.tools/dotnet-9.0.317 ../../../.tools/dotnet-9.0.317/dotnet run -- companion-bot.json
```

- `Name` 은 서버 설정 `ServerConfig.CompanionBots` 에 적힌 이름과 같아야 한다(서버는 그 이름만 동료로 쓴다).
- 계정이 없으면 처음 접속할 때 **성직자**로 만든다(옷은 서버가 성직자 기본 옷을 입힌다).
- `MapFolder` 는 벽 파일(`map번호.txt`, 앱의 `mobile/client/assets/world`) 폴더 — 설정 파일 자리에서 센다. 없으면 벽을 모른 채 걷는다.
- `HealOwnerPercent`(기본 70) · `HealSelfPercent`(기본 50) — 이 % 아래면 회복.

## 판단

알맹이 `Lod.Mobile.Core/World/Companion.cs` 의 `CompanionBrain`(시험 `mobile/tests/.../CompanionTests.cs`):
멈춤(혼수·죽음) > 주인 회복 > 봇 체력 포션(40%) > 자기 회복 마법 > 봇 마력 포션(30%) > 해제(디나르콜리·디소루마) > 버프 유지(서버가 알린 상태에 없을 때만) > 따라가기(3칸 넘으면 걷고 2칸 안이면 선다) > 쉬기 > 대기.
포션 기준은 설정 `PotionHealthPercent`·`PotionManaPercent` 로 바꾼다. 포션·장비는 주인이 앱의 봇 장비창에서 넘겨 준다.
레벨·마법·체력은 서버가 부를 때마다 맞춘다(부른 사람 레벨 −2). 자세한 것은 `docs/mobile-client.md` 「동료 봇」.

## 클라우드

`scripts/cloud-server.sh deploy` 가 이 프로그램과 벽 파일을 올리고 `lod-bot` 서비스를 깐다.
처음 한 번 `scripts/cloud-server.sh bot-config` — 비밀번호를 묻고 클라우드의 `~/lod-bot/companion-bot.json` 에만 적는다.
