# 월드맵을 메뉴 단추로 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development` 로 작업 단위로 실행한다.

**Goal:** 숨은 칸을 밟지 않아도 **마을에서 단추 하나로** 월드맵을 열고, 열었다가 그냥 닫을 수 있게 한다.
그래서 계정을 만든 새 캐릭터가 노비스마을에서 시작해 포테의숲까지 한 줄로 갈 수 있다.

**Architecture:** 서버가 "월드맵을 연다"를 정하고, 열린 동안 고르기 말고 모든 패킷을 버린다
(`NetworkServer.cs:141`). 그래서 화면에 단추만 달아서는 안 되고 **서버에 말 두 개를 새로 만든다**:
① 클라이언트 → 서버 `0xF0` "월드맵 열어 줘" · ② 기존 `0x3F` 에 **갈 맵 번호 0 = 취소**.
알맹이에 그 둘을 보내는 길을 내고, 화면에 아이콘 단추와 닫기 단추를 붙인다. **서버는 포크에서 고친다.**

**Tech Stack:** 서버 C#/.NET 5 (Hades 포크 `sangchane/Dark-Ages-Private-Server`) · 알맹이 C#/.NET 9 ·
화면 Godot 4.6 + C# · 시험 xunit

**Spec:** 이 문서의 "사실" 절. 바탕: `plans/worldmap-suomi-pote.md`(앞 작업) · `docs/original-ui-451.md`(화면 규칙표) ·
`docs/mobile-client.md`(실행·인자·함정)

## 사용자 결정 (2026-09-19)

- **마을에서만 열린다.** 사냥터·던전에서는 안 열린다.
- **취소를 넣는다.** 열었다가 그냥 닫을 수 있다.

## 사실 — 고치기 전에 확인한 것 (2026-09-19)

| 무엇 | 어디 | 값 |
|---|---|---|
| 새 캐릭터 시작 자리 | `scripts/server-config/LoruleConfig.template.json:27-31` | **이미 노비스마을(20373) (37,29)** 다. 게시판알리미(34,29)에서 3칸. **고칠 것 없다** |
| 서버가 실제로 읽는 설정 | `sources/wren11/…/Staging/net9.0/LoruleConfig.json` | 위 틀에서 만들어진다(`scripts/lod-server.sh config`). submodule 안의 `src/Lorule.Config/LoruleConfig.json`(StartingMap 1)은 **덮어써지는 원본**이라 고쳐도 소용없다 |
| 노비스마을에서 걸어 닿는 곳 | 워프 자료 | 18곳. **노비스평원A(20393)·B(20394) 포함** — 레벨을 올릴 수 있다. 우드랜드·수오미·포테의숲에는 **못 걸어간다** |
| 노비스마을의 월드맵 칸 | `templates/warps/` | **(63,49) 한 칸뿐.** 시작 자리에서 멀고, 있는 줄 모르면 못 찾는다 — 이 계획이 푸는 문제다 |
| 월드맵 목록 | `templates/worldmaps/temuair.json` | 지금 **2곳**(수오미 20355 (40,11) · 우드랜드입구 20028 (10,18)). 앞 작업에서 줄였다 |
| 맵을 마을로 가릴 수 있나 | `areas/*.json` 의 `Flags` | **못 가린다** — 801개가 전부 `106240` 으로 같다. `MapFlags.PlayerKill`(16384)은 아무 맵도 안 켜져 있다 |
| 대신 쓸 기준 | `Sprite.cs:569` 따위 | `GetObjects<Monster>(Map, …)` — **그 맵에 살아 있는 괴물이 있나**. 마을·상점·집은 없고 사냥터·던전은 있다 |
| 빈 명령 번호 | `Network/ClientFormats/` | 쓰는 것: 00 02 03 04 05 06 07 08 0B 0E 0F 10 11 13 18 19 1B 1C 1D 24 26 29 2A 2D 2E 2F 30 32 38 39 3A 3B 3E 3F 43 44 45 47 4A 4B 4D 4E 4F 57 66 68 75 79 7B 89. **`0xF0` 은 비어 있다** |
| 명령이 어떻게 붙나 | `NetworkFormatManager.cs:18` · `NetworkServer.cs:42` | 이름으로 찾는다 — 클래스 `ClientFormatF0` 을 만들고 핸들러 `FormatF0Handler` 를 쓰면 **등록은 저절로 된다** |
| 취소가 지금 안 되는 까닭 | `GameServerHandlers.cs:1853,1870-1874` | `Portals.Find` 가 못 찾으면 `PendingNode` 가 `null` 이고 `TraverseWorldMap` 이 **`MapOpen` 을 안 내리고 그냥 돌아간다** — 영영 갇힌다 |
| 말 한 줄 보내기 | `GameServerHandlers.cs:259` | `client.SendMessage(0x02, "…")` |

### 왜 0xF0 인가

원작 클라이언트는 0x80 넘는 명령을 보내지 않는다. 높은 번호를 쓰면 **원작 패킷을 잘못 읽을 일이 없다** —
이 번호는 우리 모바일 클라이언트만 쓰는 자리다.

## Global Constraints

- **서버는 포크(`sangchane/Dark-Ages-Private-Server`)에서 고친다.** submodule 안에서 커밋하고 **push 하지 않는다**. 부모에서는 포인터만 옮긴다.
- **다른 세션이 같은 체크아웃에서 일한다.** `git add -A`·`git add .`·`git commit -a` **금지**. 파일 이름으로만 add 하고, 그 파일이 이미 더러우면 `git add -p` 로 제 조각만 담는다. 이력 조작(`reset`·`amend`·`revert`) 금지.
- **소지품(가방) 파일은 건드리지 않는다**(`mobile/client/src/PackPanel.cs` 등).
- 그냥 `git status` 금지 — `git status --short --ignore-submodules=all`.
- `dotnet` 이 PATH 에 없다 — `export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"`.
- 시험·빌드는 **앞에서** 돌린다(배경 금지). Bash `timeout` 을 600000 까지 준다.
- 화면 규칙은 `docs/original-ui-451.md` — `Greybox.Stone()` · `Main.TouchMinimum` · `Main.Gutter`.
- 커밋은 Conventional Commits + 한국어 제목. 부모 저장소 커밋 끝에 두 줄:
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>` ·
  `Claude-Session: https://claude.ai/code/session_01Ummb8CUxgpbMht8WVivCvH`

---

### Task 1: 서버 — 월드맵을 열어 달라는 말과 취소

**Files:**
- Create: `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Network/ClientFormats/ClientFormatF0.cs`
- Modify: `sources/wren11/…/Network/FormatStubs/ServerFormatStubs.cs` · `…/FormatStubs/NetworkServerStubs.cs` (빈 `virtual` 스텁 두 개)
- Modify: `sources/wren11/…/Network/Game/GameServerHandlers.cs` (`FormatF0Handler` 새로 · `Format3FHandler` 에 취소 갈래)
- Test: `tests/hades-characterization/WorldMapMenuTests.cs` (새 파일)

**Interfaces:**
- Consumes: `PortalSession.ShowFieldMap(GameClient)` · `Sprite.GetObjects<Monster>(Map, …)` · `client.SendMessage(byte, string)`
- Produces: 클라이언트가 보낼 수 있는 `0xF0`(몸통 없음) · `0x3F` 의 **갈 맵 번호 0 = 취소**

- [ ] **Step 1: 실패하는 시험을 쓴다**

`tests/hades-characterization/WorldMapMenuTests.cs`:

```csharp
using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵을 숨은 칸이 아니라 **말 한 마디로** 연다. 마을에서만 열리고(사냥터에서 열면 싸우다 갇힌다),
/// 열었다가 그냥 닫을 수도 있다(사용자 결정, 2026-09-19).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WorldMapMenuTests : IDisposable
{
    private const int NoviceTown = 20373;
    private const int NovicePlain = 20393;
    private const int SuomiTown = 20355;

    private const string Name = "menuwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Asking_for_the_world_map_in_town_opens_it()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NoviceTown, "노비스마을에 들어가지 못했습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);

        await Waiting.Until(() => world.Field is not null, "마을에서 월드맵을 달라고 했는데 오지 않았습니다.", _deadline.Token);
        Assert.Contains(world.Field!.Nodes, node => node.Name == "수오미");
    }

    [Fact]
    public async Task Asking_for_it_where_monsters_are_is_refused()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlain, 25, 25));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NovicePlain, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);

        // 괴물이 젠될 때까지 기다린다 — 젠 관리자가 세우기 전에 물으면 마을처럼 보인다.
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "노비스평원A 에 괴물이 나오지 않았습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        Assert.Null(world.Field);

        // 거절당했어도 갇히면 안 된다 — 걸음이 그대로 닿는다.
        Tile before = world.State!.Where;
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Where != before, _deadline.Token);
        Assert.NotEqual(before, world.State!.Where);
    }

    [Fact]
    public async Task Closing_the_world_map_gives_movement_back()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NoviceTown, "노비스마을에 들어가지 못했습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null, "월드맵이 오지 않았습니다.", _deadline.Token);

        await world.CloseFieldAsync(_deadline.Token);

        await Waiting.Until(() => world.Field is null, "닫았는데 월드맵이 그대로입니다.", _deadline.Token);

        // 여기가 요점이다 — 닫은 뒤 걸음이 서버에 닿아야 한다.
        Tile before = world.State!.Where;
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Where != before, _deadline.Token);
        Assert.NotEqual(before, world.State!.Where);
        Assert.Equal(NoviceTown, world.State!.Map.Id);
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다**

```bash
export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"
dotnet test tests/hades-characterization --filter WorldMapMenuTests
```
기대: 컴파일 실패 — `OpenFieldAsync`·`CloseFieldAsync` 가 없다. (그 둘은 Task 2 다. **Task 1 은 서버만 고치고, 이 시험은 Task 2 가 끝나야 통과한다.** Task 1 의 GREEN 은 아래 Step 5 의 서버 빌드와 수동 확인이다.)

- [ ] **Step 3: 새 말을 만든다**

`ClientFormatF0.cs`:

```csharp
namespace Darkages.Network.ClientFormats
{
    /// <summary>
    /// "월드맵을 열어 줘." 원작에는 없는 말이다 — 원작은 바닥의 숨은 칸을 밟아야 열렸고, 모바일에서는
    /// 그 칸을 찾을 길이 없어 메뉴 단추를 두었다(사용자, 2026-09-19). 원작 클라이언트는 0x80 넘는 명령을
    /// 보내지 않으므로 이 번호는 우리 클라이언트 전용이다.
    /// </summary>
    public class ClientFormatF0 : NetworkFormat
    {
        public ClientFormatF0()
        {
            Secured = true;
            Command = 0xF0;
        }

        public override void Serialize(NetworkPacketReader reader)
        {
        }

        public override void Serialize(NetworkPacketWriter writer)
        {
        }
    }
}
```

`ServerFormatStubs.cs` 와 `NetworkServerStubs.cs` 의 `Format3FHandler` 스텁 옆에 각각:

```csharp
        protected virtual void FormatF0Handler(TClient client, ClientFormatF0 format)
        {
        }
```

- [ ] **Step 4: 핸들러를 쓴다**

`GameServerHandlers.cs`, `Format3FHandler` 옆에:

```csharp
        /// <summary>
        /// 월드맵을 열어 달라는 말. 괴물이 있는 맵에서는 거절한다 — 창이 열린 동안 서버는 고르기 말고
        /// 이 접속의 패킷을 모두 버리므로(`NetworkServer.cs:141`), 싸우는 중에 열면 손이 묶인다.
        /// </summary>
        protected override void FormatF0Handler(GameClient client, ClientFormatF0 format)
        {
            if (client?.Aisling == null || !client.Aisling.LoggedIn)
                return;

            if (client.MapOpen)
                return;

            if (client.Aisling.Map == null || !ServerContext.GlobalWorldMapTemplateCache.ContainsKey(client.Aisling.World))
                return;

            var monsters = client.Aisling.GetObjects<Monster>(client.Aisling.Map, i => i != null && i.Alive);

            if (monsters != null && monsters.Any())
            {
                client.SendMessage(0x02, "이곳에서는 지도를 펼 수 없습니다.");
                return;
            }

            client.Aisling.PortalSession = new PortalSession { FieldNumber = client.Aisling.World };
            client.Aisling.PortalSession.ShowFieldMap(client);
        }
```

주: `GetObjects<T>` 를 부르는 정확한 모양은 `Types/Sprite.cs:569` 를 본보기로 맞춘다(그 파일이 같은 일을 한다).
`ShowFieldMap` 은 `EnterAbyss()` 를 부르지 **않는다** — 메뉴로 여는 것이므로 남들 눈에서 사라질 까닭이 없다.

그리고 `Format3FHandler` 의 맨 앞(로그인 확인 뒤)에 취소 갈래를 더한다:

```csharp
            // 갈 맵 0 = 취소. 원작에는 없지만 메뉴로 여는 이상 닫을 길이 있어야 한다(사용자, 2026-09-19).
            // 여기서 MapOpen 을 내리지 않으면 이 접속은 영영 걸음도 말도 못 한다.
            if (format.Index == 0)
            {
                client.PendingNode = null;
                client.MapOpen = false;
                client.Aisling.PortalSession = new PortalSession { IsMapOpen = false };

                if (client.Aisling.Abyss)
                    client.Aisling.LeaveAbyss(client);

                client.Refresh();
                return;
            }
```

- [ ] **Step 5: 서버를 빌드한다**

```bash
export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"
dotnet build sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base
```
기대: 경고 없이 성공. 실패하면 `GetObjects` 의 모양을 `Types/Sprite.cs:569` 에 맞춘다.

- [ ] **Step 6: 커밋 (두 저장소)**

```bash
git -C sources/wren11/Dark-Ages-Private-Server add \
  src/Hades.Server.Base/Network/ClientFormats/ClientFormatF0.cs \
  src/Hades.Server.Base/Network/FormatStubs/ServerFormatStubs.cs \
  src/Hades.Server.Base/Network/FormatStubs/NetworkServerStubs.cs \
  src/Hades.Server.Base/Network/Game/GameServerHandlers.cs
git -C sources/wren11/Dark-Ages-Private-Server commit -m "feat: 월드맵을 말 한 마디로 열고 닫는다" -m "0xF0 = 열어 줘(마을에서만) · 0x3F 의 갈 맵 0 = 취소."
git add sources/wren11/Dark-Ages-Private-Server tests/hades-characterization/WorldMapMenuTests.cs
git commit
```
**`push` 하지 않는다.**

---

### Task 2: 알맹이 — 열어 달라고 말하고, 닫는다

**Files:**
- Modify: `mobile/src/Lod.Mobile.Core/World/WorldClient.cs`
- Test: `mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs` (있는 파일에 더한다)

**Interfaces:**
- Consumes: Task 1 의 `0xF0` 와 `0x3F` 취소 · 이미 있는 `private Task Send(byte, byte[], CancellationToken)` · `FieldChoice(int)`
- Produces:
  - `public Task WorldClient.OpenFieldAsync(CancellationToken)`
  - `public Task WorldClient.CloseFieldAsync(CancellationToken)`

- [ ] **Step 1: 실패하는 시험을 쓴다**

`mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs` 에 더한다:

```csharp
    [Fact]
    public void Closing_the_field_sends_a_map_number_of_zero()
    {
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, WorldClient.FieldChoice(0));
    }
```

- [ ] **Step 2: 돌려서 실패를 본다**

```bash
export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"
dotnet test mobile/tests/Lod.Mobile.Core.Tests --filter WorldMapTests
```
기대: 통과한다(`FieldChoice` 는 이미 있다). 이 시험은 **취소가 0 이라는 약속을 못 박는 것**이고,
진짜 RED 는 아래 Step 3 의 `OpenFieldAsync` 가 없어서 나는 컴파일 실패다(Task 1 의 시험이 그것을 부른다).

- [ ] **Step 3: 보내는 길 둘을 넣는다**

`WorldClient.cs`, `ChooseFieldAsync` 옆에:

```csharp
/// <summary>
/// 월드맵을 열어 달라고 서버에 말한다. 원작에는 없는 말이다 — 원작은 바닥의 숨은 칸을 밟아야 열렸다.
/// 마을이 아니면 서버가 거절하고 말 한 줄만 돌려준다(싸우는 중에 열면 손이 묶이기 때문이다).
/// </summary>
public Task OpenFieldAsync(CancellationToken cancellationToken) =>
    Send(OpenFieldCommand, [], cancellationToken);

/// <summary>
/// 월드맵을 그냥 닫는다. 갈 맵 번호 0 이 취소라고 서버와 약속했다 — 0 은 어느 맵의 번호도 아니다.
/// </summary>
public Task CloseFieldAsync(CancellationToken cancellationToken) =>
    Send(ChooseFieldCommand, FieldChoice(0), cancellationToken);
```

상수 칸에:

```csharp
/// <summary>월드맵을 열어 달라는 말. 원작 클라이언트는 0x80 넘는 명령을 보내지 않으므로 이 번호는 우리 것이다.</summary>
private const byte OpenFieldCommand = 0xF0;
```

- [ ] **Step 4: 돌려서 통과를 본다**

```bash
dotnet test mobile/tests/Lod.Mobile.Core.Tests
dotnet test tests/hades-characterization --filter WorldMapMenuTests
```
기대: 알맹이 전부 통과 · `WorldMapMenuTests` 3개 통과(Task 1 의 서버 변경과 맞물려야 한다).

- [ ] **Step 5: 커밋**

```bash
git add mobile/src/Lod.Mobile.Core/World/WorldClient.cs mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs
git commit -m "feat(mobile): 월드맵을 열어 달라고 말하고, 닫는다"
```

---

### Task 3: 화면 — 메뉴 아이콘과 닫기

**Files:**
- Modify: `mobile/client/src/FieldPanel.cs` (닫기 단추)
- Modify: `mobile/client/src/GameScreen.cs` (아이콘 단추 · 배선)

**Interfaces:**
- Consumes: Task 2 의 `OpenFieldAsync`·`CloseFieldAsync` · 이미 있는 `Field`·`ChooseFieldAsync`·`_chosenField`
- Produces: `FieldPanel.Close`(Button) · 위 줄의 "지도" 단추

- [ ] **Step 1: 창에 닫기를 붙인다**

`FieldPanel.cs` 의 머리줄(`_title` 이 있는 줄)을 `TalkPanel` 처럼 바꾼다 — 이름 왼쪽, 닫기 오른쪽:

```csharp
    /// <summary>창을 그냥 닫는다. 서버에 "취소"를 보내야 조작이 돌아온다 — 화면만 숨기면 손이 묶인 채다.</summary>
    public Button Close { get; }
```

만드는 곳에서:

```csharp
        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_title);

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        head.AddChild(Close);
```

그리고 `inside.AddChild(_title);` 를 `inside.AddChild(head);` 로 바꾼다.

**앞 작업의 `<remarks>` 주석(닫기가 없는 까닭)은 이제 틀렸다 — 고쳐 쓴다**: 이제 닫을 수 있고,
닫으면 서버에 취소를 보낸다는 뜻으로.

- [ ] **Step 2: 위 줄에 "지도" 단추를 단다**

`GameScreen.cs` 의 `BuildTopRow()` 안, 지금 "인벤토리" 단추를 만드는 자리 옆에 같은 모양으로 하나 더 만든다.
글자는 `지도`. 크기·틀은 옆 단추와 똑같이 맞춘다(그 함수가 쓰는 것을 그대로 쓴다 — 새 치수를 만들지 마라).

배선:

```csharp
        _map.Pressed += () => _ = _server?.OpenFieldAsync(System.Threading.CancellationToken.None);
```

`_field.Close.Pressed` 는:

```csharp
        _field.Close.Pressed += () =>
        {
            _field.Visible = false;
            _ = _server?.CloseFieldAsync(System.Threading.CancellationToken.None);
        };
```

**주의**: `_chosenField` 는 **건드리지 마라.** 취소는 고른 것이 아니므로, 서버가 창을 거두면
`Field` 가 `null` 이 되어 `_Process` 가 알아서 정리한다.

- [ ] **Step 3: 빌드한다**

```bash
export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"
dotnet build mobile/client
```
기대: 경고 0.

- [ ] **Step 4: 커밋**

```bash
git add mobile/client/src/FieldPanel.cs mobile/client/src/GameScreen.cs
git commit -m "feat(mobile): 지도 단추로 월드맵을 열고 닫는다"
```

---

### Task 4: 계정 만들기에서 포테의숲까지 한 줄로

**Files:**
- Modify: `tests/hades-characterization/WorldMapMenuTests.cs`
- Modify: `docs/mobile-client.md` · `NEXT.md`

**Interfaces:**
- Consumes: Task 1~3 전부 · 앞 작업의 `WorldMapTests` 의 벽 읽기·길찾기 방식
- Produces: 없음

- [ ] **Step 1: 여정 시험을 쓴다**

`WorldMapMenuTests.cs` 에 [Fact] 를 하나 더한다. 하는 일:
계정을 새로 만들고 → 노비스마을 (37,29) 에서 시작해 → **레벨을 21 로 올리고**(저장 파일의 `ExpLevel`,
앞 작업 `WorldMapTests` 가 하는 그대로 — 실제로 싸워서 올리는 것은 `NovicePlay` 가 따로 덮는다) →
`OpenFieldAsync` 로 지도를 열고 → 수오미를 고르고 → 수오미마을을 가로질러 → 포테의숲1존 (33,47) 에 닿는다.

**수오미 가로지르기는 앞 작업의 방식을 그대로 쓴다** — `tests/hades-characterization/WorldMapTests.cs` 의
`Walled`/`IsWall`(맵 파일 + `sotp.dat`)과 `Pathing.Way(… , reach: 200)`, 그리고 "먼저 새로 묻고 → 그 자리 보고 걷기"
차례. **그 도우미들을 복사하지 말고**, 필요하면 `WorldMapTests` 의 것을 `internal static` 으로 꺼내 함께 쓴다.

- [ ] **Step 2: 돌린다**

```bash
export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"
dotnet test tests/hades-characterization --filter "WorldMapMenuTests|WorldMapTests"
```
기대: 전부 통과. 여정 시험 하나가 3~4분 걸린다 — **앞에서** 돌린다.

- [ ] **Step 3: 화면 두 장**

서버를 켜고(`scripts/lod-server.sh restart`) 새 계정으로 붙어,
① 노비스마을에서 "지도" 단추를 누른 화면 ② 닫기를 눌러 조작이 돌아온 화면을 찍는다.
`./scripts/godot.sh -- --login <이름>:1234 --shot <파일> --shot-after 12`
(**`scripts/godot.sh` 에는 `--path` 를 주지 않는다** — 래퍼가 이미 넣는다, `docs/mobile-client.md` 함정 절.)
찍은 것을 **읽어서 눈으로 확인한다.** 안 나왔으면 고치지 말고 무엇이 보였는지 적어 올린다.

- [ ] **Step 4: 문서**

- `docs/mobile-client.md` — "지도" 단추가 생겼다 · 마을에서만 열린다 · 닫으면 취소가 간다 · 0xF0 은 우리가 만든 말이다.
- `NEXT.md` — `[끝남/월드맵·수오미 연결]` 에 짧게 덧붙인다. **다른 세션도 고치는 파일이니 고치기 직전에 다시 읽고 `git add -p` 로 제 조각만 담는다.**

- [ ] **Step 5: 커밋**

---

## 자기 점검

**1. 명세 덮기** — "메뉴 아이콘으로"(Task 3) · "마을에서만"(Task 1 의 괴물 검사) · "취소"(Task 1 의 0 갈래 + Task 3 의 닫기) ·
"계정 생성에서 포테의숲까지"(Task 4). 시작 자리는 **이미 노비스마을**이라 할 일이 없다(사실 절).

**2. 자리표시자** — 없음. Task 3 의 "지금 인벤토리 단추를 만드는 자리"만 파일을 보고 맞춰야 하는데,
그 함수(`BuildTopRow`)와 규칙("옆 단추가 쓰는 것을 그대로")을 적어 두었다.

**3. 이름이 맞물리나** — `OpenFieldAsync`·`CloseFieldAsync`·`FieldChoice(0)`·`FieldPanel.Close`·`ClientFormatF0`·`FormatF0Handler` 가 Task 1→2→3→4 에서 같은 철자다.

**4. 남는 위험**
- `GetObjects<Monster>` 의 정확한 모양을 Task 1 에서 맞춰야 한다 — `Types/Sprite.cs:569` 가 본보기다.
- 괴물이 **아직 안 젠된** 사냥터는 마을처럼 보인다. 그래서 거절 시험이 괴물을 먼저 기다린다.
- `0xF0` 이 비어 있는 것은 확인했지만, 나중에 원작 자료를 더 들여올 때 겹치면 그때 옮긴다.
