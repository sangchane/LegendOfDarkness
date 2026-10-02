const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const generator = require('../scripts/generate-dashboard-snapshot.js');

const nextFixture = `
<!-- NEXT-ACTION:START -->
- **[지금/화면]** **전투·인벤토리를 Godot에 연결** 배치안을 검증한다
- **[다음/서버]** **영역 잠금을 줄인다** 여러 사람의 충돌을 방지한다
- **[대기/Mac]** 같은 커밋을 실제 기기에서 확인한다
<!-- NEXT-ACTION:END -->
`;

test('NEXT-ACTION에서 현재·다음·대기 작업만 구조화한다', () => {
  const roadmap = generator.parseNextAction(nextFixture);
  assert.deepEqual(roadmap.current, [{ area: '화면', title: '전투·인벤토리를 Godot에 연결', detail: '배치안을 검증한다' }]);
  assert.equal(roadmap.next[0].area, '서버');
  assert.equal(roadmap.deferred[0].area, 'Mac');
});

test('지금 NEXT.md 꼴(**[현재/때] 제목** 설명)도 현재 작업으로 읽는다', () => {
  const roadmap = generator.parseNextAction('<!-- NEXT-ACTION:START -->\n- **[현재/2026-10-02 밤] 손맛 고치기 — 설치 끝.** 워프 칸 이름표\n<!-- NEXT-ACTION:END -->');
  assert.deepEqual(roadmap.current, [{ area: '2026-10-02 밤', title: '손맛 고치기 — 설치 끝.', detail: '워프 칸 이름표' }]);
});

test('현재 작업이 없거나 마커가 없으면 실패한다', () => {
  assert.throws(() => generator.parseNextAction('no markers'), /NEXT-ACTION markers/);
  assert.throws(() => generator.parseNextAction('<!-- NEXT-ACTION:START -->\n- **[다음/화면]** 다음\n<!-- NEXT-ACTION:END -->'), /current task/);
});

test('스냅샷은 입력을 바꾸지 않는다', () => {
  const roadmap = generator.parseNextAction(nextFixture);
  const snapshot = generator.buildSnapshot({
    roadmap,
    verification: { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null },
    generatedAt: '2026-09-11T00:00:00.000Z',
  });
  assert.equal(snapshot.schemaVersion, 2);
  assert.ok(Object.isFrozen(snapshot.roadmap.current[0]));
  assert.equal(Object.isFrozen(roadmap.current[0]), true);   // parseNextAction 이 이미 얼려 둔다
  assert.equal(snapshot.git, undefined);
  assert.equal(snapshot.graphify, undefined);
});

test('대시보드 모델은 불완전한 스냅샷을 거부한다', () => {
  const model = require('../docs/dashboard-model.js');
  const complete = generator.buildSnapshot({
    roadmap: generator.parseNextAction(nextFixture),
    verification: { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null },
    generatedAt: '2026-09-11T00:00:00.000Z',
  });
  assert.equal(model.isValidDashboardSnapshot(complete), true);
  assert.equal(model.isValidDashboardSnapshot({ ...complete, roadmap: { current: [] } }), false);
  assert.equal(model.isValidDashboardSnapshot({ ...complete, verification: undefined }), false);
});

test('직렬화는 script 종료 문자를 실행 가능한 마크업으로 만들지 않는다', () => {
  const snapshot = generator.buildSnapshot({
    roadmap: { current: [{ area: '화면', title: '</script><script>bad()</script>', detail: '' }], next: [], deferred: [] },
    verification: { status: 'unknown' },
    generatedAt: '2026-09-11T00:00:00.000Z',
  });
  const source = generator.serializeSnapshot(snapshot);
  const context = { globalThis: {} };
  assert.doesNotMatch(source, /<\/script>/i);
  vm.runInNewContext(source, context);
  assert.equal(context.globalThis.LOD_DASHBOARD_SNAPSHOT.roadmap.current[0].title, '</script><script>bad()</script>');
});

test('CLI 옵션과 원자 저장 실패를 안전하게 처리한다', () => {
  assert.throws(() => generator.parseArguments(['--output', '--verify'], root), /--output/);
  assert.throws(() => generator.parseArguments(['--generated-at', 'not-a-date'], root), /--generated-at/);
  assert.deepEqual(generator.VERIFICATION_FILES, ['tests/dashboard-snapshot.test.js', 'tests/docs-dashboard.test.js', 'tests/operations-docs.test.js']);
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'lod-dashboard-snapshot-'));
  const output = path.join(directory, 'snapshot.js');
  fs.writeFileSync(output, 'previous');
  assert.throws(() => generator.writeSnapshotAtomic(output, { unsupported: 1n }), /serialize/i);
  assert.equal(fs.readFileSync(output, 'utf8'), 'previous');
});

test('index는 스냅샷을 먼저 읽고 안전하게 렌더링한다', () => {
  const html = fs.readFileSync(path.join(root, 'docs', 'index.html'), 'utf8');
  const script = fs.readFileSync(path.join(root, 'docs', 'dashboard.js'), 'utf8');
  assert.ok(html.indexOf('<script src="dashboard-snapshot.js"></script>') < html.indexOf('<script src="dashboard.js"></script>'));
  for (const id of ['snapshot-current-title', 'snapshot-verification-title']) {
    assert.match(html, new RegExp(`id="${id}"`));
  }
  assert.match(script, /isValidDashboardSnapshot/);
  assert.doesNotMatch(script, /innerHTML\s*=/);
  assert.doesNotMatch(html + script, /Graphite|gt\.ps1/i);
});
