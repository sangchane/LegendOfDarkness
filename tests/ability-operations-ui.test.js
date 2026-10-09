const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const read = (name) => fs.readFileSync(path.join(root, name), 'utf8');

function browserGlobal(file, name) {
  const context = { window: {} };
  vm.runInNewContext(read(file), context);
  return context.window[name];
}

test('ability operations data contains every current server template once', () => {
  const data = browserGlobal('docs/ability-operations-data.js', 'LOD_ABILITY_OPERATIONS');
  const folders = ['skills', 'spells'];
  const expected = folders.reduce((count, folder) => count + fs.readdirSync(path.join(
    root, 'sources/wren11/Dark-Ages-Private-Server/database/server/templates', folder))
    .filter((file) => file.endsWith('.json')).length, 0);

  assert.equal(data.목록.length, expected);
  assert.equal(new Set(data.목록.map((row) => row.운영키)).size, data.목록.length);
  assert.ok(data.목록.some((row) => row.운영키 === 'skill:통배권'));
  assert.ok(data.목록.some((row) => row.운영키 === 'spell:쿠로토'));
  for (const row of data.목록) {
    assert.ok(['기술', '마법'].includes(row.갈래));
    assert.match(row.운영키, /^(skill|spell):.+/);
    assert.ok(Array.isArray(row.게임.이펙트));
    assert.ok(Array.isArray(row.게임.소리));
  }
});

test('effect and sound catalogs only advertise files that can be served', () => {
  const media = browserGlobal('docs/ability-media-catalog.js', 'LOD_ABILITY_MEDIA');
  assert.ok(media.이펙트.length > 100);
  assert.ok(media.소리.length >= 160);
  for (const effect of media.이펙트) {
    assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/ability-effects', effect.파일)), true,
      `missing effect ${effect.번호}`);
    assert.ok(effect.프레임 > 0 && effect.바탕[0] > 0 && effect.바탕[1] > 0);
  }
  for (const sound of media.소리) {
    assert.equal(fs.existsSync(path.join(root, 'docs/ui/assets/ability-sounds', sound.파일)), true,
      `missing sound ${sound.번호}`);
  }
});

test('mobile operations UI is one-level and exposes explicit connection states', () => {
  const html = read('docs/index.html');
  const css = read('docs/abilities.css');
  const script = read('docs/abilities.js');

  assert.match(html, /id="ability-kind-tabs"/);
  assert.match(html, /data-ability-kind="기술"/);
  assert.match(html, /data-ability-kind="마법"/);
  assert.match(html, /id="ability-editor"/);
  assert.match(html, /id="ability-apply"/);
  assert.match(script, /ability-operations-data/);
  assert.match(script, /\/api\/ability-overrides/);
  assert.match(script, /읽기 전용|저장 충돌|운영에 반영/);
  assert.match(html, /href="abilities\.css"/);
  assert.match(css, /\.ability-kind-tabs button \{[^}]*min-height: 44px/);
  assert.match(css, /\.ability-apply \{[^}]*min-height: 52px/);
  // 폰에서는 목록을 누르면 무대와 편집이 한 장(전체 화면)으로 열린다.
  assert.match(css, /\.abx-detail\.is-open \{[^}]*height: 100dvh/);
  assert.doesNotMatch(html, /data-ability-step=/);
});

test('every ability says whom it hits and on which side its picture lands', () => {
  const data = browserGlobal('docs/ability-operations-data.js', 'LOD_ABILITY_OPERATIONS');
  const media = browserGlobal('docs/ability-media-catalog.js', 'LOD_ABILITY_MEDIA');
  const aims = new Set(['자기 자신', '앞의 적', '주변 적 여럿', '고른 적', '고른 아군', '파티 모두', '고른 대상']);
  for (const row of data.목록) {
    assert.ok(aims.has(row.자리.대상), `${row.운영키} 대상 ${row.자리.대상}`);
    // 자리마다 나눈 그림은 게임이 실제로 보내는 그림 안에 있어야 한다.
    for (const n of [...row.자리.쓴쪽, ...row.자리.맞는쪽]) {
      assert.ok(row.게임.이펙트.includes(n), `${row.운영키} 의 ${n} 이 보내는 그림에 없다`);
    }
  }
  const at = Object.fromEntries(data.목록.map((row) => [row.운영키, row.자리]));
  assert.equal(at['skill:Assail'].대상, '앞의 적');
  assert.equal(at['spell:쿠로'].대상, '고른 아군');
  assert.equal(at['spell:라그나로크'].대상, '주변 적 여럿');
  // `effect @get_myid, 쓴쪽, 대상` — 대상이 자기라 두 그림 모두 쓴 사람 위다.
  assert.equal(at['skill:피닉스모드'].대상, '자기 자신');
  assert.equal(at['skill:피닉스모드'].맞는쪽.length, 0);
  for (const effect of media.이펙트) { assert.match(effect.색, /^[가-힣]+$/, `그림 ${effect.번호} 의 빛깔 이름`); }
});
