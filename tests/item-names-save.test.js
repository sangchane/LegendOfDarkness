// 아이템 한글 이름 저장 — 리뷰 2026-10-08 11번. 바꾼 칸 하나만 보내고, 서버가 거절하면(401·400·500) 화면에 알린다.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'docs', 'items.js'), 'utf8');

// items.js 가 쓰는 만큼만 흉내 낸 DOM — 아이디마다 요소 하나, 만든 요소는 자식·듣는 함수를 기억한다.
function element(tag) {
  return {
    tagName: tag, children: [], listeners: {}, style: {}, attributes: {}, hidden: false, textContent: '', value: '',
    classList: { add() {}, remove() {}, toggle() {} },
    appendChild(child) { this.children.push(child); return child; },
    replaceChildren(...children) { this.children = children; },
    setAttribute(name, value) { this.attributes[name] = value; },
    getAttribute(name) { return this.attributes[name]; },
    addEventListener(type, listener) { (this.listeners[type] = this.listeners[type] || []).push(listener); },
    getBoundingClientRect() { return { left: 0, right: 0, top: 0, width: 0, height: 0 }; },
    scrollIntoView() {},
  };
}

function page(putStatus, putBody) {
  const nodes = {};
  const requests = [];
  const toasts = [];
  const context = {
    window: {
      LOD_SIGNED_IN: true,
      LodDashboard: { toast: (message) => toasts.push(message) },
      LOD_ITEMS: {
        목록: [
          { en: 'Stick', ko: '', slot: '무기', cls: '공용', lv: 1, ic: -1, stats: [] },
          { en: 'Eppe', ko: '', slot: '무기', cls: '공용', lv: 1, ic: -1, stats: [] },
        ],
        총: 2, 슬롯: ['무기'], 직업: ['공용'], 아이콘: { 너비: 32 },
      },
      innerWidth: 1000, innerHeight: 800,
    },
    document: {
      readyState: 'complete',
      getElementById: (id) => (nodes[id] = nodes[id] || element('div')),
      createElement: element,
      querySelectorAll: () => [],
      addEventListener() {},
    },
    localStorage: { getItem: () => null, setItem() {} },
    location: { protocol: 'https:' },
    setTimeout: (callback) => { callback(); return 1; },
    clearTimeout() {},
    fetch: (url, options = {}) => {
      requests.push({ url, method: options.method || 'GET', body: options.body && JSON.parse(options.body) });
      const status = options.method === 'PUT' ? putStatus : 200;
      const body = options.method === 'PUT' ? putBody : {};
      return Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });
    },
  };
  vm.runInNewContext(source, context);
  const settle = () => new Promise((resolve) => setImmediate(resolve));
  const rename = async (index, value) => {
    await settle();
    // 목록 줄을 누르면 옆 판의 한글 이름 칸이 그 물건을 가리킨다 — 거기서 고친다.
    const row = nodes['item-grid'].children[index].children[0];
    row.listeners.click.forEach((listener) => listener());
    const input = nodes['item-name'];
    input.value = value;
    input.listeners.change.forEach((listener) => listener());
    await settle(); await settle();
  };
  return { requests, toasts, rename };
}

test('saving one name sends only that name', async () => {
  const view = page(200, { Stick: '막대기' });
  await view.rename(0, '막대기');
  const puts = view.requests.filter((request) => request.method === 'PUT');
  assert.deepEqual(puts.map((request) => request.body), [{ changes: { Stick: '막대기' } }]);
  assert.deepEqual(view.toasts, []);
});

test('a refused save is shown as a failure', async () => {
  for (const [status, error, shown] of [[401, '로그인이 필요합니다.', /로그인/], [400, '값은 {글자: 글자|정수|null} 꼴이어야 합니다.', /꼴이어야/],
    [500, '저장하지 못했습니다.', /저장하지 못했습니다/]]) {
    const view = page(status, { error });
    await view.rename(1, '에페');
    assert.equal(view.toasts.length, 1, `${status} 를 알리지 않았다`);
    assert.match(view.toasts[0], shown);
  }
});

test('a failed name is sent again with the next edit', async () => {
  const view = page(500, { error: '저장하지 못했습니다.' });
  await view.rename(0, '막대기');
  await view.rename(1, '에페');
  const puts = view.requests.filter((request) => request.method === 'PUT');
  assert.deepEqual(puts[1].body, { changes: { Eppe: '에페', Stick: '막대기' } });
});
