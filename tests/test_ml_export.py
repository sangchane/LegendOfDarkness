import gzip
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('export_activity', Path(__file__).resolve().parents[1] / 'scripts/ml/export-activity.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class MlExportTests(unittest.TestCase):
    """SC-005 — 학습용 사본에 IP·사람 이름이 없고, 모든 줄이 JSON 이며, 숫자 칸은 그대로다(설계 autopilot/eco-bots/)."""

    def test_copy_has_no_ip_or_names_keeps_numbers_and_skips_today(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'activity').mkdir()
            (root / 'eco').mkdir()
            lines = [
                {'id': 'a', 'at': '2026-10-05T01:00:00Z', 'kind': 'xp', 'player': 'Monk5', 'bot': False, 'ip': '203.0.113.9',
                 'session': 's', 'xp': 120, 'gold': 3, 'map': 20015, 'x': 3, 'y': 4, 'level': 30, 'expTotal': 999, 'goldNow': 50,
                 'detail': '맵 20015 (3,4) · 레벨 30 · Monk5', 'meta': {}},
                {'id': 'b', 'at': '2026-10-05T01:00:01Z', 'kind': 'xp', 'player': '전사봇일', 'bot': True, 'ip': '127.0.0.1', 'xp': 5},
                # 대신 사냥 중인 사람도 bot=true — 이름은 가명으로. 거래 상대·기기 번호도.
                {'id': 'c', 'at': '2026-10-05T01:00:02Z', 'kind': 'ledger', 'player': 'Hunter7', 'bot': True,
                 'meta': {'counterparty': 'Trader9', 'delta': -5, 'install': '6f1c2b8e-0000-4000-8000-000000000000'}},
            ]
            (root / 'activity' / '2026-10-05.jsonl').write_text('\n'.join(json.dumps(l, ensure_ascii=False) for l in lines) + '\n\n', encoding='utf-8')
            (root / 'activity' / '2026-10-06.jsonl').write_text(json.dumps(lines[0]) + '\n', encoding='utf-8')
            with gzip.open(root / 'eco' / '2026-10-05.jsonl.gz', 'wt', encoding='utf-8') as eco:
                eco.write('{"v":1,"bot":"전사봇일","ev":"kill"}\n')

            bots = module.bot_names('{ // 주석\n "EcoBots": [ "전사봇일" ], "CompanionBots": [ "동료사제" ] }')
            done = module.export(root / 'activity', root / 'eco', root / 'ml', b'0123456789abcdef-salt', today='2026-10-06', bots=bots)

            self.assertEqual(2, len(done))
            self.assertFalse((root / 'ml' / 'activity' / '2026-10-06.jsonl.gz').exists())
            with gzip.open(root / 'ml' / 'activity' / '2026-10-05.jsonl.gz', 'rt', encoding='utf-8') as read:
                text = read.read()
            rows = [json.loads(line) for line in text.splitlines()]
            self.assertNotIn('Monk5', text)
            self.assertNotIn('Hunter7', text)
            self.assertNotIn('Trader9', text)
            self.assertNotIn('6f1c2b8e', text)
            self.assertEqual(-5, rows[2]['meta']['delta'])
            self.assertNotIn('203.0.113.9', text)
            self.assertTrue(all('ip' not in row and 'detail' not in row and 'player' not in row for row in rows))
            self.assertEqual(module.pseudonym(b'0123456789abcdef-salt', 'monk5'), rows[0]['p'])
            self.assertEqual((20015, 30, 999), (rows[0]['map'], rows[0]['level'], rows[0]['expTotal']))
            self.assertEqual('전사봇일', rows[1]['bot_name'])
            self.assertTrue((root / 'ml' / 'eco' / '2026-10-05.jsonl.gz').exists())

            # 다시 돌려도 이미 있는 것은 그대로.
            self.assertEqual([], module.export(root / 'activity', root / 'eco', root / 'ml', b'0123456789abcdef-salt', today='2026-10-06', bots=bots))


if __name__ == '__main__':
    unittest.main()
