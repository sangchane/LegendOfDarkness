# Godot C# mobile export smoke test

This experiment proves the shared client foundation before gameplay work starts.
It intentionally contains no Hades protocol or game features.

## Windows gate

Run from the workspace root:

```powershell
.\experiments\godot-csharp-mobile-smoke\scripts\verify-windows.ps1
```

The script verifies the application source contract, restores and builds C# with
.NET 9, runs the scene headlessly, and exports an Android debug APK. Local SDKs,
Godot binaries, the `.godot/` import cache, and build outputs are excluded from
Git. Godot's asset metadata such as `icon.svg.import` remains tracked.

The 2026-09-09 gate passed with Godot 4.6 Mono, .NET SDK 9.0.317, JDK 21,
Android SDK/API 35, and an Android 15 x86_64 emulator. The APK passed v2/v3
signature verification, emitted `MOBILE_SMOKE_OK` from C# on Android, and
rendered the landscape smoke screen. The APK is generated at
`build/android/MobileSmoke.apk` and is intentionally not committed.

## Mac and iOS handoff

The committed iOS preset is a handoff scaffold, not an export-ready signed
preset. Its Team ID is deliberately blank because signing identities must not be
committed. Checkout the same commit on a Mac and install Godot 4.6 .NET, .NET 9, Xcode,
and the Godot Mono export templates. First verify `--import`,
`--build-solutions`, and a macOS desktop run. Then fill the iOS preset's Team ID,
export the Xcode project, build for an arm64 iPhone, and run it on a real device.
Signing identities and provisioning profiles must remain machine-local.

The checked application runtime and project configuration contain no
Windows-only API or absolute Windows path. The Windows-specific SDK locations
live in `scripts/verify-windows.ps1`; that script is a local verification
harness and is intentionally outside the portability scan.

Godot 4.6 C# mobile export is experimental. A Windows Android export does not
replace the later macOS/Xcode/iPhone acceptance gate.
