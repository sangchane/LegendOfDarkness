# 월드맵 — 수오미 한 노드로 포테의숲까지 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development` (권장) 또는
> `superpowers:executing-plans` 로 이 계획을 작업 단위로 실행한다. 단계는 `- [ ]` 로 표시한다.

**Goal:** 모바일 클라이언트가 월드맵(0x2E)을 읽고 한 곳을 골라(0x3F) 보낼 수 있게 해서,
우드랜드입구에서 월드맵을 열어 **수오미**로 가고 거기서 걸어서 **포테의숲 1존**에서 사냥할 수 있게 한다.

**Architecture:** 서버는 이미 다 돼 있다 — `temuair.json`(FieldNumber 1)에 포털 24개(수오미 → 20355 (40,11)),
`WarpType.World` 워프 20장, 배경 `fieldmaps/field001.png`. 빠진 것은 **모바일 쪽 한 쌍의 패킷뿐**이다.
알맹이(`Lod.Mobile.Core`)에 읽기·보내기를 넣고, 화면(`mobile/client`)에는 이름 목록 하나만 얹는다.
그림 얹은 월드맵은 이번 범위가 **아니다**(사용자 결정, 2026-09-19).

**Tech Stack:** C#/.NET 9 (알맹이·시험), Godot 4.6 + C# (화면), xunit, 서버는 C#/.NET 5 (Hades) — **서버는 안 고친다**.

**Spec:** 이 문서의 "사실" 절이 명세다. 근거는 모두 코드에서 읽은 것이고 줄 번호를 달았다.
바탕 문서: `docs/pote-forest.md`(포테의숲이 이미 들어가 있다) · `docs/mobile-client.md`(실행·인자·함정) ·
`docs/original-ui-451.md`(화면 규칙표).

---

## 사실 — 고치기 전에 확인한 것 (2026-09-19)

| 무엇 | 어디 | 값 |
|---|---|---|
| 월드맵 정의 | `database/server/templates/worldmaps/temuair.json` | FieldNumber **1**, 포털 **24**개. 24개 목적지가 **모두** 서버 맵에 있다(확인함) |
| 수오미 노드 | 같은 파일 | 이름 `수오미` · 맵 **20355** · 설 칸 **(40,11)** · 점 (345,112) |
| 월드맵 여는 칸 | `templates/warps/warp 우드랜드입구 to world map.json` | 맵 **20028** 의 (9,23)(10,23)(11,23) — 20028 은 40x24 라 **아래 가장자리**다 |
| 수오미에서 여는 칸 | `warp 수오미마을 to world map.json` | 맵 20355 의 (39,0)(40,0)(41,0) — 위 가장자리 |
| 포테의숲 들어가는 칸 | `docs/pote-forest.md` | 수오미마을 **(99,24~27)** → 포테의숲1존 **(33,47)**, 레벨 21~51 |
| 칸이 벽인가 | `lod20028.map`·`lod20355.map` 을 `Area.ParseMapWalls` 대로 읽음 | (10,22)·(10,23)·(40,0)·(40,1)·(40,11)·(99,24~27) **모두 길** |
| 나가는 0x2E 모양 | `Network/ServerFormats/ServerFormat2E.cs:30-70` | 아래 "패킷" 절 |
| 들어오는 0x3F 모양 | `Network/ClientFormats/ClientFormat3F.cs:14-17` | `int Index` 하나 = **갈 맵 번호**. `GameServerHandlers.cs:1853` 이 그 번호로 노드를 찾는다 |
| 숫자 차례 | `Network/NetworkPacketWriter.cs:50-70` | short·int 모두 **큰 끝 먼저**(big-endian) |
| **창이 열린 동안** | `Network/NetworkServer.cs:141` | `client.MapOpen` 이면 **`ClientFormat3F` 말고 모든 패킷을 버린다** |
| 모바일에 있는가 | `mobile/` 전체 grep | `0x2E`·월드맵 **한 줄도 없다** |
| 맵 그림 | `mobile/client/assets/world/` | `map20355-*.png`(수오미마을) · `map20263~20269-*.png`(포테의숲) **이미 있다** |

### 이것이 지금 버그다

`NetworkServer.cs:141` 때문에, **지금 모바일로 월드맵 칸을 밟으면 캐릭터가 영영 멎는다.**
서버는 0x2E 를 보내지만 클라이언트가 못 읽고, 클라이언트가 보내는 걸음·말·공격은 서버가 전부 버린다.
`PortalSession.TransitionToMap` 이 먼저 `EnterAbyss()` 를 부르므로(`Types/PortalSession.cs:66`) 남들 눈에서도 사라진다.
**이 계획의 Task 1~3 이 그대로 이 버그의 수리다.** 그래서 화면보다 배선이 먼저다.

### 패킷 — 0x2E 의 몸통 (`ServerFormat2E.Serialize`)

```
StringA  그림 이름      1바이트 길이 + CP949 바이트   ("field001")
byte     노드 수
byte     마당 번호                                   (1)
노드마다:
  short  점 Y                                        ← Y 가 먼저다
  short  점 X
  StringA 이름                                       ("수오미")
  int    갈 맵 번호                                  (20355)
  short  그 맵에서 설 X                              (40)
  short  그 맵에서 설 Y                              (11)
6바이트  서버가 채우는 아무 값 — 읽지 않는다
```

나가는 0x3F 의 몸통은 **4바이트, 갈 맵 번호 하나**다(big-endian).

## Global Constraints

- **서버(`sources/` 아래)는 고치지 않는다.** 외부 원본 submodule 이다(`AGENTS.md`). 이 계획은 모바일만 건드린다.
- **`NEXT.md` 의 소지품 작업과 겹치지 않는다** — `PackPanel.cs`·`GameClient.cs:505` 근처·소지품 시험은 건드리지 않는다.
- 새 파일의 주석과 시험 이름은 **기존 파일의 말투를 따른다**(영어 XML 주석 + 한국어 근거 주석이 섞인 현행 방식).
- 화면을 만들면 **`docs/original-ui-451.md` 규칙표**를 따른다 — 틀은 `Greybox.Stone()`, 단추 최소 크기 `Main.TouchMinimum`, 간격 `Main.Gutter`.
- **월드맵 창에는 닫기 단추를 두지 않는다.** 서버가 0x3F 말고 다 버리므로 닫아도 조작이 안 돌아온다.
  원작도 한 곳을 고르기 전에는 못 빠져나온다.
- 시험은 `dotnet test` 로 돈다. 서버를 띄우는 시험은 `tests/hades-characterization`, 바이트만 보는 시험은
  `mobile/tests/Lod.Mobile.Core.Tests` 에 둔다.
- 커밋은 Conventional Commits + 한국어 제목(`WORKFLOW.md`).

---

### Task 1: 알맹이가 월드맵 안내(0x2E)를 읽는다

서버 없이 바이트만 보는 작업이다. 여기서 틀리면 뒤가 전부 틀리므로 먼저 한다.

**Files:**
- Create: `mobile/src/Lod.Mobile.Core/World/WorldMap.cs`
- Modify: `mobile/src/Lod.Mobile.Core/World/WorldClient.cs` (상수는 28줄 옆, 분기는 347줄 옆, 도우미는 949줄 옆)
- Test: `mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs`

**Interfaces:**
- Consumes: 없음 (첫 작업)
- Produces:
  - `public sealed record WorldMapNode(string Name, int AreaId, int X, int Y, int PointX, int PointY);`
  - `public sealed record WorldMapInfo(string Field, int FieldNumber, IReadOnlyList<WorldMapNode> Nodes);`
  - `public static WorldMapInfo WorldClient.ReadWorldMap(ReadOnlySpan<byte> body)`
  - `public WorldMapInfo? WorldClient.Field { get; }` — 창이 열려 있으면 그 내용, 아니면 `null`

- [ ] **Step 1: 실패하는 시험을 쓴다**

`mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs` 를 만든다:

```csharp
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 서버가 월드맵 창에 쓰는 바이트(ServerFormat2E). 점은 Y 가 먼저 나오고, 그 뒤에 이름·갈 맵·설 칸이 온다.
/// </summary>
public sealed class WorldMapTests
{
    [Fact]
    public void A_field_names_its_picture_and_every_place_on_it()
    {
        WorldMapInfo field = WorldClient.ReadWorldMap(
        [
            0x08, 0x66, 0x69, 0x65, 0x6C, 0x64, 0x30, 0x30, 0x31, // "field001"
            0x01,                                                 // 노드 하나
            0x01,                                                 // 마당 1
            0x00, 0x70,                                           // 점 Y 112
            0x01, 0x59,                                           // 점 X 345
            0x06, 0xBC, 0xF6, 0xBF, 0xC0, 0xB9, 0xCC,             // "수오미"
            0x00, 0x00, 0x4F, 0x83,                               // 맵 20355
            0x00, 0x28,                                           // X 40
            0x00, 0x0B,                                           // Y 11
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66                    // 서버가 채우는 아무 값
        ]);

        Assert.Equal("field001", field.Field);
        Assert.Equal(1, field.FieldNumber);
        Assert.Equal(new WorldMapNode("수오미", 20355, 40, 11, 345, 112), Assert.Single(field.Nodes));
    }

    [Fact]
    public void Two_places_are_read_one_after_the_other()
    {
        WorldMapInfo field = WorldClient.ReadWorldMap(
        [
            0x08, 0x66, 0x69, 0x65, 0x6C, 0x64, 0x30, 0x30, 0x31,
            0x02,
            0x01,
            0x00, 0x70, 0x01, 0x59,
            0x06, 0xBC, 0xF6, 0xBF, 0xC0, 0xB9, 0xCC,             // "수오미"
            0x00, 0x00, 0x4F, 0x83, 0x00, 0x28, 0x00, 0x0B,
            0x01, 0x0D, 0x01, 0x44,
            0x04, 0xBE, 0xC6, 0xBA, 0xA7,                         // "아벨"
            0x00, 0x00, 0x4E, 0x36, 0x00, 0x3A, 0x00, 0x16,       // 맵 20022... 아래 주석
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ]);

        Assert.Equal(2, field.Nodes.Count);
        Assert.Equal("수오미", field.Nodes[0].Name);
        Assert.Equal("아벨", field.Nodes[1].Name);
        Assert.Equal(58, field.Nodes[1].X);
        Assert.Equal(22, field.Nodes[1].Y);
    }
}
```

주: 둘째 시험의 `0x00 0x00 0x4E 0x36` 은 20022 다. 실제 아벨은 20030 이지만 이 시험이 보는 것은
**두 번째 노드를 차례대로 읽어 내는가**이지 값이 맞는가가 아니다 — 맵 번호는 아무 수라도 된다.

- [ ] **Step 2: 돌려서 실패를 본다**

```bash
dotnet test mobile/tests/Lod.Mobile.Core.Tests --filter WorldMapTests
```
기대: 컴파일 실패 — `WorldMapInfo`·`WorldMapNode`·`ReadWorldMap` 이 없다.

- [ ] **Step 3: 기록 두 개를 만든다**

`mobile/src/Lod.Mobile.Core/World/WorldMap.cs`:

```csharp
namespace Lod.Mobile.Core.World;

/// <summary>
/// 월드맵 위의 한 곳. <paramref name="PointX"/>·<paramref name="PointY"/> 는 그림 위의 점이고,
/// <paramref name="X"/>·<paramref name="Y"/> 는 도착한 맵에서 설 칸이다.
/// </summary>
/// <remarks>
/// 점은 원작 640x480 그림을 기준으로 적혀 있다(temuair.json 의 값이 X 78~533 · Y 33~398 이다).
/// 지금 서버가 가진 그림 field001.png 는 1283x962 이므로 그리는 쪽이 두 배로 얹어야 한다.
/// </remarks>
public sealed record WorldMapNode(string Name, int AreaId, int X, int Y, int PointX, int PointY);

/// <summary>한 장의 월드맵. 그림 이름과 그 위의 곳들.</summary>
public sealed record WorldMapInfo(string Field, int FieldNumber, IReadOnlyList<WorldMapNode> Nodes);
```

- [ ] **Step 4: 읽기를 넣는다**

`WorldClient.cs` 의 `ReadMotion`(949줄) 아래에 붙인다:

```csharp
/// <summary>
/// 월드맵 창(ServerFormat2E): 그림 이름, 곳의 수, 마당 번호, 그리고 곳마다 점(Y 가 먼저다)·이름·
/// 갈 맵·그 맵에서 설 칸. 끝의 여섯 바이트는 서버가 채우는 아무 값이라 읽지 않는다.
/// </summary>
public static WorldMapInfo ReadWorldMap(ReadOnlySpan<byte> body)
{
    int at = 0;

    string picture = Words(body, ref at);
    int count = Byte(body, ref at);
    int number = Byte(body, ref at);

    List<WorldMapNode> nodes = new(count);

    for (int index = 0; index < count; index++)
    {
        int pointY = (short)Word(body, ref at);
        int pointX = (short)Word(body, ref at);
        string name = Words(body, ref at);
        int area = (int)Long(body, ref at);
        int x = (short)Word(body, ref at);
        int y = (short)Word(body, ref at);

        nodes.Add(new WorldMapNode(name, area, x, y, pointX, pointY));
    }

    return new WorldMapInfo(picture, number, nodes);
}
```

`Byte`·`Word`·`Long`·`Words` 는 이미 이 파일 1524~1537줄에 있는 도우미다.

- [ ] **Step 5: 돌려서 통과를 본다**

```bash
dotnet test mobile/tests/Lod.Mobile.Core.Tests --filter WorldMapTests
```
기대: 시험 2개 통과.

- [ ] **Step 6: 창이 열린 것을 기억하게 한다**

`WorldClient.cs` 상수 칸(28줄 옆)에:

```csharp
/// <summary>월드맵 창이 열렸다는 알림(ServerFormat2E).</summary>
private const byte WorldMapCommand = 0x2E;
```

들 자리에 밭 하나를 둔다(`_motions` 같은 다른 밭 옆):

```csharp
private WorldMapInfo? _field;

/// <summary>
/// 월드맵 창이 열려 있으면 그 내용. 열려 있는 동안 서버는 <see cref="ChooseFieldAsync"/> 말고는
/// 이 접속의 패킷을 모두 버린다(`NetworkServer.cs:141`) — 걸음도 말도 닿지 않는다.
/// </summary>
public WorldMapInfo? Field => _field;
```

`Listen` 의 `case MapChangedCommand:`(347줄) 옆에 분기를 더한다:

```csharp
case WorldMapCommand:
    try
    {
        _field = ReadWorldMap(HadesCipher.DecodeSecured(frame, session.Parameters));
    }
    catch (ProtocolException cut)
    {
        NoteUnread($"월드맵 안내를 읽다가 끊겼습니다: {cut.Message}");
    }

    continue;
```

그리고 `case MapChangedCommand:` 안, `map = ReadMap(...)` 바로 뒤에 한 줄을 더한다 — 맵이 바뀌면 창은 닫힌 것이다:

```csharp
_field = null;
```

- [ ] **Step 7: 빌드하고 시험 전체를 돌린다**

```bash
dotnet test mobile/tests/Lod.Mobile.Core.Tests
```
기대: 전부 통과(새 2개 포함).

- [ ] **Step 8: 커밋**

```bash
git add mobile/src/Lod.Mobile.Core/World/WorldMap.cs mobile/src/Lod.Mobile.Core/World/WorldClient.cs mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs
git commit -m "feat(mobile): 월드맵 안내를 읽는다"
```

---

### Task 2: 월드맵에서 한 곳을 고른다 (0x3F 보내기)

**Files:**
- Modify: `mobile/src/Lod.Mobile.Core/World/WorldClient.cs` (보내는 것들이 모인 820~840줄 옆)
- Test: `mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs` (Task 1 에서 만든 파일에 더한다)

**Interfaces:**
- Consumes: Task 1 의 `WorldMapInfo`·`WorldMapNode`·`Field`
- Produces: `public Task WorldClient.ChooseFieldAsync(int areaId, CancellationToken cancellationToken)`

- [ ] **Step 1: 실패하는 시험을 쓴다**

`WorldMapTests.cs` 에 더한다 — 보내는 몸통이 네 바이트인지 본다. 접속 없이 몸통만 보려고
`WorldClient` 의 보내기를 그대로 부를 수는 없으므로, **몸통을 만드는 것만** 따로 드러내 시험한다:

```csharp
    [Fact]
    public void Choosing_a_place_sends_its_map_number_in_four_bytes()
    {
        Assert.Equal(new byte[] { 0x00, 0x00, 0x4F, 0x83 }, WorldClient.FieldChoice(20355));
    }
```

- [ ] **Step 2: 돌려서 실패를 본다**

```bash
dotnet test mobile/tests/Lod.Mobile.Core.Tests --filter WorldMapTests
```
기대: 컴파일 실패 — `FieldChoice` 가 없다.

- [ ] **Step 3: 몸통 만들기와 보내기를 넣는다**

`WorldClient.cs`, `RefreshAsync`(823줄) 아래:

```csharp
/// <summary>
/// 월드맵에서 고른 곳의 맵 번호. 서버는 이 번호로 자기 목록에서 곳을 찾는다
/// (`GameServerHandlers.cs:1853`) — 그림 위의 점이 아니라 갈 맵이 열쇠다.
/// </summary>
public static byte[] FieldChoice(int areaId)
{
    byte[] body = new byte[4];

    BinaryPrimitives.WriteInt32BigEndian(body, areaId);

    return body;
}

/// <summary>
/// 월드맵에서 한 곳을 골라 보낸다. 창이 열려 있는 동안 서버는 이것 말고 이 접속의 패킷을 모두
/// 버리므로(`NetworkServer.cs:141`), 고르기 전에는 걷지도 말하지도 못한다 — 창에 닫기가 없는 까닭이다.
/// </summary>
public Task ChooseFieldAsync(int areaId, CancellationToken cancellationToken) =>
    Send(ChooseFieldCommand, FieldChoice(areaId), cancellationToken);
```

상수 칸(`CooldownCommand` 옆)에:

```csharp
/// <summary>
/// 월드맵에서 곳을 고른다. 오는 <see cref="CooldownCommand" /> 와 번호가 같지만 나가는 것은 이쪽이다.
/// </summary>
private const byte ChooseFieldCommand = 0x3F;
```

- [ ] **Step 4: 돌려서 통과를 본다**

```bash
dotnet test mobile/tests/Lod.Mobile.Core.Tests --filter WorldMapTests
```
기대: 시험 3개 통과.

- [ ] **Step 5: 커밋**

```bash
git add mobile/src/Lod.Mobile.Core/World/WorldClient.cs mobile/tests/Lod.Mobile.Core.Tests/World/WorldMapTests.cs
git commit -m "feat(mobile): 월드맵에서 한 곳을 골라 보낸다"
```

---

### Task 3: 진짜 서버로 우드랜드 → 월드맵 → 수오미

여기서 바이트가 실제 서버와 맞는지 판가름난다. Task 1·2 의 시험은 우리가 믿는 것을 적은 것이고, 이것은 서버가 동의하는지 본다.

**Files:**
- Create: `tests/hades-characterization/WorldMapTests.cs`

**Interfaces:**
- Consumes: Task 1 의 `WorldClient.Field`, Task 2 의 `ChooseFieldAsync`
- Produces: 없음 (시험)

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵 — 우드랜드입구 아래 가장자리(9~11,23)를 밟으면 서버가 창을 띄우고(ServerFormat2E),
/// 그 창에서 한 곳을 고르기 전까지는 다른 패킷을 모두 버린다(`NetworkServer.cs:141`).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WorldMapTests : IDisposable
{
    private const int WoodlandGate = 20028;
    private const int SuomiTown = 20355;

    private const string Name = "mapwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Stepping_onto_the_woodland_edge_opens_the_world_map_with_suomi_on_it()
    {
        // 20028 은 40x24 — (10,23) 이 아래 가장자리이고 월드맵을 여는 칸이다. (10,22) 도 (10,23) 도 길이다.
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);

        WorldMapInfo field = world.Field!;

        Assert.Equal("field001", field.Field);
        Assert.Equal(24, field.Nodes.Count);

        WorldMapNode suomi = Assert.Single(field.Nodes, node => node.Name == "수오미");

        Assert.Equal(SuomiTown, suomi.AreaId);
        Assert.Equal(40, suomi.X);
        Assert.Equal(11, suomi.Y);
    }

    [Fact]
    public async Task Choosing_suomi_puts_the_character_down_in_suomi()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);

        await world.ChooseFieldAsync(SuomiTown, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            $"수오미를 골랐는데 수오미마을로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // 창은 맵이 바뀌면 닫힌다 — 안 닫히면 그 뒤로 걸음이 서버에 닿지 않는다.
        Assert.Null(world.Field);
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다**

```bash
dotnet test tests/hades-characterization --filter WorldMapTests
```
기대: 실패. Task 1·2 를 안 했다면 컴파일 실패, 했다면 `field001` 이나 노드 수에서 어긋난다.

- [ ] **Step 3: 어긋난 곳만 고친다**

서버가 실제로 보낸 바이트가 Task 1 의 읽기와 다르면 **읽기 쪽을 고친다**(서버는 안 고친다).
다시 볼 곳: `ServerFormat2E.Serialize`(`Network/ServerFormats/ServerFormat2E.cs:41-57`) 의 쓰는 차례,
`NetworkPacketWriter.WriteStringA`(95줄, 길이 1바이트 + CP949).

- [ ] **Step 4: 돌려서 통과를 본다**

```bash
dotnet test tests/hades-characterization --filter WorldMapTests
```
기대: 시험 2개 통과.

- [ ] **Step 5: 커밋**

```bash
git add tests/hades-characterization/WorldMapTests.cs mobile/src/Lod.Mobile.Core/World/
git commit -m "test(mobile): 우드랜드에서 월드맵을 열어 수오미로 간다"
```

---

### Task 4: 수오미에서 걸어서 포테의숲 1존까지 — 여정 하나로

Task 3 까지가 "링크"이고, 이것이 사용자가 말한 "포테의숲 사냥터 테스트"다.
`PoteForestTests` 는 이미 수오미 안에서 시작해 동쪽 끝만 밟는다. 이 시험은 **월드맵부터** 시작한다.

**Files:**
- Modify: `tests/hades-characterization/WorldMapTests.cs`

**Interfaces:**
- Consumes: Task 3 의 시험 뼈대, `Waiting.WalkUntil`, `world.Creatures`
- Produces: 없음 (시험)

- [ ] **Step 1: 실패하는 시험을 쓴다**

`WorldMapTests.cs` 에 더한다:

```csharp
    private const int ForestOne = 20263;

    [Fact]
    public async Task The_whole_way_from_woodland_to_the_first_zone_of_pote_forest()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        // 수오미마을 → 포테의숲1존은 5.99 에서 레벨 21~51 이다(docs/pote-forest.md).
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        System.Text.Json.Nodes.JsonNode character = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 21;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);
        await world.ChooseFieldAsync(SuomiTown, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            "수오미마을로 가지 않았습니다.", _deadline.Token);

        // 월드맵은 (40,11) 에 내려놓는다. 포테의숲 입구는 동쪽 끝 (99,24~27) 이라 아래로 내려간 뒤 동쪽으로 간다.
        await Waiting.WalkUntil(world, Direction.South,
            () => world.State is { } state && state.Where.Y >= 25, _deadline.Token);

        await Waiting.WalkUntil(world, Direction.East,
            () => world.State?.Map.Id == ForestOne, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == ForestOne && state.Where == new Tile(33, 47),
            $"포테의숲1존 33,47 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        HashSet<uint> met = [];

        for (int tick = 0; tick < 120 && met.Count < 2; tick++)
        {
            foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
            {
                met.Add(mob.Serial);
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        Assert.True(met.Count >= 2, $"포테의숲1존 입구에 1분 서 있는 동안 괴물이 {met.Count}마리만 보였습니다.");
    }
```

- [ ] **Step 2: 돌려서 실패를 본다**

```bash
dotnet test tests/hades-characterization --filter "WorldMapTests.The_whole_way_from_woodland_to_the_first_zone_of_pote_forest"
```
기대: 실패한다. 어디서 멈추는지 메시지가 말해 준다.

- [ ] **Step 3: 걷는 길을 맞춘다**

내려놓는 자리 (40,11) 에서 (99,25) 까지 가는 길에 벽이 있으면 `Waiting.WalkUntil` 은 제자리걸음만 한다.
그때는 알맹이의 길찾기를 쓴다 — `mobile/src/Lod.Mobile.Core/World/Pathing.cs` 에 이미 있고
`WoodlandProgressionTests` 가 쓰는 방식을 그대로 따른다. **새 길찾기를 쓰지 않는다.**

- [ ] **Step 4: 돌려서 통과를 본다**

```bash
dotnet test tests/hades-characterization --filter WorldMapTests
```
기대: 시험 3개 통과.

- [ ] **Step 5: 커밋**

```bash
git add tests/hades-characterization/WorldMapTests.cs
git commit -m "test(mobile): 우드랜드에서 월드맵을 거쳐 포테의숲까지 걸어 본다"
```

---

### Task 5: 화면 — 어디로 갈지 고르는 창

배선이 끝난 뒤에 얹는다. 그림 없는 이름 목록이다(사용자 결정).

**Files:**
- Create: `mobile/client/src/FieldPanel.cs`
- Modify: `mobile/client/src/GameScreen.cs` (밭은 24~25줄 옆, 만들기는 119줄 옆, 얹기는 180줄 `Cover` 안의 배열)

**Interfaces:**
- Consumes: Task 1 의 `WorldMapInfo`·`WorldMapNode`, Task 2 의 `ChooseFieldAsync`
- Produces:
  - `public sealed partial class FieldPanel : PanelContainer`
  - `public void FieldPanel.Show(WorldMapInfo field)` — 창을 채우고 보이게 한다
  - `public event Action<int>? Chosen` — 고른 곳의 맵 번호

- [ ] **Step 1: 창을 만든다**

`mobile/client/src/FieldPanel.cs`:

```csharp
using System;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 월드맵 창: 갈 수 있는 곳의 이름을 줄로 세운다. 그림 위에 점을 찍는 원작 모습은 아직이고,
/// 지금은 고를 수만 있으면 된다.
/// </summary>
/// <remarks>
/// **닫기가 없다.** 이 창이 열려 있는 동안 서버는 고르기 말고 이 접속의 패킷을 모두 버리므로
/// (`NetworkServer.cs:141`), 닫아 봐야 걸음도 말도 닿지 않는다. 한 곳을 골라야 빠져나온다.
/// </remarks>
public sealed partial class FieldPanel : PanelContainer
{
    private readonly Label _title = new() { Text = "어디로 갈까" };
    private readonly VBoxContainer _places = new();

    public FieldPanel()
    {
        Name = "Field";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        _places.AddThemeConstantOverride("separation", Main.Gutter / 2);

        ScrollContainer scroll = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(_places);

        inside.AddChild(_title);
        inside.AddChild(scroll);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", Main.Gutter);
        margin.AddThemeConstantOverride("margin_right", Main.Gutter);
        margin.AddThemeConstantOverride("margin_top", Main.Gutter);
        margin.AddThemeConstantOverride("margin_bottom", Main.Gutter);
        margin.AddChild(inside);

        AddChild(margin);
    }

    /// <summary>고른 곳의 맵 번호.</summary>
    public event Action<int>? Chosen;

    /// <summary>창을 채우고 보인다. 창은 늘 통째로 다시 짓는다 — 월드맵은 한 번에 하나뿐이다.</summary>
    public void Show(WorldMapInfo field)
    {
        foreach (Node old in _places.GetChildren())
        {
            old.QueueFree();
        }

        foreach (WorldMapNode place in field.Nodes)
        {
            Button row = new()
            {
                Text = place.Name,
                CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
            };

            int area = place.AreaId;
            row.Pressed += () => Chosen?.Invoke(area);

            _places.AddChild(row);
        }

        Visible = true;
    }
}
```

- [ ] **Step 2: 화면에 얹는다**

`GameScreen.cs` 24~25줄 옆에 밭을 더한다:

```csharp
    private FieldPanel _field = null!;
```

119줄 `_talk = new TalkPanel();` 앞에:

```csharp
        _field = new FieldPanel();
        _field.Chosen += area =>
        {
            _field.Visible = false;
            _ = _server?.ChooseFieldAsync(area, System.Threading.CancellationToken.None);
        };
```

180줄 `foreach (Control panel in new Control[] { _pack, _talk, _chat })` 의 배열에 `_field` 를 더한다:

```csharp
        foreach (Control panel in new Control[] { _pack, _talk, _chat, _field })
```

그리고 `_talk.SizeFlagsVertical = SizeFlags.ExpandFill;` 옆에:

```csharp
        // 곳이 스물넷이라 남는 높이를 다 쓴다 — 대화 창과 같다.
        _field.SizeFlagsVertical = SizeFlags.ExpandFill;
```

- [ ] **Step 3: 창이 떴을 때 보여 준다**

`GameScreen` 의 `_Process`(381줄 근처, `Main.OpeningPack` 을 보는 곳) 옆에 더한다:

```csharp
        // 월드맵은 서버가 띄우는 것이지 사람이 여는 것이 아니다. 온 것을 그대로 보여 준다.
        if (_server?.Field is { } field && !_field.Visible)
        {
            _field.Show(field);
        }
        else if (_server?.Field is null && _field.Visible)
        {
            _field.Visible = false;
        }
```

- [ ] **Step 4: 빌드한다**

```bash
dotnet build mobile/client
```
기대: 경고 없이 성공.

- [ ] **Step 5: 커밋**

```bash
git add mobile/client/src/FieldPanel.cs mobile/client/src/GameScreen.cs
git commit -m "feat(mobile): 월드맵에서 갈 곳을 고르는 창"
```

---

### Task 6: 화면 한 장으로 확인하고 문서를 갱신한다

**Files:**
- Modify: `docs/mobile-client.md` (인자표 아래 "확인한 것" 절)
- Modify: `NEXT.md` · `WORKLOG.md`

**Interfaces:**
- Consumes: Task 5 의 화면
- Produces: 없음

- [ ] **Step 1: 서버를 띄운다**

```bash
./scripts/lod-server.sh
```
기대: 2610·2615 가 열렸다는 줄. 안 열리면 기록을 보고 멈춘다(`NEXT.md` 함정).

- [ ] **Step 2: 시험 캐릭터를 우드랜드입구에 세운다**

서버를 멈춘 뒤에 고친다 — 켜 둔 채로 고치면 서버가 덮어쓴다(`NEXT.md` 함정).

```bash
python3 - <<'PY'
import json
from pathlib import Path
saved = Path("sources/wren11/Dark-Ages-Private-Server/database/server/aislings/watch.json")
d = json.loads(saved.read_text(encoding="utf-8-sig"))
d["CurrentMapId"], d["X"], d["Y"] = 20028, 10, 22
d["ExpLevel"] = max(d.get("ExpLevel", 1), 21)
saved.write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")
print("watch 를 우드랜드입구 10,22 에 세웠다")
PY
./scripts/lod-server.sh
```

- [ ] **Step 3: 월드맵이 뜬 화면을 찍는다**

```bash
./scripts/godot.sh --path mobile/client -- --login watch:1234 --walk S --shot /tmp/worldmap.png --shot-after 12
```
기대: `/tmp/worldmap.png` 에 "어디로 갈까" 창과 그 안의 수오미 줄이 보인다.
**`--screen game` 으로 찍지 않는다** — 서버에 안 붙어 확인이 아니다(`NEXT.md` 함정).

- [ ] **Step 4: 수오미에 선 화면을 찍는다**

```bash
./scripts/godot.sh --path mobile/client -- --login watch:1234 --shot /tmp/suomi.png --shot-after 12
```
Step 3 에서 수오미를 골랐으면 캐릭터는 수오미마을에 서 있다. 맵 그림(`map20355-floor.png`)이 깔렸는지 본다.

- [ ] **Step 5: 찍은 것을 본다**

두 장을 읽어 창과 맵이 제대로 나왔는지 눈으로 확인한다. 안 나왔으면 Task 5 로 돌아간다 —
"찍혔다"는 "됐다"가 아니다.

- [ ] **Step 6: 문서를 갱신한다**

- `docs/mobile-client.md` — 월드맵 창이 생겼다는 것과, **닫기가 없는 까닭**을 한 줄.
- `NEXT.md` — `[다음/다른 세션과 겹치지 않게]` 줄을 지우고 `[끝남/월드맵·수오미 연결]` 로 바꾼다.
  남은 것(그림 얹은 원작식 월드맵 · 아벨 등 나머지 노드 확인)을 적는다.
- `WORKLOG.md` — 한 줄.

- [ ] **Step 7: 커밋**

```bash
git add docs/mobile-client.md NEXT.md WORKLOG.md
git commit -m "docs(mobile): 월드맵으로 수오미에 가고 포테의숲에서 사냥한 것을 적는다"
```

---

## 자기 점검 (계획을 쓴 뒤 다시 본 것)

**1. 명세 덮기** — 사용자가 말한 세 가지:
- "월드맵 구성" → Task 1·2(읽고 고르기) · Task 5(창). 서버 쪽은 이미 돼 있어 만들 것이 없다(사실 절).
- "수오미 마을 링크" → Task 3 이 노드가 실제로 오는지와 골라서 도착하는지를 잰다.
- "포테의숲 사냥터 테스트" → Task 4(자동 시험) · Task 6(화면 한 장). 사용자가 고른 합격 기준 그대로다.

**2. 자리표시자** — "적절히"·"TBD"·"비슷하게" 없음. 모든 단계에 돌릴 명령과 기대값이 있다.
Task 3 Step 3 과 Task 4 Step 3 은 "어긋나면 어디를 본다"까지 적었다.

**3. 이름이 맞물리나** — `WorldMapInfo`·`WorldMapNode`·`ReadWorldMap`·`Field`·`FieldChoice`·`ChooseFieldAsync`·
`FieldPanel.Show`·`FieldPanel.Chosen` 이 Task 1 → 2 → 3 → 4 → 5 에서 같은 철자로 쓰였다.

**4. 남는 위험**
- Task 3 이 처음으로 진짜 바이트를 본다. 여기서 어긋나면 Task 1 의 읽기를 고친다 — 서버는 안 고친다.
- `temuair.json` 에 우드랜드 노드가 **넷인데 모두 같은 맵 20028** 이다. 서버는 맵 번호로 노드를 찾으므로
  (`GameServerHandlers.cs:1853`) 넷 중 어느 것을 눌러도 첫 번째로 간다. 수오미는 하나뿐이라 이번 범위에는 걸리지 않는다.
  원작식 그림 월드맵을 만들 때 다시 봐야 한다.
- 수오미 내려놓는 자리 (40,11) 에서 동쪽 끝 (99,25) 까지 100x100 맵을 가로지른다. 벽이 있으면 Task 4 Step 3.
