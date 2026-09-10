const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const root = path.resolve(__dirname, '..');
const operationsDir = path.join(root, 'docs', 'operations');

const documents = [
  'README.md',
  'service-readiness.md',
  'incident-response.md',
  'backup-restore.md',
  'release-rollback.md',
  'security-maintenance.md',
];

function readDocument(name) {
  return fs.readFileSync(path.join(operationsDir, name), 'utf8');
}

test('운영 문서 묶음과 탐색 링크가 모두 존재한다', () => {
  for (const name of documents) {
    assert.ok(fs.existsSync(path.join(operationsDir, name)), `${name} is missing`);
  }

  const index = readDocument('README.md');
  for (const name of documents.slice(1)) {
    assert.match(index, new RegExp(`\\(${name.replace('.', '\\.')}\\)`));
  }
});

test('준비도 문서는 현재 상태와 공개 운영 금지 조건을 구분한다', () => {
  const content = readDocument('service-readiness.md');

  for (const phrase of ['현재 단계', '공개 테스트', '운영 금지 조건', '판정 근거', '결정 필요']) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }
});

test('장애 대응 문서는 심각도, 역할, 전체 대응 흐름과 비밀 보호를 정의한다', () => {
  const content = readDocument('incident-response.md');

  for (const phrase of ['SEV-1', 'Incident Commander', '탐지', '완화', '복구', '사후 검토', '비밀']) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }
});

test('백업 문서는 승인 전 목표와 복원 검증을 명확히 표시한다', () => {
  const content = readDocument('backup-restore.md');

  for (const phrase of [
    'RPO',
    'RTO',
    '결정 필요',
    '복원 리허설',
    '무결성',
    '불변 보관',
    '삭제 내성 시험',
    '운영 자격 증명으로 수정하거나 삭제할 수 없는',
  ]) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }

  assert.doesNotMatch(content, /rm\s+-rf|Remove-Item\s+.*-Recurse/i);
});

test('릴리스 문서는 공급망 근거, 호환성, 롤백 판단을 포함한다', () => {
  const content = readDocument('release-rollback.md');

  for (const phrase of [
    'Graphite',
    'provenance',
    'SBOM',
    '호환성',
    '롤백 트리거',
    'digest',
    '신뢰된 빌더',
    '서명한 provenance를 검증',
  ]) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }

  assert.doesNotMatch(content, /서명\/체크섬/);
});

test('보안 유지보수 문서는 알려진 위험과 목표 통제를 연결한다', () => {
  const content = readDocument('security-maintenance.md');

  for (const phrase of [
    '.NET 5',
    '비밀번호 해시',
    'TLS',
    'rate limiting',
    'RBAC',
    '로그 마스킹',
    'SBOM',
    '독립 저장소',
    'append-only',
    '시간 동기화',
    '무결성 이상 경보',
  ]) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }
});

test('모든 운영 문서는 구현 완료로 오해되지 않도록 상태를 명시한다', () => {
  for (const name of documents.slice(1)) {
    const content = readDocument(name);
    assert.match(content, /현재 (구현 )?상태|현재 단계/, `${name} lacks a current-state statement`);
  }
});

test('운영 문서의 로컬 Markdown 링크가 실제 파일을 가리킨다', () => {
  const markdownLink = /\[[^\]]+\]\((?!https?:\/\/)([^)#]+)(?:#[^)]+)?\)/g;

  for (const name of documents) {
    const content = readDocument(name);
    for (const match of content.matchAll(markdownLink)) {
      const target = path.resolve(operationsDir, match[1]);
      assert.ok(fs.existsSync(target), `${name} has a broken link: ${match[1]}`);
    }
  }
});

test('보안 사고 대응은 일반 장애 완화 외에 자격 증명과 증거를 다룬다', () => {
  const content = readDocument('incident-response.md');

  for (const phrase of ['세션·토큰 폐기', '노출 비밀 회전', '증거 보존', '침해 통지 판단']) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }
});

test('복원 절차는 운영 전환 시 동시 쓰기와 재손상을 방지한다', () => {
  const content = readDocument('backup-restore.md');

  for (const phrase of ['운영 쓰기 차단', '백업 이후 변경분', '전환 직전 안전 복사본', '원자적 교체', '중단 조건']) {
    assert.ok(content.includes(phrase), `missing phrase: ${phrase}`);
  }
});

test('근거가 연결되지 않은 준비도 항목은 부분 완료로 표시하지 않는다', () => {
  const content = readDocument('service-readiness.md');

  assert.match(content, /\| 사용자 여정 \|[^\n]+\| 미구현 \|/);
  assert.match(content, /\| 데이터 \|[^\n]+\| 미구현 \|/);
});
