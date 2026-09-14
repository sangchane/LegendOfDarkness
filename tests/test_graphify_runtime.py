import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location(
    "graphify_runtime", ROOT / "scripts" / "graphify_runtime.py"
)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
find_graphify_python = MODULE.find_graphify_python
configure_utf8_stdio = MODULE.configure_utf8_stdio
execute_graphify_script = MODULE.execute_graphify_script


class GraphifyRuntimeTest(unittest.TestCase):
    def test_windows_waits_for_graphify_child_and_returns_its_status(self):
        calls = []

        def run(command, **kwargs):
            calls.append((command, kwargs))
            return subprocess.CompletedProcess(command, 7)

        status = execute_graphify_script(
            Path("graphify-python.exe"), Path("build.py"), ["novaonline"],
            platform="win32", run=run,
        )

        self.assertEqual(status, 7)
        self.assertEqual(calls, [
            (["graphify-python.exe", "build.py", "novaonline"], {"check": False})
        ])

    def test_configures_console_streams_for_utf8_graph_labels(self):
        class Stream:
            def __init__(self):
                self.calls = []

            def reconfigure(self, **kwargs):
                self.calls.append(kwargs)

        stdout = Stream()
        stderr = Stream()

        configure_utf8_stdio(stdout, stderr)

        self.assertEqual(stdout.calls, [{"encoding": "utf-8"}])
        self.assertEqual(stderr.calls, [{"encoding": "utf-8"}])

    def test_windows_uses_graphifyy_uv_tool_environment(self):
        with tempfile.TemporaryDirectory() as tmp:
            tool_dir = Path(tmp)
            python = tool_dir / "graphifyy" / "Scripts" / "python.exe"
            python.parent.mkdir(parents=True)
            python.touch()
            calls = []

            def run(command, **kwargs):
                calls.append((command, kwargs))
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            result = find_graphify_python(
                platform="win32", uv_tool_dir=tool_dir, run=run
            )

            self.assertEqual(result, python.resolve())
            self.assertEqual(calls[0][0], [str(python.resolve()), "-c", "import graphify"])

    def test_windows_obtains_uv_tool_dir_when_not_supplied(self):
        with tempfile.TemporaryDirectory() as tmp:
            tool_dir = Path(tmp)
            python = tool_dir / "graphifyy" / "Scripts" / "python.exe"
            python.parent.mkdir(parents=True)
            python.touch()
            calls = []

            def run(command, **kwargs):
                calls.append(command)
                if command == ["uv", "tool", "dir"]:
                    return subprocess.CompletedProcess(
                        command, 0, stdout=f"{tool_dir}\n", stderr=""
                    )
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            result = find_graphify_python(platform="win32", run=run)

            self.assertEqual(result, python.resolve())
            self.assertEqual(calls, [
                ["uv", "tool", "dir"],
                [str(python.resolve()), "-c", "import graphify"],
            ])

    def test_posix_keeps_graphify_launcher_shebang_behavior(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            python = root / "python"
            python.touch()
            launcher = root / "graphify"
            launcher.write_text(f"#!{python}\n", encoding="utf-8")

            def run(command, **kwargs):
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            result = find_graphify_python(
                platform="linux", graphify_executable=launcher, run=run
            )

            self.assertEqual(result, python.resolve())

    def test_rejects_tool_python_that_cannot_import_graphify(self):
        with tempfile.TemporaryDirectory() as tmp:
            tool_dir = Path(tmp)
            python = tool_dir / "graphifyy" / "Scripts" / "python.exe"
            python.parent.mkdir(parents=True)
            python.touch()

            def run(command, **kwargs):
                return subprocess.CompletedProcess(
                    command, 1, stdout="", stderr="ModuleNotFoundError: graphify"
                )

            with self.assertRaisesRegex(RuntimeError, "cannot import graphify"):
                find_graphify_python(
                    platform="win32", uv_tool_dir=tool_dir, run=run
                )


if __name__ == "__main__":
    unittest.main()
