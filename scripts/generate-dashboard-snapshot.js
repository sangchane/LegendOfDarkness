const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const START_MARKER = '<!-- NEXT-ACTION:START -->';
const END_MARKER = '<!-- NEXT-ACTION:END -->';
const VERIFICATION_FILES = Object.freeze([
  'tests/dashboard-snapshot.test.js',
  'tests/docs-dashboard.test.js',
  'tests/operations-docs.test.js',
]);

function stripInlineMarkdown(value) {
  return value.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1').replace(/[*_`]/g, '').trim();
}

function parseTask(line) {
  const match = line.match(/^-\s+\*\*\[(지금|다음|대기)\/([^\]]+)\]\*\*\s*(.+)$/);
  if (!match) return null;
  const titleMatch = match[3].match(/^\*\*(.+?)\*\*\s*(.*)$/);
  return Object.freeze({
    kind: match[1],
    area: stripInlineMarkdown(match[2]),
    title: stripInlineMarkdown(titleMatch ? titleMatch[1] : match[3]),
    detail: stripInlineMarkdown(titleMatch ? titleMatch[2] : ''),
  });
}

function parseNextAction(content) {
  const start = content.indexOf(START_MARKER);
  const end = content.indexOf(END_MARKER);
  if (start < 0 || end < 0 || end <= start) throw new Error('NEXT-ACTION markers are missing or invalid');
  const tasks = content.slice(start + START_MARKER.length, end).split(/\r?\n/).map(parseTask).filter(Boolean);
  const selectKind = (kind) => Object.freeze(tasks
    .filter((task) => task.kind === kind)
    .map(({ area, title, detail }) => Object.freeze({ area, title, detail })));
  const roadmap = Object.freeze({ current: selectKind('지금'), next: selectKind('다음'), deferred: selectKind('대기') });
  if (roadmap.current.length === 0) throw new Error('NEXT-ACTION current task is missing');
  return roadmap;
}

function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;
  Object.values(value).forEach(deepFreeze);
  return Object.freeze(value);
}

function nullableCount(value) {
  return Number.isInteger(value) && value >= 0 ? value : null;
}

function buildSnapshot({ roadmap, git, graphify, verification, generatedAt }) {
  return deepFreeze({
    schemaVersion: 2,
    generatedAt: String(generatedAt),
    source: { nextPath: 'NEXT.md', graphPath: 'graphify-out/graph.json', markersFound: true },
    roadmap: {
      current: roadmap.current.map(({ area, title, detail = '' }) => ({ area: String(area), title: String(title), detail: String(detail) })),
      next: roadmap.next.map(({ area, title, detail = '' }) => ({ area: String(area), title: String(title), detail: String(detail) })),
      deferred: roadmap.deferred.map(({ area, title, detail = '' }) => ({ area: String(area), title: String(title), detail: String(detail) })),
    },
    git: {
      branch: String(git.branch || 'unknown'), sha: String(git.sha || 'unknown'), subject: String(git.subject || ''),
      dirty: typeof git.dirty === 'boolean' ? git.dirty : null,
    },
    graphify: {
      status: graphify.status === 'available' ? 'available' : 'unknown',
      nodes: nullableCount(graphify.nodes), links: nullableCount(graphify.links), communities: nullableCount(graphify.communities),
      graphHtml: graphify.graphHtml === true, report: graphify.report === true,
      obsidian: {
        status: graphify.obsidian && graphify.obsidian.status === 'available' ? 'available' : 'unknown',
        notes: nullableCount(graphify.obsidian && graphify.obsidian.notes),
        canvas: Boolean(graphify.obsidian && graphify.obsidian.canvas),
      },
    },
    verification: {
      status: ['passed', 'failed'].includes(verification.status) ? verification.status : 'unknown',
      command: String(verification.command || ''), passed: nullableCount(verification.passed) || 0,
      failed: nullableCount(verification.failed) || 0, checkedAt: verification.checkedAt ? String(verification.checkedAt) : null,
    },
  });
}

function serializeSnapshot(snapshot) {
  try {
    const json = JSON.stringify(snapshot).replace(/</g, '\\u003c').replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029');
    return `(function(root){"use strict";root.LOD_DASHBOARD_SNAPSHOT=${json};})(typeof globalThis!=="undefined"?globalThis:this);\n`;
  } catch (error) {
    throw new Error(`Failed to serialize dashboard snapshot: ${error.message}`);
  }
}

function writeSnapshotAtomic(outputPath, snapshot) {
  const source = serializeSnapshot(snapshot);
  if (fs.existsSync(outputPath) && fs.readFileSync(outputPath, 'utf8') === source) return false;
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  const temporaryPath = path.join(path.dirname(outputPath), `.${path.basename(outputPath)}.${process.pid}.${Date.now()}.tmp`);
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

function collectGit(rootDirectory, ignoredPath = 'docs/dashboard-snapshot.js') {
  const shaResult = run('git', ['rev-parse', '--short=7', 'HEAD'], rootDirectory);
  if (shaResult.status !== 0) return { branch: 'unknown', sha: 'unknown', subject: '', dirty: null };
  const sha = shaResult.stdout.trim();
  const branchResult = run('git', ['branch', '--show-current'], rootDirectory);
  const subjectResult = run('git', ['show', '-s', '--format=%s', sha], rootDirectory);
  const statusResult = run('git', ['status', '--porcelain'], rootDirectory);
  const dirty = statusResult.status === 0
    ? statusResult.stdout.split(/\r?\n/).filter(Boolean).some((line) => line.slice(3).replace(/\\/g, '/') !== ignoredPath)
    : null;
  return {
    branch: branchResult.status === 0 ? branchResult.stdout.trim() || `detached@${sha}` : `detached@${sha}`,
    sha, subject: subjectResult.status === 0 ? subjectResult.stdout.trim() : '', dirty,
  };
}

function countMarkdownFiles(directory) {
  if (!fs.existsSync(directory)) return null;
  return fs.readdirSync(directory, { withFileTypes: true }).reduce((count, entry) => {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) return count + countMarkdownFiles(entryPath);
    return count + (entry.isFile() && entry.name.toLowerCase().endsWith('.md') ? 1 : 0);
  }, 0);
}

function unknownGraphify() {
  return { status: 'unknown', nodes: null, links: null, communities: null, graphHtml: false, report: false, obsidian: { status: 'unknown', notes: null, canvas: false } };
}

function collectGraphify(rootDirectory) {
  const output = path.join(rootDirectory, 'graphify-out');
  const graphPath = path.join(output, 'graph.json');
  if (!fs.existsSync(graphPath)) return unknownGraphify();
  try {
    const graph = JSON.parse(fs.readFileSync(graphPath, 'utf8'));
    if (!Array.isArray(graph.nodes) || !Array.isArray(graph.links)) return unknownGraphify();
    const communities = new Set(graph.nodes.map((node) => node.community).filter((value) => value !== null && value !== undefined));
    const obsidianDirectory = path.join(output, 'obsidian');
    const notes = countMarkdownFiles(obsidianDirectory);
    const canvas = fs.existsSync(path.join(obsidianDirectory, 'graph.canvas'));
    return {
      status: 'available', nodes: graph.nodes.length, links: graph.links.length, communities: communities.size,
      graphHtml: fs.existsSync(path.join(output, 'graph.html')), report: fs.existsSync(path.join(output, 'GRAPH_REPORT.md')),
      obsidian: { status: notes !== null ? 'available' : 'unknown', notes, canvas },
    };
  } catch {
    return unknownGraphify();
  }
}

function verifyDashboard(rootDirectory, checkedAt) {
  const args = ['--test', ...VERIFICATION_FILES];
  const result = run(process.execPath, args, rootDirectory);
  const output = `${result.stdout || ''}\n${result.stderr || ''}`;
  const passedMatch = output.match(/(?:ℹ|#)\s*pass\s+(\d+)/);
  const failedMatch = output.match(/(?:ℹ|#)\s*fail\s+(\d+)/);
  return {
    status: result.status === 0 ? 'passed' : 'failed', command: `node ${args.join(' ')}`,
    passed: passedMatch ? Number(passedMatch[1]) : 0,
    failed: failedMatch ? Number(failedMatch[1]) : result.status === 0 ? 0 : 1, checkedAt,
  };
}

function parseArguments(args, rootDirectory) {
  const readValue = (flag) => {
    const index = args.indexOf(flag);
    if (index < 0) return null;
    const value = args[index + 1];
    if (!value || value.startsWith('--')) throw new Error(`${flag} requires a value`);
    return value;
  };
  const allowed = new Set(['--verify', '--output', '--generated-at']);
  args.forEach((arg, index) => {
    if (arg.startsWith('--') && !allowed.has(arg)) throw new Error(`unknown option: ${arg}`);
    if (!arg.startsWith('--') && (index === 0 || !['--output', '--generated-at'].includes(args[index - 1]))) throw new Error(`unexpected argument: ${arg}`);
  });
  const outputValue = readValue('--output') || 'docs/dashboard-snapshot.js';
  const output = path.resolve(rootDirectory, outputValue);
  if (output !== rootDirectory && !output.startsWith(`${rootDirectory}${path.sep}`)) throw new Error('--output must stay inside the project');
  const generatedAt = readValue('--generated-at') || new Date().toISOString();
  if (Number.isNaN(Date.parse(generatedAt))) throw new Error('--generated-at must be an ISO-compatible date');
  return { verify: args.includes('--verify'), output, generatedAt: new Date(generatedAt).toISOString() };
}

function main(args = process.argv.slice(2)) {
  const rootDirectory = path.resolve(__dirname, '..');
  const options = parseArguments(args, rootDirectory);
  const verification = options.verify ? verifyDashboard(rootDirectory, options.generatedAt) : { status: 'unknown' };
  const snapshot = buildSnapshot({
    roadmap: parseNextAction(fs.readFileSync(path.join(rootDirectory, 'NEXT.md'), 'utf8')),
    git: collectGit(rootDirectory), graphify: collectGraphify(rootDirectory), verification, generatedAt: options.generatedAt,
  });
  const changed = writeSnapshotAtomic(options.output, snapshot);
  process.stdout.write(`${changed ? 'updated' : 'unchanged'} ${path.relative(rootDirectory, options.output)}\n`);
  return snapshot;
}

module.exports = { VERIFICATION_FILES, buildSnapshot, collectGit, collectGraphify, parseArguments, parseNextAction, serializeSnapshot, verifyDashboard, writeSnapshotAtomic };

if (require.main === module) {
  try { main(); } catch (error) { process.stderr.write(`dashboard snapshot failed: ${error.message}\n`); process.exitCode = 1; }
}
