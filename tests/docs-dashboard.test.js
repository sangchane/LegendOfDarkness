const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const root = path.resolve(__dirname, '..');
const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8');

test('dashboard exposes one in-page application shell for every primary workspace', () => {
  const html = read('docs/index.html');

  assert.match(html, /<nav[^>]+aria-label="주 메뉴"/);
  assert.match(html, /data-view-target="overview"/);
  assert.match(html, /data-view-target="system"/);
  assert.match(html, /data-view-target="flows"/);
  assert.match(html, /data-view-target="delivery"/);
  assert.match(html, /data-view-target="knowledge"/);
  assert.match(html, /data-view-target="operations"/);
  assert.match(html, /data-view-target="prototypes"/);
  assert.equal((html.match(/<section[^>]+data-view=/g) || []).length, 7);
});

test('dashboard maps features to components, evidence, tests, and operational signals', () => {
  const html = read('docs/index.html');
  const data = require('../docs/dashboard-data.js');

  assert.match(html, /id="component-catalog"/);
  assert.match(html, /id="flow-steps"/);
  assert.ok(data.components.length >= 6);
  assert.ok(data.flows.some((flow) => flow.id === 'login'));
  assert.ok(data.flows.some((flow) => flow.id === 'movement'));
  for (const flow of data.flows) {
    assert.ok(flow.steps.every((step) => step.component && step.source && step.evidence && step.signal));
  }
});

test('dashboard avoids invented progress and labels unavailable telemetry honestly', () => {
  const html = read('docs/index.html');

  assert.doesNotMatch(html, /--value:\s*\d+%/);
  assert.match(html, /미계측/);
  assert.match(html, /근거 기준일/);
});

test('dashboard applies the Toss-inspired light design token system', () => {
  const html = read('docs/index.html');
  const css = read('docs/dashboard.css');

  assert.match(html, /<meta name="color-scheme" content="light">/);
  assert.match(css, /--brand:\s*#3182f6/);
  assert.match(css, /--brand-text:\s*#1554b8/);
  assert.match(css, /--radius-card:\s*20px/);
  assert.match(css, /Pretendard/);
  assert.match(css, /\.toss-surface/);
  assert.match(css, /\.toss-surface \.readiness-table\{[^}]*overflow-x:auto/);
  assert.match(css, /\.toss-surface \.game-preview[^}]*color:#fff/);
  assert.match(css, /\.toss-surface \.signal-primary\{[^}]*background:var\(--brand-text\)/);
});

test('screen experiments render directly inside the dashboard', () => {
  const html = read('docs/index.html');

  assert.doesNotMatch(html, /<dialog|<iframe|data-preview=/);
  assert.equal((html.match(/data-demo-screen=/g) || []).length, 6);
  assert.match(html, /id="inline-game-preview"/);
  assert.match(html, /data-hud-toggle="touch"/);
  assert.equal((html.match(/data-hud-move=/g) || []).length, 5);
  assert.equal((html.match(/data-hud-action=/g) || []).length, 3);
  assert.match(html, /data-hud-target/);
  assert.match(html, /id="hud-target-hp"/);
  assert.match(html, /id="hud-feedback"/);
  assert.match(html, /ui\/assets\/world-safehouse\.png/);
});

test('interactive tabs expose their panels and current mobile sources', () => {
  const html = read('docs/index.html');
  const script = read('docs/dashboard.js');
  const data = require('../docs/dashboard-data.js');
  const mobile = data.components.find((entry) => entry.id === 'mobile');

  assert.match(html, /id="flow-panel"[^>]+role="tabpanel"/);
  assert.match(html, /aria-controls="screen-layer-login"/);
  assert.match(html, /id="screen-layer-login"[^>]+role="tabpanel"/);
  assert.match(script, /ArrowLeft/);
  assert.match(script, /primary-nav.*focus/);
  assert.match(script, /is-cooling/);
  assert.match(script, /targetHp/);
  assert.match(mobile.source, /mobile\/client/);
  assert.doesNotMatch(mobile.source, /experiments\/godot-csharp-mobile-smoke/);
});

test('the primary workspace is consistently named dashboard', () => {
  const html = read('docs/index.html');
  const script = read('docs/dashboard.js');

  assert.match(html, /LOD 개발 대시보드/);
  assert.doesNotMatch(html + script, /관제실/);
});

test('knowledge board covers game content and live-operation concerns', () => {
  const data = require('../docs/dashboard-data.js');
  const categories = new Set(data.knowledge.map((entry) => entry.category));

  for (const required of ['skills', 'spells', 'characters', 'quests', 'operations']) {
    assert.ok(categories.has(required), `missing ${required} knowledge category`);
  }
});

test('operations workspace links every runbook and separates documented standards from implementation', () => {
  const html = read('docs/index.html');
  const data = require('../docs/dashboard-data.js');
  const requiredRunbooks = [
    'operations/README.md',
    'operations/service-readiness.md',
    'operations/incident-response.md',
    'operations/backup-restore.md',
    'operations/release-rollback.md',
    'operations/security-maintenance.md',
  ];

  for (const runbook of requiredRunbooks) {
    assert.match(html, new RegExp(`href="${runbook.replace('.', '\\.')}"`));
  }

  assert.match(html, /기준 문서화 ≠ 기능 구현/);
  assert.match(html, /레거시 인증은 존재/);
  assert.match(html, /공개 운영 통제 미구현/);
  assert.doesNotMatch(html, /장애 대응<\/strong>[^\n]+공백/);

  const operationCards = [...html.matchAll(/<article class="ops-card">([\s\S]*?)<\/article>/g)].map((match) => match[1]);
  const expectedCardStates = new Map([
    ['계정·보안', '통제 미구현'],
    ['저장·백업', '기준 문서화'],
    ['관측·장애 대응', '기준 문서화'],
    ['배포·업데이트', '기준 문서화'],
    ['게임 운영 도구', '대기'],
    ['권리·정책', '결정 필요'],
  ]);
  assert.equal(operationCards.length, expectedCardStates.size);
  for (const [title, state] of expectedCardStates) {
    const card = operationCards.find((entry) => entry.includes(`<h2>${title}</h2>`));
    assert.ok(card, `missing operations card: ${title}`);
    assert.match(card, new RegExp(`class="badge[^"]*">${state}<`));
  }

  const css = read('docs/dashboard.css');
  assert.match(css, /\.toss-surface \.ops-card>\.quiet-link\{[^}]*min-height:24px/);

  const operationEntries = data.knowledge.filter((entry) => entry.category === 'operations');
  assert.equal(operationEntries.length, 2);
  assert.ok(operationEntries.every((entry) => ['risk', 'partial'].includes(entry.status)));
  assert.ok(operationEntries.every((entry) => entry.source.startsWith('docs/operations/')));
  assert.ok(operationEntries.every((entry) => !/런북 기준 정의|운영 설계 필요/.test(entry.next + entry.source)));

  const contentFlow = data.flows.find((flow) => flow.id === 'content');
  const deploymentStep = contentFlow.steps.find((step) => step.component === '운영 배포');
  assert.equal(deploymentStep.source, 'docs/operations/release-rollback.md');
  assert.match(deploymentStep.evidence, /기준 문서화/);
  assert.match(deploymentStep.evidence, /자동화 미구현/);
});

test('Graphite panel distinguishes the tracked stack from the current branch', () => {
  const data = require('../docs/dashboard-data.js');

  assert.equal(data.graphite.currentBranch, 'test/hades-characterization');
  assert.equal(data.graphite.currentBranchTracked, false);
  assert.equal(data.graphite.stack.at(-1).branch, 'main');
  assert.ok(data.graphite.stack.some((entry) => entry.branch === 'chore/graphite-workflow'));
});

test('dashboard model normalizes views and filters knowledge without mutating data', () => {
  const model = require('../docs/dashboard-model.js');
  const entries = Object.freeze([
    Object.freeze({ title: '기본 공격', category: 'skills', summary: 'Assail' }),
    Object.freeze({ title: '운영 로그', category: 'operations', summary: '관측성' }),
  ]);

  assert.equal(model.normalizeView('delivery'), 'delivery');
  assert.equal(model.normalizeView('unknown'), 'overview');
  assert.deepEqual(model.filterKnowledge(entries, 'skills', ''), [entries[0]]);
  assert.deepEqual(model.filterKnowledge(entries, 'all', '관측'), [entries[1]]);
  assert.equal(entries.length, 2);
});

test('obsolete standalone screen experiment pages are removed', () => {
  assert.equal(fs.existsSync(path.join(root, 'docs/nav.js')), false);
  assert.equal(fs.existsSync(path.join(root, 'docs/ui/wireframes.html')), false);
  assert.equal(fs.existsSync(path.join(root, 'docs/ui/hud-mockup.html')), false);

  const referringDocs = [
    'docs/mobile-test-v1-wireframes.md',
    'docs/mobile-ui-references.md',
    'docs/original-sprite-animation.md',
  ].map(read).join('\n');
  assert.doesNotMatch(referringDocs, /(?:hud-mockup|wireframes)\.html/);
});
