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
            config=root/'config.json';config.write_text('// comment\n{"ServerConfig":{"CompanionBots":["HELPER"]}}')
            store=module.ActivityStore(root/'activity.sqlite',characters=str(characters),server_config=str(config))
            self.assertEqual(store.report('2026-10-02','2026-10-03')['savedCharacters'],[])
            self.assertTrue(store.report('2026-10-02','2026-10-03',bots=True)['savedCharacters'][0]['bot'])

if __name__ == '__main__':unittest.main()
