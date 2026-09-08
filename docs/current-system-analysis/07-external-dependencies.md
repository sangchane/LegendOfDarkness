# 07. 외부 DLL과 라이브러리

## 주요 관리형(.NET/JavaScript) 의존성

- Hades/Lorule: .NET 5, Microsoft.Extensions.*, Roslyn Scripting, Newtonsoft.Json, Serilog, ServiceStack, YamlDotNet, 내장 ZLib 구현. `Lorule.GameServer` 출력에 `netstandard.dll`을 복사한다.
- Medenia client: Phaser, Svelte, Vite, Tailwind/PostCSS, Comlink, stats.js, uuid.
- Medenia server: Bun, TypeORM, sqlite3, argon2, ws, easystarjs, gl-matrix, iconv-lite, intersects.
- Arbiter: .NET 10, Avalonia, CommunityToolkit.Mvvm, Microsoft DI/Logging, Xaml Behaviors.
- Spark: .NET 8 WPF/WinForms, `Spark.Interop`.
- SleepHunter4: .NET 9 WPF, `Microsoft.Windows.Compatibility`; OS `user32.dll`, `kernel32.dll` P/Invoke.
- DAGL: System.Drawing, Silk.NET Windowing/Input/OpenGL, ImGui.NET, BCrypt.Net, TextCopy.
- PalMake: Magick.NET. 저장소에 `bmp2epf.exe`와 MSVC 런타임 DLL이 포함된다.
- Decipher: .NET Framework 4.7.2, Google Cloud Translation V2, Newtonsoft.Json, Sentry, Costura/Fody.
- da-lib: .NET 9, SkiaSharp, KGySoft.Drawing.SkiaSharp.
- DADataViewer: .NET Framework 4.6.1, NAudio 1.7.3.

## 네이티브 의존성

- DungMunkey/Dark-Ages: GCC/G++ C++11, SDL2, SDL2_ttf, SDL2_mixer, pthread.
- bmp2epf: Visual C++ v141, Windows GDI+ (`Gdiplus.lib`).
- da / Dark-Ages-AI-Bot: Visual C++ v143 DLL, Windows 프로세스/후킹 API.
- ETDA: Visual C++ v140/v141, DirectDraw (`ddraw.lib`, `dxguid.lib`), Detours 헤더; C# 쪽 Fasm.NET, MemorySharp, log4net.

## 외부 파일·서비스

- 레거시 Dark Ages 실행 파일과 DAT/EPF/PAL/MAP 자료: Spark, SleepHunter4, Arbiter, da, ETDA, 데이터 도구가 요구한다.
- Google Cloud Translation 자격 JSON: Decipher 서버가 요구한다. 저장소에 비밀키를 넣어서는 안 된다.
- Hades의 `LoruleConfig.json`, `MServerTable.xml`, `Notification.txt`, `database/server` 콘텐츠.
- Medenia의 `.env`, SQLite 런타임 파일, 브라우저용 대형 public assets.

**확인됨:** Windows 전용 DLL/프로세스 접근 의존성은 모바일에서 사용할 수 없다. sqlite3·argon2·SkiaSharp·Magick.NET은 플랫폼별 네이티브 바이너리를 동반할 수 있어 목표 모바일 플랫폼별 검증이 필요하다.

