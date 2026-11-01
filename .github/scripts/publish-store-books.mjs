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
 * Slice 192: a book at the SAME version as live also gets its `meta.json` (only that file, never a package) uploaded when
 * the built one differs in content from the one in the bucket, because store metadata (credit lines) is listing data, not
 * book content, and must not cost every device a re-download through a version bump.
 *
 * Slice 239: adding, changing or deleting a store story needs no version stamp and no app build.
 *   --live-state <file>  (BEFORE the build) writes the live store's `{ <id>: { version, contentHash } }` (hash from each live meta.json, null for a book published
 *                        before the hash existed), which `BookBuilder build --live <file>` turns into versions: unchanged hash = the live version (nothing to upload),
 *                        changed or unhashed = max(book.json version, 1 + the highest of live and every story-v<n>.dino found in the bucket), so an existing package is never reused
 *                        or overwritten; meta.json goes up last, so a half-finished run leaves live meta behind its content and the next run publishes at a new version.
 *                        wrangler cannot list a bucket, so the packages are found by probing upward from the live version until PROBE_WINDOW versions in a row are absent.
 *   --check-content      (FIRST step) fails when a content folder's book.json kind is Starter but the folder is missing from dino-starters.json (a new starter must build the app), or the reverse.
 *   Package versions are never reused: right before uploading story-v<n>.dino of a hashed (pipeline-managed) book, the bucket is asked whether that exact object exists; if it does the built package
 *   is renamed to n+1 and meta.json rewritten (up to MAX_VERSION_BUMPS, then the book fails and no meta goes up). This makes the probe window's gap harmless. Only after that the package, then meta.json LAST.
 *   --delist             (dino-store.yml passes it on every run; the guards below make that safe) a live story whose content folder is gone loses its listing, i.e. its meta.json only (packages stay for installed copies).
 *                        Only stories this pipeline manages (the live meta.json carries a contentHash) and whose id has no content folder at all (an invalid book is skipped, not delisted) are delisted.
 *                        The ids are logged first (the dry-run line), then deleted; more than MAX_DELETIONS ids, or half the live list, is refused (a broken checkout, not a deletion).
 *
 * Usage: node publish-store-books.mjs [--dry-run] [--verify] [--delist] [--check-content] [--live-state <file>] [--root <store folder>] [--live-file <api json>]
 *   (no flag)   upload what is behind (with --delist, then delist what has no content folder); fails when CLOUDFLARE_API_TOKEN is unset
 *   --dry-run   print the plan, upload nothing, need no token
 *   --verify    fail when any built book is still behind the live list (retried: the list is cached ~60 s)
 * Env: STORE_API_URL (default https://dinoreader.sirous.uk/api/stories), CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID.
 */

import { appendFileSync, existsSync, mkdtempSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { BUCKET_NAME, publishedStateFrom, wranglerArgs, wranglerCommand, wranglerDeleteArgs } from './r2-wrangler.mjs';

export const STORE_API_URL = 'https://dinoreader.sirous.uk/api/stories';
const KEY_PREFIX = 'dinoreader/stories';
const META_FILE = 'meta.json';
const COVER_FILE = 'cover.jpg';
const STORY_ID_PATTERN = /^[0-9a-f]{32}$/;
const VERIFY_ATTEMPTS = 7;
const FETCH_TIMEOUT_MS = 30000;
const VERIFY_WAIT_MS = 20000;
/** More delistings than this in one run is refused (Slice 239): it means the build saw too few content folders. */
export const MAX_DELETIONS = 3;
/** Packages are probed upward until this many versions in a row are absent (wrangler cannot list a bucket). */
export const PROBE_WINDOW = 3;
/** Right before an upload, a package version found already in the bucket is moved up this many times at most (the probe can miss a package; a version is never reused). */
export const MAX_VERSION_BUMPS = 20;
const CONTENT_ROOT = 'content/dino';
const STARTERS_FILE = join(fileURLToPath(new URL('.', import.meta.url)), 'dino-starters.json');
const BOOK_FILE = 'book.json';
const STARTER_KIND = 'starter';
const STORE_KIND = 'store';

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

/** Pure: the meta.json upload alone, for a book whose version is already live. */
export function metaUpload({ id }, folder) {
    return { name: META_FILE, key: `${KEY_PREFIX}/${id}/${META_FILE}`, path: join(folder, META_FILE), contentType: 'application/json' };
}

function canonical(value) {
    if (Array.isArray(value)) return value.map(canonical);
    if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonical(value[key])]));
    return value;
}

/** Pure: whether the built meta.json text differs in content (key order and whitespace ignored) from the live one; a missing or unreadable live meta counts as different. */
export function metaDiffers(builtText, liveText) {
    if (liveText === null || liveText === undefined) return true;
    try {
        return JSON.stringify(canonical(JSON.parse(builtText))) !== JSON.stringify(canonical(JSON.parse(liveText)));
    } catch {
        return true;
    }
}

/** Pure: whether the bucket's meta.json text is absent or has exactly the built version (a newer or unreadable one must never be overwritten by an older run). */
export function bucketVersionMatches(liveText, builtVersion) {
    if (liveText === null || liveText === undefined) return true;
    try {
        return JSON.parse(liveText).version === builtVersion;
    } catch {
        return false;
    }
}

/**
 * Re-uploads meta.json alone for each book whose built version equals the live one but whose built meta differs from the
 * bucket's. `liveMeta(upload)` returns the bucket's meta.json text, or null when there is none. A failing book is skipped, the
 * run then throws listing them (the error carries `done`). Returns the ids refreshed.
 */
export function runMetaRefresh(plan, root, { put, liveMeta }) {
    const done = [];
    const failures = [];
    for (const book of plan.filter((item) => item.outcome === Outcome.UpToDate)) {
        try {
            const upload = metaUpload(book, join(root, book.id));
            const live = liveMeta(upload);
            if (!metaDiffers(readFileSync(upload.path, 'utf8'), live)) continue;
            if (!bucketVersionMatches(live, book.version)) {
                console.warn(`warning: ${book.id} bucket meta.json is not v${book.version}; not refreshing it`);
                continue;
            }
            put(upload);
            done.push(book.id);
        } catch (error) {
            failures.push(`${book.id}: ${error instanceof Error ? error.message : String(error)}`);
        }
    }
    if (failures.length > 0) throw Object.assign(new Error(`${failures.length} store meta.json refresh(es) failed: ${failures.join('; ')}`), { done });
    return done;
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
    return body.stories.map((story) => ({ id: story.id, version: story.version, title: story.title }));
}

/** Pure: the contentHash in a live meta.json text, or null (no meta, unreadable, or published before the hash existed). */
export function hashFromMeta(metaText) {
    if (metaText === null || metaText === undefined) return null;
    try {
        const hash = JSON.parse(metaText).contentHash;
        return typeof hash === 'string' && hash.length > 0 ? hash : null;
    } catch {
        return null;
    }
}

/** Pure: the highest package version that exists, probing `exists(version)` upward from `from` until `window` versions in a row are absent; 0 when none. */
export function highestPackage(exists, from, window = PROBE_WINDOW) {
    let highest = 0;
    for (let version = from, misses = 0; misses < window; version++) {
        if (exists(version)) {
            highest = version;
            misses = 0;
        } else {
            misses++;
        }
    }
    return highest;
}

/**
 * Pure: `{ <id>: { version, contentHash, maxPackage } }` for the live list plus `extraIds` (store books that are not live: version 0, so a delisted book's old packages are found too).
 * `liveMeta(id)` returns that story's meta.json text or null; `maxPackage(id, from)` the highest story-v<n>.dino in the bucket at or above `from`.
 */
export function liveStateOf(live, liveMeta, maxPackage = () => 0, extraIds = []) {
    const entries = live.map(({ id, version }) => [id, { version, contentHash: hashFromMeta(liveMeta(id)), maxPackage: maxPackage(id, version + 1) }]);
    const liveIds = new Set(live.map((story) => story.id));
    const extra = extraIds.filter((id) => !liveIds.has(id)).map((id) => [id, { version: 0, contentHash: null, maxPackage: maxPackage(id, 1) }]);
    return Object.fromEntries([...entries, ...extra]);
}

/** Pure: each content folder as `{ stem, id, kind }` (kind lower-cased) from `readBook(stem)`'s parsed book.json. */
export function contentBooks(stems, readBook) {
    return stems.map((stem) => {
        const info = readBook(stem);
        return { stem, id: info.id, kind: String(info.kind ?? '').toLowerCase() };
    });
}

/** Pure: the drift between the content folders' kinds and dino-starters.json (a list of stems); empty when they agree. */
export function starterDrift(books, starters) {
    const problems = [];
    const byStem = new Map(books.map((book) => [book.stem, book]));
    for (const book of books) {
        if (book.kind === STARTER_KIND && !starters.includes(book.stem)) problems.push(`${book.stem} is a Starter in ${BOOK_FILE} but missing from .github/scripts/dino-starters.json (a new starter must build the app: add it there and to the dinoreader.yml paths)`);
        if (book.kind !== STARTER_KIND && book.kind !== STORE_KIND) problems.push(`${book.stem}/${BOOK_FILE} has an unknown kind "${book.kind}"`);
    }
    for (const stem of starters) {
        const book = byStem.get(stem);
        if (!book) problems.push(`dino-starters.json lists ${stem} but there is no content/dino/${stem} folder`);
        else if (book.kind !== STARTER_KIND) problems.push(`dino-starters.json lists ${stem} but its ${BOOK_FILE} kind is ${book.kind}`);
    }
    return problems;
}

function readContentBooks() {
    const stems = readdirSync(CONTENT_ROOT, { withFileTypes: true }).filter((entry) => entry.isDirectory() && existsSync(join(CONTENT_ROOT, entry.name, BOOK_FILE))).map((entry) => entry.name);
    return contentBooks(stems, (stem) => JSON.parse(readFileSync(join(CONTENT_ROOT, stem, BOOK_FILE), 'utf8')));
}

function checkContent() {
    const problems = starterDrift(readContentBooks(), JSON.parse(readFileSync(STARTERS_FILE, 'utf8')));
    if (problems.length > 0) throw new Error(`The content folders and dino-starters.json disagree: ${problems.join(' | ')}`);
    console.log('The content folders and dino-starters.json agree.');
}

/**
 * Pure: the live stories this pipeline manages whose content folder is gone: no built folder, no content folder of that id (`contentIds`: a book that is only invalid
 * has a folder and is not delisted) and `isManaged(id)` (its live meta.json carries a contentHash; a hand-uploaded or older story has none and is left alone).
 */
export function planDeletions(built, live, { contentIds = new Set(), isManaged = () => true } = {}) {
    const builtIds = new Set(built.map((book) => book.id));
    return live.filter((story) => STORY_ID_PATTERN.test(story.id ?? '') && !builtIds.has(story.id) && !contentIds.has(story.id) && isManaged(story.id));
}

/** Pure: throws when a run would delist too many stories at once (more than MAX_DELETIONS, or half of the live list). */
export function checkDeletionLimit(deletions, live) {
    if (deletions.length > MAX_DELETIONS || (deletions.length > 0 && deletions.length * 2 > live.length)) {
        throw new Error(`Refusing to delist ${deletions.length} of ${live.length} live stories (${deletions.map((d) => d.id).join(', ')}): the build probably saw too few content folders. Nothing was delisted.`);
    }
}

/** Pure: the one object a delisting removes: meta.json (the Worker lists a folder only with it); the packages stay for installed copies. */
export function delistKey(id) {
    return `${KEY_PREFIX}/${id}/${META_FILE}`;
}

/** Logs the plan first (the dry-run line of each id), then delists each with `remove(key)`. A failure is reported after the others ran. Returns the ids delisted. */
export function runDeletions(deletions, { remove }) {
    deletions.forEach((story) => console.log(`delist: ${story.id} (${story.title ?? 'untitled'}, live v${story.version}) has no content folder; removing its meta.json, keeping its packages`));
    const done = [];
    const failures = [];
    for (const story of deletions) {
        try {
            remove(delistKey(story.id));
            done.push(story.id);
        } catch (error) {
            failures.push(`${story.id}: ${error instanceof Error ? error.message : String(error)}`);
        }
    }
    if (failures.length > 0) throw Object.assign(new Error(`${failures.length} delisting(s) failed: ${failures.join('; ')}`), { done });
    return done;
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
            done.push({ id: book.id, version: uploadBook(book, root, { put, packageExists, hasCover }) });
        } catch (error) {
            failures.push(`${book.id}: ${error instanceof Error ? error.message : String(error)}`);
        }
    }
    if (failures.length > 0) throw Object.assign(new Error(`${failures.length} store book(s) failed to upload: ${failures.join('; ')}`), { done });
    return done;
}

/** Moves a built folder to another version: the package is renamed and meta.json gets the new version. */
function moveToVersion(folder, from, to) {
    renameSync(join(folder, packageName(from)), join(folder, packageName(to)));
    const metaPath = join(folder, META_FILE);
    writeFileSync(metaPath, `${JSON.stringify({ ...JSON.parse(readFileSync(metaPath, 'utf8')), version: to }, null, 2)}\n`);
}

/**
 * Right before the upload: the version this book goes up as. A hashed (pipeline-managed) book never reuses a package version: while `story-v<n>.dino` exists in the bucket the built
 * folder moves to n+1 (at most MAX_VERSION_BUMPS, then it throws). A book without a hash (a starter, versioned by its stamp) keeps its version, and an existing package is then not sent again.
 * Returns `{ version, packageThere }`.
 */
function settleVersion(book, folder, packageExists) {
    const managed = hashFromMeta(readFileSync(join(folder, META_FILE), 'utf8')) !== null;
    let version = book.version;
    for (let bumps = 0; ; bumps++) {
        if (!packageExists({ key: `${KEY_PREFIX}/${book.id}/${packageName(version)}` })) return { version, packageThere: false };
        if (!managed) return { version, packageThere: true };
        if (bumps >= MAX_VERSION_BUMPS) throw new Error(`${book.id}: ${MAX_VERSION_BUMPS} package versions above v${book.version} already exist in the bucket; nothing was uploaded.`);
        console.log(`${packageName(version)} of ${book.id} already exists; building as v${version + 1}`);
        moveToVersion(folder, version, version + 1);
        version++;
    }
}

function uploadBook(book, root, { put, packageExists, hasCover }) {
    const folder = join(root, book.id);
    if (!existsSync(join(folder, packageName(book.version)))) throw new Error(`${book.id}: meta.json says v${book.version} but ${packageName(book.version)} is missing.`);
    const { version, packageThere } = settleVersion(book, folder, packageExists);
    for (const upload of uploadsFor({ id: book.id, version }, folder, hasCover(folder))) {
        if (upload.isPackage && packageThere) {
            console.log(`${upload.key} already exists, not overwriting`);
            continue;
        }
        put(upload);
    }
    return version;
}

/** Pure: the step-summary markdown. */
export function summaryText(plan, uploaded, metaRefreshed = [], delisted = []) {
    const lines = ['### Dino store books'];
    const count = (outcome) => plan.filter((item) => item.outcome === outcome).length;
    lines.push(`${plan.length} built: ${uploaded.length} uploaded, ${count(Outcome.UpToDate)} up to date, ${count(Outcome.LiveNewer)} live newer (skipped), ${count(Outcome.Invalid)} invalid.`);
    uploaded.forEach((book) => lines.push(`- uploaded ${book.id} v${book.version}`));
    metaRefreshed.forEach((id) => lines.push(`- refreshed meta.json of ${id} (same version, changed listing data)`));
    delisted.forEach((id) => lines.push(`- delisted ${id} (content folder deleted; packages kept)`));
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

function removeFromBucket(key) {
    console.log(`Deleting ${BUCKET_NAME}/${key}`);
    const result = runWrangler(wranglerDeleteArgs(key));
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`wrangler delete of ${key} exited with status ${result.status}.`);
}

function metaInBucket(upload) {
    const folder = mkdtempSync(join(tmpdir(), 'store-meta-'));
    try {
        const file = join(folder, 'live-meta.json');
        const result = runWrangler(['r2', 'object', 'get', `${BUCKET_NAME}/${upload.key}`, '--remote', '--file', file], true);
        if (result.error) throw result.error;
        return publishedStateFrom({ status: result.status, output: `${result.stdout}${result.stderr}` }) ? readFileSync(file, 'utf8') : null;
    } finally {
        rmSync(folder, { recursive: true, force: true });
    }
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

function isManagedStory(id) {
    const managed = hashFromMeta(metaInBucket({ key: `${KEY_PREFIX}/${id}/${META_FILE}` })) !== null;
    if (!managed) console.log(`not delisting ${id}: its live meta.json has no contentHash (not published by this pipeline)`);
    return managed;
}

function packageProbe(id, from) {
    return highestPackage((version) => packageInBucket({ key: `${KEY_PREFIX}/${id}/${packageName(version)}` }), from);
}

async function writeLiveState(args) {
    const target = args[args.indexOf('--live-state') + 1];
    const storeIds = readContentBooks().filter((book) => book.kind === STORE_KIND).map((book) => book.id);
    const state = liveStateOf(await fetchLive(args, process.env), (id) => metaInBucket({ key: `${KEY_PREFIX}/${id}/${META_FILE}` }), packageProbe, storeIds);
    writeFileSync(target, JSON.stringify(state, null, 2));
    console.log(`Live store state of ${Object.keys(state).length} stories written to ${target}`);
}

async function main() {
    const args = process.argv.slice(2);
    if (args.includes('--check-content')) return checkContent();
    if (args.includes('--live-state')) return writeLiveState(args);
    const rootFlag = args.indexOf('--root');
    const root = resolve(rootFlag === -1 ? 'artifacts/store' : args[rootFlag + 1]);
    if (args.includes('--verify')) return verify(args, root);
    const dryRun = args.includes('--dry-run');
    if (!dryRun && !process.env.CLOUDFLARE_API_TOKEN) {
        throw new Error('CLOUDFLARE_API_TOKEN is not set, so no store book was published.');
    }
    if (!dryRun && !process.env.CLOUDFLARE_ACCOUNT_ID) throw new Error('CLOUDFLARE_ACCOUNT_ID is required when CLOUDFLARE_API_TOKEN is set.');
    const built = readBuilt(root);
    const live = await fetchLive(args, process.env);
    const plan = planStoreUploads(built, live);
    const deletions = args.includes('--delist') ? planDeletions(built, live, { contentIds: new Set(readContentBooks().map((book) => book.id)), isManaged: isManagedStory }) : [];
    if (dryRun) {
        plan.forEach((item) => console.log(`${item.outcome}: ${item.id} built v${item.version}, live ${item.liveVersion ?? 'none'}`));
        deletions.forEach((story) => console.log(`delist: ${story.id} (${story.title ?? 'untitled'}), no content folder`));
        return;
    }
    checkDeletionLimit(deletions, live);
    let uploaded = [];
    let refreshed = [];
    let delisted = [];
    const errors = [];
    for (const [step, assign] of [
        [() => runUploads(plan, root, { put: putToBucket, packageExists: packageInBucket }), (done) => { uploaded = done; }],
        [() => runMetaRefresh(plan, root, { put: putToBucket, liveMeta: metaInBucket }), (done) => { refreshed = done; }],
        [() => runDeletions(deletions, { remove: removeFromBucket }), (done) => { delisted = done; }],
    ]) {
        try {
            assign(step());
        } catch (error) {
            assign(error.done ?? []);
            errors.push(error);
        }
    }
    writeSummary(summaryText(plan, uploaded, refreshed, delisted));
    if (plan.some((item) => item.outcome === Outcome.Invalid)) errors.push(new Error('A built store book has no valid version in meta.json.'));
    if (errors.length > 0) throw new Error(errors.map((error) => error.message).join(' | '));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
    main().catch((error) => {
        console.error(`::error::${error instanceof Error ? error.message : String(error)}`);
        process.exitCode = 1;
    });
}
