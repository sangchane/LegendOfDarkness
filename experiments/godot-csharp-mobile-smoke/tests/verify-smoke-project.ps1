[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-FileContains {
    param(
        [Parameter(Mandatory)] [string] $RelativePath,
        [Parameter(Mandatory)] [string] $Pattern,
        [Parameter(Mandatory)] [string] $Description
    )

    $path = Join-Path $projectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("missing file: $RelativePath")
        return
    }

    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    if ($content -notmatch $Pattern) {
        $failures.Add("$RelativePath does not satisfy: $Description")
    }
}

Assert-FileContains 'project.godot' 'run/main_scene="res://Main.tscn"' 'main scene is configured'
Assert-FileContains 'project.godot' 'window/handheld/orientation=4' 'sensor landscape orientation is configured'
Assert-FileContains 'project.godot' 'textures/vram_compression/import_etc2_astc=true' 'Android texture import is enabled'
Assert-FileContains 'MobileSmoke.csproj' 'Godot\.NET\.Sdk/4\.6\.0' 'Godot 4.6 C# SDK is pinned'
Assert-FileContains 'MobileSmoke.csproj' '<TargetFramework>net9\.0</TargetFramework>' '.NET 9 is targeted'
Assert-FileContains 'MobileSmoke.csproj' 'android/\*\*;build/\*\*' 'generated Android files are excluded from C# compilation'
Assert-FileContains 'MobileSmoke.sln' 'MobileSmoke\.csproj' 'the C# project is registered in a solution'
Assert-FileContains 'Main.tscn' 'script = ExtResource\("1_main"\)' 'the main scene uses the C# script'
Assert-FileContains 'Main.cs' 'MOBILE_SMOKE_OK' 'runtime success marker is emitted'
Assert-FileContains 'export_presets.cfg' 'platform="Android"' 'Android export preset exists'
Assert-FileContains 'export_presets.cfg' 'platform="iOS"' 'iOS export preset exists for the later Mac gate'
Assert-FileContains 'export_presets.cfg' 'application/app_store_team_id=""' 'the machine-local Apple Team ID is not committed'
Assert-FileContains 'export_presets.cfg' 'application/bundle_identifier="com\.fallendev\.lod\.mobilesmoke"' 'the iOS bundle id is stable'
Assert-FileContains 'export_presets.cfg' 'architectures/arm64=true' 'physical iPhone arm64 is enabled for the later Mac gate'
Assert-FileContains 'export_presets.cfg' 'package/unique_name="com\.fallendev\.lod\.mobilesmoke"' 'Android package id is stable'
Assert-FileContains 'export_presets.cfg' 'architectures/arm64-v8a=true' 'physical Android arm64 is enabled'
Assert-FileContains 'export_presets.cfg' 'architectures/x86_64=true' 'the local Android emulator architecture is enabled'
Assert-FileContains 'project.godot' 'config/icon="res://icon\.svg"' 'an application icon is configured'

$portableFiles = Get-ChildItem -LiteralPath $projectRoot -File -Recurse |
    Where-Object { $_.Extension -in '.cs', '.csproj', '.sln', '.godot', '.tscn', '.cfg', '.import', '.uid', '.gdextension' }
$windowsOnlyPattern = '(?i)([A-Z]:\\|user32|kernel32|Microsoft\.Win32|System\.Windows\.Forms|System\.Drawing)'
foreach ($file in $portableFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if ($content -match $windowsOnlyPattern) {
        $relativePath = [System.IO.Path]::GetRelativePath($projectRoot, $file.FullName)
        $failures.Add("Windows-only dependency or absolute path: $relativePath")
    }
}

if ($failures.Count -gt 0) {
    throw "Smoke project contract failed:`n$($failures -join [Environment]::NewLine)"
}

Write-Output 'Smoke project contract: PASS'
