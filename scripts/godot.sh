#!/bin/bash
# 모바일 클라이언트를 여는 고도. 이것으로 열어야 ".NET SDK 를 설치하라" 는 말이 안 뜬다.
#
#   scripts/godot.sh                       편집기로 프로젝트를 연다
#   scripts/godot.sh --headless --import   그림을 들여온다(재료를 새로 뽑은 뒤 한 번)
#   scripts/godot.sh -- --login monk:1234  게임으로 바로 들어간다(-- 뒤는 클라이언트 인자)
#
# **왜 이게 필요한가.** 고도는 C# 을 빌드하려고 `dotnet` 을 PATH 에서 찾는다. 이 작업공간은 .NET 을
# 저장소 안(.tools/)에 두고 시스템에는 깔지 않으므로, 그냥 열면 못 찾고 "SDK 를 설치하라" 고 한다
# (사용자, 2026-09-19). 여기서 경로를 붙여 준다 — 시스템에 무엇을 깔 필요가 없다.
#
# 시스템에 깔린 고도(/Applications/Godot.app)는 **C# 이 없는 판**이라 이 프로젝트를 못 연다
# (스크립트를 못 읽어 화면이 비어 나온다). 반드시 작업공간 안의 모노 판을 쓴다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT="$ROOT/.tools/godot-4.6-mono/Godot_mono.app/Contents/MacOS/Godot"
DOTNET="$ROOT/.tools/dotnet-9.0.317"
GODOT_DOTNET_SHIM="$ROOT/scripts/godot-dotnet"

if [ ! -x "$GODOT" ]; then
    echo "모노 판 고도가 없습니다: $GODOT" >&2
    echo "docs/mobile-client.md 의 '빌드와 실행' 을 보세요." >&2
    exit 1
fi

if [ ! -x "$DOTNET/dotnet" ]; then
    echo ".NET 이 없습니다: $DOTNET" >&2
    exit 1
fi

if [ ! -x "$GODOT_DOTNET_SHIM/dotnet" ] || [ ! -x "$ROOT/mobile/client/dotnet" ]; then
    echo "Godot용 저장소 .NET 탐색기가 빠졌습니다. 저장소의 scripts/godot-dotnet/ 와 mobile/client/dotnet 을 복원하세요." >&2
    exit 1
fi

# Godot 4.6's editor tools themselves target net8.0 even though this project
# targets net9.0.  Keep the host deterministic: Finder and a bare shell do not
# inherit the repository SDK path, and then Godot displays its misleading SDK
# installation prompt.  The checked-in SDK deliberately includes this runtime.
if ! "$DOTNET/dotnet" --list-runtimes | grep -q '^Microsoft.NETCore.App 8\.'; then
    echo "Godot 4.6 needs the bundled .NET 8 runtime, but it is missing from: $DOTNET" >&2
    echo "Restore the repository .tools/dotnet-9.0.317 tool bundle; do not install a system SDK." >&2
    exit 1
fi

export DOTNET_ROOT="$DOTNET"
# Godot 4.6's plugin falls back to its current working directory while finding
# dotnet, where mobile/client/dotnet is the repository-local shim. The shim
# leaves its SDK-list probe alive after output so the editor receives it.
export PATH="$GODOT_DOTNET_SHIM:$DOTNET:$PATH"
export DOTNET_MULTILEVEL_LOOKUP=0

exec "$GODOT" --path "$ROOT/mobile/client" "$@"
