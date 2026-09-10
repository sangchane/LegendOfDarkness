<#
.SYNOPSIS
    Draws the client's art out of this repository's own .dat archives.

.DESCRIPTION
    Every picture under mobile/client/assets comes from here, so nothing is hand-edited and nothing is
    carried in from a restored copy made elsewhere. Re-run after changing tools/dat-extract.

    The frame layout these sheets rely on — two drawings per action, the other two directions mirrored —
    is written down in docs/original-sprite-animation.md.
#>
[CmdletBinding()]
param(
    [string] $Game = "$PSScriptRoot/../sources/Dark-Ages-Private-Server-master/game",
    [string] $Maps = "$PSScriptRoot/../sources/Dark-Ages-Private-Server-master/database/server/maps",
    [string] $Output = "$PSScriptRoot/../mobile/client/assets"
)

$ErrorActionPreference = 'Stop'

$tool = Resolve-Path "$PSScriptRoot/../tools/dat-extract/bin/Debug/net8.0/dat-extract.dll" -ErrorAction SilentlyContinue

if (-not $tool) {
    throw "Build the extractor first: dotnet build tools/dat-extract/DatExtract.csproj"
}

# net8.0 output, so the shared runtime rather than the workspace SDK.
$dotnet = 'C:/Program Files/dotnet/dotnet.exe'

function Invoke-Extract {
    param([string[]] $Arguments)

    & $dotnet $tool @Arguments | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "dat-extract failed: $($Arguments -join ' ')"
    }
}

New-Item -ItemType Directory -Force -Path "$Output/world", "$Output/actor" | Out-Null

Write-Output 'Drawing the safe house floor...'
Invoke-Extract @('map', "$Game/seo.dat", "$Maps/lod1.map", '30', '31', "$Output/world/safehouse.png")

Write-Output 'Stacking the wardrobe into figures...'
# Body, then what it wears, then what it wears on its head — the order the original draws them in.
Invoke-Extract @('pose', "$Game/khan.dat", 'mb00101,mi00101,MH28501', "$Output/actor/hero-walk.png", '0,1,2,3,4,5,6,7,8,9', '1')
Invoke-Extract @('pose', "$Game/khan.dat", 'mb00102,mi00102,MH28502', "$Output/actor/hero-attack.png", '0,1,2,3', '1')
Invoke-Extract @('pose', "$Game/khan.dat", 'mb00101,MU06101,MH28501', "$Output/actor/npc-walk.png", '0,1,2,3,4,5,6,7,8,9', '1')

Write-Output 'Drawing a creature...'
# 'transparent' rather than the Korean spelling: an argument in Hangul does not survive PowerShell's
# hand-off to a native executable on this machine, and the sheet comes out with its background filled in.
Invoke-Extract @('mpf', "$Game/hades.dat", 'MNS001.MPF', "$Output/actor/wasp.png", '1', 'transparent')

Get-ChildItem -Path $Output -Recurse -Filter *.png | ForEach-Object {
    Write-Output ("  {0}  {1:N0} bytes" -f $_.FullName.Substring($_.FullName.IndexOf('assets')), $_.Length)
}
