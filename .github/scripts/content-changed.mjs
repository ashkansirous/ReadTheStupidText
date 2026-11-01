#!/usr/bin/env node
/**
 * Whether this run of release.yml's `publish-store` may delist stories (Slice 239): only a push that changed something under content/dino/, or a manual run.
 * The job runs on every push to main, and a delisting is the one destructive step, so a docs or app push must never reach it. When git cannot say what changed
 * (the `before` commit cannot be fetched, a force push) nothing is delisted; the next push that touches content/dino/ does it.
 *
 * Env: EVENT (github.event_name), BEFORE (github.event.before), SHA (github.sha), GITHUB_OUTPUT. Writes `delist=true|false`.
 */

import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const CONTENT_PREFIX = 'content/dino/';
const PUSH_EVENT = 'push';
const NO_COMMIT = /^0+$/;

/** Pure: whether any changed file lies under content/dino/; null (unknown) counts as no. */
export function contentChanged(changedFiles) {
    return Array.isArray(changedFiles) && changedFiles.some((file) => file.startsWith(CONTENT_PREFIX));
}

/** Pure: whether delisting is allowed: a manual run always, a push only when its changed files include book content. */
export function delistAllowed(event, changedFiles) {
    return event !== PUSH_EVENT || contentChanged(changedFiles);
}

/** The files changed between two commits, or null when git cannot say. The shallow checkout lacks `before`, so it is fetched (one commit) first. */
function changedBetween(before, sha) {
    try {
        execFileSync('git', ['fetch', '--no-tags', '--depth=1', 'origin', before], { stdio: 'ignore' });
        const out = execFileSync('git', ['diff', '--no-renames', '--name-only', before, sha], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
        return out ? out.split('\n') : [];
    } catch {
        return null;
    }
}

function main() {
    const { EVENT, BEFORE, SHA } = process.env;
    const usable = EVENT === PUSH_EVENT && BEFORE && !NO_COMMIT.test(BEFORE) && SHA;
    const allowed = EVENT === PUSH_EVENT ? Boolean(usable) && delistAllowed(EVENT, changedBetween(BEFORE, SHA)) : delistAllowed(EVENT, null);
    console.log(`delist=${allowed}`);
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `delist=${allowed}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main();
}
