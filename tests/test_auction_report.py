import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / 'scripts/ops/auction-report.py'


def report(lines: list[str]) -> subprocess.CompletedProcess:
    with tempfile.TemporaryDirectory() as folder:
        (Path(folder) / 'events-2026-10-08.jsonl').write_text(''.join(line + '\n' for line in lines), encoding='utf-8')
        return subprocess.run([sys.executable, '-I', str(SCRIPT), folder], capture_output=True, text=True)


def event(**fields) -> str:
    return json.dumps(fields, ensure_ascii=False)


class AuctionReportTests(unittest.TestCase):
    """리뷰 2026-10-08 #15 — 예상 산술(goldBefore/After)만 맞춰 보던 보고서가 실제 저장값(saved)과 깨진 줄을 본다."""

    def test_saved_gold_that_differs_from_the_intent_is_reported_even_when_the_arithmetic_matches(self):
        done = report([
            event(seq=1, ev='bid', who='a', listing=1, item='에페', gold=100, goldBefore=1000, goldAfter=900),
            event(seq=1, ev='saved', who='a', goldSaved=800),
            event(seq=1, ev='commit'),
        ])
        self.assertIn('예상 산술이 어긋난 줄: 0', done.stdout)
        self.assertIn('예상과 저장값이 다른 조작: 1', done.stdout)
        self.assertIn('예상 900 · 저장 800', done.stdout)
        self.assertEqual(1, done.returncode)

    def test_matching_saved_gold_passes_and_old_lines_without_saved_are_only_counted(self):
        done = report([
            event(seq=1, ev='bid', who='a', listing=1, item='에페', gold=100, goldBefore=1000, goldAfter=900),
            event(seq=1, ev='saved', who='a', goldSaved=900),
            event(seq=1, ev='commit'),
            event(seq=2, ev='post', who='b', listing=2, item='에페', gold=10, goldBefore=50, goldAfter=40),
            event(seq=2, ev='commit'),
        ])
        self.assertIn('예상과 저장값이 다른 조작: 0', done.stdout)
        self.assertIn('저장값 기록이 없는 조작(옛 줄이거나 saved 줄을 못 씀): 1', done.stdout)
        self.assertEqual(0, done.returncode, done.stdout + done.stderr)

    def test_broken_lines_in_the_middle_and_at_the_end_are_located(self):
        done = report([
            event(seq=1, ev='post', who='a', listing=1, item='에페', gold=10, goldBefore=50, goldAfter=40),
            '{"seq": 1, "ev": "sav',
            event(seq=1, ev='commit'),
            '{"seq": 2, "ev"',
        ])
        self.assertIn('읽지 못한 줄: 2', done.stdout)
        self.assertIn('events-2026-10-08.jsonl:2', done.stdout)
        self.assertIn('events-2026-10-08.jsonl:4', done.stdout)
        self.assertEqual(1, done.returncode)


if __name__ == '__main__':
    unittest.main()
