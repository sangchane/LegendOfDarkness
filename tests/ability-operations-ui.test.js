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
  const css = read('docs/dashboard.css');
  const script = read('docs/abilities.js');

  assert.match(html, /id="ability-kind-tabs"/);
  assert.match(html, /data-ability-kind="기술"/);
  assert.match(html, /data-ability-kind="마법"/);
  assert.match(html, /id="ability-editor"/);
  assert.match(html, /id="ability-apply"/);
  assert.match(script, /ability-operations-data/);
  assert.match(script, /\/api\/ability-overrides/);
  assert.match(script, /읽기 전용|저장 충돌|운영에 반영/);
  assert.match(css, /\.ability-kind-tabs[^}]*min-height:44px/s);
  assert.match(css, /\.ability-apply[^}]*min-height:52px/s);
  assert.match(css, /\.ability-editor[^}]*100dvh/s);
  assert.doesNotMatch(html, /data-ability-step=/);
});
