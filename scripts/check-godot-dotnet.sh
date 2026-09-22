#!/bin/bash
# Regression check for the Finder launcher and Godot's managed editor plugin.
# A plain `dotnet --list-sdks` check is insufficient: Godot 4.6 used to lose
# the output of that exact probe and show an SDK-install prompt anyway.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT_DIR="$ROOT/.tools/dotnet-9.0.317"
DOTNET="$DOTNET_ROOT_DIR/dotnet"
LAUNCHER="$ROOT/scripts/open-lod-client.command"
PROJECT_DOTNET="$ROOT/mobile/client/dotnet"
LOG="$(mktemp "${TMPDIR:-/tmp}/lod-godot-dotnet.XXXXXX")"
trap 'rm -f "$LOG"' EXIT

if [ ! -x "$DOTNET" ]; then
    echo "missing bundled dotnet: $DOTNET" >&2
    exit 1
fi

if [ ! -x "$LAUNCHER" ]; then
    echo "Finder launcher is not executable: $LAUNCHER" >&2
    exit 1
fi

if [ ! -x "$PROJECT_DOTNET" ]; then
    echo "Godot's project-local dotnet finder is missing: $PROJECT_DOTNET" >&2
    exit 1
fi

"$DOTNET" --list-sdks | grep -q '^9\.0\.317 '
"$DOTNET" --list-runtimes | grep -q '^Microsoft.NETCore.App 8\.0\.31 '
"$DOTNET" --list-runtimes | grep -q '^Microsoft.NETCore.App 9\.0\.19 '

# Finder does not inherit an interactive shell's SDK variables or PATH.  Start
# at the public .command boundary with only the standard macOS environment,
# and keep the editor alive until the C# plugin has initialized.
env -i \
    HOME="$HOME" \
    USER="${USER:-$(id -un)}" \
    LOGNAME="${LOGNAME:-$(id -un)}" \
    SHELL=/bin/zsh \
    TMPDIR="${TMPDIR:-/tmp}" \
    PATH=/usr/bin:/bin:/usr/sbin:/sbin \
    "$LAUNCHER" --headless --editor --verbose --quit-after 300 >"$LOG" 2>&1

if ! grep -Fq "Found .NET Sdk version '9.0.317': $DOTNET_ROOT_DIR/sdk/9.0.317" "$LOG"; then
    echo "Godot did not finish C# SDK discovery with SDK 9.0.317:" >&2
    cat "$LOG" >&2
    exit 1
fi

if grep -Eiq '\.NET Sdk not found|Sequence contains no elements|Microsoft\.Build.*(not found|Could not load)|MSBuild.*(error|fail)' "$LOG"; then
    echo "Godot C# SDK/MSBuild initialization failed:" >&2
    grep -Ein '\.NET Sdk not found|Sequence contains no elements|Microsoft\.Build.*(not found|Could not load)|MSBuild.*(error|fail)' "$LOG" >&2
    exit 1
fi

# The same repository SDK must still compile the client outside the editor.
env -i \
    HOME="$HOME" \
    USER="${USER:-$(id -un)}" \
    LOGNAME="${LOGNAME:-$(id -un)}" \
    TMPDIR="${TMPDIR:-/tmp}" \
    PATH=/usr/bin:/bin:/usr/sbin:/sbin \
    DOTNET_ROOT="$DOTNET_ROOT_DIR" \
    "$DOTNET" build "$ROOT/mobile/client/LodClient.csproj" --nologo --verbosity quiet

grep -F "Found .NET Sdk version '9.0.317': $DOTNET_ROOT_DIR/sdk/9.0.317" "$LOG"
echo "Godot C# Finder launch and client build: SDK 9.0.317 passed."
