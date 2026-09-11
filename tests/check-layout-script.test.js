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

function runWithFakeGodot(lines, exitCode = 0) {
  const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'lod-layout-check-'));
  const fakeGodot = path.join(temporary, 'fake-godot.cmd');
  const body = ['@echo off', ...lines.map((line) => `echo ${line}`), `exit /b ${exitCode}`];

  fs.writeFileSync(fakeGodot, `${body.join('\r\n')}\r\n`, 'utf8');

  try {
    return spawnSync(
      powershell,
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', checker, '-Godot', fakeGodot],
      { cwd: root, encoding: 'utf8' },
    );
  } finally {
    fs.rmSync(temporary, { recursive: true, force: true });
  }
}

test('layout checker accepts a completed Godot layout run', () => {
  const result = runWithFakeGodot(['GREYBOX_LAYOUT_OK']);

  assert.equal(result.status, 0, result.stdout + result.stderr);
});

test('layout checker rejects a run with no completion marker', () => {
  const result = runWithFakeGodot([]);

  assert.notEqual(result.status, 0, result.stdout + result.stderr);
});

test('layout checker rejects a non-zero Godot exit', () => {
  const result = runWithFakeGodot(['GREYBOX_LAYOUT_OK'], 7);

  assert.notEqual(result.status, 0, result.stdout + result.stderr);
});

test('layout checker rejects Godot script errors', () => {
  const result = runWithFakeGodot(['SCRIPT ERROR: invalid call', 'GREYBOX_LAYOUT_OK']);

  assert.notEqual(result.status, 0, result.stdout + result.stderr);
});

test('layout checker allows the known shutdown leak after a completed run', () => {
  const result = runWithFakeGodot([
    'GREYBOX_LAYOUT_OK',
    'ERROR: 1 RID allocations of type Font were leaked at exit.',
  ]);

  assert.equal(result.status, 0, result.stdout + result.stderr);
});
