const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const root = path.resolve(__dirname, '..');
const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8');

const readBrowserGlobal = (relativePath, globalName) => {
  const vm = require('node:vm');
  const context = { window: {} };
  vm.runInNewContext(read(relativePath), context);
  return context.window[globalName];
};

test('dashboard exposes one in-page application shell for every primary workspace', () => {
  const html = read('docs/index.html');

  assert.match(html, /<nav[^>]+aria-label="주 메뉴"/);
  assert.match(html, /data-view-target="overview"/);
  assert.match(html, /data-view-target="system"/);
  assert.match(html, /data-view-target="flows"/);
  assert.match(html, /data-view-target="delivery"/);
  assert.match(html, /data-view-target="world"/);
  assert.match(html, /data-view-target="knowledge"/);
  assert.match(html, /data-view-target="operations"/);
  assert.match(html, /data-view-target="prototypes"/);

  // 버튼 하나에 화면 하나. 개수를 적어 두면 화면을 더할 때마다 고쳐야 하고, 그것은 검사를
  // 하지 않는 것과 같다. 둘을 맞대 보고, 버튼이 가리키는 화면이 실제로 있는지 본다.
  const buttons = [...html.matchAll(/data-view-target="([a-z]+)"/g)].map((m) => m[1]);
  const sections = [...html.matchAll(/<section[^>]+data-view="([a-z]+)"/g)].map((m) => m[1]);
  assert.deepEqual([...buttons].sort(), [...sections].sort());

  // 목록에 없는 화면은 열리지 않는다 — normalizeView 가 overview 로 돌려보낸다.
  const model = read('docs/dashboard-model.js');
  for (const view of sections) {
    assert.ok(model.includes(`"${view}"`), `dashboard-model.js 의 views 에 ${view} 가 없다`);
  }
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

  assert.doesNotMatch(html, /<dialog|data-preview=/);
  assert.equal((html.match(/<iframe/g) || []).length, 1);
  assert.match(html, /id="graphify-frame"/);
  assert.match(html, /sandbox="allow-scripts"/);
  assert.match(html, /aria-describedby="graphify-description"/);
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

test('ability workspace uses the original gui06 palette and complete icon sheets', () => {
  const html = read('docs/index.html');
  const script = read('docs/abilities.js');
  const css = read('docs/dashboard.css');
  const data = readBrowserGlobal('docs/abilities-data.js', 'ABILITY_DATA');
  const pngSize = (relativePath) => {
    const png = fs.readFileSync(path.join(root, relativePath));
    assert.equal(png.subarray(1, 4).toString('ascii'), 'PNG');
    return [png.readUInt32BE(16), png.readUInt32BE(20)];
  };

  assert.match(html, /data-view-target="abilities"/);
  assert.match(html, /data-view="abilities"/);
  // 선행 사슬 트리에서 카드 격자로 바꿨다 — 613개를 트리로 보면 "무엇이 있는지" 가 안 보인다.
  // 사슬은 카드 안의 「선행」 한 줄로 남는다.
  assert.match(html, /id="ability-grid"/);
  assert.match(html, /id="ability-stage"/);                 // 샌드백 무대
  assert.match(css, /\.ability-card\s*\{[^}]*grid-template-columns/s);
  assert.match(css, /\.sandbag-shot\s*\{[^}]*steps\(var\(--frames\)\)/s);
  assert.match(script, /ability-icons\//);                  // 아이콘 시트를 계속 쓴다
  assert.match(script, /LOD_ABILITY_EFFECTS/);              // 연출 색인을 읽는다
  assert.equal(data['요약']['전체'], 613);
  assert.equal(data['요약']['자동확정'], 19);
  assert.equal(data['요약']['사용자수정'], 21);
  const abilities = data['묶음'].flatMap((group) => group['목록']);
  const twoHanded = abilities.find((ability) => ability['이름'] === 'Two-handed Attack');
  const crasher = abilities.find((ability) => ability['이름'] === 'Crasher');
  assert.equal(twoHanded['한글'], '투핸드어택');
  assert.equal(twoHanded['이름출처'], '서버팩 3개 일치');
  assert.equal(crasher['이름출처'], '사용자 수정');
  for (const group of data['묶음']) {
    const position = new Map(group['목록'].map((ability, index) => [ability['이름'], index]));
    for (const ability of group['목록']) {
      if (position.has(ability['선행'])) {
        assert.ok(position.get(ability['선행']) < position.get(ability['이름']),
          `${group['직업']} ${group['갈래']}: ${ability['선행']} must precede ${ability['이름']}`);
      }
    }
  }
  assert.doesNotMatch(script, /item007\.pal|색표는 짐작/);
  assert.deepEqual(pngSize('docs/ability-icons/skill.png'), [560, 280]);
  assert.deepEqual(pngSize('docs/ability-icons/spell.png'), [560, 595]);
});

test('world map separates regions, exact map search, and directional warps', () => {
  const html = read('docs/index.html');
  const data = readBrowserGlobal('docs/world-map-data.js', 'WORLD_MAP_DATA');
  const world = require('../docs/world-map-model.js').create(data);

  assert.match(html, /id="world-regions"/);
  assert.match(html, /data-world-filter="isolated"/);
  assert.match(html, /들어오는 길과 나가는 길/);
  assert.equal(world.clusters.length, data['요약']['덩어리']);
  assert.equal(world.search('죽음의마을1')[0].name, '죽음의마을1');

  const deathVillage = world.maps.find((map) => map.name === '죽음의마을1');
  const links = world.connections(deathVillage.id);
  assert.ok(links.incoming.length > 0);
  assert.ok(links.outgoing.length > 0);
  assert.equal(world.status(deathVillage.id), 'both');
});

test('world map pins are touch and keyboard operable and data sources are labelled', () => {
  const script = read('docs/world-map.js');

  assert.match(script, /<button type="button" class="map-pin"/);
  assert.match(script, /data-map-pin/);
  assert.match(script, /Hades 서버/);
  assert.match(script, /Hades 실제 맵/);
  assert.doesNotMatch(script, /5\.99 팩 추출본/);
  assert.doesNotMatch(script, /들어오기만 한다/);
});

test('world map switches among Hades Novice, Porte, and Woodland before the full draft', () => {
  const html = read('docs/index.html');
  const focus = read('docs/map-focus.js');
  const images = readBrowserGlobal('docs/map-images-data.js', 'MAP_IMAGES');
  const builder = read('scripts/build-map-images.py');

  assert.match(html, /id="novice-village-focus"/);
  assert.match(html, /id="porte-forest-focus"/);
  assert.match(html, /data-map-focus-target="novice"/);
  assert.match(html, /data-map-focus-target="porte"/);
  assert.match(html, /data-map-focus-target="woodland"/);
  assert.match(html, /<details class="world-draft">/);
  assert.match(html, /Hades 지역 실제 맵·워프/);
  assert.match(focus, /기준 · Hades/);
  assert.match(focus, /Hades → 서버팩 3개 합의/);
  assert.match(focus, /if \(hades\.length\)/);
  assert.match(focus, /map-pin-reference/);
  assert.match(focus, /map-pin-hades/);
  assert.match(focus, /노비스마을/);
  assert.match(focus, /포테의숲/);
  assert.match(focus, /우드랜드입구/);
  assert.doesNotMatch(focus, /3갈래/);
  assert.equal(Object.keys(images).filter((name) => name.startsWith('포테의숲')).length, 7);
  const noviceNames = ['노비스마을', '노비스마을식당', '노비스무기방어구상점', '노비스민가1',
    '노비스민가2', '노비스잡화상점', '노비스주점', '노비스평원A', '노비스평원B'];
  assert.equal(Object.keys(images).filter((name) => name.startsWith('노비스')).length, 9);
  assert.ok(noviceNames.every((name) => images[name]), 'every directly connected Novice map has an image');
  assert.equal(images['노비스마을']['표시'].length, 18);
  assert.equal(images['노비스마을']['워프출처'], 'Hades templates/warps');
  assert.ok(images['노비스마을']['표시'].some((pin) => pin['도착'].includes('월드맵')));
  const woodlandNames = ['우드랜드입구', '우드랜드1-1', '우드랜드1-2', '우드랜드1-3', '우드랜드2-1',
    '우드랜드3-1', '우드랜드4-1', '우드랜드5-1', '우드랜드6-1', '우드랜드14-1'];
  assert.equal(Object.keys(images).filter((name) => name.startsWith('우드랜드')).length, 10);
  assert.ok(woodlandNames.every((name) => images[name]), 'every map directly connected to Woodland entrance has an image');
  assert.equal(images['우드랜드입구']['표시'].length, 31);
  assert.equal(images['우드랜드입구']['워프출처'], 'Hades templates/warps');
  assert.ok(images['우드랜드입구']['표시'].some((pin) => pin['도착'].includes('월드맵')));
  assert.equal(images['포테의숲1존']['참고표시'].length, 0);
  assert.equal(images['포테의숲1존']['워프출처'], '없음');
  assert.equal(images['포테의숲4존']['참고표시'].length, 0);
  assert.equal(images['포테의숲보스존']['참고표시'].length, 0);
  assert.equal(images['포테의숲보스존']['워프출처'], '없음');
  assert.equal(
    Object.values(images).reduce((sum, image) => sum + image['참고표시'].length, 0),
    0,
  );
  assert.match(builder, /database.+server/);
  assert.match(builder, /archives.+seo/);
  assert.match(builder, /if not marks/);
  assert.match(builder, /Hades templates\/warps/);
  assert.match(builder, /agreed_pack_warps/);
  assert.match(builder, /5\.99-server/);
  assert.match(builder, /honden-community/);
  assert.match(builder, /novaonline/);
  assert.match(builder, /set\.intersection/);
  assert.doesNotMatch(builder, /Downloads/);
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

test('knowledge graph panel exposes Graphify and Obsidian without obsolete stacked-PR UI', () => {
  const html = read('docs/index.html');
  const sources = html + read('docs/dashboard.js') + read('docs/dashboard-data.js') + read('scripts/generate-dashboard-snapshot.js');

  assert.match(html, /Graphify 지식 그래프/);
  assert.match(html, /Obsidian vault/);
  assert.match(html, /Obsidian에서 열기/);
  assert.match(html, /graphify-out\/GRAPH_REPORT\.md/);
  assert.doesNotMatch(sources, /Graphite|gt\.ps1/i);
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
