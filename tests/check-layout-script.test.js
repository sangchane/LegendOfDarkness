const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const test = require('node:test');

const root = path.resolve(__dirname, '..');
const checker = path.join(root, 'scripts', 'check-layout.ps1');
const powershell = path.join(
  process.env.SystemRoot || 'C:\\Windows',
  'System32',
  'WindowsPowerShell',
  'v1.0',
  'powershell.exe',
);

// 이 검사기는 PowerShell 과 .cmd 로 도는 Windows 전용이다. 없는 곳에서 그냥 돌리면 spawn 이 실패해
// status 가 null 이 되고, 'status !== 0' 을 묻는 시험 셋이 **돌지도 않은 채 초록으로** 통과한다.
// 못 도는 것보다 못 돈 것이 초록으로 보이는 쪽이 나쁘므로, 여기서는 건너뛴다고 말한다.
const unavailable = fs.existsSync(powershell)
  ? false
  : `PowerShell 이 없다 (${powershell}) — 이 검사기는 Windows 전용이다`;

function runWithFakeGodot(lines, exitCode = 0) {
  const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'lod-layout-check-'));
  const fakeGodot = path.join(temporary, 'fake-godot.cmd');
  const body = ['@echo off', ...lines.map((line) => `echo ${line}`), `exit /b ${exitCode}`];

  fs.writeFileSync(fakeGodot, `${body.join('\r\n')}\r\n`, 'utf8');

  try {
    const result = spawnSync(
      powershell,
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', checker, '-Godot', fakeGodot],
      { cwd: root, encoding: 'utf8' },
    );

    // 시작조차 못 했다면 그 자체가 실패다. status 는 그때 null 이고, 'status !== 0' 은 그것도 맞다고 한다.
    assert.equal(result.error, undefined, `검사기를 실행하지 못했다: ${result.error}`);
    assert.notEqual(result.status, null, '검사기가 시작되지 않았다');

    return result;
  } finally {
    fs.rmSync(temporary, { recursive: true, force: true });
  }
}

test('layout checker accepts a completed Godot layout run', { skip: unavailable }, () => {
  const result = runWithFakeGodot(['GREYBOX_LAYOUT_OK']);

  assert.equal(result.status, 0, result.stdout + result.stderr);
});

test('layout checker rejects a run with no completion marker', { skip: unavailable }, () => {
  const result = runWithFakeGodot([]);

  assert.notEqual(result.status, 0, result.stdout + result.stderr);
});

test('layout checker rejects a non-zero Godot exit', { skip: unavailable }, () => {
  const result = runWithFakeGodot(['GREYBOX_LAYOUT_OK'], 7);

  assert.notEqual(result.status, 0, result.stdout + result.stderr);
});

test('layout checker rejects Godot script errors', { skip: unavailable }, () => {
  const result = runWithFakeGodot(['SCRIPT ERROR: invalid call', 'GREYBOX_LAYOUT_OK']);

  assert.notEqual(result.status, 0, result.stdout + result.stderr);
});

test('layout checker allows the known shutdown leak after a completed run', { skip: unavailable }, () => {
  const result = runWithFakeGodot([
    'GREYBOX_LAYOUT_OK',
    'ERROR: 1 RID allocations of type Font were leaked at exit.',
  ]);

  assert.equal(result.status, 0, result.stdout + result.stderr);
});
