"""관리자 전용 접속·활동 기록. stdlib SQLite, 로그 본문은 허용 필드만 저장한다."""
import glob
import json
import os
import re
import sqlite3
import threading
import time
from datetime import datetime, timedelta, timezone
from contextlib import contextmanager
from pathlib import Path
from urllib.parse import urlsplit

KST = timezone(timedelta(hours=9))


def allowed_meta(value):
    if not isinstance(value, dict):
        return {}
    strings = {'install': 64, 'run': 64, 'platform': 32, 'model': 80, 'os': 80, 'version': 64,
               'screen': 80, 'action': 100, 'error': 100, 'previousScreen': 80, 'occurredAt': 64,
               'transaction': 80, 'asset': 16, 'item': 200, 'reason': 200,
               'counterparty': 64, 'from': 80, 'to': 80}
    result = {key: value[key][:limit] for key, limit in strings.items() if isinstance(value.get(key), str)}
    for key in ('seconds', 'quantity', 'delta', 'balance'):
        if isinstance(value.get(key), int) and not isinstance(value[key], bool) and abs(value[key]) <= 2**63-1:
            result[key] = value[key]
    for key in ('reported', 'beforeLogin'):
        if isinstance(value.get(key), bool):
            result[key] = value[key]
    return result


class ActivityStore:
    def __init__(self, database, web='', game='', characters='', server_config='', ml_activity=''):
        self.database = Path(database)
        self.sources = {'web': web, 'game': game}
        self.characters = Path(characters) if characters else None
        self.server_config = Path(server_config) if server_config else None
        # 운영 서비스(ability-ops-service.py)는 넘기지 않는다 — 머신러닝 사본 폴더는 서비스 환경변수에서.
        ml_activity = ml_activity or os.environ.get('LOD_ML_ACTIVITY', '')
        self.ml_activity = Path(ml_activity) if ml_activity else None
        self.lock = threading.Lock()
        self.status = {'collectedAt': None, 'errors': [], 'retentionDays': 90}
        self.database.parent.mkdir(parents=True, exist_ok=True)
        self.database.touch(mode=0o600, exist_ok=True)
        os.chmod(self.database, 0o600)
        with self.connect() as db:
            db.executescript('''
                CREATE TABLE IF NOT EXISTS events (
                    id TEXT PRIMARY KEY, at TEXT NOT NULL, day TEXT NOT NULL,
                    source TEXT NOT NULL, kind TEXT NOT NULL, player TEXT NOT NULL,
                    bot INTEGER NOT NULL, ip TEXT NOT NULL, agent TEXT NOT NULL,
                    path TEXT NOT NULL, status INTEGER NOT NULL, bytes INTEGER NOT NULL,
                    detail TEXT NOT NULL, session TEXT NOT NULL, amount INTEGER NOT NULL,
                    xp INTEGER NOT NULL, gold INTEGER NOT NULL, seconds INTEGER NOT NULL);
                CREATE INDEX IF NOT EXISTS event_day ON events(day, source, bot, player);
                CREATE TABLE IF NOT EXISTS offsets (file TEXT PRIMARY KEY, position INTEGER NOT NULL);
            ''')
            if 'meta' not in {row['name'] for row in db.execute('PRAGMA table_info(events)')}:
                db.execute("ALTER TABLE events ADD COLUMN meta TEXT NOT NULL DEFAULT '{}'")
            db.execute('CREATE INDEX IF NOT EXISTS event_session ON events(source, kind, session)')
            # 생태 봇이 하루 100만 줄 넘게 남긴다(2026-10-07~) — 사람만 볼 때 봇 줄을 아예 건너뛰게 bot 을 맨 앞에.
            db.execute('CREATE INDEX IF NOT EXISTS event_bot ON events(bot, source, kind, day)')
            # 봇 이상 구간(autopilot/bot-data-cleanup 3) — 봇 줄을 7일 뒤 지워도 이것은 89일 남는다.
            db.execute('''CREATE TABLE IF NOT EXISTS anomalies (
                player TEXT NOT NULL, rule TEXT NOT NULL, start TEXT NOT NULL, "end" TEXT NOT NULL,
                minutes INTEGER NOT NULL, map INTEGER, x INTEGER, y INTEGER, detail TEXT NOT NULL,
                actions INTEGER NOT NULL, PRIMARY KEY (player, rule, start))''')
        os.chmod(self.database, 0o600)

    @contextmanager
    def connect(self):
        db = sqlite3.connect(self.database, timeout=10)
        db.row_factory = sqlite3.Row
        try:
            with db:
                yield db
        finally:
            db.close()

    def collect(self):
        errors, behind = [], False
        with self.lock, self.connect() as db:
            for source, pattern in self.sources.items():
                if not pattern:
                    continue
                files = sorted(glob.glob(pattern))
                if not files:
                    errors.append(source + ': 기록 파일이 아직 없습니다.')
                for filename in files:
                    try:
                        with open(filename, 'rb') as stream:
                            stat = os.fstat(stream.fileno())
                            key = f'{source}:{stat.st_dev}:{stat.st_ino}'
                            row = db.execute('SELECT position FROM offsets WHERE file=?', (key,)).fetchone()
                            position = row[0] if row and row[0] <= stat.st_size else 0
                            stream.seek(position)
                            # ponytail: 한 번에 파일당 2MB, 그 이상은 다음 수집에서 이어 읽는다.
                            budget = 2 * 1024 * 1024
                            while budget > 0:
                                line = stream.readline(65537)
                                if not line:
                                    break
                                if len(line) > 65536:
                                    while line and not line.endswith(b'\n'):
                                        line = stream.readline(65537)
                                    errors.append(source + ': 너무 긴 기록을 건너뛰었습니다.')
                                    position = stream.tell()
                                    continue
                                if not line.endswith(b'\n'):
                                    break
                                budget -= len(line)
                                try:
                                    self.insert(db, source, json.loads(line))
                                except (ValueError, TypeError, KeyError, OverflowError):
                                    errors.append(source + ': 읽을 수 없는 기록을 건너뛰었습니다.')
                                position = stream.tell()
                            behind = behind or (source == 'game' and budget <= 0)
                            db.execute('INSERT OR REPLACE INTO offsets VALUES (?,?)', (key, position))
                    except OSError:
                        errors.append(source + ': 기록 파일을 읽지 못했습니다.')
            db.commit()  # 이상 구간 계산이 길어도 읽은 줄은 먼저 남긴다
            if not behind:  # 덜 읽은 게임 기록이 있으면 칸이 비어 보인다 — 다 따라잡은 뒤에 센다
                try:
                    self.find_anomalies(db)
                except OSError as error:
                    errors.append('이상 구간: ' + str(error))
            today = datetime.now(KST).date()
            cutoff = (today - timedelta(days=89)).isoformat()
            db.execute('DELETE FROM events WHERE day < ?', (cutoff,))
            db.execute('DELETE FROM anomalies WHERE start < ?', (cutoff,))
            done = db.execute("SELECT position FROM offsets WHERE file='anomalies'").fetchone()
            if done:
                # 봇 줄은 7일 — 그 날의 이상 구간을 남긴 뒤에만 지운다. 하루치(100만 줄)를 한 번에 지우면 1분 가까이 조회가 막혀
                # 수집마다 2만 줄씩. event_bot 으로 가면 봇 줄 전체를 훑는다.
                week = min((today - timedelta(days=7)).isoformat(), datetime.fromtimestamp(done[0], KST).date().isoformat())
                db.execute('''DELETE FROM events WHERE rowid IN
                    (SELECT rowid FROM events INDEXED BY event_day WHERE day < ? AND bot=1 LIMIT 20000)''', (week,))
            recent = (today - timedelta(days=3)).isoformat()
            for filename in glob.glob(self.sources['game']):
                path = Path(filename)
                try:
                    day = datetime.strptime(path.stem, '%Y-%m-%d').date().isoformat()
                    # 원본은 하루 1GB 가까이 — 머신러닝 사본을 만들고 다 읽은 날은 3일 뒤 지운다. 사본이 없으면 89일.
                    copied = self.ml_activity is not None and (self.ml_activity / f'{day}.jsonl.gz').exists()
                    if copied:
                        stat = path.stat()
                        read = db.execute('SELECT position FROM offsets WHERE file=?', (f'game:{stat.st_dev}:{stat.st_ino}',)).fetchone()
                        copied = bool(read) and read[0] >= stat.st_size
                    if day < cutoff or (copied and day < recent):
                        path.unlink()
                except ValueError:
                    pass
                except OSError:
                    errors.append('game: 오래된 원시 기록을 지우지 못했습니다.')
            self.status = {'collectedAt': datetime.now(KST).isoformat(), 'errors': sorted(set(errors)), 'retentionDays': 90}

    def insert(self, db, source, event):
        at = datetime.fromisoformat(str(event['at']).replace('Z', '+00:00'))
        if at.tzinfo is None:
            raise ValueError('timezone required')
        at = at.astimezone(KST)
        text = lambda key, limit=200: str(event.get(key) or '')[:limit]
        if not text('id'):
            raise ValueError('id required')
        path = urlsplit(text('path', 1024)).path
        status, size = int(event.get('status') or 0), max(0, int(event.get('bytes') or 0))
        kind = text('kind', 80)
        if source == 'web':
            method = text('method', 10)
            if method != 'GET':
                return
            download = path in ('/download/LodClient.ipa', '/download/LodClient-windows.zip')
            page = path == '/' or path.endswith('.html') or path.endswith('/')
            if not download and not page:
                return
            kind = 'download' if download and status in (200, 206) and size > 0 else 'visit'
        detail = text('detail', 1000)
        db.execute('''INSERT OR IGNORE INTO events
            (id,at,day,source,kind,player,bot,ip,agent,path,status,bytes,detail,session,amount,xp,gold,seconds,meta)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)''', (
            source + ':' + text('id', 160), at.isoformat(), at.date().isoformat(), source, kind,
            text('player', 64), int(event.get('bot') is True), text('ip', 64), text('agent', 512),
            path, status, size, detail, text('session', 80), max(1, int(event.get('count') or 1)),
            int(event.get('xp') or 0), int(event.get('gold') or 0), max(0, int(event.get('seconds') or 0)),
            json.dumps(allowed_meta(event.get('meta')), ensure_ascii=False)))

    def find_anomalies(self, db):
        """봇 줄(동료 봇 제외)을 10분 칸으로 보고 이상 구간을 남긴다 — 끝난 칸만, 한 번만(offsets 'anomalies' = 어디까지 했나)."""
        now = datetime.now(KST)
        # 봇은 5분마다 묶어 쓴다 — 칸이 끝나고 10분 기다려 늦게 쓰인 줄까지 담는다.
        ready = (now - timedelta(minutes=10)).replace(second=0, microsecond=0)
        ready -= timedelta(minutes=ready.minute % 10)
        row = db.execute("SELECT position FROM offsets WHERE file='anomalies'").fetchone()
        begin = (datetime.fromtimestamp(row[0], KST) if row else
                 datetime.combine(now.date() - timedelta(days=89), datetime.min.time(), KST))
        companions = self.config_names(('CompanionBots',)) if self.server_config else set()
        while begin < ready:
            end = min(ready, datetime.combine(begin.date() + timedelta(days=1), datetime.min.time(), KST))
            day = begin.date().isoformat()
            players = [row[0] for row in db.execute(
                "SELECT DISTINCT player FROM events WHERE day=? AND source='game' AND bot=1", (day,))]
            for player in players:
                if player and player.lower() not in companions:
                    self.player_anomalies(db, player, day, begin, end)
            begin = end
            db.execute("INSERT OR REPLACE INTO offsets VALUES ('anomalies',?)", (int(begin.timestamp()),))
            if players:
                break  # ponytail: 한 번에 하루치 — 밀린 날(처음 540만 줄)은 다음 수집(30초 뒤)에서 이어 간다.

    def player_anomalies(self, db, player, day, begin, end):
        # 건수는 sum(amount) — 옛 줄은 한 줄 한 건, 봇을 5분씩 묶은 뒤로는 count(2026-10-11).
        bins = db.execute('''SELECT bin, max(at||'|'||place) last, count(DISTINCT place) places,
            sum(kind IN ('login','logout')) edges, sum(kind='heartbeat') beats,
            sum(CASE WHEN kind LIKE 'request_%' THEN amount ELSE 0 END) requests,
            sum(CASE WHEN kind IN ('request_skill','request_spell') THEN amount ELSE 0 END) actions,
            sum(kind IN ('map','request_shop') OR reason LIKE '%.HandleBuy' OR reason LIKE '%.HandleSell') shop,
            sum(CASE WHEN asset='xp' AND delta>0 THEN amount ELSE 0 END) xp,
            sum(CASE WHEN asset IN ('xp','gold','item') AND delta>0 THEN amount ELSE 0 END) income
            FROM (SELECT substr(at,1,15) bin, at, kind, amount, substr(detail,1,instr(detail||' · ',' · ')-1) place,
                CASE WHEN kind='ledger' THEN json_extract(meta,'$.asset') END asset,
                CASE WHEN kind='ledger' THEN json_extract(meta,'$.delta') END delta,
                CASE WHEN kind='ledger' THEN ifnull(json_extract(meta,'$.reason'),'') ELSE '' END reason
                FROM events WHERE day=? AND source='game' AND bot=1 AND player=? AND at>=? AND at<?)
            GROUP BY bin ORDER BY bin''', (day, player, begin.isoformat(), end.isoformat()))
        step, runs = timedelta(minutes=10), {}
        for row in bins:
            start = datetime.fromisoformat(row['bin'] + '0:00+09:00')
            whole = row['edges'] == 0 and row['beats'] > 0  # 접속·종료가 낀 칸은 일부만 있었다
            hits = {'stand': row['places'] == 1 and row['xp'] == 0 and row['requests'] == 0,
                    'blind': row['actions'] >= 20 and row['xp'] == 0,
                    'income': row['shop'] == 0 and row['income'] == 0}
            for rule, hit in hits.items():
                if not (whole and hit):
                    continue
                run = runs.setdefault(rule, [])
                if run and run[-1][1] == start:
                    run[-1][1] += step
                    run[-1][3] += row['actions']
                else:
                    run.append([start, start + step, row['last'].split('|', 1)[1], row['actions']])
        for rule, run in runs.items():
            for start, finish, place, actions in run:
                # 앞 계산에서 끝난 칸에 바로 이어지면 그 구간을 늘린다(하루·수집 경계를 넘어 한 구간).
                before = db.execute('SELECT start,actions FROM anomalies WHERE player=? AND rule=? AND "end"=?',
                                    (player, rule, start.isoformat())).fetchone()
                if before:
                    start, actions = datetime.fromisoformat(before['start']), actions + before['actions']
                found = re.match(r'맵 (-?\d+) \((-?\d+),(-?\d+)\)', place)
                db.execute('INSERT OR REPLACE INTO anomalies VALUES (?,?,?,?,?,?,?,?,?,?)', (
                    player, rule, start.isoformat(), finish.isoformat(), (finish - start) // timedelta(minutes=1),
                    *(map(int, found.groups()) if found else (None, None, None)),
                    {'stand': 'request 0 · xp 0', 'blind': f'skill+spell {actions}회 · xp 0', 'income': 'xp·gold·item 0'}[rule],
                    actions))

    def config_names(self, keys):
        try:
            # 기존 cloud-server.sh의 bot_names와 같은 설정 목록. 이 설정에는 // 주석이 있다.
            text = self.server_config.read_text(encoding='utf-8-sig')
            return {str(name).lower() for key in keys
                    for match in [re.search(rf'"{key}"\s*:\s*(\[[^\]]*\])', text)] if match
                    for name in json.loads(match.group(1))}
        except (OSError, ValueError, TypeError):
            raise OSError('봇 설정을 읽지 못했습니다.')

    def report(self, start, end, player='', bots=False):
        first, last = datetime.strptime(start, '%Y-%m-%d').date(), datetime.strptime(end, '%Y-%m-%d').date()
        if not 0 <= (last - first).days < 90 or len(player) > 64:
            raise ValueError('기간은 1~90일, 캐릭터 이름은 64자 이내로 입력하세요.')
        where, args = 'day BETWEEN ? AND ?', [first.isoformat(), last.isoformat()]
        if not bots:
            where += ' AND bot=0'
        if player:
            where += " AND (source='web' OR lower(player)=lower(?))"
            args.append(player)
        with self.connect() as db:
            def rows(sql):
                result = [dict(row) for row in db.execute(sql, args)]
                for row in result:
                    if 'meta' in row:
                        row['meta'] = json.loads(row['meta'])
                return result
            daily = rows(f'''SELECT day, sum(kind='visit') visits, sum(kind='download') downloads,
                sum(CASE WHEN kind='download' THEN bytes ELSE 0 END) bytes,
                sum(kind IN ('login','legacy_login')) logins,
                count(DISTINCT CASE WHEN source='game' AND player!='' AND kind!='login_failure' THEN player END) players
                FROM events WHERE {where} GROUP BY day ORDER BY day''')
            visitors = rows(f'''SELECT ip,agent,count(*) requests,sum(kind='download') downloads,
                sum(CASE WHEN kind='download' THEN bytes ELSE 0 END) bytes,min(at) firstAt,max(at) lastAt
                FROM events WHERE {where} AND source='web' GROUP BY ip,agent ORDER BY lastAt DESC LIMIT 200''')
            players = rows(f'''SELECT player,bot,sum(kind IN ('login','legacy_login')) logins,
                sum(kind='logout') logouts, sum(kind='kill') kills, sum(kind='map') maps,
                sum(CASE WHEN kind LIKE 'request_%' THEN amount ELSE 0 END) actions,
                sum(CASE WHEN kind='state' THEN xp ELSE 0 END) xp,
                sum(CASE WHEN kind='state' THEN gold ELSE 0 END) gold,
                sum(CASE WHEN kind='logout' THEN seconds ELSE 0 END) seconds,min(at) firstAt,max(at) lastAt
                FROM events WHERE {where} AND source='game' AND player!='' AND kind!='login_failure'
                GROUP BY player,bot ORDER BY lastAt DESC LIMIT 200''')
            actions = rows(f'''SELECT kind,sum(amount) count FROM events WHERE {where} AND source='game'
                GROUP BY kind ORDER BY count DESC''')
            events = rows(f"SELECT * FROM events WHERE {where} AND source='game' ORDER BY at DESC,id DESC LIMIT 200")
            downloads = rows(f"SELECT * FROM events WHERE {where} AND kind='download' ORDER BY at DESC,id DESC LIMIT 200")
            totals = db.execute(f'''SELECT sum(kind='visit') visits,sum(kind='download') downloads,
                sum(CASE WHEN kind='download' THEN bytes ELSE 0 END) bytes,
                sum(kind IN ('login','legacy_login')) logins,
                count(DISTINCT CASE WHEN source='game' AND player!='' AND kind!='login_failure' THEN player END) players,
                count(DISTINCT CASE WHEN source='web' THEN ip END) addresses
                FROM events WHERE {where}''', args).fetchone()
            devices = rows(f'''SELECT player,meta,min(at) firstAt,max(at) lastAt,count(*) logins
                FROM events WHERE {where} AND kind='app_device'
                GROUP BY player,json_extract(meta,'$.install'),json_extract(meta,'$.platform'),
                json_extract(meta,'$.model'),json_extract(meta,'$.os'),json_extract(meta,'$.version')
                ORDER BY lastAt DESC LIMIT 200''')
            diagnostics = rows(f"SELECT * FROM events WHERE {where} AND kind IN ('login_failure','app_error') ORDER BY at DESC LIMIT 200")
            screens = rows(f'''SELECT player,json_extract(meta,'$.screen') screen,sum(amount) count,
                sum(coalesce(json_extract(meta,'$.seconds'),0)) seconds FROM events WHERE {where} AND kind='app_screen'
                GROUP BY player,screen ORDER BY count DESC LIMIT 200''')
            buttons = rows(f'''SELECT player,json_extract(meta,'$.screen') screen,json_extract(meta,'$.action') action,sum(amount) count
                FROM events WHERE {where} AND kind='app_button' GROUP BY player,screen,action ORDER BY count DESC LIMIT 200''')
            exits = rows(f'''SELECT player,coalesce(json_extract(meta,'$.screen'),json_extract(meta,'$.previousScreen')) screen,json_extract(meta,'$.action') action,
                json_extract(meta,'$.error') error,sum(amount) count FROM events WHERE {where}
                AND (kind='app_lifecycle' OR (kind='app_error' AND json_extract(meta,'$.error')='unclean_exit'))
                GROUP BY player,screen,action,error ORDER BY count DESC LIMIT 200''')
            ledger = rows(f"SELECT * FROM events WHERE {where} AND kind='ledger' ORDER BY at DESC,id DESC LIMIT 200")
            economy = rows(f'''SELECT player,json_extract(meta,'$.asset') asset,json_extract(meta,'$.item') item,
                json_extract(meta,'$.reason') reason,
                sum(max(0,coalesce(json_extract(meta,'$.delta'),0))) gained,
                sum(max(0,-coalesce(json_extract(meta,'$.delta'),0))) spent,
                sum(coalesce(json_extract(meta,'$.delta'),0)) net,
                sum(coalesce(json_extract(meta,'$.quantity'),0)) quantity,count(*) transactions
                FROM events WHERE {where} AND kind='ledger' GROUP BY player,asset,item,reason ORDER BY transactions DESC''')
            analytics = self.analytics(db, first, last, player, bots)
            # 봇만의 표라 「봇 포함」과 상관없이 보인다. 수입 끊김은 20분 이상만(10분짜리는 이어 붙이려고 남겨 둔다).
            anomalies = [dict(row) for row in db.execute(f'''SELECT player,rule,start,"end",minutes,map,x,y,detail
                FROM anomalies WHERE start>=? AND start<? AND (rule!='income' OR minutes>=20)
                {'AND lower(player)=lower(?)' if player else ''} ORDER BY start DESC LIMIT 200''',
                [first.isoformat(), (last + timedelta(days=1)).isoformat()] + ([player] if player else []))]
        return {'summary': {key: value or 0 for key,value in dict(totals).items()},
                'daily': daily, 'visitors': visitors, 'players': players, 'actions': actions,
                'events': events, 'downloads': downloads, 'collection': self.status, 'limit': 200,
                'savedCharacters': self.saved_characters(player, bots), 'devices': devices,
                'diagnostics': diagnostics, 'screens': screens, 'buttons': buttons, 'exits': exits,
                'ledger': ledger, 'economy': economy, 'anomalies': anomalies, **analytics}

    def analytics(self, db, first, last, player, bots):
        where, args = "source='game' AND player!=''", []
        if not bots:
            where += ' AND bot=0'
        if player:
            where += ' AND lower(player)=lower(?)'
            args.append(player)
        now = datetime.now(KST)
        # First observed means first in the retained log, not registration or lifetime first play.
        days = list(db.execute(f'''SELECT lower(player) player,day FROM events WHERE {where}
            AND kind IN ('login','heartbeat') GROUP BY lower(player),day ORDER BY day''', args))
        active, first_seen = {}, {}
        for row in days:
            if row['day'] > now.date().isoformat():
                continue
            active.setdefault(row['day'], set()).add(row['player'])
            first_seen.setdefault(row['player'], row['day'])
        returning = []
        cursor = first
        while cursor <= last:
            day = cursor.isoformat()
            people = active.get(day, set())
            new = sum(first_seen[who] == day for who in people)
            returning.append({'day': day, 'players': len(people), 'firstObserved': new, 'returning': len(people)-new})
            cursor += timedelta(days=1)
        yesterday = now.date()-timedelta(days=1)
        cohort = {who: datetime.strptime(day, '%Y-%m-%d').date() for who,day in first_seen.items()
                  if first.isoformat() <= day <= last.isoformat()}
        retention = []
        for period in (1, 7, 30):
            eligible = {who: day+timedelta(days=period) for who,day in cohort.items()
                        if day+timedelta(days=period) <= yesterday}
            returned = sum(who in active.get(day.isoformat(), set()) for who,day in eligible.items())
            retention.append({'days': period, 'cohort': len(cohort), 'eligible': len(eligible),
                              'returned': returned, 'rate': round(returned/len(eligible)*100, 1) if eligible else None})
        sessions = db.execute(f'''SELECT lower(player) player,session,min(at) firstAt,max(at) lastAt,
            min(CASE WHEN kind='login' THEN at END) loginAt,
            min(CASE WHEN kind='logout' THEN at END) logoutAt
            FROM events WHERE {where} AND session!='' AND kind IN ('login','heartbeat','logout')
            GROUP BY lower(player),session''', args)
        start = datetime.combine(first, datetime.min.time(), KST)
        end = datetime.combine(last+timedelta(days=1), datetime.min.time(), KST)
        buckets, current, last_observed = {}, set(), None
        for row in sessions:
            observed = datetime.fromisoformat(row['lastAt'])
            last_observed = max(last_observed, observed) if last_observed else observed
            joined = datetime.fromisoformat(row['loginAt'] or row['firstAt'])
            left = datetime.fromisoformat(row['logoutAt']) if row['logoutAt'] else min(now, observed+timedelta(seconds=90))
            if not row['logoutAt'] and joined <= now and (now-observed).total_seconds() <= 90:
                current.add(row['player'])
            begin, finish = max(start, joined), min(end, left, now)
            hour = begin.replace(minute=0, second=0, microsecond=0)
            while hour < finish:
                next_hour = hour+timedelta(hours=1)
                buckets.setdefault(hour, []).extend([(max(begin,hour), 1, row['player']), (min(finish,next_hour), -1, row['player'])])
                hour = next_hour
        concurrency = []
        hour = start
        while hour < min(end,now):
            counts, peak = {}, 0
            # Ends before starts at an equal timestamp: intervals are half-open, duplicate accounts count once.
            for _, change, who in sorted(buckets.get(hour, [])):
                counts[who] = counts.get(who, 0)+change
                if counts[who] == 0:
                    del counts[who]
                peak = max(peak,len(counts))
            concurrency.append({'hour': hour.isoformat(), 'peak': peak})
            hour += timedelta(hours=1)
        return {'analyticsSince': min(first_seen.values(), default=None), 'returning': returning,
                'retention': retention, 'concurrency': concurrency,
                'current': {'players': len(current), 'lastObserved': last_observed.isoformat() if last_observed else None,
                            'stale': last_observed is None or (now-last_observed).total_seconds()>90}}

    def saved_characters(self, player, bots):
        if self.characters is None:
            return []
        if not self.characters.is_dir():
            raise OSError('캐릭터 기록 폴더를 읽지 못했습니다.')
        if not self.server_config:
            # 설정이 있으면 설정 목록만 쓴다 — 봇 줄 수백만을 헛되이 훑지 않게 설정이 없을 때만 읽는다.
            with self.connect() as db:
                bot_names = {row[0].lower() for row in db.execute("SELECT DISTINCT player FROM events WHERE bot=1")}
        else:
            bot_names = self.config_names(('CompanionBots', 'EcoBots'))  # 동료 봇과 생태계 봇(2026-10-06~)
        result = []
        for path in sorted(self.characters.glob('*.json')):
            try:
                if path.stat().st_size > 4 * 1024 * 1024:
                    continue
                who = json.loads(path.read_text(encoding='utf-8-sig'))
                name = str(who.get('Username') or '')[:64]
                is_bot = name.lower() in bot_names
                if not name or (player and name.lower() != player.lower()) or (is_bot and not bots):
                    continue
                counters = who.get('MonsterKillCounters') or {}
                kills = [{'monster': str(name)[:200], 'count': int(record.get('TotalKills') or 0),
                          'lastAt': str(record.get('TimeKilled') or '')[:64]}
                         for name,record in counters.items() if isinstance(record, dict)]
                result.append({'player': name, 'bot': is_bot, 'level': int(who.get('ExpLevel') or 0),
                    'xp': int(who.get('ExpTotal') or 0), 'gold': int(who.get('GoldPoints') or 0),
                    'map': int(who.get('CurrentMapId') or 0), 'lastLogout': str(who.get('LastLogged') or '')[:64],
                    'kills': sum(row['count'] for row in kills), 'monsters': sorted(kills, key=lambda row: -row['count'])})
            except (OSError, ValueError, TypeError, AttributeError):
                raise OSError('캐릭터 누적 기록을 읽지 못했습니다.')
        return result

    def start(self):
        def worker():
            while True:
                try:
                    self.collect()
                except (OSError, sqlite3.Error):
                    self.status['errors'] = ['기록 수집에 실패했습니다. 운영 로그를 확인하세요.']
                time.sleep(30)
        threading.Thread(target=worker, daemon=True).start()
