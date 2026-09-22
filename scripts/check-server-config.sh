#!/bin/bash
# Verify that both Hades redirect configurations advertise the same address.
# Usage: check-server-config.sh [Staging/net9.0]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGING="${1:-$ROOT/sources/wren11/Dark-Ages-Private-Server/Staging/net9.0}"

python3 - "$STAGING" <<'PY'
import re
import sys
from pathlib import Path
from xml.etree import ElementTree

staging = Path(sys.argv[1])
xml_path = staging / "MServerTable.xml"
json_path = staging / "LoruleConfig.json"
missing = [str(p) for p in (xml_path, json_path) if not p.is_file()]
if missing:
    raise SystemExit("missing server config: " + ", ".join(missing))

addr = ElementTree.parse(xml_path).findtext("./Servers/MServer/Addr")
text = json_path.read_text(encoding="utf-8-sig")
# LoruleConfig is JSON with comments; ServerIP itself is a plain string, so
# extracting this one field avoids accepting a stale/generated config pair.
match = re.search(r'"ServerIP"\s*:\s*"([^"]+)"', text)
config_ip = match.group(1) if match else None
if not addr or not config_ip:
    raise SystemExit("server config has no redirect address")
if addr != config_ip:
    raise SystemExit(f"redirect address mismatch: MServerTable={addr!r}, LoruleConfig={config_ip!r}")
print(f"server config OK: {addr} (lobby and game redirect)")
PY
