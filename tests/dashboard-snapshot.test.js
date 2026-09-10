const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const generator = require('../scripts/generate-dashboard-snapshot.js');

const nextFixture = `
- outside marker
<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일

- **[지금/화면]** **전투·인벤토리를 Godot에 올린다.** 배치는 시안에 있다
- **[다음/서버]** **전역 잠금을 줄인다.** 여러 사람이 붙은 뒤 결정한다
- **[대기/Mac]** 같은 커밋을 실기기에서 확인한다
<!-- NEXT-ACTION:END -->
- **[다음/무시]** 마커 밖 항목
`;

test('NEXT-ACTION 안의 현재·다음·대기 작업만 구조화한다', () => {
  const roadmap = generator.parseNextAction(nextFixture);

  assert.deepEqual(roadmap.current, [{ area: '화면', title: '전투·인벤토리를 Godot에 올린다.', detail: '배치는 시안에 있다' }]);
  assert.deepEqual(roadmap.next, [{ area: '서버', title: '전역 잠금을 줄인다.', detail: '여러 사람이 붙은 뒤 결정한다' }]);
  assert.deepEqual(roadmap.deferred, [{ area: 'Mac', title: '같은 커밋을 실기기에서 확인한다', detail: '' }]);
  assert.equal(JSON.stringify(roadmap).includes('마커 밖'), false);
});

test('NEXT-ACTION 마커나 현재 작업이 없으면 조용히 빈 상태를 만들지 않는다', () => {
  assert.throws(() => generator.parseNextAction('no markers'), /NEXT-ACTION markers/);
  assert.throws(
    () => generator.parseNextAction('<!-- NEXT-ACTION:START -->\n- **[다음/화면]** 다음\n<!-- NEXT-ACTION:END -->'),
    /current task/,
  );
});

test('스냅샷은 입력을 바꾸지 않고 깊게 동결한다', () => {
  const roadmap = generator.parseNextAction(nextFixture);
  const git = { branch: 'feature/test', sha: 'abc1234', subject: 'safe', dirty: true };
  const snapshot = generator.buildSnapshot({
    roadmap,
    git,
    graphite: { status: 'available', stack: ['feature/test', 'main'] },
    verification: { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null },
    generatedAt: '2026-09-10T00:00:00.000Z',
  });

  assert.equal(snapshot.schemaVersion, 1);
  assert.equal(snapshot.git.branch, 'feature/test');
  assert.equal(snapshot.verification.status, 'unknown');
  assert.ok(Object.isFrozen(snapshot));
  assert.ok(Object.isFrozen(snapshot.roadmap.current));
  assert.deepEqual(git, { branch: 'feature/test', sha: 'abc1234', subject: 'safe', dirty: true });
});

test('브라우저 스크립트 출력은 script 종료 문자열을 실행 가능한 데이터로 만들지 않는다', () => {
  const snapshot = generator.buildSnapshot({
    roadmap: { current: [{ area: '화면', title: '</script><script>bad()</script>', detail: '' }], next: [], deferred: [] },
    git: { branch: 'main', sha: 'abc1234', subject: 'safe', dirty: false },
    graphite: { status: 'unknown', stack: [] },
    verification: { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null },
    generatedAt: '2026-09-10T00:00:00.000Z',
  });
  const source = generator.serializeSnapshot(snapshot);
  const context = { globalThis: {} };

  assert.doesNotMatch(source, /<\/script>/i);
  vm.runInNewContext(source, context);
  assert.equal(context.globalThis.LOD_DASHBOARD_SNAPSHOT.roadmap.current[0].title, '</script><script>bad()</script>');
});

test('원자 저장은 직렬화 실패 시 기존 스냅샷을 보존한다', () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'lod-dashboard-snapshot-'));
  const output = path.join(directory, 'snapshot.js');
  fs.writeFileSync(output, 'previous', 'utf8');

  assert.throws(() => generator.writeSnapshotAtomic(output, { unsupported: 1n }), /serialize/i);
  assert.equal(fs.readFileSync(output, 'utf8'), 'previous');
});

test('대시보드는 스냅샷을 fallback보다 먼저 읽고 안전한 DOM API로 반영한다', () => {
  const html = fs.readFileSync(path.join(root, 'docs', 'index.html'), 'utf8');
  const script = fs.readFileSync(path.join(root, 'docs', 'dashboard.js'), 'utf8');
  const snapshotIndex = html.indexOf('<script src="dashboard-snapshot.js"></script>');
  const applicationIndex = html.indexOf('<script src="dashboard.js"></script>');

  assert.ok(snapshotIndex > -1 && snapshotIndex < applicationIndex);
  for (const id of [
    'snapshot-current-title',
    'snapshot-current-detail',
    'snapshot-verification-title',
    'snapshot-branch',
    'snapshot-generated-date',
    'snapshot-branch-detail',
    'snapshot-branch-state',
  ]) {
    assert.match(html, new RegExp(`id="${id}"`));
  }
  assert.match(script, /schemaVersion\s*!==\s*1/);
  assert.match(script, /LOD_DASHBOARD_SNAPSHOT/);
  assert.doesNotMatch(script, /innerHTML\s*=/);
  assert.match(html, /data-copy-command="node scripts\/generate-dashboard-snapshot\.js --verify"/);
});
