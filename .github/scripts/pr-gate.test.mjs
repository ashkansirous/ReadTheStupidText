import { test } from 'node:test';
import assert from 'node:assert/strict';
import { verdict, Verdict } from './pr-gate.mjs';

const OWN = 'all-checks';
const run = (name, status, conclusion = null) => ({ name, status, conclusion });

test('passes when every other check finished green, skipped or neutral', () => {
  const runs = [run(OWN, 'in_progress'), run('test', 'completed', 'success'),
    run('ios', 'completed', 'skipped'), run('lint', 'completed', 'neutral')];
  assert.equal(verdict(runs, [{ context: 'Workers Builds', state: 'success' }], OWN).verdict, Verdict.Pass);
});

test('ignores its own check run', () => {
  assert.equal(verdict([run(OWN, 'in_progress')], [], OWN).verdict, Verdict.Pass);
});

test('waits while a check run is queued or running, or a status is pending', () => {
  assert.equal(verdict([run('android', 'queued')], [], OWN).verdict, Verdict.Pending);
  assert.deepEqual(verdict([], [{ context: 'cf', state: 'pending' }], OWN).waiting, ['cf']);
});

test('fails on a failed, cancelled or timed-out check run, even while others still run', () => {
  for (const conclusion of ['failure', 'cancelled', 'timed_out', 'action_required']) {
    const result = verdict([run('windows', 'completed', conclusion), run('ios', 'in_progress')], [], OWN);
    assert.equal(result.verdict, Verdict.Fail, conclusion);
    assert.deepEqual(result.failed, [`windows: ${conclusion}`]);
  }
});

test('waits on a workflow run that has not completed, but not on its own run', () => {
  const workflowRuns = [{ id: 7, name: 'pr-gate', status: 'in_progress' }, { id: 8, name: 'android', status: 'queued' }];
  const result = verdict([], [], OWN, workflowRuns, '7');
  assert.equal(result.verdict, Verdict.Pending);
  assert.deepEqual(result.waiting, ['android (workflow run)']);
  assert.equal(verdict([], [], OWN, [{ id: 7, name: 'pr-gate', status: 'in_progress' }], '7').verdict, Verdict.Pass);
  assert.equal(verdict([], [], OWN, [{ id: 8, name: 'x', status: 'completed', conclusion: 'success' }], '7').verdict, Verdict.Pass);
});

test('a failure with something still running reports the waiting list', () => {
  const result = verdict([run('windows', 'completed', 'failure')], [], OWN, [{ id: 8, name: 'ios', status: 'queued' }], '7');
  assert.equal(result.verdict, Verdict.Fail);
  assert.deepEqual(result.waiting, ['ios (workflow run)']);
});

test('fails on a completed workflow run that failed without any check run, but not one that has check runs', () => {
  const broken = { id: 9, name: 'android', status: 'completed', conclusion: 'startup_failure', check_suite_id: 90 };
  assert.deepEqual(verdict([], [], OWN, [broken], '7').failed, ['android (workflow run): startup_failure']);
  const withRun = { ...run('build', 'completed', 'success'), check_suite: { id: 90 } };
  const rerunGreen = { ...broken, conclusion: 'failure' };
  assert.equal(verdict([withRun], [], OWN, [rerunGreen], '7').verdict, Verdict.Pass);
  assert.equal(verdict([], [], OWN, [{ ...broken, conclusion: 'cancelled' }], '7').verdict, Verdict.Pass);
  const noSuiteId = { id: 9, name: 'android', status: 'completed', conclusion: 'failure' };
  assert.equal(verdict([run('build', 'completed', 'success')], [], OWN, [noSuiteId], '7').verdict, Verdict.Fail);
});

test('fails on a failed or errored commit status', () => {
  assert.equal(verdict([], [{ context: 'cf', state: 'failure' }], OWN).verdict, Verdict.Fail);
  assert.equal(verdict([], [{ context: 'cf', state: 'error' }], OWN).verdict, Verdict.Fail);
});
