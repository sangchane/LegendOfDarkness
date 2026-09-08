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
Assert-FileContains 'project.godot' 'display/window/handheld/orientation=1' 'landscape orientation is configured'
Assert-FileContains 'MobileSmoke.csproj' 'Godot\.NET\.Sdk/4\.6\.0' 'Godot 4.6 C# SDK is pinned'
Assert-FileContains 'MobileSmoke.csproj' '<TargetFramework>net9\.0</TargetFramework>' '.NET 9 is targeted'
Assert-FileContains 'Main.tscn' 'script = ExtResource\("1_main"\)' 'the main scene uses the C# script'
Assert-FileContains 'Main.cs' 'MOBILE_SMOKE_OK' 'runtime success marker is emitted'
Assert-FileContains 'export_presets.cfg' 'platform="Android"' 'Android export preset exists'
Assert-FileContains 'export_presets.cfg' 'platform="iOS"' 'iOS export preset exists for the later Mac gate'
Assert-FileContains 'export_presets.cfg' 'package/unique_name="com\.fallendev\.lod\.mobilesmoke"' 'Android package id is stable'

$portableFiles = Get-ChildItem -LiteralPath $projectRoot -File -Recurse |
    Where-Object { $_.Extension -in '.cs', '.csproj', '.godot', '.tscn', '.cfg' }
$windowsOnlyPattern = '(?i)([A-Z]:\\|user32|kernel32|Microsoft\.Win32|System\.Windows\.Forms|System\.Drawing)'
foreach ($file in $portableFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if ($content -match $windowsOnlyPattern) {
        $relativePath = [System.IO.Path]::GetRelativePath($projectRoot, $file.FullName)
        $failures.Add("Windows-only dependency or absolute path: $relativePath")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Output 'Smoke project contract: PASS'
