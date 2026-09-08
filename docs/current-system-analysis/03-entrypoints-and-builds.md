# 03. 빌드 방식과 실행 시작점

| 저장소 | 언어/프레임워크 | 빌드 | 시작점 또는 핵심 클래스 |
|---|---|---|---|
| Spark | C#, .NET 8, WPF/WinForms | `Spark.sln`, `dotnet build` | `Spark.App`, `MainViewModel`, `RuntimePatcher` |
| DAGL | C#, .NET 8 Windows, Silk.NET/OpenGL/ImGui | `src/741/DarkAges.Library.sln` | 실행점 없음; `NetworkManager`, `Character`, `MapFile` |
| Arbiter | C#, .NET 10, Avalonia/MVVM | `Arbiter.sln` | `Arbiter.App.Program.Main`, `App`, `ProxyServer` |
| SleepHunter4 | C#, .NET 9 Windows, WPF | `SleepHunter/SleepHunter.sln`; win-x64 publish | `SleepHunter.App`, `MainWindow`, `PlayerManager` |
| dark-ages-ts | TypeScript, Bun, Turbo, Phaser, Svelte | 루트 `bun install`, `bun run dev`; 클라이언트 Vite | client `src/main.ts`; server `src/index.ts` → `GameServer` |
| DAMapEditor | C#, .NET 8 Windows Forms | `MapEditor.sln` | `MapEditor.Program.Main` → `Form1` |
| PalMake | C#, .NET 7 WPF | `PalMake.sln` | `App` → `MainWindow`; `FileProcessingService` |
| Decipher | C#, .NET Framework 4.7.2 Windows Forms/Console | `Akorade.sln`, `Decipher Server.sln` | 양쪽 `Program.Main`; `Client`, `Server` |
| DungMunkey/Dark-Ages | C++11, SDL2/SDL_ttf/SDL_mixer | GNU `Makefile` → `darkages` | `Darkages.cpp::main` → `CDarkages` |
| Hades/Lorule | C# .NET 5 서버; .NET Framework 4.6.1 도구 클라이언트 | `src/Hades.sln` | `Lorule.GameServer.Program.Main` → `ServerContext` |
| bmp2epf | C++, Visual Studio v141, GDI+ | `bmp2epf.vcxproj` | `main.cpp` |
| da-lib | C#, .NET 9, SkiaSharp | `DALib.sln` | 라이브러리; `DataArchive`, `MapFile`, `EpfFile` |
| da | C++17, Visual Studio v143, DLL | `DarkAgesHook.sln` | `main.cpp`, `Hooks`, `GameBot` |
| Dark-Ages-AI-Bot | C++20, Visual Studio v143, DLL | `src/pop.sln` | `dllmain.cpp`, `packet_processor`, `overlay_manager` |
| ETDA | C++ DLL + C# .NET Framework 4.5 WinForms | `BotCore.sln`, `EtDA.vcxproj` | `EtDA/main.cpp`; `Bot/Program.cs`; `GameStateEngine` |
| DADataViewer | C#, .NET Framework 4.6.1 WinForms | `DADataViewer.sln` | `Program.Main` → `MainForm` |
| Archivist | C#, .NET Framework 4.6.1 WPF | `Archivist.sln` | `App.OnStartup` → `MainViewModel`; 기능 미구현 |

## 주요 빌드 주의사항

- **확인됨:** Hades 서버는 지원 종료된 .NET 5와 오래된 ASP.NET/ServiceStack 패키지를 사용한다.
- **확인됨:** `DAMapEditor/MapEditor.csproj`는 저장소 밖 `../dalib/DALib/DALib.csproj`를 참조한다. 현재 배치에서는 자동 연결되지 않는다.
- **확인됨:** `PalMake`는 `bin/`, `obj/`, `bmp2epf.exe`, Visual C++ 런타임 DLL까지 저장소에 커밋했다.
- **확인됨:** ETDA의 네이티브 프로젝트는 Windows SDK 8.1 및 v140/v141 도구 집합 흔적이 있어 최신 Visual Studio에서 그대로 빌드되지 않을 수 있다.
- **미확인:** 이번 단계에서는 사용자 요청에 따라 빌드·실행·패키지 설치를 하지 않았다.

