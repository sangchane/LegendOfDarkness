import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('rename_bots', Path(__file__).resolve().parents[1] / 'scripts/ops/rename-bots.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

BOM = b'\xef\xbb\xbf'


class RenameBotsTests(unittest.TestCase):
    """SC-5 — 클라우드 봇 이름 사제→성직자: 캐릭터 파일·Username·봇 설정·경매·유찰 목록, 미리보기는 안 바꾸고, 새 이름이 있으면 건너뛴다."""

    def make(self, root):
        (root / 'aislings').mkdir()
        (root / 'auction').mkdir()
        (root / 'bot').mkdir()
        # BOM 있는 파일 · 다른 칸에 같은 글자(사제봇1 이 Nickname 에) — Username 만 바뀌어야 한다.
        (root / 'aislings/사제봇1.json').write_bytes(BOM + '{\n  "Nickname": "사제봇1",\n  "Username": "사제봇1",\n  "Level": 41\n}'.encode())
        (root / 'aislings/사제봇1.json.backup').write_bytes('{"Username": "사제봇1"}'.encode())
        (root / 'aislings/동료사제.json').write_text('{\r\n  "Username": "동료사제"\r\n}', encoding='utf-8')
        (root / 'aislings/사제봇2.json').write_text('{"Username": "사제봇2"}', encoding='utf-8')
        (root / 'aislings/성직자봇2.json').write_text('{"Username": "성직자봇2"}', encoding='utf-8')  # 이미 있음 → 건너뜀
        (root / 'aislings/전사봇1.json').write_text('{"Username": "전사봇1"}', encoding='utf-8')
        (root / 'eco.json').write_text(json.dumps({'Password': 'p"w', 'Bots': [
            {'Name': '사제봇1', 'Path': 4}, {'Name': '사제봇2', 'Path': 4}, {'Name': '사제봇3', 'Path': 4}, {'Name': '전사봇1', 'Path': 1}]},
            ensure_ascii=False, indent=2), encoding='utf-8')
        (root / 'bot/companion-bot-1.json').write_text('{"Name": "동료사제", "Password": "x"}', encoding='utf-8')
        (root / 'auction/auction.json').write_text(json.dumps({'Listings': [{'Seller': '사제봇1', 'Bidder': '전사봇1'},
            {'Seller': '전사봇1', 'Bidder': '사제봇1'}], 'Claims': [{'Owner': '사제봇1'}, {'Owner': '사제봇2'}]}, ensure_ascii=False), encoding='utf-8')
        (root / 'unlisted.json').write_text('{"사제봇1": ["에페"], "전사봇1": []}', encoding='utf-8')

    def run_on(self, root, apply):
        lines = []
        done = module.run(apply, aislings=root / 'aislings', eco=root / 'eco.json', bot_dir=root / 'bot', auction=root / 'auction',
                          unlisted=root / 'unlisted.json', backup_to=root / 'backup', log=lines.append)
        return done, lines

    def test_preview_changes_nothing(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            self.make(root)
            before = {p: p.read_bytes() for p in root.rglob('*') if p.is_file()}
            done, lines = self.run_on(root, apply=False)
            self.assertEqual([('사제봇1', '성직자봇1'), ('사제봇3', '성직자봇3'), ('동료사제', '동료성직자')], done)
            self.assertEqual(before, {p: p.read_bytes() for p in root.rglob('*') if p.is_file()})
            self.assertFalse((root / 'backup').exists())

    def test_apply_renames_files_names_and_trades(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            self.make(root)
            done, lines = self.run_on(root, apply=True)
            self.assertEqual(3, len(done))
            a = root / 'aislings'
            self.assertFalse((a / '사제봇1.json').exists())
            self.assertEqual(BOM + '{\n  "Nickname": "사제봇1",\n  "Username": "성직자봇1",\n  "Level": 41\n}'.encode(), (a / '성직자봇1.json').read_bytes())
            self.assertEqual('{"Username": "성직자봇1"}', (a / '성직자봇1.json.backup').read_text(encoding='utf-8'))
            self.assertEqual(b'{\r\n  "Username": "\xeb\x8f\x99\xeb\xa3\x8c\xec\x84\xb1\xec\xa7\x81\xec\x9e\x90"\r\n}', (a / '동료성직자.json').read_bytes())
            # 새 이름이 이미 있던 사제봇2 는 그대로 — 캐릭터·설정·경매 모두
            self.assertEqual('{"Username": "사제봇2"}', (a / '사제봇2.json').read_text(encoding='utf-8'))
            self.assertTrue(any('경고' in line and '사제봇2' in line for line in lines))
            eco = json.loads((root / 'eco.json').read_text(encoding='utf-8'))
            self.assertEqual(['성직자봇1', '사제봇2', '성직자봇3', '전사봇1'], [b['Name'] for b in eco['Bots']])
            self.assertEqual('p"w', eco['Password'])
            self.assertEqual('동료성직자', json.loads((root / 'bot/companion-bot-1.json').read_text(encoding='utf-8'))['Name'])
            book = json.loads((root / 'auction/auction.json').read_text(encoding='utf-8'))
            self.assertEqual([('성직자봇1', '전사봇1'), ('전사봇1', '성직자봇1')], [(l['Seller'], l['Bidder']) for l in book['Listings']])
            self.assertEqual(['성직자봇1', '사제봇2'], [c['Owner'] for c in book['Claims']])
            self.assertEqual({'성직자봇1': ['에페'], '전사봇1': []}, json.loads((root / 'unlisted.json').read_text(encoding='utf-8')))
            # 백업은 바꾸기 전 모습
            self.assertTrue((root / 'backup/aislings/사제봇1.json').exists())
            self.assertIn('사제봇1', (root / 'backup/eco.json').read_text(encoding='utf-8'))
            self.assertIn('동료사제', (root / 'backup/companion-bot-1.json').read_text(encoding='utf-8'))
            # 다시 돌리면 할 일이 없다
            self.assertEqual([], module.run(True, aislings=a, eco=root / 'eco.json', bot_dir=root / 'bot', auction=root / 'auction',
                                            unlisted=root / 'unlisted.json', log=lambda _: None))


if __name__ == '__main__':
    unittest.main()
