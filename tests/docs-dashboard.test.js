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
  assert.match(html, /data-view-target="roadmap"/);
  assert.match(html, /data-view-target="knowledge"/);
  assert.match(html, /data-view-target="operations"/);
  assert.match(html, /data-view-target="graphite"/);
  assert.match(html, /data-view-target="prototypes"/);
  assert.equal((html.match(/<section[^>]+data-view=/g) || []).length, 6);
});

test('prototype pages open inside the dashboard instead of replacing it', () => {
  const html = read('docs/index.html');

  assert.match(html, /<dialog[^>]+id="preview-dialog"/);
  assert.match(html, /<iframe[^>]+id="preview-frame"/);
  assert.match(html, /data-preview="ui\/wireframes\.html"/);
  assert.match(html, /data-preview="ui\/hud-mockup\.html"/);
});

test('knowledge board covers game content and live-operation concerns', () => {
  const data = require('../docs/dashboard-data.js');
  const categories = new Set(data.knowledge.map((entry) => entry.category));

  for (const required of ['skills', 'spells', 'characters', 'quests', 'operations']) {
    assert.ok(categories.has(required), `missing ${required} knowledge category`);
  }
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

  assert.equal(model.normalizeView('graphite'), 'graphite');
  assert.equal(model.normalizeView('unknown'), 'overview');
  assert.deepEqual(model.filterKnowledge(entries, 'skills', ''), [entries[0]]);
  assert.deepEqual(model.filterKnowledge(entries, 'all', '관측'), [entries[1]]);
  assert.equal(entries.length, 2);
});

test('shared navigation points secondary pages back to the management dashboard', () => {
  const nav = read('docs/nav.js');

  assert.match(nav, /관리 허브/);
  assert.match(nav, /index\.html\?view=prototypes/);
  assert.doesNotMatch(nav, /document\.querySelector\(link\.getAttribute\("href"\)\)/);
});
