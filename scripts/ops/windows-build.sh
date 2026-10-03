#!/bin/bash
# 윈도우 PC판을 만들어 내려받기 페이지(/download/)에 올린다. 맥에서 그대로 만든다(서명 없음).
#
#   scripts/ops/windows-build.sh build      build/windows/LodClient-windows.zip 을 만든다
#   scripts/ops/windows-build.sh release    만들고 클라우드 내려받기 페이지에 올린다
#
# 압축 안에는 LegendOfDarkness/ 폴더 하나 — LodClient.exe(그림·소리가 들어 있음)와 C# 폴더
# data_LodClient_windows_x86_64/ 가 나란히 있어야 뜬다. 서명이 없어 처음 열 때 윈도우가 「PC 보호」 창을 띄운다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CLIENT="$ROOT/mobile/client"
OUT="$CLIENT/build/windows"
ZIP="$OUT/LodClient-windows.zip"
GODOT="$ROOT/.tools/godot-4.6-mono/Godot_mono.app/Contents/MacOS/Godot"
export DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317"
export PATH="$DOTNET_ROOT:$PATH"

build() (
    mkdir -p "$OUT"
    stage="$(mktemp -d "$OUT/.build.XXXXXX")"
    trap 'rm -rf "$stage"' EXIT
    fresh="$stage/LegendOfDarkness"
    mkdir "$fresh"
    "$GODOT" --headless --path "$CLIENT" --import
    # 앱이 켜질 때 내려받기 페이지의 번호와 견준다(AppUpdate) — release 가 build/windows/version.txt 를 올린다.
    date +%Y%m%d%H%M > "$CLIENT/app-version.txt"
    "$GODOT" --headless --path "$CLIENT" --export-release "Windows" "$fresh/LodClient.exe"
    cp "$CLIENT/app-version.txt" "$fresh/../version.txt"

    # 새로 내보낸 파일만 검사한다 — 이전 성공본으로 이번 실패를 가리지 않는다.
    if [ ! -s "$fresh/LodClient.exe" ] || [ ! -s "$fresh/data_LodClient_windows_x86_64/LodClient.dll" ]; then
        echo "윈도우판을 만들지 못했습니다 — 실행 파일이나 C# 폴더가 없습니다." >&2
        exit 1
    fi

    # 맥 시험용 자동 로그인(login.cfg)이 실리면 받는 사람 모두 그 계정으로 들어간다(윈도우판은 그 파일을 읽는다).
    # 내보내기 설정(exclude_filter)이 빼지만, 올리기 전에 한 번 더 본다.
    if [ -f "$CLIENT/login.cfg" ] && strings "$fresh/LodClient.exe" | grep -axF "$(head -1 "$CLIENT/login.cfg")" >/dev/null; then
        echo "시험 계정(login.cfg)이 실렸습니다 — export_presets.cfg 의 exclude_filter 를 보십시오." >&2
        exit 1
    fi

    (cd "$stage" && zip -qr -X -9 "$stage/LodClient-windows.zip" LegendOfDarkness)
    mv -f "$fresh/LodClient.exe" "$OUT/LodClient.exe"
    rm -rf "$OUT/data_LodClient_windows_x86_64"
    mv "$fresh/data_LodClient_windows_x86_64" "$OUT/"
    mv -f "$stage/LodClient-windows.zip" "$ZIP"
    mv -f "$stage/version.txt" "$OUT/version.txt"
    echo "만들었습니다 — $ZIP ($(du -h "$ZIP" | cut -f1))"
)

case "${1:-build}" in
    build) build ;;
    release) build; LOD_CLOUD_IP="${LOD_CLOUD_IP:-161.33.43.117}" "$ROOT/scripts/ops/cloud-dashboard.sh" release windows ;;
    *) echo "쓸 수 있는 것: build release"; exit 2 ;;
esac
