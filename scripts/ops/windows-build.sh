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

build() {
    mkdir -p "$OUT"
    "$GODOT" --headless --path "$CLIENT" --import >/dev/null 2>&1 || true
    "$GODOT" --headless --path "$CLIENT" --export-release "Windows" "$OUT/LodClient.exe" >/dev/null 2>&1 || true

    # EXPORT 결과를 믿지 않는다(ios-build.sh 와 같은 이유) — 실행 파일과 C# 폴더가 다 있는지 본다.
    if [ ! -s "$OUT/LodClient.exe" ] || [ ! -s "$OUT/data_LodClient_windows_x86_64/LodClient.dll" ]; then
        echo "윈도우판을 만들지 못했습니다 — 실행 파일이나 C# 폴더가 없습니다." >&2
        exit 1
    fi

    # 맥 시험용 자동 로그인(login.cfg)이 실리면 받는 사람 모두 그 계정으로 들어간다(윈도우판은 그 파일을 읽는다).
    # 내보내기 설정(exclude_filter)이 빼지만, 올리기 전에 한 번 더 본다.
    if [ -f "$CLIENT/login.cfg" ] && strings "$OUT/LodClient.exe" | grep -qxF "$(head -1 "$CLIENT/login.cfg")"; then
        echo "시험 계정(login.cfg)이 실렸습니다 — export_presets.cfg 의 exclude_filter 를 보십시오." >&2
        exit 1
    fi

    local stage
    stage="$(mktemp -d)"
    mkdir "$stage/LegendOfDarkness"
    cp -R "$OUT/LodClient.exe" "$OUT/data_LodClient_windows_x86_64" "$stage/LegendOfDarkness/"
    rm -f "$ZIP"
    (cd "$stage" && zip -qr -X -9 "$ZIP" LegendOfDarkness)
    rm -rf "$stage"
    echo "만들었습니다 — $ZIP ($(du -h "$ZIP" | cut -f1))"
}

case "${1:-build}" in
    build) build ;;
    release) build; LOD_CLOUD_IP="${LOD_CLOUD_IP:-161.33.43.117}" "$ROOT/scripts/ops/cloud-dashboard.sh" release windows ;;
    *) echo "쓸 수 있는 것: build release"; exit 2 ;;
esac
