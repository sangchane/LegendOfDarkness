"""Locate the Python interpreter belonging to the graphifyy uv tool."""

from __future__ import annotations

import shutil
import subprocess
import sys
import os
from pathlib import Path
from typing import Callable, Sequence


Run = Callable[..., subprocess.CompletedProcess[str]]


def configure_utf8_stdio(*streams: object) -> None:
    """Keep Korean graph labels and box-drawing progress text writable on Windows."""
    for stream in streams:
        reconfigure = getattr(stream, "reconfigure", None)
        if callable(reconfigure):
            reconfigure(encoding="utf-8")


def execute_graphify_script(
    python: Path,
    script: Path,
    args: Sequence[str],
    *,
    platform: str = sys.platform,
    run: Run = subprocess.run,
) -> int:
    """Run via the tool interpreter, waiting for it on Windows."""
    command = [str(python), str(script), *args]
    if platform == "win32":
        return run(command, check=False).returncode
    os.execv(command[0], command)
    return 0


def _run(command: Sequence[str], run: Run) -> subprocess.CompletedProcess[str]:
    try:
        return run(list(command), capture_output=True, text=True, check=False)
    except OSError as exc:
        raise RuntimeError(f"failed to run {command[0]}: {exc}") from exc


def _windows_tool_python(uv_tool_dir: Path | None, run: Run) -> Path:
    if uv_tool_dir is None:
        result = _run(["uv", "tool", "dir"], run)
        if result.returncode != 0 or not result.stdout.strip():
            detail = result.stderr.strip() or "uv tool dir returned no path"
            raise RuntimeError(f"cannot locate uv tool directory: {detail}")
        uv_tool_dir = Path(result.stdout.strip())
    return Path(uv_tool_dir).expanduser().resolve() / "graphifyy" / "Scripts" / "python.exe"


def _posix_tool_python(graphify_executable: Path | None) -> Path:
    if graphify_executable is None:
        executable = shutil.which("graphify")
        if not executable:
            raise RuntimeError("graphify is not installed: uv tool install graphifyy")
        graphify_executable = Path(executable)

    try:
        graphify_executable = Path(graphify_executable)
        first_line = graphify_executable.read_text(
            encoding="utf-8", errors="replace"
        ).splitlines()[0]
    except (OSError, IndexError) as exc:
        raise RuntimeError(
            f"cannot read graphify launcher: {graphify_executable}"
        ) from exc

    python = first_line.removeprefix("#!").strip()
    if not first_line.startswith("#!") or not python:
        raise RuntimeError(f"graphify launcher has no shebang: {graphify_executable}")
    # Do not resolve the uv environment's ``bin/python`` symlink.  Python uses
    # the invoked venv path to select that environment's site-packages; resolving
    # it to the Homebrew/system interpreter loses graphify itself.
    return Path(python).expanduser().absolute()


def find_graphify_python(
    *,
    platform: str = sys.platform,
    uv_tool_dir: Path | None = None,
    graphify_executable: Path | None = None,
    run: Run = subprocess.run,
) -> Path:
    """Return a graphify-capable interpreter, validating it before use.

    uv installs Windows entry points as PE ``.exe`` launchers, so their bytes
    cannot be parsed as a Unix shebang. The tool environment has a stable
    interpreter location beneath ``uv tool dir`` instead.
    """
    if platform == "win32":
        python = _windows_tool_python(uv_tool_dir, run)
    else:
        python = _posix_tool_python(graphify_executable)

    if not python.is_file():
        raise RuntimeError(f"graphify Python interpreter does not exist: {python}")

    result = _run([str(python), "-c", "import graphify"], run)
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "unknown import error"
        raise RuntimeError(f"graphify Python cannot import graphify: {detail}")
    return python
