import importlib.util
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location(
    "build_server_pack_vault", ROOT / "scripts" / "build-server-pack-vault.py"
)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ServerPackVaultCommandTest(unittest.TestCase):
    def test_table_commands_survive_without_disassembly_evidence(self):
        table = {
            "출처실행파일": "Legend.exe",
            "명령": [{"이름": "warp", "인자서명": "sii", "인자수": 3}],
        }

        self.assertEqual(
            MODULE.merge_command_records(table, []),
            [
                {
                    "이름": "warp",
                    "인자서명": "sii",
                    "인자수": 3,
                    "증거": {},
                    "출처": "Legend.exe",
                }
            ],
        )

    def test_evidence_enrichment_and_provenance_remain_unchanged(self):
        table = {
            "출처실행파일": "table.exe",
            "명령": [{"이름": "warp", "인자서명": "sii", "인자수": 3}],
        }
        evidence_record = {"이름": "warp", "함수주소": "0x1234"}
        evidence = {
            "출처실행파일": "evidence.exe",
            "명령": [evidence_record],
        }

        self.assertEqual(
            MODULE.merge_command_records(table, evidence),
            [
                {
                    "이름": "warp",
                    "인자서명": "sii",
                    "인자수": 3,
                    "증거": evidence_record,
                    "출처": "evidence.exe",
                }
            ],
        )


if __name__ == "__main__":
    unittest.main()
