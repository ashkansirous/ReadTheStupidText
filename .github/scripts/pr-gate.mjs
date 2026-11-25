#!/usr/bin/env node
// Slice 456: the one always-run check that `main` requires. Every other PR workflow is path-filtered, so a required
// check on any of them would block PRs that never start it. This waits until every other check run and commit status on
// the PR's head commit has finished, then passes only when none failed. Auto-merge (dependabot-auto-merge.yml) waits on it.
//
// Usage: node pr-gate.mjs <owner/repo> <sha> <own check name> <own run id>   (needs GH_TOKEN; gh on PATH)
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

const OK_CONCLUSIONS = new Set(['success', 'neutral', 'skipped']);
const OK_STATES = new Set(['success']);
const PENDING_STATES = new Set(['pending']);
const POLL_SECONDS = 30;
const SETTLE_SECONDS = 90;        // other workflows' check runs may not exist yet in the first minute
const STABLE_POLLS = 2;           // "all finished" must hold on two polls in a row before passing
const MAX_MINUTES = 170;          // the job's timeout-minutes is 180; the slowest PR build (Dino iOS) takes about 50
const FAIL_STANDS_MINUTES = 10;   // a failure with nothing running must stand this long before the gate fails (time to re-run the red job)
const READ_TRIES = 5;             // a failed `gh api` read is retried before it fails the gate
const READ_RETRY_SECONDS = 15;

export const Verdict = Object.freeze({ Pending: 'pending', Pass: 'pass', Fail: 'fail' });

/**
 * Pure: the gate's verdict from the head commit's check runs, statuses and workflow runs, leaving out its own check
 * run and its own workflow run. A workflow run that has not completed counts as still waiting even before it has
 * created any check run.
 */
export function verdict(checkRuns, statuses, ownName, workflowRuns = [], ownRunId = '') {
  const others = checkRuns.filter((run) => run.name !== ownName);
  const failed = [
    ...others.filter((run) => run.status === 'completed' && !OK_CONCLUSIONS.has(run.conclusion))
      .map((run) => `${run.name}: ${run.conclusion}`),
    ...statuses.filter((s) => !OK_STATES.has(s.state) && !PENDING_STATES.has(s.state))
      .map((s) => `${s.context}: ${s.state}`),
    // A workflow run can fail with no check run at all (a broken workflow file, startup_failure, action_required).
    // A cancelled run is left out: concurrency cancels superseded runs, and its jobs' check runs carry any real failure.
    ...workflowRuns.filter((wf) => wf.status === 'completed' && String(wf.id) !== String(ownRunId)
      && !OK_CONCLUSIONS.has(wf.conclusion) && wf.conclusion !== 'cancelled'
      && !checkRuns.some((run) => wf.check_suite_id != null && run.check_suite?.id === wf.check_suite_id))
      .map((wf) => `${wf.name} (workflow run): ${wf.conclusion}`),
  ];
  const waiting = [
    ...others.filter((run) => run.status !== 'completed').map((run) => run.name),
    ...statuses.filter((s) => PENDING_STATES.has(s.state)).map((s) => s.context),
    ...workflowRuns.filter((wf) => wf.status !== 'completed' && String(wf.id) !== String(ownRunId))
      .map((wf) => `${wf.name} (workflow run)`),
  ];
  if (failed.length > 0) return { verdict: Verdict.Fail, failed, waiting };
  return { verdict: waiting.length > 0 ? Verdict.Pending : Verdict.Pass, failed: [], waiting };
}

const sleep = (seconds) => new Promise((resolve) => setTimeout(resolve, seconds * 1000));

async function ghJson(path) {
  for (let attempt = 1; ; attempt += 1) {
    try {
      const { stdout } = await execFileAsync('gh', ['api', '--paginate', '--slurp', path],
        { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
      return JSON.parse(stdout);
    } catch (error) {
      if (attempt >= READ_TRIES) throw error;
      console.log(`gh api ${path} failed (try ${attempt} of ${READ_TRIES}): ${error.message.split('\n')[0]}`);
      await sleep(READ_RETRY_SECONDS);
    }
  }
}

async function readChecks(repo, sha) {
  const runs = (await ghJson(`repos/${repo}/commits/${sha}/check-runs?filter=latest&per_page=100`))
    .flatMap((page) => page.check_runs);
  const statuses = (await ghJson(`repos/${repo}/commits/${sha}/status?per_page=100`))
    .flatMap((page) => page.statuses);
  const workflowRuns = (await ghJson(`repos/${repo}/actions/runs?head_sha=${sha}&per_page=100`))
    .flatMap((page) => page.workflow_runs);
  return { runs, statuses, workflowRuns };
}

/** Tracks how long the same failure has stood with nothing running; true once it has stood long enough. */
function failureHasStood(state, result) {
  if (result.verdict !== Verdict.Fail || result.waiting.length > 0) {
    state.key = null;
    return false;
  }
  const key = result.failed.join('\n');
  if (key !== state.key) {
    state.key = key;
    state.since = Date.now();
  }
  return (Date.now() - state.since) / 60000 >= FAIL_STANDS_MINUTES;
}

async function main([repo, sha, ownName, ownRunId]) {
  if (!repo || !sha || !ownName || !ownRunId) {
    throw new Error('usage: pr-gate.mjs <owner/repo> <sha> <own check name> <own run id>');
  }
  const started = Date.now();
  const failure = { key: null, since: 0 };
  let stable = 0;
  while ((Date.now() - started) / 60000 < MAX_MINUTES) {
    const { runs, statuses, workflowRuns } = await readChecks(repo, sha);
    const result = verdict(runs, statuses, ownName, workflowRuns, ownRunId);
    if (failureHasStood(failure, result)) {
      console.log(`Failed checks (stood ${FAIL_STANDS_MINUTES} minutes, nothing running):\n  ${result.failed.join('\n  ')}`);
      process.exit(1);
    }
    if (result.verdict === Verdict.Fail) {
      console.log(`Failing, waiting for a re-run or the rest:\n  ${result.failed.join('\n  ')}`);
      await sleep(POLL_SECONDS);
      continue;
    }
    const settled = (Date.now() - started) / 1000 >= SETTLE_SECONDS;
    stable = result.verdict === Verdict.Pass && settled ? stable + 1 : 0;
    if (stable >= STABLE_POLLS) {
      const others = runs.filter((checkRun) => checkRun.name !== ownName).length;
      console.log(`All ${others} other check runs and ${statuses.length} statuses passed.`);
      return;
    }
    console.log(`Waiting on: ${result.waiting.join(', ') || '(settling)'}`);
    await sleep(POLL_SECONDS);
  }
  console.log(`Gave up after ${MAX_MINUTES} minutes with checks still running.`);
  process.exit(1);
}

if (process.argv[1]?.endsWith('pr-gate.mjs')) {
  main(process.argv.slice(2)).catch((error) => { console.error(error.message); process.exit(1); });
}
