"""저장소 안 .NET 과 dat-extract 도구. 시스템에는 .NET 을 깔지 않는다."""
import subprocess

from lib._paths import ROOT

DOTNET = ROOT / ".tools" / "dotnet-9.0.317" / "dotnet"
TOOL = ROOT / "tools" / "dat-extract" / "bin" / "Release" / "net8.0" / "dat-extract.dll"


def run_tool(*args):
    """dat-extract 를 돌린다. 실패해도 결과를 그대로 돌려준다(부른 쪽이 본다)."""
    return subprocess.run([str(DOTNET), str(TOOL), *map(str, args)],
                          capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)
