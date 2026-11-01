#!/usr/bin/env node
/**
 * Whether release.yml's `site-checks` job (the downloads Worker's tests) runs for a push to main (Slice 239). A push that changes only
 * Dino book content (content/dino/**, a store story) must start no test, so the job is skipped; anything else, an unknown range and a manual run run it.
 * Measured from the push's own `before` commit: the job is advisory (no other job needs it), so a skipped run's changes need not carry over.
 *
 * Env: EVENT (github.event_name), BEFORE (github.event.before), SHA (github.sha), GITHUB_OUTPUT. Writes `site_checks=true|false`.
 */

import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const CONTENT_PREFIX = 'content/dino/';
const PUSH_EVENT = 'push';
const NO_COMMIT = /^0+$/;

/** Pure: false only for a non-empty change set that lies entirely under content/dino/. */
export function siteChecksNeeded(changedFiles) {
    if (changedFiles === null || changedFiles.length === 0) return true;
    return changedFiles.some((file) => !file.startsWith(CONTENT_PREFIX));
}

/** The files changed between two commits, or null when git cannot say (a missing commit, a force push). */
function changedBetween(before, sha) {
    try {
        const out = execFileSync('git', ['diff', '--no-renames', '--name-only', before, sha], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
        return out ? out.split('\n') : [];
    } catch {
        return null;
    }
}

function main() {
    const { EVENT, BEFORE, SHA } = process.env;
    const usable = EVENT === PUSH_EVENT && BEFORE && !NO_COMMIT.test(BEFORE) && SHA;
    const needed = usable ? siteChecksNeeded(changedBetween(BEFORE, SHA)) : true;
    console.log(`site_checks=${needed}`);
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `site_checks=${needed}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main();
}
