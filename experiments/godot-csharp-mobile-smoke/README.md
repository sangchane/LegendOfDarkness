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

The 2026-09-11 Mac gate passed everything except the device install. Godot 4.6
Mono, .NET SDK 9.0.317, the 4.6.stable.mono export templates, and Xcode 26.5 with
the iOS 26.5 SDK produced a signed 31 MB `MobileSmoke.ipa`: arm64, `iPhoneOS`,
bundle `com.fallendev.lod.mobilesmoke`, signed with an Apple Development identity
under an automatically issued team provisioning profile. `--import`,
`--build-solutions`, `dotnet_publish_project`, `generate_xcframework`, and
`xcodebuild` archive and export all succeeded headlessly.

Two things that cost time and are worth knowing next time. Xcode was installed
but `xcode-select` pointed at the Command Line Tools, so `xcodebuild` refused to
run; `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer` fixes that
without sudo. And the Team ID is the certificate's OU field, not the identifier
in parentheses after the certificate name — read it with
`security find-certificate -c "Apple Development" -p | openssl x509 -noout -subject`.
The Team ID was filled in only for the duration of the export and reverted
immediately; it is not committed.

The device step passed the same day on an iPad (9th generation, iPad12,2,
iPadOS 26.5.2). The app installs and launches on real arm64 hardware, so the
Mac and iOS gate is closed. The build is iPhone and iPad both: `UIDeviceFamily`
is `[1, 2]` with a 14.0 minimum, so either kind of device can take it.

One trap cost most of the time and will cost it again. **Godot's iOS export
cannot register a device.** It shells out to `xcodebuild` without
`-allowProvisioningDeviceRegistration`, so a device that is not already in the
team provisioning profile fails at install with `0xe8008012`, "This provisioning
profile cannot be installed on this device" — and re-exporting never helps,
because each export reuses the same profile. Build the generated Xcode project
directly, once, with the flag:

```bash
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
xcodebuild -project build/ios/MobileSmoke.xcodeproj -scheme MobileSmoke \
  -destination "platform=iOS,id=<device UDID>" \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
```

That reissues the profile for that device; install the `.app` it leaves in
DerivedData with `xcrun devicectl device install app`. Verify with
`security cms -D -i <app>/embedded.mobileprovision` that `ProvisionedDevices`
actually lists the UDID — that check turns a vague signing failure into a fact.

Two smaller ones. Developer Mode must be on, and enabling it is not finished
until the device reboots and the post-unlock prompt is accepted. And a
`Failed to allocate RSD device` error is the Mac's CoreDevice layer, not the
cable; opening Xcode once cleared it.

The checked application runtime and project configuration contain no
Windows-only API or absolute Windows path. The Windows-specific SDK locations
live in `scripts/verify-windows.ps1`; that script is a local verification
harness and is intentionally outside the portability scan.

Godot 4.6 C# mobile export is experimental. A Windows Android export does not
replace the later macOS/Xcode/iPhone acceptance gate.
