#!/bin/bash
# macOS Finder launcher for the C# Godot client.  Do not open project.godot
# with a system Godot: it cannot see this repository's private .NET runtime.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec "$ROOT/scripts/godot.sh" "$@"
