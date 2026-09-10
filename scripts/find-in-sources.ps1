<#
.SYNOPSIS
    원작이 어떻게 하는지 참고 저장소 16개에서 한 번에 찾는다.

.DESCRIPTION
    `sources/` 아래 저장소는 크고(dark-ages-ts 하나가 node_modules 포함 수만 파일) 보통 검색은 거기서
    멎는다. 이 스크립트는 각 저장소에서 `git grep` 을 돌린다 — **커밋된 파일만** 보므로 node_modules·
    bin·obj 는 애초에 대상이 아니고, 열여섯 개를 다 뒤져도 1초 안쪽이다.

    커밋된 **산출물**(아틀라스·테이블·에셋)도 같이 걸린다. 원작 규칙은 코드보다 거기 적혀 있는 일이
    잦다 — docs/where-the-answers-are.md 참고.

.EXAMPLE
    ./scripts/find-in-sources.ps1 setDye -Include *.ts
    ./scripts/find-in-sources.ps1 'offsets:' -Include *.atlas -Limit 5
    ./scripts/find-in-sources.ps1 '\.Dye\(' -Include *.cs        # 정규식(-E)이다
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)] [string] $Pattern,
    [string[]] $Include,
    [int] $Limit = 40,
    [switch] $CaseSensitive
)

$ErrorActionPreference = 'Continue'

# 결과에 한글 경로·주석이 섞이므로 콘솔을 UTF-8 로 맞춘다.
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$repositories = Get-ChildItem -Path "$PSScriptRoot/../sources" -Directory |
    ForEach-Object { Get-ChildItem -Path $_.FullName -Directory } |
    Where-Object { Test-Path (Join-Path $_.FullName '.git') }

$arguments = @('grep', '-n', '-E')
if (-not $CaseSensitive) { $arguments += '-i' }
$arguments += @('-e', $Pattern)
if ($Include) { $arguments += @('--') + $Include }

$found = 0

foreach ($repository in $repositories) {
    $hits = & git -C $repository.FullName @arguments 2>$null

    if (-not $hits) { continue }

    $name = "$($repository.Parent.Name)/$($repository.Name)"

    foreach ($hit in $hits) {
        if ($found -ge $Limit) {
            Write-Output "... $Limit 줄에서 끊었습니다. -Limit 을 올리거나 -Include 로 좁히세요."
            return
        }

        Write-Output "sources/$name/$hit"
        $found++
    }
}

if ($found -eq 0) {
    Write-Output "'$Pattern' 은(는) 참고 저장소의 커밋된 파일에 없습니다."
}
