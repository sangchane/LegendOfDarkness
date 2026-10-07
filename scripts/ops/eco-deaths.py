import json, collections, re, subprocess, sys
from datetime import datetime, timedelta

# 생태계 봇 죽음 나누기 — 역할(파티원·파티장·성직자·파티 없음) · 혼수 뒤였나 · 성직자가 깨우러 왔나 · 시간별.
# 클라우드에서 돈다(eco 기록 + lod-eco 일지): ssh … "python3 - 2026-10-08T00:00 2026-10-08T09:00" < scripts/ops/eco-deaths.py
since, until = sys.argv[1], sys.argv[2]
def T(s): return datetime.fromisoformat(s[:19])

wakes = collections.defaultdict(list)
j = subprocess.run(['journalctl', '-u', 'lod-eco', '--since', (T(since) + timedelta(hours=9)).strftime('%Y-%m-%d %H:%M:%S'),
                    '--until', (T(until) + timedelta(hours=9)).strftime('%Y-%m-%d %H:%M:%S'), '--no-pager', '-o', 'short-iso'],
                   capture_output=True, text=True).stdout
for l in j.splitlines():
    m = re.search(r'(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d) \[생태계\] (사제봇\d+): (주인|파티원) 깨우기', l)
    if m: wakes[m.group(2)].append(datetime.fromisoformat(m.group(1)) - timedelta(hours=9))

party, leader = {}, {}
prev = collections.defaultdict(lambda: collections.deque(maxlen=6))
role, coma, woke, hours, bots = collections.Counter(), collections.Counter(), collections.Counter(), collections.Counter(), set()
for name in sorted(set([since[:10], until[:10]])):
    try: f = open(f'/home/ubuntu/lod-eco/eco/{name}.jsonl')
    except FileNotFoundError: continue
    for line in f:
        d = json.loads(line); b, ev = d['bot'], d['ev']
        if ev == 'party' and 'joined' in d['data']: party[b] = d['data']['joined']; leader[b] = d['data']['leader']
        if ev == 'party' and 'left' in d['data']: party.pop(b, None); leader.pop(b, None)
        if since <= d['at'] < until:
            bots.add(b)
            if ev == 'death':
                r = 'priest' if b.startswith('사제') else 'solo' if b not in leader else 'leader' if leader[b] == b else 'member'
                c = any(x == 'Wait' for x in prev[b])
                role[r] += 1; coma[(r, c)] += 1; hours[d['at'][:13]] += 1
                if c and b in party:
                    pr = [p for p in party[b] if p.startswith('사제')]
                    t = T(d['at'])
                    woke[(r, bool(pr and any(timedelta(0) <= t - w <= timedelta(seconds=30) for w in wakes[pr[0]])))] += 1
        prev[b].append(d['state'])

span = max((T(until) - T(since)).total_seconds() / 3600, 1 / 60)
print(f'{since}~{until} ({span:.1f}h, 봇 {len(bots)})  죽음 {sum(role.values())} = {sum(role.values())/span:.1f}/h')
print(' 역할', dict(role)); print(' (역할,혼수 뒤)', dict(coma)); print(' (역할,깨우기 시도)', dict(woke)); print(' 시간별', dict(sorted(hours.items())))
