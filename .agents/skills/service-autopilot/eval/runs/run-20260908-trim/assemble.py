#!/usr/bin/env python3
"""산출물 디렉터리를 s<N>-b.md 한 파일로 병합 (0907 런과 같은 순서·구분자).

용도: python assemble.py S4   → s4-rpi-ip-camera/ → s4-b.md
      python assemble.py S8   → s8-elder-fall-alert/ → s8-b.md
"""
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
HERE = Path(__file__).resolve().parent
DIRS = {"S4": "s4-rpi-ip-camera", "S8": "s8-elder-fall-alert"}
ORDER = ["00-seed", "01-recon", "02-blindspot-register", "03-prd", "04-architecture",
         "05-api-contract", "06-test-design", "07-ops-design", "08-readiness-report", "decision-log"]
for seed in (sys.argv[1:] or ["S4"]):
    src = HERE / DIRS[seed.upper()]
    parts, missing = [], []
    for name in ORDER:
        p = src / f"{name}.md"
        if p.is_file():
            parts.append(f"<!-- ===== {name}.md ===== -->\n" + p.read_text(encoding="utf-8").rstrip() + "\n")
        else:
            missing.append(name)
    out = HERE / f"{seed.lower()}-b.md"
    out.write_text("\n".join(parts), encoding="utf-8")
    print(f"{out.name}: {out.stat().st_size:,} bytes, {len(parts)} files" + (f", missing: {', '.join(missing)}" if missing else ""))
