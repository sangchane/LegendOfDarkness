"""graphify 는 uv tool 로 따로 깔려 있다. 그쪽 파이썬으로 옮겨 탄다."""
import sys
from pathlib import Path

from graphify_runtime import execute_graphify_script, find_graphify_python


def ensure_graphify_python(script):
    """graphify 를 못 들이면 `script` 를 graphify 파이썬으로 다시 띄운다(돌아오지 않는다)."""
    try:
        import graphify  # noqa: F401
        return
    except ImportError:
        pass
    try:
        py = find_graphify_python()
    except RuntimeError as exc:
        sys.exit(str(exc))
    raise SystemExit(execute_graphify_script(py, Path(script).resolve(), sys.argv[1:]))
