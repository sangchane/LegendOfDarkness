import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location(
    "monster_page", ROOT / "scripts/gen/world/build-monster-page-data.py")
page = importlib.util.module_from_spec(spec)
spec.loader.exec_module(page)


class MonsterPageTest(unittest.TestCase):
    def test_server_drop_overrides_gold_and_cut_levels(self):
        definitions = [
            {"Name": "해안", "AreaID": 20455, "Exp": 22380, "DropRate": 0.02, "Drops": {"$values": ["반지"]}},
            {"Name": "광산", "AreaID": 20797, "Exp": 18000, "GoldMinimum": 10000, "DropRate": 0, "Drops": {"$values": ["반지"]}},
            {"Name": "노비스", "AreaID": 20393, "Exp": 1200, "Drops": {"$values": ["시약1", "시약2"]}},
            {"Name": "기본식", "AreaID": 20797, "Level": 2},
            {"Name": "미정의", "AreaID": 20797, "DropRate": 1, "Drops": {"$values": ["없는물건"]}},
        ]
        facts = {"반지": {"DropRate": 0.4}, "시약1": {"DropRate": 1.2}, "시약2": {"DropRate": 1.2}}
        with tempfile.TemporaryDirectory() as scratch:
            monsters = Path(scratch)
            folder = monsters / "group"
            folder.mkdir()
            for i, monster in enumerate(definitions):
                (folder / f"{i}.json").write_text(json.dumps(monster))
            with patch.object(page, "MONSTERS", monsters):
                rows = {r["이름"]: r for r in page.monster_rows(
                    {20455: "뤼케시온해안1-A", 20797: "구광산1-1", 20393: "노비스평원A"}, facts)}
        self.assertEqual(rows["해안"]["드랍"][0]["실제확률"], 0.03)
        self.assertEqual(rows["해안"]["감산레벨"], 51)
        self.assertEqual(rows["해안"]["금화"], [1790, 2686])
        self.assertEqual(rows["광산"]["드랍"][0]["실제확률"], 0)
        self.assertEqual(rows["광산"]["금화"], [10000, 10000])
        self.assertEqual([d["실제확률"] for d in rows["노비스"]["드랍"]], [0.9, 0.1])
        self.assertEqual(rows["노비스"]["금화"], [19, 29])
        self.assertEqual(rows["기본식"]["경험치"], 1020)
        self.assertEqual(rows["미정의"]["드랍"][0]["실제확률"], 0)


if __name__ == "__main__":
    unittest.main()
