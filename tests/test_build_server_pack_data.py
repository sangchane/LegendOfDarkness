import importlib.util
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
SPEC = importlib.util.spec_from_file_location(
    "build_server_pack_data", ROOT / "scripts" / "build-server-pack-data.py"
)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
parse_manifest = MODULE.parse_manifest


class ServerPackManifestTest(unittest.TestCase):
    def test_single_hash_documentation_is_not_a_manifest_entry(self):
        text = """# 타입[0]: 장비 아이템 & 소비 아이템
#  -속성[0]: 평범한 아이템(도토리,웅담)
item:db/item/weapon/sword.txt
"""

        self.assertEqual(
            parse_manifest(text),
            [("item", "db/item/weapon/sword.txt")],
        )


if __name__ == "__main__":
    unittest.main()
