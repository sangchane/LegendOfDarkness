"""NPC 적재(write_mundanes)가 상점 NPC(write_shops)를 덮지 않는 것을 지킨다.

둘은 같은 파일 이름(`NPC@맵#x,y`)을 쓴다. 전에는 `--kind mundanes --write` 만 따로 돌리면 상점 NPC 의
DefaultMerchantStock 이 말없이 사라졌다. 순서와 상관없이 상점 쪽이 남아야 한다.
모든 경로를 임시 폴더로 돌려 sources/ 에는 쓰지 않는다.
"""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location("pack_import", ROOT / "tools" / "pack-import" / "import.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def spawn(npc, mp, x, y, script=""):
    return {"맵": mp, "좌표": [str(x), str(y)], "NPC": npc, "raw": [mp, str(x), str(y), "2", npc, script]}


class MundanesSkipShopSpotsTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        base = Path(self.tmp.name)
        extracted, scripts = base / "extracted", base / "npc-scripts"
        extracted.mkdir()
        scripts.mkdir()
        (scripts / "무기만들자.cs").write_text("// 옮겨진 스크립트", encoding="utf-8")

        def dump(kind, rows):
            (extracted / f"{kind}.json").write_text(json.dumps(rows, ensure_ascii=False), encoding="utf-8")

        dump("npcs", [{"이름": "베이가", "fields": {"이름": "베이가", "이미지": "35", "말하기": ["0", "물약 팝니다"]}},
                      {"이름": "가렌", "fields": {"이름": "가렌", "이미지": "7", "말하기": ["0", "안녕"]}}])
        dump("npc_spawns", [spawn("베이가", "노비스잡화상점", 3, 14),         # 상점 자리 — 정의 있는 NPC
                            spawn("가렌", "노비스마을", 5, 6),                # 상점 아님
                            spawn("무기만들자", "화론-민가3", 14, 14, "무기만들자")])  # 상점 자리 — 스크립트 NPC
        dump("shops", [{"이름": "체력포션", "갈래": "물건사기", "아이템": ["하급체력포션"]},
                       {"이름": "화론재료사기", "갈래": "물건사기", "아이템": ["철광석"]}])
        dump("items", [{"이름": "하급체력포션"}, {"이름": "철광석"}])

        idtable = base / "ids.tsv"
        idtable.write_text("# 번호표\n"
                           "x\t노비스잡화상점\t20001\ta.map\t노비스잡화상점\n"
                           "x\t노비스마을\t20002\tb.map\t노비스마을\n"
                           "x\t화론-민가3\t20003\tc.map\t화론-민가3\n", encoding="utf-8")
        shopbind = base / "shops.tsv"
        shopbind.write_text("# 상점 결합표\n"
                            "베이가\t노비스잡화상점\t3\t14\t물건사기\t체력포션\n"
                            "무기만들자\t화론-민가3\t14\t14\t물건사기\t화론재료사기\n", encoding="utf-8")

        self.out = base / "server" / "templates" / "mundanes"
        for name, value in (("SERVER", base / "server"), ("EXTRACTED", extracted), ("IDTABLE", idtable),
                            ("SHOPBIND", shopbind), ("NPC_SCRIPTS", scripts)):
            patcher = mock.patch.object(MODULE, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def tearDown(self):
        self.tmp.cleanup()

    def read(self, name):
        return json.loads((self.out / f"{name}.json").read_text(encoding="utf-8"))

    def assert_shops_survive(self):
        self.assertEqual(self.read("베이가@노비스잡화상점#3,14")["DefaultMerchantStock"], ["하급체력포션"])
        self.assertEqual(self.read("무기만들자@화론-민가3#14,14")["ScriptKey"], "shop1")
        self.assertEqual(self.read("가렌@노비스마을#5,6")["ScriptKey"], "pack_speaker")

    def test_mundanes_after_shops_keeps_merchant_stock(self):
        MODULE.write_shops(None)
        keep, _ = MODULE.eligible_mundanes(None)
        n, _mute, shop = MODULE.write_mundanes(keep)
        self.assertEqual((n, shop), (1, 2))
        self.assert_shops_survive()

    def test_mundanes_before_shops_gives_same_result(self):
        keep, _ = MODULE.eligible_mundanes(None)
        MODULE.write_mundanes(keep)
        MODULE.write_shops(None)
        self.assert_shops_survive()

    def test_mundanes_alone_does_not_write_shop_spots(self):
        keep, _ = MODULE.eligible_mundanes(None)
        MODULE.write_mundanes(keep)
        self.assertEqual(sorted(p.stem for p in self.out.glob("*.json")), ["가렌@노비스마을#5,6"])


if __name__ == "__main__":
    unittest.main()
