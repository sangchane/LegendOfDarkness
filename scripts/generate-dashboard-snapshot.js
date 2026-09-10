const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const START_MARKER = '<!-- NEXT-ACTION:START -->';
const END_MARKER = '<!-- NEXT-ACTION:END -->';
const VERIFICATION_FILES = ['tests/docs-dashboard.test.js', 'tests/operations-docs.test.js'];

function stripInlineMarkdown(value) {
  return value
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/[*_`]/g, '')
    .trim();
}

function parseTask(line) {
  const match = line.match(/^-\s+\*\*\[(지금|다음|대기)\/([^\]]+)\]\*\*\s*(.+)$/);
  if (!match) return null;

  const titleMatch = match[3].match(/^\*\*(.+?)\*\*\s*(.*)$/);
  const title = stripInlineMarkdown(titleMatch ? titleMatch[1] : match[3]);
  const detail = stripInlineMarkdown(titleMatch ? titleMatch[2] : '');
  return Object.freeze({ kind: match[1], area: stripInlineMarkdown(match[2]), title, detail });
}

function parseNextAction(content) {
  const start = content.indexOf(START_MARKER);
  const end = content.indexOf(END_MARKER);
  if (start < 0 || end < 0 || end <= start) {
    throw new Error('NEXT-ACTION markers are missing or invalid');
  }

  const tasks = content
    .slice(start + START_MARKER.length, end)
    .split(/\r?\n/)
    .map(parseTask)
    .filter(Boolean);
  const selectKind = (kind) => tasks
    .filter((task) => task.kind === kind)
    .map(({ area, title, detail }) => Object.freeze({ area, title, detail }));
  const roadmap = Object.freeze({
    current: Object.freeze(selectKind('지금')),
    next: Object.freeze(selectKind('다음')),
    deferred: Object.freeze(selectKind('대기')),
  });

  if (roadmap.current.length === 0) throw new Error('NEXT-ACTION current task is missing');
  return roadmap;
}

function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;
  Object.values(value).forEach(deepFreeze);
  return Object.freeze(value);
}

function cloneTask(task) {
  return { area: String(task.area), title: String(task.title), detail: String(task.detail || '') };
}

function buildSnapshot({ roadmap, git, graphite, verification, generatedAt }) {
  const snapshot = {
    schemaVersion: 1,
    generatedAt: String(generatedAt),
    source: { nextPath: 'NEXT.md', markersFound: true },
    roadmap: {
      current: roadmap.current.map(cloneTask),
      next: roadmap.next.map(cloneTask),
      deferred: roadmap.deferred.map(cloneTask),
    },
    git: {
      branch: String(git.branch || 'unknown'),
      sha: String(git.sha || 'unknown'),
      subject: String(git.subject || ''),
      dirty: Boolean(git.dirty),
    },
    graphite: {
      status: graphite.status === 'available' ? 'available' : 'unknown',
      stack: Array.isArray(graphite.stack) ? graphite.stack.map(String) : [],
    },
    verification: {
      status: ['passed', 'failed'].includes(verification.status) ? verification.status : 'unknown',
      command: String(verification.command || ''),
      passed: Number.isInteger(verification.passed) ? verification.passed : 0,
      failed: Number.isInteger(verification.failed) ? verification.failed : 0,
      checkedAt: verification.checkedAt ? String(verification.checkedAt) : null,
    },
  };
  return deepFreeze(snapshot);
}

function serializeSnapshot(snapshot) {
  try {
    const json = JSON.stringify(snapshot)
      .replace(/</g, '\\u003c')
      .replace(/\u2028/g, '\\u2028')
      .replace(/\u2029/g, '\\u2029');
    return `(function(root){"use strict";root.LOD_DASHBOARD_SNAPSHOT=${json};})(typeof globalThis!=="undefined"?globalThis:this);\n`;
  } catch (error) {
    throw new Error(`Failed to serialize dashboard snapshot: ${error.message}`);
  }
}

function writeSnapshotAtomic(outputPath, snapshot) {
  const source = serializeSnapshot(snapshot);
  if (fs.existsSync(outputPath) && fs.readFileSync(outputPath, 'utf8') === source) return false;

  const directory = path.dirname(outputPath);
  fs.mkdirSync(directory, { recursive: true });
  const temporaryPath = path.join(directory, `.${path.basename(outputPath)}.${process.pid}.${Date.now()}.tmp`);
  try {
    fs.writeFileSync(temporaryPath, source, { encoding: 'utf8', flag: 'wx' });
    fs.renameSync(temporaryPath, outputPath);
  } catch (error) {
    if (fs.existsSync(temporaryPath)) fs.unlinkSync(temporaryPath);
    throw error;
  }
  return true;
}

function run(command, args, cwd) {
  return childProcess.spawnSync(command, args, { cwd, encoding: 'utf8', shell: false, windowsHide: true });
}

function collectGit(rootDirectory) {
  const shaResult = run('git', ['rev-parse', '--short=7', 'HEAD'], rootDirectory);
  if (shaResult.status !== 0) return { branch: 'unknown', sha: 'unknown', subject: '', dirty: false };
  const sha = shaResult.stdout.trim();
  const branchResult = run('git', ['branch', '--show-current'], rootDirectory);
  const subjectResult = run('git', ['show', '-s', '--format=%s', sha], rootDirectory);
  const statusResult = run('git', ['status', '--porcelain'], rootDirectory);
  return {
    branch: branchResult.stdout.trim() || `detached@${sha}`,
    sha,
    subject: subjectResult.status === 0 ? subjectResult.stdout.trim() : '',
    dirty: statusResult.status === 0 && statusResult.stdout.trim().length > 0,
  };
}

function collectGraphite(rootDirectory) {
  const executable = process.platform === 'win32' ? 'powershell.exe' : 'pwsh';
  const result = run(executable, ['-NoProfile', '-File', path.join(rootDirectory, 'scripts', 'gt.ps1'), 'log', 'short'], rootDirectory);
  if (result.status !== 0) return { status: 'unknown', stack: [] };
  const stack = result.stdout
    .split(/\r?\n/)
    .map((line) => line.replace(/^[^\w./-]+/, '').trim())
    .filter(Boolean);
  return stack.length ? { status: 'available', stack } : { status: 'unknown', stack: [] };
}

function verifyDashboard(rootDirectory, checkedAt) {
  const args = ['--test', ...VERIFICATION_FILES];
  const result = run(process.execPath, args, rootDirectory);
  const output = `${result.stdout || ''}\n${result.stderr || ''}`;
  const passedMatch = output.match(/(?:ℹ|#)\s*pass\s+(\d+)/);
  const failedMatch = output.match(/(?:ℹ|#)\s*fail\s+(\d+)/);
  return {
    status: result.status === 0 ? 'passed' : 'failed',
    command: `node ${args.join(' ')}`,
    passed: passedMatch ? Number(passedMatch[1]) : 0,
    failed: failedMatch ? Number(failedMatch[1]) : result.status === 0 ? 0 : 1,
    checkedAt,
  };
}

function parseArguments(args, rootDirectory) {
  const valueAfter = (flag) => {
    const index = args.indexOf(flag);
    return index >= 0 ? args[index + 1] : null;
  };
  return {
    verify: args.includes('--verify'),
    graphite: !args.includes('--skip-graphite'),
    output: path.resolve(rootDirectory, valueAfter('--output') || 'docs/dashboard-snapshot.js'),
    generatedAt: valueAfter('--generated-at') || new Date().toISOString(),
  };
}

function main(args = process.argv.slice(2)) {
  const rootDirectory = path.resolve(__dirname, '..');
  const options = parseArguments(args, rootDirectory);
  const roadmap = parseNextAction(fs.readFileSync(path.join(rootDirectory, 'NEXT.md'), 'utf8'));
  const verification = options.verify
    ? verifyDashboard(rootDirectory, options.generatedAt)
    : { status: 'unknown', command: '', passed: 0, failed: 0, checkedAt: null };
  const snapshot = buildSnapshot({
    roadmap,
    git: collectGit(rootDirectory),
    graphite: options.graphite ? collectGraphite(rootDirectory) : { status: 'unknown', stack: [] },
    verification,
    generatedAt: options.generatedAt,
  });
  const changed = writeSnapshotAtomic(options.output, snapshot);
  process.stdout.write(`${changed ? 'updated' : 'unchanged'} ${path.relative(rootDirectory, options.output)}\n`);
  return snapshot;
}

module.exports = {
  buildSnapshot,
  collectGit,
  collectGraphite,
  parseNextAction,
  serializeSnapshot,
  verifyDashboard,
  writeSnapshotAtomic,
};

if (require.main === module) {
  try {
    main();
  } catch (error) {
    process.stderr.write(`dashboard snapshot failed: ${error.message}\n`);
    process.exitCode = 1;
  }
}
