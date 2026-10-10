import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from datetime import datetime, timezone

spec = importlib.util.spec_from_file_location('activity_store', Path(__file__).resolve().parents[1] / 'scripts/ops/activity_store.py')
module = importlib.util.module_from_spec(spec) if spec else None
if spec and spec.loader and Path(spec.origin).exists():
    spec.loader.exec_module(module)

class ActivityTests(unittest.TestCase):
    def test_expansion_preserves_old_database_and_computes_observed_analytics(self):
        import sqlite3
        from datetime import timedelta
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            database = root/'activity.sqlite'
            store = module.ActivityStore(database)
            with store.connect() as db:
                # Exercise an existing deployment's schema, including rows inserted before the upgrade.
                db.execute('ALTER TABLE events DROP COLUMN meta')
            store = module.ActivityStore(database)
            base = datetime.now(module.KST).replace(hour=0,minute=0,second=0,microsecond=0)-timedelta(days=10)
            def add(db, kind, who, minute, session='', meta=None, **extra):
                event = dict(id=f'{kind}-{who}-{minute}',at=(base+timedelta(minutes=minute)).isoformat(),kind=kind,player=who,session=session,meta=meta or {},**extra)
                store.insert(db,'game',event)
            with store.connect() as db:
                add(db,'login','One',0,'one')
                add(db,'login','Two',10,'two')
                add(db,'logout','One',20,'one',seconds=1200)
                add(db,'logout','Two',30,'two',seconds=1200)
                add(db,'login','One',1440,'next')
                add(db,'heartbeat','One',1441,'next',seconds=60)
                add(db,'login','helper',12,'bot',bot=True)
                add(db,'login_failure','never-created',7,meta={'reason':'account'})
                add(db,'app_device','One',1,'one',meta={'install':'abcdef','platform':'iOS','model':'iPhone16,1','os':'18.0','version':'42','password':'SECRET','reported':True})
                add(db,'app_button','One',2,'one',count=3,meta={'action':'inventory','screen':'game'})
                add(db,'app_error','One',3,'one',meta={'error':'unclean_exit','screen':'game'})
                add(db,'ledger','One',4,'one',meta={'asset':'gold','delta':50,'balance':100,'reason':'loot','transaction':'tx'})
                add(db,'state','One',5,'one',gold=50)
                add(db,'ledger','One',6,'one',meta={'asset':'item','item':'물약','delta':0,'quantity':2,'from':'inventory','to':'equipment','reason':'equip'})
            day=base.date().isoformat(); end=(base+timedelta(days=1)).date().isoformat()
            report=store.report(day,end)
            self.assertEqual(report['concurrency'][0]['peak'],2)
            self.assertEqual(report['current']['players'],0) # old unclosed sessions time out
            self.assertEqual(report['retention'][0]['eligible'],2)
            self.assertEqual(report['retention'][0]['returned'],1)
            self.assertEqual(report['retention'][0]['rate'],50)
            self.assertIsNone(next(r for r in report['retention'] if r['days']==30)['rate'])
            self.assertEqual(report['returning'][1]['returning'],1)
            self.assertEqual(report['players'][0]['gold'],50) # ledger does not double state delta
            self.assertEqual(report['economy'][0]['gained'],50)
            self.assertEqual(report['buttons'][0]['count'],3)
            self.assertEqual(report['summary']['players'],2)
            self.assertEqual(report['daily'][0]['players'],2)
            self.assertNotIn('never-created',[row['player'] for row in report['players']])
            failure = next(row for row in report['diagnostics'] if row['kind']=='login_failure')
            self.assertEqual(failure['meta']['reason'],'account')
            self.assertEqual(report['devices'][0]['meta']['model'],'iPhone16,1')
            self.assertNotIn('SECRET',json.dumps(report))
            self.assertEqual(store.report(day,end,player='two')['buttons'],[])
            self.assertGreater(store.report(day,end,bots=True)['concurrency'][0]['peak'],2)

    def test_incremental_collection_filters_head_bots_and_keeps_partial_line(self):
        self.assertTrue(hasattr(module, 'ActivityStore'), '활동 저장 기능이 필요합니다')
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            web, game = root/'web.jsonl', root/'game.jsonl'
            now = datetime.now(timezone.utc).isoformat()
            rows = [dict(id=str(i), at=now, ip='1.2.3.4', path='/download/LodClient.ipa?secret=omit', method=method, status=200, bytes=size, agent='<script>') for i,method,size in [(1,'HEAD',0),(2,'GET',120),(3,'GET',40)]]
            web.write_text(''.join(json.dumps(r)+'\n' for r in rows)+json.dumps(dict(rows[0],id='4',method='GET',bytes=20)))
            game.write_text(json.dumps(dict(id='g1',at=now,kind='login',player='Monk5',bot=False,session='s'))+'\n'+json.dumps(dict(id='g2',at=now,kind='login',player='bot',bot=True))+'\n')
            store = module.ActivityStore(root/'activity.sqlite', str(web), str(game))
            store.collect(); store.collect()
            day = datetime.now().strftime('%Y-%m-%d')
            report = store.report(day,day)
            self.assertEqual(report['summary']['downloads'],2)
            self.assertEqual(report['summary']['bytes'],160)
            self.assertEqual(report['summary']['logins'],1)
            self.assertNotIn('secret',json.dumps(report))
            self.assertEqual(store.report(day,day,bots=True)['summary']['logins'],2)
            self.assertEqual(store.report(day,day,player='nope')['summary']['logins'],0)
            with web.open('a') as f:f.write('\n')
            store.collect()
            self.assertEqual(store.report(day,day)['summary']['downloads'],3)
            web.rename(root/'web.jsonl.1');web.write_text(json.dumps(dict(rows[0],id='5',method='GET',bytes=10))+'\n')
            store.collect()
            self.assertEqual(store.report(day,day)['summary']['downloads'],4)
            with self.assertRaises(ValueError):store.report('invalid',day)

    def test_saved_character_stats_expose_only_allowlisted_cumulative_fields(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            characters = root/'characters';characters.mkdir()
            (characters/'one.json').write_text(json.dumps(dict(Username='Monk5',Password='never expose',ExpLevel=47,ExpTotal=21000,GoldPoints=500,CurrentMapId=20304,MonsterKillCounters={'벌':{'TotalKills':8,'TimeKilled':'2026-09-27'}})))
            store = module.ActivityStore(root/'activity.sqlite', characters=str(characters))
            report = store.report('2026-10-02','2026-10-03')
            self.assertEqual(report['savedCharacters'][0]['kills'],8)
            self.assertNotIn('never expose',json.dumps(report))
            self.assertEqual(store.report('2026-10-02','2026-10-03',player='another')['savedCharacters'],[])

    def test_saved_bot_without_activity_is_filtered_from_server_configuration(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);characters=root/'characters';characters.mkdir()
            (characters/'one.json').write_text(json.dumps({'Username':'helper'}))
            # 생태계 봇(EcoBots, 2026-10-06~)도 봇이다 — 빠지면 저장된 캐릭터에 봇 40명이 사람으로 섞였다.
            (characters/'two.json').write_text(json.dumps({'Username':'전사봇1'}))
            config=root/'config.json';config.write_text('// comment\n{"ServerConfig":{"CompanionBots":["HELPER"],\n"EcoBots": [ "전사봇1" ]}}')
            store=module.ActivityStore(root/'activity.sqlite',characters=str(characters),server_config=str(config))
            self.assertEqual(store.report('2026-10-02','2026-10-03')['savedCharacters'],[])
            self.assertEqual([row['bot'] for row in store.report('2026-10-02','2026-10-03',bots=True)['savedCharacters']],[True,True])

    def test_people_only_report_skips_bot_rows_by_index(self):
        # 생태 봇이 하루 100만 줄 넘게 남기면서(2026-10-07~) 사람만 보는 조회가 30초를 넘어 nginx 60초에 끊겼다(504).
        import sqlite3
        from contextlib import contextmanager
        plans=[]
        class Explained(sqlite3.Connection):
            def execute(self,sql,args=()):
                if 'FROM events' in sql:
                    plans.append(' / '.join(row[3] for row in super().execute('EXPLAIN QUERY PLAN '+sql,args)))
                return super().execute(sql,args)
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);characters=root/'characters';characters.mkdir()
            store=module.ActivityStore(root/'activity.sqlite',characters=str(characters))
            @contextmanager
            def connect():
                db=sqlite3.connect(store.database,factory=Explained);db.row_factory=sqlite3.Row
                try:
                    with db:yield db
                finally:db.close()
            store.connect=connect
            store.report('2026-10-10','2026-10-11')
        self.assertTrue(plans)
        for plan in plans:
            self.assertIn('event_bot',plan,plan)

    def test_bot_anomalies_from_old_and_new_rows_once(self):
        # SC-3 — 옛 꼴(한 줄 한 건)과 새 꼴(5분 묶음 count) 모두, 동료 봇·사람·상점·맵 이동은 빼고, 다시 돌려도 한 번만.
        from datetime import timedelta
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);config=root/'config.json'
            config.write_text('// 주석\n{"CompanionBots":["helper"],"EcoBots":["Stander"]}')
            store=module.ActivityStore(root/'activity.sqlite',server_config=str(config))
            day=datetime.now(module.KST).replace(hour=0,minute=0,second=0,microsecond=0)-timedelta(days=2)
            serial=iter(range(10**6))
            def add(db,who,minute,kind,place=(1,5,5),bot=True,**extra):
                store.insert(db,'game',dict(id=str(next(serial)),at=(day+timedelta(minutes=minute)).isoformat(),kind=kind,
                    player=who,bot=bot,detail='맵 %d (%d,%d) · 레벨 1'%place,**extra))
            def stand(db,who,first,last,bot=True):
                add(db,who,first,'login',bot=bot)
                for minute in range(first+1,last):
                    add(db,who,minute,'heartbeat',bot=bot);add(db,who,minute,'state',bot=bot)
                add(db,who,last,'logout',bot=bot)
            ledger=lambda asset,delta,count=1,reason='Monsterexp.HandleExp':dict(count=count,meta={'asset':asset,'delta':delta,'reason':reason})
            with store.connect() as db:
                stand(db,'Stander',595,635)   # 옛 꼴: 10:00~10:30 가만히(접속·종료가 낀 칸은 빼고)
                stand(db,'Helper',595,635)    # 동료 봇
                stand(db,'Person',595,635,bot=False)
                stand(db,'Night',1415,1475)   # 23:40~다음 날 00:30 — 하루·수집 경계를 넘어 한 구간
                # 새 꼴: 5분마다 묶음. 11:00~11:20 기술·마법 25번씩인데 경험치 0, 자리는 움직인다.
                add(db,'Blinder',655,'login')
                for minute in (662,667,672,677):
                    add(db,'Blinder',minute,'heartbeat',place=(2,minute,1))
                for minute in (662,672):
                    add(db,'Blinder',minute,'request_skill',place=(2,minute,2),count=15)
                    add(db,'Blinder',minute,'request_spell',place=(2,minute,3),count=10)
                add(db,'Blinder',685,'logout')
                # 정상 사냥 두 칸 뒤 10분만 수입 0 — 20분이 안 돼 보이지 않는다.
                add(db,'Hunter',705,'login')
                for minute in (712,717,722,727,732,737):
                    add(db,'Hunter',minute,'heartbeat',place=(3,minute,1))
                for minute in (712,722):
                    add(db,'Hunter',minute,'request_skill',place=(3,minute,2),count=30)
                    add(db,'Hunter',minute,'ledger',place=(3,minute,3),**ledger('xp',400,40))
                add(db,'Hunter',732,'request_item',place=(3,732,2),count=3)
                add(db,'Hunter',745,'logout')
                # 수입 0 이 40분이지만 가운데 상점(장부 이유)·맵 이동 칸이 끊는다.
                add(db,'Shopper',795,'login')
                for minute in range(796,840):
                    add(db,'Shopper',minute,'heartbeat',place=(4,minute,1))
                add(db,'Shopper',812,'ledger',**ledger('gold',-50,reason='GameServer.HandleBuy'))
                add(db,'Shopper',825,'map')
                add(db,'Shopper',845,'logout')
            def found():
                with store.connect() as db:
                    return [tuple(row) for row in db.execute('SELECT * FROM anomalies ORDER BY player,rule')]
            for _ in range(3):
                store.collect()
            first=found()
            for _ in range(2):
                store.collect()
            self.assertEqual(found(),first)
            self.assertEqual(store.status['errors'],[])
            start=lambda minute:(day+timedelta(minutes=minute)).isoformat()
            report=store.report(day.date().isoformat(),(day+timedelta(days=1)).date().isoformat())
            got={(r['player'],r['rule']):(r['start'],r['minutes'],r['detail']) for r in report['anomalies']}
            self.assertEqual(got,{
                ('Stander','stand'):(start(600),30,'request 0 · xp 0'),('Stander','income'):(start(600),30,'xp·gold·item 0'),
                ('Night','stand'):(start(1420),50,'request 0 · xp 0'),('Night','income'):(start(1420),50,'xp·gold·item 0'),
                ('Blinder','blind'):(start(660),20,'skill+spell 50회 · xp 0'),('Blinder','income'):(start(660),20,'xp·gold·item 0')})
            row=next(r for r in report['anomalies'] if r['player']=='Stander')
            self.assertEqual((row['map'],row['x'],row['y'],row['end']),(1,5,5,start(630)))
            self.assertEqual({r['player'] for r in store.report(day.date().isoformat(),day.date().isoformat(),player='blinder')['anomalies']},{'Blinder'})

    def test_bot_rows_wait_for_anomalies_then_seven_days_and_raw_files_after_ml_copy(self):
        # SC-4 — 봇 줄은 이상 구간을 남긴 뒤 7일, 사람 줄은 89일. 원본은 머신러닝 사본이 있고 다 읽은 3일 지난 날만.
        from datetime import timedelta
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);game=root/'game';ml=root/'ml';game.mkdir();ml.mkdir()
            today=datetime.now(module.KST).replace(hour=0,minute=0,second=0,microsecond=0)
            name=lambda days:(today-timedelta(days=days)).date().isoformat()
            line=lambda days:json.dumps(dict(id='raw'+str(days),at=(today-timedelta(days=days)).isoformat(),kind='login',player='Person'))+'\n'
            for days in (2,4,5,6,100):
                (game/f'{name(days)}.jsonl').write_text(line(days))
            with (game/f'{name(6)}.jsonl').open('a') as raw:raw.write('{"id":"half')  # 아직 다 안 읽은 날
            for days in (2,4,6):
                (ml/f'{name(days)}.jsonl.gz').write_bytes(b'')
            config=root/'config.json'
            store=module.ActivityStore(root/'activity.sqlite',game=str(game/'*.jsonl'),server_config=str(config),ml_activity=str(ml))
            old=today-timedelta(days=8)
            with store.connect() as db:
                store.insert(db,'game',dict(id='login',at=(old+timedelta(minutes=595)).isoformat(),kind='login',player='Eco1',bot=True,detail='맵 7 (1,1)'))
                for minute in range(596,640):
                    store.insert(db,'game',dict(id=str(minute),at=(old+timedelta(minutes=minute)).isoformat(),kind='heartbeat',player='Eco1',bot=True,detail='맵 7 (1,1)'))
                store.insert(db,'game',dict(id='person',at=(old+timedelta(minutes=600)).isoformat(),kind='login',player='Person'))
                store.insert(db,'game',dict(id='recent',at=(today-timedelta(days=6,hours=-1)).isoformat(),kind='heartbeat',player='Eco1',bot=True))
            def rows(day):
                with store.connect() as db:
                    return [tuple(row) for row in db.execute('SELECT player,bot FROM events WHERE day=? GROUP BY player,bot',(day,))]
            store.collect()  # 설정을 못 읽어 이상 구간을 못 셌다 — 봇 줄도 그대로
            self.assertIn('이상 구간: 봇 설정을 읽지 못했습니다.',store.status['errors'])
            self.assertEqual(sorted(rows(name(8))),[('Eco1',1),('Person',0)])
            self.assertEqual(sorted(path.name for path in game.iterdir()),[f'{name(days)}.jsonl' for days in (6,5,2)])
            config.write_text('{"CompanionBots":[]}')
            for _ in range(3):
                store.collect()
            self.assertEqual(rows(name(8)),[('Person',0)])
            self.assertIn(('Eco1',1),rows(name(6)))
            self.assertEqual(sorted((r['player'],r['rule'],r['minutes']) for r in store.report(name(8),name(8))['anomalies']),
                             [('Eco1','income',40),('Eco1','stand',40)])

    def test_collection_reads_events_only_by_index(self):
        # 봇 줄 540만(2026-10-11) — 이상 구간·보관 쿼리가 표를 통째로 훑으면 30초 수집이 밀린다.
        import sqlite3
        from contextlib import contextmanager
        from datetime import timedelta
        plans=[]
        class Explained(sqlite3.Connection):
            def execute(self,sql,args=()):
                if 'FROM events' in sql:
                    plans.append(' / '.join(row[3] for row in super().execute('EXPLAIN QUERY PLAN '+sql,args)))
                return super().execute(sql,args)
        with tempfile.TemporaryDirectory() as folder:
            store=module.ActivityStore(Path(folder)/'activity.sqlite')
            with store.connect() as db:
                store.insert(db,'game',dict(id='1',at=(datetime.now(module.KST)-timedelta(days=9)).isoformat(),kind='heartbeat',player='Eco1',bot=True))
            @contextmanager
            def connect():
                db=sqlite3.connect(store.database,factory=Explained);db.row_factory=sqlite3.Row
                try:
                    with db:yield db
                finally:db.close()
            store.connect=connect
            store.collect();store.collect()
        self.assertGreaterEqual(len(plans),5)
        for plan in plans:
            self.assertIn('event_day',plan,plan)

if __name__ == '__main__':unittest.main()
