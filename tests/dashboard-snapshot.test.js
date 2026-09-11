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

test('현재 작업이 없거나 마커가 없으면 실패한다', () => {
  assert.throws(() => generator.parseNextAction('no markers'), /NEXT-ACTION markers/);
  assert.throws(() => generator.parseNextAction('<!-- NEXT-ACTION:START -->\n- **[다음/화면]** 다음\n<!-- NEXT-ACTION:END -->'), /current task/);
});

test('Graphify 결과와 Obsidian vault의 실제 규모를 읽는다', () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'lod-graphify-'));
  const output = path.join(directory, 'graphify-out');
  fs.mkdirSync(path.join(output, 'obsidian', 'nested'), { recursive: true });
  fs.writeFileSync(path.join(output, 'graph.json'), JSON.stringify({
    nodes: [{ id: 'a', community: 1 }, { id: 'b', community: 2 }, { id: 'c', community: 2 }],
    links: [{ source: 'a', target: 'b' }, { source: 'b', target: 'c' }],
  }));
  fs.writeFileSync(path.join(output, 'obsidian', 'a.md'), '# A');
  fs.writeFileSync(path.join(output, 'obsidian', 'nested', 'b.md'), '# B');
  fs.writeFileSync(path.join(output, 'obsidian', 'graph.canvas'), '{}');

  assert.deepEqual(generator.collectGraphify(directory), {
    status: 'available', nodes: 3, links: 2, communities: 2,
    graphHtml: false, report: false,
    obsidian: { status: 'available', notes: 2, canvas: true },
  });
});

test('Graphify 결과가 없거나 손상되면 허위 숫자를 만들지 않는다', () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'lod-graphify-missing-'));
  assert.deepEqual(generator.collectGraphify(directory), {
    status: 'unknown', nodes: null, links: null, communities: null,
    graphHtml: false, report: false,
    obsidian: { status: 'unknown', notes: null, canvas: false },
  });
  fs.mkdirSync(path.join(directory, 'graphify-out'));
  fs.writeFileSync(path.join(directory, 'graphify-out', 'graph.json'), '{broken');
  assert.equal(generator.collectGraphify(directory).status, 'unknown');
});

test('스냅샷은 입력을 바꾸지 않고 Graphify 상태와 알 수 없는 Git 상태를 보존한다', () => {
  const graphify = { status: 'available', nodes: 3, links: 2, communities: 2, graphHtml: true, report: true, obsidian: { status: 'available', notes: 4, canvas: true } };
  const snapshot = generator.buildSnapshot({
    roadmap: generator.parseNextAction(nextFixture),
    git: { branch: 'feature/test', sha: 'abc1234', subject: 'safe', dirty: null },
    graphify,
    verification: { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null },
    generatedAt: '2026-09-11T00:00:00.000Z',
  });
  assert.equal(snapshot.schemaVersion, 2);
  assert.equal(snapshot.git.dirty, null);
  assert.equal(snapshot.graphify.nodes, 3);
  assert.equal(snapshot.graphify.obsidian.notes, 4);
  assert.ok(Object.isFrozen(snapshot.graphify.obsidian));
  assert.equal(graphify.nodes, 3);
});

test('대시보드 모델은 불완전한 스냅샷을 거부한다', () => {
  const model = require('../docs/dashboard-model.js');
  const complete = generator.buildSnapshot({
    roadmap: generator.parseNextAction(nextFixture),
    git: { branch: 'main', sha: 'abc1234', subject: 'safe', dirty: null },
    graphify: { status: 'unknown', nodes: null, links: null, communities: null, graphHtml: false, report: false, obsidian: { status: 'unknown', notes: null, canvas: false } },
    verification: { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null },
    generatedAt: '2026-09-11T00:00:00.000Z',
  });
  assert.equal(model.isValidDashboardSnapshot(complete), true);
  assert.equal(model.isValidDashboardSnapshot({ ...complete, graphify: undefined }), false);
  assert.equal(model.isValidDashboardSnapshot({ ...complete, verification: undefined }), false);
  assert.equal(model.isValidDashboardSnapshot({ ...complete, git: { ...complete.git, dirty: 'clean' } }), false);
});

test('직렬화는 script 종료 문자를 실행 가능한 마크업으로 만들지 않는다', () => {
  const snapshot = generator.buildSnapshot({
    roadmap: { current: [{ area: '화면', title: '</script><script>bad()</script>', detail: '' }], next: [], deferred: [] },
    git: { branch: 'main', sha: 'abc1234', subject: 'safe', dirty: false },
    graphify: { status: 'unknown', obsidian: {} }, verification: { status: 'unknown' },
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

test('index는 Graphify와 Obsidian을 스냅샷 우선으로 안전하게 렌더링한다', () => {
  const html = fs.readFileSync(path.join(root, 'docs', 'index.html'), 'utf8');
  const script = fs.readFileSync(path.join(root, 'docs', 'dashboard.js'), 'utf8');
  assert.ok(html.indexOf('<script src="dashboard-snapshot.js"></script>') < html.indexOf('<script src="dashboard.js"></script>'));
  for (const id of ['snapshot-current-title', 'snapshot-verification-title', 'snapshot-branch', 'snapshot-generated-date', 'snapshot-graphify-nodes', 'snapshot-graphify-links', 'snapshot-graphify-communities', 'snapshot-obsidian-notes']) {
    assert.match(html, new RegExp(`id="${id}"`));
  }
  assert.match(html, /id="graphify-frame"[^>]+graphify-out\/graph\.html[^>]+sandbox="allow-scripts"/);
  assert.match(html, /href="obsidian:\/\/open\?vault=obsidian&amp;file=graph\.canvas"/);
  assert.doesNotMatch(html, /obsidian:\/\/[^"\s]*(?:D%3A|_personal)/i);
  assert.match(html, /id="graphify-unavailable"[^>]+hidden/);
  assert.match(html, /graphify-out\/obsidian/);
  assert.match(script, /isValidDashboardSnapshot/);
  assert.match(script, /verifyGraphAsset/);
  assert.match(script, /location\.protocol === "file:"\) \{ return;/);
  assert.doesNotMatch(script, /innerHTML\s*=/);
  assert.doesNotMatch(html + script, /Graphite|gt\.ps1/i);
});
