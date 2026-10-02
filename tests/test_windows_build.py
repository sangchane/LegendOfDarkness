import os
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


class WindowsBuildTest(unittest.TestCase):
    def test_only_a_complete_success_replaces_the_previous_release(self):
        for mode in ("import-fails", "export-fails", "incomplete", "success"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as scratch:
                root = Path(scratch)
                script = root / "scripts/ops/windows-build.sh"
                script.parent.mkdir(parents=True)
                shutil.copyfile(ROOT / "scripts/ops/windows-build.sh", script)
                out = root / "mobile/client/build/windows"
                dlls = out / "data_LodClient_windows_x86_64"
                dlls.mkdir(parents=True)
                (out / "LodClient.exe").write_text("old exe")
                (dlls / "LodClient.dll").write_text("old dll")
                archive = out / "LodClient-windows.zip"
                archive.write_bytes(b"old release")

                godot = root / ".tools/godot-4.6-mono/Godot_mono.app/Contents/MacOS/Godot"
                godot.parent.mkdir(parents=True)
                godot.write_text('''#!/bin/bash
set -eu
if [[ "$*" == *--import* ]]; then
    [ "$LOD_WINDOWS_EXPORT_MODE" != import-fails ]; exit
fi
[ "$LOD_WINDOWS_EXPORT_MODE" != export-fails ] || exit 42
target="${@: -1}"
printf 'new exe' > "$target"
if [ "$LOD_WINDOWS_EXPORT_MODE" = success ]; then
    mkdir "$(dirname "$target")/data_LodClient_windows_x86_64"
    printf 'new dll' > "$(dirname "$target")/data_LodClient_windows_x86_64/LodClient.dll"
fi
''')
                godot.chmod(0o755)
                run = subprocess.run(["bash", str(script), "build"], capture_output=True,
                                     env={**os.environ, "LOD_WINDOWS_EXPORT_MODE": mode})
                if mode == "success":
                    self.assertEqual(run.returncode, 0, run.stderr)
                    with zipfile.ZipFile(archive) as packed:
                        self.assertEqual(packed.read("LegendOfDarkness/LodClient.exe"), b"new exe")
                        self.assertEqual(packed.read("LegendOfDarkness/data_LodClient_windows_x86_64/LodClient.dll"), b"new dll")
                else:
                    self.assertNotEqual(run.returncode, 0)
                    self.assertEqual(archive.read_bytes(), b"old release")
                    self.assertEqual((dlls / "LodClient.dll").read_text(), "old dll")
                self.assertEqual(list(out.glob(".build.*")), [])


if __name__ == "__main__":
    unittest.main()
