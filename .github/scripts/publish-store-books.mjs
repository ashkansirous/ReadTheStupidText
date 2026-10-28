#!/usr/bin/env node
/**
 * Publishes the free Dino Reader store books (Slice 158, Decision 163) to the downloads R2 bucket, so a merged
 * version bump reaches `dinoreader.sirous.uk/api/stories` without anyone running tools/upload-store-story.ps1.
 *
 * It compares the built folders (`artifacts/store/<id>/meta.json`, built by `dotnet run --project
 * tools/DinoReader.BookBuilder -- build` from `content/dino/`) with the LIVE list every run, not with the git diff: a cancelled
 * run or a missed book is caught by the next one. A book is uploaded only when its built version is higher than
 * the live one (or it is not live at all).
 *
 * Same rules as the hand upload: cover first, then the package, `meta.json` LAST (the Worker lists a folder only
 * when meta.json and a package are both there, so a half upload is never offered); an existing
 * `story-v<n>.dino` is never overwritten (its upload is skipped, the meta still goes up); a lower version than
 * live is never uploaded.
 *
 * Usage: node publish-store-books.mjs [--dry-run] [--verify] [--root <store folder>] [--live-file <api json>]
 *   (no flag)   upload what is behind; fails when CLOUDFLARE_API_TOKEN is unset
 *   --dry-run   print the plan, upload nothing, need no token
 *   --verify    fail when any built book is still behind the live list (retried: the list is cached ~60 s)
 * Env: STORE_API_URL (default https://dinoreader.sirous.uk/api/stories), CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID.
 */

import { appendFileSync, existsSync, mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { BUCKET_NAME, publishedStateFrom, wranglerArgs, wranglerCommand } from './r2-wrangler.mjs';

export const STORE_API_URL = 'https://dinoreader.sirous.uk/api/stories';
const KEY_PREFIX = 'dinoreader/stories';
const META_FILE = 'meta.json';
const COVER_FILE = 'cover.jpg';
const STORY_ID_PATTERN = /^[0-9a-f]{32}$/;
const VERIFY_ATTEMPTS = 7;
const FETCH_TIMEOUT_MS = 30000;
const VERIFY_WAIT_MS = 20000;

export const Outcome = { Upload: 'upload', UpToDate: 'upToDate', LiveNewer: 'liveNewer', Invalid: 'invalid' };

export const packageName = (version) => `story-v${version}.dino`;

/** Pure: a positive integer version from a meta value, else null. */
export function parseVersion(value) {
    return Number.isInteger(value) && value > 0 ? value : null;
}

/**
 * Pure: decides each built book against the live list. `built` is `[{ id, version }]` (version as read from meta.json,
 * any type), `live` is `[{ id, version }]`. Returns `{ id, version, liveVersion, outcome }` per built book, built order.
 */
export function planStoreUploads(built, live) {
    const liveById = new Map(live.map((item) => [item.id, item.version]));
    return built.map(({ id, version }) => {
        const parsed = parseVersion(version);
        const liveVersion = liveById.get(id) ?? null;
        if (!STORY_ID_PATTERN.test(id ?? '') || parsed === null) return { id, version, liveVersion, outcome: Outcome.Invalid };
        if (liveVersion === null || parsed > liveVersion) return { id, version: parsed, liveVersion, outcome: Outcome.Upload };
        return { id, version: parsed, liveVersion, outcome: parsed === liveVersion ? Outcome.UpToDate : Outcome.LiveNewer };
    });
}

/** Pure: the uploads for one book, in the order they must run: cover, package, meta.json last. */
export function uploadsFor({ id, version }, folder, hasCover) {
    const key = (name) => `${KEY_PREFIX}/${id}/${name}`;
    const uploads = [];
    if (hasCover) uploads.push({ name: COVER_FILE, key: key(COVER_FILE), path: join(folder, COVER_FILE), contentType: 'image/jpeg' });
    uploads.push({ name: packageName(version), key: key(packageName(version)), path: join(folder, packageName(version)), contentType: 'application/octet-stream', isPackage: true });
    uploads.push({ name: META_FILE, key: key(META_FILE), path: join(folder, META_FILE), contentType: 'application/json' });
    return uploads;
}

/** Pure: the live list from the /api/stories body, as `[{ id, version }]`. Throws on a body that is not the list. */
export function liveFromApi(body) {
    if (!body || !Array.isArray(body.stories)) throw new Error('The store list has no "stories" array.');
    return body.stories.map((story) => ({ id: story.id, version: story.version }));
}

function isStoryFolder(name) {
    if (STORY_ID_PATTERN.test(name)) return true;
    console.warn(`warning: ignoring ${name} in the built store folder; it is not a story id`);
    return false;
}

/** Reads every `<root>/<id>/meta.json` as `[{ id, version }]`; a folder whose name is not a story id is ignored with a warning; a folder with unreadable meta gets version null (reported as invalid). */
export function readBuilt(root) {
    const books = listBuilt(root);
    if (books.length === 0) throw new Error(`No built store books found in ${root}; the build step (dotnet run --project tools/DinoReader.BookBuilder -- build) produced nothing, so nothing was published.`);
    return books;
}

function listBuilt(root) {
    if (!existsSync(root)) return [];
    return readdirSync(root, { withFileTypes: true }).filter((entry) => entry.isDirectory() && isStoryFolder(entry.name)).map((entry) => {
        try {
            const meta = JSON.parse(readFileSync(join(root, entry.name, META_FILE), 'utf8'));
            return { id: entry.name, version: meta.version };
        } catch {
            return { id: entry.name, version: null };
        }
    });
}

/**
 * Runs the upload plan. `put(upload)` sends one file; `packageExists(upload)` says whether that package key is already
 * in the bucket (it is then not sent again). A failing book is skipped and the others still go up; the run then throws once listing every failed book
 * (the error carries `done`). A book without its meta stays unlisted. Returns the ids and versions uploaded.
 */
export function runUploads(plan, root, { put, packageExists, hasCover = (folder) => existsSync(join(folder, COVER_FILE)) }) {
    const done = [];
    const failures = [];
    for (const book of plan.filter((item) => item.outcome === Outcome.Upload)) {
        try {
            uploadBook(book, root, { put, packageExists, hasCover });
            done.push({ id: book.id, version: book.version });
        } catch (error) {
            failures.push(`${book.id}: ${error instanceof Error ? error.message : String(error)}`);
        }
    }
    if (failures.length > 0) throw Object.assign(new Error(`${failures.length} store book(s) failed to upload: ${failures.join('; ')}`), { done });
    return done;
}

function uploadBook(book, root, { put, packageExists, hasCover }) {
    const folder = join(root, book.id);
    if (!existsSync(join(folder, packageName(book.version)))) throw new Error(`${book.id}: meta.json says v${book.version} but ${packageName(book.version)} is missing.`);
    for (const upload of uploadsFor(book, folder, hasCover(folder))) {
        if (upload.isPackage && packageExists(upload)) {
            console.log(`${upload.key} already exists, not overwriting`);
            continue;
        }
        put(upload);
    }
}

/** Pure: the step-summary markdown. */
export function summaryText(plan, uploaded) {
    const lines = ['### Dino store books'];
    const count = (outcome) => plan.filter((item) => item.outcome === outcome).length;
    lines.push(`${plan.length} built: ${uploaded.length} uploaded, ${count(Outcome.UpToDate)} up to date, ${count(Outcome.LiveNewer)} live newer (skipped), ${count(Outcome.Invalid)} invalid.`);
    uploaded.forEach((book) => lines.push(`- uploaded ${book.id} v${book.version}`));
    plan.filter((item) => item.outcome === Outcome.LiveNewer).forEach((item) => lines.push(`- warning: ${item.id} built v${item.version} but live is v${item.liveVersion}; not uploaded`));
    plan.filter((item) => item.outcome === Outcome.Invalid).forEach((item) => lines.push(`- error: ${item.id} has no valid version in meta.json`));
    return lines.join('\n');
}

/** Pure: built books still behind the live list (not live, or a lower version), plus invalid ones. */
export function stillBehind(built, live) {
    return planStoreUploads(built, live).filter((item) => item.outcome === Outcome.Upload || item.outcome === Outcome.Invalid);
}

async function fetchLive(args, env) {
    const fileFlag = args.indexOf('--live-file');
    if (fileFlag !== -1) return liveFromApi(JSON.parse(readFileSync(args[fileFlag + 1], 'utf8')));
    const url = `${env.STORE_API_URL || STORE_API_URL}?t=${Date.now()}`;
    const response = await fetch(url, { headers: { 'Cache-Control': 'no-cache' }, signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) });
    if (!response.ok) throw new Error(`GET ${url} answered ${response.status}.`);
    return liveFromApi(await response.json());
}

function runWrangler(args, capture) {
    const { command, args: commandArgs } = wranglerCommand(args);
    return spawnSync(command, commandArgs, capture ? { encoding: 'utf8', env: process.env, maxBuffer: 1024 * 1024 } : { stdio: 'inherit', env: process.env });
}

function putToBucket(upload) {
    console.log(`Uploading ${upload.path} -> ${BUCKET_NAME}/${upload.key}`);
    const result = runWrangler(wranglerArgs(upload));
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`wrangler upload of ${upload.key} exited with status ${result.status}.`);
}

function packageInBucket(upload) {
    // Download to a file, not `--pipe`: the package is megabytes and would overflow the capture buffer.
    const folder = mkdtempSync(join(tmpdir(), 'store-head-'));
    try {
        const result = runWrangler(['r2', 'object', 'get', `${BUCKET_NAME}/${upload.key}`, '--remote', '--file', join(folder, 'probe')], true);
        if (result.error) throw result.error;
        return publishedStateFrom({ status: result.status, output: `${result.stdout}${result.stderr}` });
    } finally {
        rmSync(folder, { recursive: true, force: true });
    }
}

function writeSummary(text) {
    console.log(text);
    if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${text}\n`);
}

const sleep = (ms) => new Promise((done) => setTimeout(done, ms));

async function verify(args, root) {
    const built = readBuilt(root);
    let behind = [];
    for (let attempt = 1; attempt <= VERIFY_ATTEMPTS; attempt++) {
        behind = stillBehind(built, await fetchLive(args, process.env));
        if (behind.length === 0) {
            writeSummary(`Verified: all ${built.length} built store books are live.`);
            return;
        }
        if (attempt < VERIFY_ATTEMPTS) await sleep(VERIFY_WAIT_MS);
    }
    throw new Error(`Still behind the live store: ${behind.map((item) => `${item.id} (built v${item.version}, live v${item.liveVersion})`).join(', ')}`);
}

async function main() {
    const args = process.argv.slice(2);
    const rootFlag = args.indexOf('--root');
    const root = resolve(rootFlag === -1 ? 'artifacts/store' : args[rootFlag + 1]);
    if (args.includes('--verify')) return verify(args, root);
    const dryRun = args.includes('--dry-run');
    if (!dryRun && !process.env.CLOUDFLARE_API_TOKEN) {
        throw new Error('CLOUDFLARE_API_TOKEN is not set, so no store book was published.');
    }
    if (!dryRun && !process.env.CLOUDFLARE_ACCOUNT_ID) throw new Error('CLOUDFLARE_ACCOUNT_ID is required when CLOUDFLARE_API_TOKEN is set.');
    const plan = planStoreUploads(readBuilt(root), await fetchLive(args, process.env));
    if (dryRun) {
        plan.forEach((item) => console.log(`${item.outcome}: ${item.id} built v${item.version}, live ${item.liveVersion ?? 'none'}`));
        return;
    }
    let uploaded;
    try {
        uploaded = runUploads(plan, root, { put: putToBucket, packageExists: packageInBucket });
    } catch (error) {
        writeSummary(summaryText(plan, error.done ?? []));
        throw error;
    }
    writeSummary(summaryText(plan, uploaded));
    if (plan.some((item) => item.outcome === Outcome.Invalid)) throw new Error('A built store book has no valid version in meta.json.');
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
    main().catch((error) => {
        console.error(`::error::${error instanceof Error ? error.message : String(error)}`);
        process.exitCode = 1;
    });
}
