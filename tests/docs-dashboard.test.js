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
  // 게임 데이터 게시판·지식 그래프는 개발 도구라 뺐다(사용자, 2026-10-02).
  for (const view of ['system', 'flows', 'operations', 'prototypes', 'knowledge', 'delivery']) {
    assert.doesNotMatch(html, new RegExp(`data-view="${view}"`), `${view} 화면이 되살아났다`);
  }
  // 목업 6장과 HUD 시안은 실제 Godot 화면과 전혀 달랐다.
  assert.doesNotMatch(html, /data-demo-screen=|data-hud-move=|data-hud-action=/);
  assert.doesNotMatch(html, /ui\/assets\/world-safehouse/);

  // 지운 화면만 쓰던 CSS 도 함께 없앴다.
  const css = read('docs/dashboard.css');
  for (const selector of ['.phase-track', '.ops-grid', '.inline-lab', '.hud-pad', '.component-catalog', '.flow-steps',
    '.knowledge-row', '.graphify-layout', '.obsidian-panel', '.command-box', '.sidebar-status', '.topbar-state']) {
    assert.doesNotMatch(css, new RegExp(selector.replace('.', '\\.') + '[{ ,]'), `${selector} 가 남아 있다`);
  }
});

test('dashboard avoids invented progress and stale status chips', () => {
  const html = read('docs/index.html');

  assert.doesNotMatch(html, /--value:\s*\d+%/);
  assert.doesNotMatch(html, /\d+\s*%\s*(완료|달성|진척)/);
  // 옛 날짜·브랜치에 멈춰 있던 칸이다(사용자, 2026-10-02).
  assert.doesNotMatch(html, /근거 기준일|현재 브랜치/);
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

test('ability workspace operates the current Hades skill and spell catalog directly', () => {
  const html = read('docs/index.html');
  const script = read('docs/abilities.js');
  const css = read('docs/dashboard.css');
  const data = readBrowserGlobal('docs/ability-operations-data.js', 'LOD_ABILITY_OPERATIONS');

  assert.match(html, /id="ability-grid"/);
  assert.match(html, /id="ability-kind-tabs"/);
  assert.match(html, /id="ability-editor"/);
  assert.match(html, /id="ability-effect-list"/);
  assert.match(html, /id="ability-sound-list"/);
  assert.match(css, /\.ability-editor\{[^}]*100dvh/s);
  assert.match(script, /LOD_ABILITY_OPERATIONS/);
  assert.match(script, /LOD_ABILITY_MEDIA/);
  assert.match(script, /\/api\/ability-overrides/);

  assert.equal(data['목록'].length, data['셈']['전체']);
  assert.ok(data['셈']['기술'] > 0);
  assert.ok(data['셈']['마법'] > 0);
  assert.equal(new Set(data['목록'].map((row) => row['운영키'])).size, data['목록'].length);
  for (const ability of data['목록']) {
    assert.ok(['기술', '마법'].includes(ability['갈래']));
    assert.ok(ability['게임']);
    assert.ok(ability['기본']);
    assert.ok(ability['반영가능']);
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

    assert.ok(monster['감산레벨'] >= 1 && monster['감산레벨'] <= 99);
    // 서버는 배율을 적용한 확률을 목록 길이 위에 순서대로 놓는다. 남은 구간 이상으로는 못 떨어진다.
    let left = monster['드랍'].length;
    for (const drop of monster['드랍']) {
      const weight = drop['템플릿있음'] ? Math.max(0, drop['표확률'] * 1.5) : 0;
      const expected = Math.min(left, weight) / monster['드랍'].length;
      assert.ok(Math.abs(drop['실제확률'] - expected) <= 0.00005 + 1e-12,
        `${monster['이름']} / ${drop['이름']}: ${drop['실제확률']} != ${expected}`);
      assert.ok(drop['실제확률'] >= 0 && drop['실제확률'] <= 1);
      left = Math.max(0, left - weight);
    }

    // 그림 번호를 적어 놓고 파일이 없으면 카드가 빈칸으로 난다.
    if (monster['스프라이트']) {
      const art = monster['스프라이트'];
      assert.ok(fs.existsSync(path.join(root, 'docs/ui/assets/creature', `${art['이름']}.png`)),
        `${monster['이름']}: ${art['이름']}.png 가 없다`);
      assert.ok(art['칸'] >= 1 && art['너비'] > 0 && art['높이'] > 0);
    }
  }

  // 레벨 표는 화면에서 「몇 마리로 레벨업」을 셈하는 데 쓴다. 원작 곡선의 1→2 는 600 이다.
  assert.equal(data['레벨표']['2'], 600);
  assert.equal(data['규칙']['감산']['용서'], 5);
  assert.match(data['규칙']['감산근거'], /우리가 정한/);
});

test('codex experience uses the server cut level and truncates like uint experience', () => {
  const data = readBrowserGlobal('docs/monsters-data.js', 'LOD_MONSTERS');
  const source = read('docs/monsters.js').match(/function earned\(monster, level\) \{[\s\S]*?\n  \}/)[0];
  const earned = require('node:vm').runInNewContext(`(${source})`, { data });
  assert.equal(earned({ '감산레벨': 51, '레벨': 1, '경험치': 22380 }, 71), 2797);
  assert.equal(earned({ '감산레벨': 1, '경험치': 0 }, 99), 1);
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

test('world map model separates regions, exact map search, and directional warps', () => {
  const data = readBrowserGlobal('docs/world-map-data.js', 'WORLD_MAP_DATA');
  const world = require('../docs/world-map-model.js').create(data);

  // 「전체 월드 탐색기 초안」은 뺐다(사용자, 2026-10-02). 모델은 맵 그림 보기가 쓴다.
  assert.equal(fs.existsSync(path.join(root, 'docs/world-map.js')), false);
  assert.equal(world.clusters.length, data['요약']['덩어리']);
  assert.equal(world.search('죽음의마을1')[0].name, '죽음의마을1');

  const deathVillage = world.maps.find((map) => map.name === '죽음의마을1');
  const links = world.connections(deathVillage.id);
  assert.ok(links.incoming.length > 0);
  assert.ok(links.outgoing.length > 0);
  assert.equal(world.status(deathVillage.id), 'both');
});

test('map images still cover Novice, Porte, and Woodland behind the summary', () => {
  const html = read('docs/index.html');
  const focus = read('docs/map-focus.js');
  const images = readBrowserGlobal('docs/map-images-data.js', 'MAP_IMAGES');
  const builder = read('scripts/gen/world/build-map-images.py');

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

test('dashboard model normalizes views', () => {
  const model = require('../docs/dashboard-model.js');

  assert.equal(model.normalizeView('monsters'), 'monsters');
  assert.equal(model.normalizeView('changes'), 'changes');
  assert.equal(model.normalizeView('prototypes'), 'overview');   // 지운 화면은 되돌려 보낸다
  assert.equal(model.normalizeView('unknown'), 'overview');
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

test('the operations effect catalog preserves original frame geometry', () => {
  const media = readBrowserGlobal('docs/ability-media-catalog.js', 'LOD_ABILITY_MEDIA');
  assert.ok(media['이펙트'].length > 0);
  for (const shot of media['이펙트']) {
    assert.ok(Array.isArray(shot['바탕']) && shot['바탕'].length === 2, `${shot['번호']} 에 바탕이 없다`);
    assert.ok(Array.isArray(shot['기준']) && shot['기준'].length === 2, `${shot['번호']} 에 기준점이 없다`);
    assert.ok(shot['프레임'] > 0, `${shot['번호']} 에 프레임이 없다`);
    assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/ability-effects', shot['파일'])), true);
  }
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

test('an ability can only edit packet channels it actually sends', () => {
  const script = read('docs/abilities.js');
  const data = readBrowserGlobal('docs/ability-operations-data.js', 'LOD_ABILITY_OPERATIONS');
  const locked = data['목록'].filter((ability) => !ability['반영가능'].effect || !ability['반영가능'].sound);
  assert.ok(locked.length > 0, '수정할 패킷 채널이 없는 항목도 있어야 한다');
  assert.match(script, /setControl\("effect", row\["반영가능"\]\.effect\)/);
  assert.match(script, /setControl\("sound", row\["반영가능"\]\.sound\)/);
  assert.match(script, /if \(selected\["반영가능"\]\[field\]\)/);
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
