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

  // 화면마다 이름이 있어야 위쪽 빵부스러기와 문서 제목이 빈칸으로 남지 않는다.
  const script = read('docs/dashboard.js');
  for (const view of sections) {
    assert.match(script, new RegExp(`${view}:\\s*"`), `dashboard.js 의 labels 에 ${view} 가 없다`);
  }
});

test('workspaces that no longer match the build are gone', () => {
  const html = read('docs/index.html');

  // 실제 화면과 동떨어져 있던 넷을 지웠다(사용자, 2026-09-19). 되살아나면 이 검사가 잡는다.
  for (const view of ['system', 'flows', 'operations', 'prototypes']) {
    assert.doesNotMatch(html, new RegExp(`data-view="${view}"`), `${view} 화면이 되살아났다`);
  }
  // 목업 6장과 HUD 시안은 실제 Godot 화면과 전혀 달랐다.
  assert.doesNotMatch(html, /data-demo-screen=|data-hud-move=|data-hud-action=/);
  assert.doesNotMatch(html, /ui\/assets\/world-safehouse/);

  // 지운 화면만 쓰던 CSS 도 함께 없앴다.
  const css = read('docs/dashboard.css');
  for (const selector of ['.phase-track', '.ops-grid', '.inline-lab', '.hud-pad', '.component-catalog', '.flow-steps']) {
    assert.doesNotMatch(css, new RegExp(selector.replace('.', '\\.') + '[{ ,]'), `${selector} 가 남아 있다`);
  }
});

test('dashboard avoids invented progress and names its evidence date', () => {
  const html = read('docs/index.html');

  assert.doesNotMatch(html, /--value:\s*\d+%/);
  assert.doesNotMatch(html, /\d+\s*%\s*(완료|달성|진척)/);
  assert.match(html, /근거 기준일/);
  assert.match(html, /id="snapshot-generated-date"/);
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

  // 새로 더한 화면도 토큰만 쓴다 — 굳은 색을 적으면 테마를 바꿀 때 그 칸만 남는다.
  const added = css.slice(css.indexOf('/* ── 지금 되는 것'));
  assert.ok(added.length > 0, '새 화면 CSS 가 없다');
  const hardCoded = added.match(/:\s*#[0-9a-f]{3,8}\b/gi) || [];
  assert.deepEqual(hardCoded, [], `굳은 색이 남아 있다: ${hardCoded.join(', ')}`);

  // 숫자는 자릿수가 맞아야 표로 읽힌다.
  assert.match(added, /\.tally>strong\{[^}]*var\(--mono\)/);
  assert.match(added, /\.tally>strong\{[^}]*tabular-nums/);
  assert.match(added, /\.monster-stat strong\{[^}]*tabular-nums/);

  // 좁은 화면에서 접히는 규칙이 있어야 한다.
  assert.match(added, /@media\(max-width:780px\)/);
});

test('every list workspace has an empty state', () => {
  const html = read('docs/index.html');

  for (const id of ['feature-empty', 'monster-empty', 'ability-empty', 'item-empty', 'warp-empty', 'change-empty']) {
    assert.match(html, new RegExp(`id="${id}"[^>]*hidden`), `${id} 가 없거나 처음부터 보인다`);
  }
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

test('feature map data carries every row of the prose table with a verdict', () => {
  const data = readBrowserGlobal('docs/feature-map-data.js', 'LOD_FEATURES');
  const source = read('docs/feature-map.md');

  // 원본은 산문 표다. 옮긴 것이 원본과 같은 개수인지 원본을 다시 세어 맞대 본다.
  const rowsInSource = [...source.matchAll(/^\| (\d+) \|/gm)].length;
  const rows = data['묶음'].flatMap((group) => group['기능']);
  assert.equal(rows.length, rowsInSource);
  assert.equal(rows.length, data['셈']['전체']);

  const serverMarks = new Set(['돌아감', '부분', '틀만', '없음', '모름']);
  const mobileMarks = new Set(['됨', '일부', '없음', '해당없음', '모름']);
  for (const row of rows) {
    assert.ok(serverMarks.has(row['서버']), `${row['이름']}: 알 수 없는 서버 판정 ${row['서버']}`);
    assert.ok(mobileMarks.has(row['모바일']), `${row['이름']}: 알 수 없는 모바일 판정 ${row['모바일']}`);
    assert.ok(row['번호'] > 0 && row['이름']);
  }

  // 번호는 표에서 온 것이라 겹치면 안 된다.
  assert.equal(new Set(rows.map((row) => row['번호'])).size, rows.length);
  assert.match(data['조사일'], /^\d{4}-\d{2}-\d{2}$/);
});

test('overview sorts features into what can and cannot be touched today', () => {
  const html = read('docs/index.html');
  const script = read('docs/overview.js');
  const data = readBrowserGlobal('docs/feature-map-data.js', 'LOD_FEATURES');

  assert.match(html, /id="feature-board"/);
  assert.match(html, /id="impl-tally"/);
  for (const state of ['playable', 'partial', 'server', 'none']) {
    assert.match(script, new RegExp(`id: "${state}"`), `${state} 갈래가 없다`);
  }
  // 「지금 만져진다」는 서버와 모바일이 둘 다 살아 있을 때만이다.
  assert.match(script, /서버 === "돌아감" && f\.모바일 === "됨"/);
  assert.ok(data['셈']['양쪽됨'] > 0);
  assert.ok(data['셈']['양쪽됨'] < data['셈']['전체'], '전부 된다고 나오면 표를 안 읽은 것이다');
});

test('ability workspace counts the three presentation channels separately', () => {
  const html = read('docs/index.html');
  const script = read('docs/abilities.js');
  const css = read('docs/dashboard.css');
  const data = readBrowserGlobal('docs/abilities-data.js', 'ABILITY_DATA');

  assert.match(html, /id="ability-grid"/);
  assert.match(html, /id="ability-stage"/);                 // 샌드백 무대
  assert.match(html, /id="ability-presentation"/);          // 연출 채움 한 줄
  assert.match(css, /\.ability-card\s*\{[^}]*grid-template-columns/s);
  assert.match(css, /\.playing\{[^}]*steps\(var\(--frames\)\)/s);
  assert.match(script, /ability-icons\//);                  // 아이콘 시트를 계속 쓴다
  assert.match(script, /LOD_ABILITY_EFFECTS/);              // 연출 색인을 읽는다
  assert.match(script, /presentationStrip/);

  assert.equal(data['요약']['전체'], 613);
  const abilities = data['묶음'].flatMap((group) => group['목록']);
  assert.equal(abilities.length, data['요약']['전체']);

  // 기본 화면은 몸동작·이펙트·소리가 다 나가는 것으로 연다 (사용자, 2026-09-19).
  assert.match(script, /built = "다 나감"/);
  // 여러 직업에 겹치는 기술은 직업을 안 고른 동안 대표 하나만 보인다.
  assert.match(script, /cls === "all" && row\["대표"\] === false/);

  // **「스크립트 있음」은 「눌러서 보인다」가 아니다.** 연출은 한글 이름으로만 찾으므로 이름이
  // 없으면 찾아보지도 못한다 — 빈 칸을 원작의 사실처럼 적으면 화면이 거짓말을 한다.
  const rep = abilities.filter((ability) => ability['대표']);
  assert.equal(rep.length, data['요약']['서로다름']);
  assert.ok(rep.length < abilities.length, '직업에 겹치는 기술이 하나도 없다면 셈이 틀렸다');
  assert.equal(abilities.filter((a) => a['구현']).length, data['요약']['구현']);

  // 대표 자리는 네 갈래로 빠짐없이 갈린다: 셋 다 · 일부 · 이름 없음 · 표에 없음.
  assert.equal(
    data['요약']['연출셋다'] + data['요약']['연출일부']
      + data['요약']['이름없어못찾음'] + data['요약']['표에없음'],
    data['요약']['서로다름']);
  assert.ok(data['요약']['연출셋다'] > 0, '연출 셋이 다 찬 기술이 하나도 없다');
  assert.ok(data['요약']['이름없어못찾음'] > 0, '한글 이름이 613개 다 정해졌을 리 없다');

  for (const ability of rep) {
    const has = ability['모션'].length || ability['이펙트'].length || ability['소리'].length;
    if (has) { assert.equal(ability['연출막힘'], ''); continue; }
    // 연출이 비었으면 반드시 왜 비었는지가 적혀 있어야 한다.
    assert.equal(ability['연출막힘'], ability['한글'] ? '표에없음' : '한글이름없음', ability['이름']);
  }
  assert.doesNotMatch(script, /구현됐지만 아무것도 안 보인다/);

  // 선행 기술이 목록에서 뒤에 오면 사슬을 거꾸로 읽게 된다.
  for (const group of data['묶음']) {
    const position = new Map(group['목록'].map((ability, index) => [ability['이름'], index]));
    for (const ability of group['목록']) {
      if (position.has(ability['선행'])) {
        assert.ok(position.get(ability['선행']) < position.get(ability['이름']),
          `${group['직업']} ${group['갈래']}: ${ability['선행']} must precede ${ability['이름']}`);
      }
    }
  }
});

test('monster codex carries specs, drop odds, and the sprite that exists on disk', () => {
  const html = read('docs/index.html');
  const data = readBrowserGlobal('docs/monsters-data.js', 'LOD_MONSTERS');

  assert.match(html, /id="monster-grid"/);
  assert.match(html, /id="monster-level"/);                 // 내 레벨을 바꿔 가며 본다
  assert.ok(data['괴물'].length > 0);
  assert.equal(data['셈']['괴물자리'], data['괴물'].length);

  for (const monster of data['괴물']) {
    assert.ok(monster['근거'].startsWith('templates/monsters/'), `${monster['이름']}: 근거 경로가 없다`);
    assert.ok(monster['지역'] && monster['맵'] && monster['맵번호'] > 0);
    assert.ok(['선공', '반반', '비선공'].includes(monster['선공']));

    // 떨어질 확률은 두 값의 곱이다 — 목록에서 뽑힐 확률 × 그 물건의 DropRate.
    // 표값을 그대로 보여 주면 0.8 이 80% 처럼 읽힌다.
    for (const drop of monster['드랍']) {
      const expected = Math.round((drop['표확률'] / monster['드랍'].length) * 10000) / 10000;
      assert.equal(drop['실제확률'], expected, `${monster['이름']} / ${drop['이름']}`);
      assert.ok(drop['실제확률'] <= drop['표확률']);
    }

    // 그림 번호를 적어 놓고 파일이 없으면 카드가 빈칸으로 난다.
    if (monster['스프라이트']) {
      const art = monster['스프라이트'];
      assert.ok(fs.existsSync(path.join(root, 'mobile/client/assets/actor/creature', `${art['이름']}.png`)),
        `${monster['이름']}: ${art['이름']}.png 가 없다`);
      assert.ok(art['칸'] >= 1 && art['너비'] > 0 && art['높이'] > 0);
    }
  }

  // 레벨 표는 화면에서 「몇 마리로 레벨업」을 셈하는 데 쓴다. 원작 곡선의 1→2 는 600 이다.
  assert.equal(data['레벨표']['2'], 600);
  assert.equal(data['규칙']['감산']['용서'], 5);
  assert.match(data['규칙']['감산근거'], /우리가 정한/);
});

test('region warps show what is reachable, what is stranded, and what leads out', () => {
  const html = read('docs/index.html');
  const script = read('docs/region-warps.js');
  const data = readBrowserGlobal('docs/region-warps-data.js', 'LOD_REGION_WARPS');

  assert.match(html, /id="warp-chart"/);
  assert.match(html, /id="warp-region-tabs"/);
  assert.match(script, /마을에서 못 닿는 맵/);

  const regions = Object.keys(data['지역']);
  assert.ok(regions.length >= 2, '지금 걸어 다닐 수 있는 지역이 둘은 있어야 한다');

  for (const name of regions) {
    const region = data['지역'][name];
    const ids = new Set(region['맵'].map((node) => node['번호']));
    assert.ok(ids.has(region['출발점']), `${name}: 출발점이 그 지역 맵에 없다`);

    const reached = region['맵'].filter((node) => node['닿음']).length;
    assert.equal(region['셈']['닿음'], reached);
    assert.equal(region['셈']['고아'], region['맵'].length - reached);
    assert.equal(region['셈']['맵'], region['맵'].length);

    // 출발점은 언제나 닿는다. 아니라면 넓이우선 탐색이 잘못 걸어간 것이다.
    assert.ok(region['맵'].find((node) => node['번호'] === region['출발점'])['닿음']);

    for (const edge of region['연결']) {
      assert.ok(edge['칸'] > 0, '워프 칸 수가 0 이면 그 연결은 없는 것이다');
      assert.ok(ids.has(edge['부터']) || ids.has(edge['까지']));
      if (edge['밖으로']) { assert.ok(!ids.has(edge['까지'])); }
    }
    for (const way of region['밖']) {
      assert.ok(way['부터'] && way['까지']);
    }
  }

  // 수오미는 포테의숲으로 이어져 있다 — 그 한 줄이 다음에 무엇을 채울지를 가리킨다.
  assert.ok(data['지역']['수오미']['밖'].some((way) => way['까지'].startsWith('포테의숲')));
});

test('changes workspace separates restored values from ones we invented', () => {
  const html = read('docs/index.html');
  const data = readBrowserGlobal('docs/changes-data.js', 'LOD_CHANGES');

  assert.match(html, /id="change-list"/);
  assert.match(data['기준'], /^\d{4}-\d{2}-\d{2}$/);

  const kinds = new Set(['원작복원', '팩에서', '우리가정함', '더한것', '범위']);
  for (const entry of data['항목']) {
    assert.ok(kinds.has(entry['갈래']), `${entry['제목']}: 알 수 없는 갈래 ${entry['갈래']}`);
    assert.ok(entry['전'] && entry['후'] && entry['왜'], `${entry['제목']}: 전·후·왜 중 빈 칸이 있다`);
    assert.ok(Array.isArray(entry['근거']) && entry['근거'].length, `${entry['제목']}: 근거가 없다`);

    // 근거 없이 정한 값은 언제 버릴 수 있는지를 함께 적지 않으면 영원히 남는다.
    if (entry['갈래'] === '우리가정함') {
      assert.ok(entry['풀림'], `${entry['제목']}: 우리가 정한 값인데 「언제 버리나」가 없다`);
    }
  }

  const invented = data['항목'].filter((entry) => entry['갈래'] === '우리가정함');
  assert.ok(invented.length > 0, '우리가 정한 값이 하나도 없다면 목록을 안 채운 것이다');
  assert.ok(invented.some((entry) => /레벨 차이|레벨이 높으면/.test(entry['제목'])),
    '레벨차 경험치 감산은 근거 없이 정한 값이라 반드시 적혀 있어야 한다');
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

test('map images still cover Novice, Porte, and Woodland behind the summary', () => {
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
  assert.match(focus, /기준 · Hades/);
  assert.match(focus, /Hades → 서버팩 3개 합의/);
  assert.match(focus, /map-pin-hades/);
  assert.equal(Object.keys(images).filter((name) => name.startsWith('포테의숲')).length, 7);
  assert.equal(Object.keys(images).filter((name) => name.startsWith('노비스')).length, 18);
  assert.equal(images['노비스마을']['워프출처'], 'Hades templates/warps');
  assert.equal(Object.keys(images).filter((name) => name.startsWith('우드랜드')).length, 10);
  assert.equal(
    Object.values(images).reduce((sum, image) => sum + image['참고표시'].length, 0),
    0,
  );
  assert.match(builder, /Hades templates\/warps/);
  assert.doesNotMatch(builder, /Downloads/);
});

test('knowledge graph panel exposes Graphify and Obsidian without obsolete stacked-PR UI', () => {
  const html = read('docs/index.html');
  const sources = html + read('docs/dashboard.js') + read('docs/dashboard-data.js') + read('scripts/generate-dashboard-snapshot.js');

  assert.match(html, /Graphify 지식 그래프/);
  assert.match(html, /Obsidian vault/);
  assert.match(html, /Obsidian에서 열기/);
  assert.match(html, /graphify-out\/GRAPH_REPORT\.md/);
  assert.equal((html.match(/<iframe/g) || []).length, 1);
  assert.match(html, /sandbox="allow-scripts"/);
  assert.match(html, /aria-describedby="graphify-description"/);
  assert.doesNotMatch(sources, /Graphite|gt\.ps1/i);
});

test('dashboard model normalizes views and filters knowledge without mutating data', () => {
  const model = require('../docs/dashboard-model.js');
  const entries = Object.freeze([
    Object.freeze({ title: '기본 공격', category: 'skills', summary: 'Assail' }),
    Object.freeze({ title: '운영 로그', category: 'operations', summary: '관측성' }),
  ]);

  assert.equal(model.normalizeView('monsters'), 'monsters');
  assert.equal(model.normalizeView('changes'), 'changes');
  assert.equal(model.normalizeView('prototypes'), 'overview');   // 지운 화면은 되돌려 보낸다
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

test('the route shows which tile on each map leads to the next one', () => {
  const html = read('docs/index.html');
  const script = read('docs/route.js');
  const maps = readBrowserGlobal('docs/map-images-data.js', 'MAP_IMAGES');

  assert.match(html, /id="route-legs"/);
  const sources = [...html.matchAll(/<script src="([^"]+)"/g)].map((m) => m[1]);
  assert.ok(sources.includes('route.js'));

  // 길: 노비스마을 → 평원에서 21레벨 → 마을의 월드맵 칸 → 수오미 → 동쪽 끝 → 포테의숲1존.
  // 칸 좌표는 자료에서 찾는다 — 화면에 좌표를 적어 두면 워프가 옮겨져도 화면은 옛말을 한다.
  const doors = (from, to) => (maps[from]?.['표시'] ?? []).filter((m) => m['도착'].includes(to));
  for (const [from, to] of [['노비스마을', '노비스평원A'], ['노비스마을', '노비스평원B'],
                            ['노비스평원A', '노비스마을'], ['노비스마을', '월드맵'],
                            ['수오미마을', '포테의숲1존']]) {
    assert.ok(doors(from, to).length > 0, `${from} → ${to} 로 가는 칸이 자료에 없다`);
  }

  // 수오미마을은 길 한가운데다 — 그림이 없으면 길이 끊겨 보인다.
  assert.ok(maps['수오미마을'], '수오미마을 지도가 없다');
  assert.equal(fs.existsSync(path.join(root, 'docs', maps['수오미마을']['그림'])), true);

  // 좌표를 코드에 박지 않았는지. 박으면 자료와 따로 놀기 시작한다.
  assert.doesNotMatch(script, /\[\s*99\s*,\s*2[4-7]\s*\]/);
  assert.match(script, /doorsTo/);
});

test('the sandbag stage stands both at one tile apart and leaves effects their own size', () => {
  const script = read('docs/abilities.js');
  const shots = readBrowserGlobal('docs/ability-effects-data.js', 'LOD_ABILITY_EFFECTS');

  // 원작 자 그대로다. 그림은 1배로 자르고 화면에서만 확대한다 — 자를 키우면 좌표가 거짓말을 한다.
  assert.equal(shots['배율'], 1);

  // 연출마다 제 바탕과 기준점이 있다. 그것이 없으면 화면은 어디에 얼마만 하게 그릴지 알 수 없어
  // 조각을 몸통 크기로 늘리게 된다 — 일음지의 작은 반짝임이 샌드백 전체를 덮던 까닭이다.
  for (const [number, shot] of Object.entries(shots['연출'])) {
    assert.ok(Array.isArray(shot['바탕']) && shot['바탕'].length === 2, `${number} 에 바탕이 없다`);
    assert.ok(Array.isArray(shot['기준']) && shot['기준'].length === 2, `${number} 에 기준점이 없다`);
    assert.ok(shot['기준'][1] > 0 && shot['기준'][1] <= shot['바탕'][1], `${number} 기준점이 바탕 밖이다`);
  }

  // 일음지가 게임에서 쓰는 번호는 276 이다(팩 표의 42 가 아니다). 160x120 바탕에 기준점 71,95 —
  // 그 값이 바뀌면 자리가 틀어진다. 값만 견준다: vm 으로 읽은 배열은 다른 realm 것이라
  // deepEqual 이 통째로 다르다고 한다.
  assert.equal(shots['연출']['276']['바탕'].join(), '160,120');
  assert.equal(shots['연출']['276']['기준'].join(), '71,95');

  // 그림은 게임이 쏘는 번호에서만 나온다 — 팩 표의 번호로 뽑으면 화면과 게임이 다른 그림을 본다.
  const used = JSON.parse(read('data/game-data/ability-presentation.json'))['채널']['이펙트'];
  for (const number of Object.keys(shots['연출'])) {
    assert.ok(used.includes(Number(number)), `${number} 은 게임이 안 쏘는데 그림만 있다`);
  }

  // 크기를 화면이 정하지 않는다. 예전에는 96px 높이로 늘렸다.
  assert.doesNotMatch(script, /SHOT_HEIGHT/);
  // 무대는 원래 크기 그대로, 카드마다 같다 — 카드마다 맞춰 키우면 가리킬 때마다 창이 커졌다 작아졌다 하고,
  // 제일 큰 연출에 맞추면 창이 두 배가 된다. 둘 다 사용자가 물렸다(2026-09-19).
  assert.match(script, /var FLOOR = \{ wide: 228, tall: 150/);
  assert.doesNotMatch(script, /scale\(" \+ ZOOM/);
  assert.match(script, /STEP = \{ x: 28, y: 13 \}/);       // 한 칸 = 화면으로 (28,13)
  assert.match(script, /shotBox/);                          // 자리는 기준점에서 나온다

  // 샌드백은 원작 괴물 그림이다 — 팩의 연습장이 세우는 「샌드백1」의 그림 번호 154.
  assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/stage/sandbag.png')), true);
});

test('every sound and picture the stage can ask for is on disk', () => {
  const data = readBrowserGlobal('docs/abilities-data.js', 'ABILITY_DATA');
  const shots = readBrowserGlobal('docs/ability-effects-data.js', 'LOD_ABILITY_EFFECTS');
  const rows = data['묶음'].flatMap((group) => group['목록']);

  // 한때 "게임이 안 쓰는 번호는 치운다"고 생성기가 소리 24개를 지웠다(사용자, 2026-09-19 —
  // "사운드는 왜 없앤거야?"). 지우는 것은 사람이 정할 일이다. 여기서는 **부를 수 있는 것이 다 있는지**만 본다.
  // 게임이 안 보내면 팩 표 소리로 대신 울린다 — 둘 다 파일이 있어야 무대가 조용하지 않다.
  const sounds = new Set(rows.flatMap((row) =>
    (row['게임']['소리'].length ? row['게임']['소리'] : (row['소리'] || []))));
  for (const number of sounds) {
    assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/ability-sounds', `${number}.mp3`)), true,
      `소리 ${number} 이 없다`);
  }
  assert.ok(sounds.size > 0, '소리를 내는 기술이 하나도 없을 리 없다');

  for (const row of rows) {
    for (const number of row['게임']['이펙트']) {
      const shot = shots['연출'][String(number)];
      if (!shot) { continue; }           // 아카이브에 없는 번호는 화면이 조용히 건너뛴다
      assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/ability-effects', shot['파일'])), true,
        `이펙트 ${number} 그림이 없다`);
    }
  }
});

test('a skill motion is drawn wearing the outfit its own class can do it in', () => {
  const body = readBrowserGlobal('docs/body-motions-data.js', 'LOD_BODY_MOTIONS');
  const used = JSON.parse(read('data/game-data/ability-presentation.json'))['채널']['몸동작'];

  assert.ok(Object.keys(body['동작']).length > 0, '몸동작이 하나도 안 그려졌다');

  const outfits = new Map();
  for (const [number, step] of Object.entries(body['동작'])) {
    // 게임이 안 시키는 동작을 그릴 이유가 없다.
    assert.ok(used.includes(Number(number)), `${number} 은 게임이 안 시키는데 그림만 있다`);
    assert.equal(step['파일'], `motion-${number}.png`);
    assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/motion', step['파일'])), true);

    // skill.tbl 의 ST 가 "이 동작을 할 수 있는 옷"이다. 몸과 머리만 겹치면 다섯 직업이 전부 같은
    // 모습으로 나온다(사용자, 2026-09-19).
    assert.ok(step['옷'] > 0, `${number} 에 직업 의상이 없다`);
    outfits.set(step['직업'], (outfits.get(step['직업']) ?? new Set()).add(step['옷']));

    // 발은 칸 바닥이 아니라 그림 안의 발밑 줄에 있다 — 그 줄로 세워야 샌드백과 바닥이 맞는다.
    assert.ok(step['발밑'] > 0 && step['발밑'] <= body['높이'], `${number} 발밑 ${step['발밑']}`);
  }

  // 직업이 다르면 옷도 다르다. 같으면 갈아입히지 않은 것이다.
  const worn = [...outfits.values()].map((set) => [...set].join(','));
  assert.equal(new Set(worn).size, worn.length, '두 직업이 같은 옷을 입고 있다');
});

test('an ability with a script but nothing to show says so', () => {
  const script = read('docs/abilities.js');
  const data = readBrowserGlobal('docs/abilities-data.js', 'ABILITY_DATA');
  const rep = data['묶음'].flatMap((g) => g['목록']).filter((a) => a['대표']);

  // 「스크립트 있음」은 「눌러서 뭔가 나온다」가 아니다. 화면이 세던 연출은 노바온라인 팩의 표를
  // 한글 이름으로 찾은 것이고, 게임이 보내는 것은 우리 템플릿·스크립트가 정한다 — 둘은 다르다.
  // 발경은 TargetAnimation 이 0 이라 이펙트가 아예 안 나간다(사용자, 2026-09-19).
  // 채널마다 **번호 목록**이다. 참/거짓만으로는 무엇이 나가는지 알 수 없고, 그림·소리를 자르는
  // 생성기도 그 번호를 보고 자른다.
  for (const ability of rep) {
    assert.ok(ability['게임'], `${ability['이름']} 에 게임이 보내는 것이 안 적혀 있다`);
    for (const channel of ['이펙트', '소리', '몸동작']) {
      assert.ok(Array.isArray(ability['게임'][channel]), `${ability['이름']} ${channel}`);
    }
  }

  // 프라보는 팩 표가 43·33 인데 서버는 257 을 쏜다. 팩 표를 믿으면 화면이 딴 그림을 보여 준다.
  const prabo = rep.find((a) => a['이름'] === 'ard cradh');
  assert.ok(prabo && prabo['게임']['이펙트'].includes(257), '프라보는 257 을 쏜다');

  const silent = rep.filter((a) => a['구현']
    && !a['게임']['이펙트'].length && !a['게임']['소리'].length && !a['게임']['몸동작'].length);
  assert.equal(silent.length, data['요약']['게임연출없음']);
  assert.ok(silent.length > 0, '스크립트만 있고 아무것도 안 나가는 것이 하나도 없을 리 없다');

  const whole = rep.filter((a) => a['구현'] && a['게임']['이펙트'].length
    && a['게임']['소리'].length && a['게임']['몸동작'].length);
  assert.equal(whole.length, data['요약']['게임연출셋다']);
  assert.ok(whole.length > 0, '다 나가는 것이 하나도 없다면 기본 화면이 빈다');

  assert.match(script, /ability-silent/);                   // 카드에 그렇게 적는다
  assert.match(script, /게임연출없음/);                        // 한 줄 셈에도 올린다
});

test('how stale the data is stays a build-time record, not screen furniture', () => {
  const html = read('docs/index.html');
  const data = readBrowserGlobal('docs/data-freshness.js', 'LOD_FRESHNESS');
  const sources = [...html.matchAll(/<script src="([^"]+)"/g)].map((match) => match[1]);

  // 「왜 낡았나」는 자료를 다시 만드는 사람에게만 쓸모가 있다. 보러 온 사람에게는 화면 맨 위를
  // 차지하는 군더더기라 걷어냈다(사용자, 2026-09-19) — 셈은 생성기가 터미널에 찍고 이 파일에 남는다.
  assert.doesNotMatch(html, /stale-banner/);
  assert.ok(!sources.includes('freshness.js'), '낡음 띠는 화면에서 뺐다');
  assert.equal(fs.existsSync(path.join(root, 'docs/freshness.js')), false);

  const sheets = new Set(data['자료'].map((row) => row['파일']));
  for (const source of sources) {
    if (!source.endsWith('-data.js')) { continue; }
    assert.ok(sheets.has(source), `${source} 의 낡음을 아무도 재지 않는다`);
  }
  for (const row of data['자료']) {
    assert.match(row['잰때'] || data['잰때'], /^\d{4}-\d{2}-\d{2}T/);
    if (row['있음']) { assert.match(row['만든때'], /^\d{4}-\d{2}-\d{2}T/); }
    // 생성기가 없는 것은 손으로 적는 자료뿐이고, 그것은 낡았다고 판정할 수 없다.
    if (!row['생성기']) { assert.equal(row['낡음'], false); }
  }
});

test('every script the page loads exists on disk', () => {
  const html = read('docs/index.html');
  const sources = [...html.matchAll(/<script src="([^"]+)"/g)].map((match) => match[1]);

  assert.ok(sources.length > 0);
  for (const source of sources) {
    assert.ok(fs.existsSync(path.join(root, 'docs', source)), `docs/${source} 가 없다`);
  }

  // 자료는 화면보다 먼저 실려야 한다 — overview.js 가 다른 화면의 셈까지 한 줄로 보여 준다.
  const lastData = sources.reduce((last, name, index) => (name.endsWith('-data.js') ? index : last), -1);
  assert.ok(sources.indexOf('overview.js') > lastData, 'overview.js 가 자료 파일보다 먼저 실린다');
});
