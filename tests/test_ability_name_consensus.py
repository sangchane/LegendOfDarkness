import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))

from ability_name_consensus import build_consensus, load_consensus  # noqa: E402


def hades(name="Wind Blade", kind="skill", icon=15):
    return {"name": name, "kind": kind, "raw": ["", f"{icon}/0/0"]}


def packed(name, icon=15):
    return {"이름": name, "fields": {"이미지": str(icon)}}


class AbilityNameConsensusTest(unittest.TestCase):
    def test_accepts_only_an_exact_three_pack_name(self):
        rows = {pack: {"skill": [packed("윈드블레이드")], "spell": []}
                for pack in ("5.99-server", "honden-community", "novaonline")}
        accepted, _ = build_consensus([hades()], rows)
        self.assertEqual(accepted["Wind Blade"]["korean"], "윈드블레이드")

    def test_rejects_pack_disagreement_and_hades_ambiguity(self):
        rows = {
            "5.99-server": {"skill": [packed("윈드블레이드")], "spell": []},
            "honden-community": {"skill": [packed("윈드블레이드")], "spell": []},
            "novaonline": {"skill": [packed("다른이름")], "spell": []},
        }
        accepted, _ = build_consensus([hades()], rows)
        self.assertEqual(accepted, {})

        rows["novaonline"]["skill"] = [packed("윈드블레이드")]
        accepted, _ = build_consensus([hades(), hades("Another")], rows)
        self.assertEqual(accepted, {})

    def test_current_sources_produce_the_reviewed_count(self):
        accepted, _ = load_consensus()
        self.assertEqual(len(accepted), 19)
        self.assertEqual(sum(v["kind"] == "skill" for v in accepted.values()), 9)
        self.assertEqual(sum(v["kind"] == "spell" for v in accepted.values()), 10)


if __name__ == "__main__":
    unittest.main()
