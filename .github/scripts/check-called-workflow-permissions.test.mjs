// Slice 242 item 7. Run: node --test .github/scripts/check-called-workflow-permissions.test.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { checkWorkflows, parseWorkflow, readWorkflowDir } from './check-called-workflow-permissions.mjs';

const CALLER_HEAD = `name: release
permissions:
  contents: read
jobs:
  build-dinowin:
`;

// PR #702's shape: the caller job grants only contents: write, the called build job asks for actions: write too.
const CALLED_ASKING_ACTIONS_WRITE = `name: dino windows
on:
  workflow_call:
permissions:
  contents: read
jobs:
  build:
    runs-on: windows-latest
    permissions:
      contents: write   # gh release upload
      actions: write    # gh cache delete
    steps:
      - run: echo hi
`;

function caller(permissionsBlock) {
  return `${CALLER_HEAD}${permissionsBlock}    uses: ./.github/workflows/dinoreader-windows.yml\n`;
}

test('PR #702: a called job asking actions: write under a caller job granting only contents: write fails', () => {
  const problems = checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: write   # gh release upload\n'),
    'dinoreader-windows.yml': CALLED_ASKING_ACTIONS_WRITE,
  });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /release\.yml job build-dinowin calls dinoreader-windows\.yml/);
  assert.match(problems[0], /actions: write/);
});

test('hotfix #704: the caller job also granting actions: write passes', () => {
  const problems = checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: write\n      actions: write    # why\n'),
    'dinoreader-windows.yml': CALLED_ASKING_ACTIONS_WRITE,
  });
  assert.deepEqual(problems, []);
});

test('a read request under a write grant of the same permission passes; read under none fails', () => {
  const called = CALLED_ASKING_ACTIONS_WRITE.replace('actions: write', 'actions: read');
  assert.deepEqual(checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: write\n      actions: write\n'),
    'dinoreader-windows.yml': called,
  }), []);
  assert.equal(checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: write\n'),
    'dinoreader-windows.yml': called,
  }).length, 1);
});

test('a caller job with no permissions inherits the workflow-level ones', () => {
  const problems = checkWorkflows({
    'release.yml': caller(''),
    'dinoreader-windows.yml': CALLED_ASKING_ACTIONS_WRITE,
  });
  assert.equal(problems.length, 2);   // contents: write and actions: write both exceed the workflow's contents: read
});

test('a caller with no permissions anywhere is skipped (the repository default cannot be read from files)', () => {
  const problems = checkWorkflows({
    'release.yml': 'name: r\njobs:\n  build-dinowin:\n    uses: ./.github/workflows/dinoreader-windows.yml\n',
    'dinoreader-windows.yml': CALLED_ASKING_ACTIONS_WRITE,
  });
  assert.deepEqual(problems, []);
});

test('the called workflow top-level permissions count too', () => {
  const called = 'name: c\non:\n  workflow_call:\npermissions:\n  packages: write\njobs:\n  build:\n    runs-on: x\n';
  assert.equal(checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: write\n'),
    'dinoreader-windows.yml': called,
  }).length, 1);
});

test('a called workflow that does not exist is reported', () => {
  const problems = checkWorkflows({ 'release.yml': caller('    permissions:\n      contents: write\n') });
  assert.match(problems[0], /which was not found/);
});

test('the parser reads scalar, empty and block permissions and ignores comments', () => {
  const parsed = parseWorkflow('permissions: read-all\njobs:\n  a:\n    permissions: {}\n    uses: ./.github/workflows/x.yml # note\n  b:\n    permissions:\n      contents: write # why\n');
  assert.deepEqual(parsed.topPermissions, { all: 'read' });
  assert.deepEqual(parsed.jobs.a.permissions, { map: {} });
  assert.equal(parsed.jobs.a.uses, './.github/workflows/x.yml');
  assert.deepEqual(parsed.jobs.b.permissions, { map: { contents: 'write' } });
});

test('a top-level write that every job overrides with read asks for nothing', () => {
  const called = 'name: c\non:\n  workflow_call:\npermissions:\n  contents: write\njobs:\n  a:\n    permissions:\n      contents: read\n  b:\n    permissions:\n      contents: read\n';
  assert.deepEqual(checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: read\n'),
    'dinoreader-windows.yml': called,
  }), []);
  const oneJobWithout = called.replace('  b:\n    permissions:\n      contents: read\n', '  b:\n    runs-on: x\n');
  assert.equal(checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: read\n'),
    'dinoreader-windows.yml': oneJobWithout,
  }).length, 1);   // job b inherits the top-level contents: write
});

test('the parser reads jobs and keys at 4-space indentation', () => {
  const parsed = parseWorkflow('jobs:\n    a:\n        permissions:\n            contents: write\n        uses: ./.github/workflows/x.yml\n');
  assert.deepEqual(parsed.jobs.a.permissions, { map: { contents: 'write' } });
  assert.equal(parsed.jobs.a.uses, './.github/workflows/x.yml');
  assert.deepEqual(parsed.unparsed, []);
});

test('a permissions shape the reader cannot read fails loudly instead of passing', () => {
  const calledWith = (permissions) => `name: c\non:\n  workflow_call:\njobs:\n  build:\n    runs-on: x\n    permissions:\n${permissions}`;
  for (const shape of ['      contents: ${{ inputs.level }}\n', '      contents: [write]\n', '      - contents: write\n']) {
    const problems = checkWorkflows({
      'release.yml': caller('    permissions:\n      contents: read\n'),
      'dinoreader-windows.yml': calledWith(shape),
    });
    assert.ok(problems.some((problem) => /dinoreader-windows\.yml: .*cannot read/.test(problem)), shape);
  }
  const flowBad = checkWorkflows({
    'release.yml': caller('    permissions:\n      contents: read\n'),
    'dinoreader-windows.yml': 'name: c\njobs:\n  build:\n    permissions: ${{ inputs.p }}\n',
  });
  assert.ok(flowBad.some((problem) => /cannot read the permissions value/.test(problem)));
});

test('quoted and flow-style permissions are read, not skipped', () => {
  const parsed = parseWorkflow('jobs:\n  a:\n    permissions: { contents: write, "actions": "read" }\n  b:\n    permissions:\n      "contents": \'write\'\n');
  assert.deepEqual(parsed.jobs.a.permissions, { map: { contents: 'write', actions: 'read' } });
  assert.deepEqual(parsed.jobs.b.permissions, { map: { contents: 'write' } });
  assert.deepEqual(parsed.unparsed, []);
});

test('main: every called workflow asks for no more than its caller job grants', () => {
  const problems = checkWorkflows(readWorkflowDir(fileURLToPath(new URL('../workflows', import.meta.url))));
  assert.deepEqual(problems, []);
});
