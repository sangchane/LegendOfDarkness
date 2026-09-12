<#
.SYNOPSIS
    5.99 서버팩 zip 에서 이식에 필요한 맵 파일 521개만 꺼낸다. (Windows 전용)

.DESCRIPTION
    `plans/5.99-필요한-맵파일.tsv` 가 정답지다. 팩은 맵 807개를 선언하지만 파일은 521개뿐이고
    (한 방을 여러 곳이 돌려 쓴다 — 혼돈의탑 71방이 한 파일), 아카이브 5,017개 중 10% 다.
    합쳐야 8.4MB.

    **zip 이름은 CP949 다.** 그냥 풀면 폴더 이름이 깨진다. 그래서 Expand-Archive 를 쓰지 않고
    인코딩 949 로 zip 을 열어 **필요한 항목만** 골라 쓴다. 3,270개를 다 풀 필요가 없다.

    **경로 구조를 지킨다.** 521개 경로에 같은 파일 이름이 43쌍 있다
    (`뤼케시온필드` 둘이 서로 다른 폴더의 `lod505.map` 이다). 한 폴더에 쏟으면 덮인다.

    꺼낼 때 **크기를 대조한다.** 기대 바이트는 `너비 x 높이 x 6` 이고 TSV 2열에 있다.
    안 맞으면 쓰지 않는다 — 서버가 이름을 대고 거르지만(MapIntegrityTests) 여기서 걸러야
    원인이 분명하다.

.PARAMETER Zip
    `5.99 서버팩.zip` 경로.

.PARAMETER Destination
    꺼낸 파일을 둘 곳. 기본값은 저장소의 `data/map-source/5.99-server`.
    TSV 1열의 상대 경로(`db/maps/...`)를 그대로 이어 붙인다.

.EXAMPLE
    pwsh scripts/fetch-599-maps.ps1 -Zip 'D:\_personal\LOD_\5.99 서버팩.zip'

.EXAMPLE
    # 쓰지 않고 zip 안에 몇 개가 있는지만 본다
    pwsh scripts/fetch-599-maps.ps1 -Zip '...\5.99 서버팩.zip' -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string] $Zip,

    [string] $Destination
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $repo 'plans\5.99-필요한-맵파일.tsv'

if (-not $Destination) {
    $Destination = Join-Path $repo 'data\map-source\5.99-server'
}

if (-not (Test-Path -LiteralPath $Zip))      { throw "zip 이 없다: $Zip" }
if (-not (Test-Path -LiteralPath $manifest)) { throw "목록이 없다: $manifest" }

# 목록을 읽는다. 주석(#)과 빈 줄은 건너뛴다.
$wanted = [ordered]@{}
foreach ($line in Get-Content -LiteralPath $manifest -Encoding UTF8) {
    if (-not $line -or $line.StartsWith('#')) { continue }
    $col = $line -split "`t"
    if ($col.Count -lt 2) { continue }
    $wanted[$col[0]] = [int] $col[1]
}
$totalMb = [math]::Round((($wanted.Values | Measure-Object -Sum).Sum) / 1MB, 1)
Write-Host "목록: 맵 파일 $($wanted.Count)개 · 합계 $totalMb MB"

Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
$cp949 = [System.Text.Encoding]::GetEncoding(949)

$archive = [System.IO.Compression.ZipFile]::Open(
    $Zip, [System.IO.Compression.ZipArchiveMode]::Read, $cp949)
try {
    # zip 안 경로는 맨 앞에 폴더가 하나 더 붙어 있을 수 있다. 정확히 맞는 것을 먼저 보고,
    # 없으면 뒤에서부터 맞춘다.
    $exact = @{}
    foreach ($entry in $archive.Entries) {
        if (-not $entry.Name) { continue }          # 폴더 항목
        $exact[$entry.FullName.Replace('\', '/')] = $entry
    }

    $written = @(); $missing = @(); $wrongSize = @()

    foreach ($rel in $wanted.Keys) {
        $expected = $wanted[$rel]

        $entry = $exact[$rel]
        if (-not $entry) {
            $entry = $archive.Entries | Where-Object {
                $_.FullName.Replace('\', '/').EndsWith('/' + $rel)
            } | Select-Object -First 1
        }

        if (-not $entry) { $missing += $rel; continue }

        if ($entry.Length -ne $expected) {
            $wrongSize += [pscustomobject]@{ 경로 = $rel; 실제 = $entry.Length; 기대 = $expected }
            continue
        }

        $out = Join-Path $Destination ($rel -replace '/', '\')
        if ($PSCmdlet.ShouldProcess($out, '꺼낸다')) {
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $out) | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $out, $true)
        }
        $written += $rel
    }
}
finally {
    $archive.Dispose()
}

Write-Host ''
Write-Host "꺼냄     $($written.Count)"
Write-Host "없음     $($missing.Count)"
Write-Host "크기다름 $($wrongSize.Count)"

if ($missing.Count) {
    Write-Host ''; Write-Host '없는 것 (앞 15개):'
    $missing | Select-Object -First 15 | ForEach-Object { Write-Host "   $_" }
}
if ($wrongSize.Count) {
    Write-Host ''; Write-Host '크기가 다른 것 (앞 15개):'
    $wrongSize | Select-Object -First 15 | Format-Table -AutoSize
}

Write-Host ''
if ($written.Count -eq $wanted.Count) {
    Write-Host "통과 — $($wanted.Count)개가 다 나왔고 크기가 다 맞다." -ForegroundColor Green
    Write-Host "다음: git add data/map-source · git commit · git push"
}
else {
    Write-Host "관문 미통과. 계획 0단계는 '없음 0 · 크기다름 0' 이라야 지나간다." -ForegroundColor Yellow
    Write-Host "크기가 다른 것이 20개를 넘으면 zip 이 다른 판이다 — 계획의 '계획을 바꿔야 할 때' 참고."
}
