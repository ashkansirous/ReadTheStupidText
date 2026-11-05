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
 *   --audio-behind       (Slice 265, BEFORE the build) prints `<book stem> <language>` for each audio pack in a book's audio-cast.json `audioPacks` that the live /api/stories `audio` lacks (or lists lower),
 *                        so the workflow runs `git lfs pull` for those books only and `BookBuilder build-audio` for those languages. Their zips then go up before the cover, package and meta.json,
 *                        never overwriting an existing object (the same version is the same bytes).
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
const AUDIO_PACK_FILE = /^audio-[a-z]{2,3}(?:-[a-z0-9]{2,8})?-v\d+\.zip$/;
const AUDIO_CAST_FILE = 'audio-cast.json';
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
export function runMetaRefresh(plan, root, { put, liveMeta, packageExists = () => false, audioExists = packageExists }) {
    const done = [];
    const failures = [];
    for (const book of plan.filter((item) => item.outcome === Outcome.UpToDate)) {
        try {
            putAudioPacks(book, join(root, book.id), { put, packageExists: audioExists }); // Slice 265: a pack built in this run goes up even when the meta is unchanged
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

/**
 * Pure (Slice 265): the audio pack zips a built book folder holds that its meta.json lists (`audio`: language to {version}), as uploads. Only a pack built in this run is in the folder
 * (the build step builds just the languages the live store is behind on), so a listed pack that is not there is already live and is skipped. `fileExists(path)` is injectable.
 */
export function audioUploadsFor({ id }, folder, metaText, fileExists = existsSync) {
    let audio;
    try {
        audio = JSON.parse(metaText).audio;
    } catch {
        return [];
    }
    if (!audio || typeof audio !== 'object') return [];
    return Object.keys(audio).sort().map((language) => `audio-${language}-v${audio[language]?.version}.zip`)
        .filter((name) => AUDIO_PACK_FILE.test(name) && fileExists(join(folder, name)))
        .map((name) => ({ name, key: `${KEY_PREFIX}/${id}/${name}`, path: join(folder, name), contentType: 'application/zip', isAudioPack: true }));
}

/**
 * Pure (Slice 265): whether the live /api/stories list already offers this audio pack. The Worker lists a pack only when its zip is in the bucket, so the list answers "is it there"
 * without downloading the zip (a `wrangler r2 object get` fetches the whole file). `upload.key` is `dinoreader/stories/<id>/audio-<language>-v<n>.zip`.
 */
export function audioPackIsLive(live, upload) {
    const match = /\/([0-9a-f]{32})\/(audio-.+\.zip)$/.exec(upload.key ?? '');
    const pack = match ? audioPackFileOf(match[2]) : null;
    if (!pack) return false;
    const story = live.find((item) => item.id === match[1]);
    return Number(story?.audio?.[pack.language]?.version) === pack.version;
}

function audioPackFileOf(file) {
    const match = /^audio-(.+)-v(\d+)\.zip$/.exec(file);
    return match ? { language: match[1], version: Number(match[2]) } : null;
}

/**
 * Pure (Slice 265): meta.json text with its `audio` entries limited to packs that are live (`liveAudio`: language to {version}) or whose zip was built into this folder (`fileExists`). A pack whose
 * build or LFS fetch failed is dropped, so a listed pack is always in the bucket. Returns `{ text, removed }`; `text` is unchanged when nothing is dropped or the meta is unreadable.
 */
export function pruneUnbuiltAudio(metaText, folder, liveAudio, fileExists = existsSync) {
    let meta;
    try {
        meta = JSON.parse(metaText);
    } catch {
        return { text: metaText, removed: [] };
    }
    if (!meta.audio || typeof meta.audio !== 'object') return { text: metaText, removed: [] };
    const kept = {};
    const removed = [];
    for (const [language, info] of Object.entries(meta.audio)) {
        const live = Number(liveAudio?.[language]?.version) >= Number(info?.version); // a higher live version is fine (audioPacksBehind builds nothing then)
        if (live || fileExists(join(folder, `audio-${language}-v${info?.version}.zip`))) kept[language] = info;
        else removed.push(language);
    }
    if (removed.length === 0) return { text: metaText, removed };
    const { audio, ...rest } = meta;
    return { text: `${JSON.stringify(Object.keys(kept).length > 0 ? { ...rest, audio: kept } : rest, null, 2)}\n`, removed };
}

/** Rewrites each built book's meta.json without the audio packs that are neither live nor built here (see pruneUnbuiltAudio); logs each dropped pack. */
function pruneBuiltMetas(root, built, live) {
    for (const { id } of built) {
        const path = join(root, id, META_FILE);
        if (!existsSync(path)) continue;
        const { text, removed } = pruneUnbuiltAudio(readFileSync(path, 'utf8'), join(root, id), live.find((story) => story.id === id)?.audio, existsSync);
        if (removed.length === 0) continue;
        console.warn(`warning: ${id} meta.json lists audio pack(s) ${removed.join(', ')} that are neither live nor built in this run; not listing them`);
        writeFileSync(path, text);
    }
}

/** Warns about audio zips built for a book whose outcome sends nothing (live newer or invalid), so a pack built for nothing is visible. */
function warnUnsentAudio(plan, root) {
    for (const item of plan.filter((entry) => entry.outcome === Outcome.LiveNewer || entry.outcome === Outcome.Invalid)) {
        const folder = join(root, item.id);
        const metaPath = join(folder, META_FILE);
        const zips = existsSync(metaPath) ? audioUploadsFor({ id: item.id }, folder, readFileSync(metaPath, 'utf8')) : [];
        if (zips.length > 0) console.warn(`warning: ${item.id} (${item.outcome}) has built audio pack(s) ${zips.map((zip) => zip.name).join(', ')} that are not uploaded`);
    }
}

/** Sends the audio packs of one built book that are not in the bucket yet (never overwritten: the same version is the same content), before the cover, package and meta.json. Returns the names sent. */
export function putAudioPacks(book, folder, { put, packageExists }) {
    const sent = [];
    for (const upload of audioUploadsFor(book, folder, readFileSync(join(folder, META_FILE), 'utf8'))) {
        if (packageExists(upload)) {
            console.log(`${upload.key} already exists, not overwriting`);
            continue;
        }
        put(upload);
        sent.push(upload.name);
    }
    return sent;
}

/**
 * Pure (Slice 265): the book languages whose audio pack is behind the live store, so the build step must fetch that book's clips from Git LFS and pack them. `books` is
 * `[{ stem, id, packs: { <language>: version } }]` (the committed `audioPacks` records of each book's audio-cast.json), `live` the /api/stories list with each story's `audio`
 * (only the packs whose zip is in the bucket). A language is behind when the live store lacks its pack or lists a lower version; a live higher version is left alone.
 */
export function audioPacksBehind(books, live) {
    const liveById = new Map(live.map((story) => [story.id, story.audio ?? {}]));
    return books.map(({ stem, id, packs }) => {
        const liveAudio = liveById.get(id) ?? {};
        const languages = Object.keys(packs).sort().filter((language) => !(Number(liveAudio[language]?.version) >= packs[language]));
        return { stem, languages };
    }).filter((item) => item.languages.length > 0);
}

/** Pure: the live list from the /api/stories body, as `[{ id, version }]`. Throws on a body that is not the list. */
export function liveFromApi(body) {
    if (!body || !Array.isArray(body.stories)) throw new Error('The store list has no "stories" array.');
    return body.stories.map((story) => ({ id: story.id, version: story.version, title: story.title, audio: story.audio ?? null }));
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

/** Pure: the `bytes` (positive integer) in a live meta.json text, or null (Slice 260: the size of the package that is live). */
export function bytesFromMeta(metaText) {
    if (metaText === null || metaText === undefined) return null;
    try {
        const bytes = JSON.parse(metaText).bytes;
        return Number.isInteger(bytes) && bytes > 0 ? bytes : null;
    } catch {
        return null;
    }
}

/** Pure: `{ bytes }` for the live state when the live meta carries one, else `{}` (BookBuilder then lists the size of what it built). */
function bytesEntry(metaText) {
    const bytes = bytesFromMeta(metaText);
    return bytes === null ? {} : { bytes };
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
    const entries = live.map(({ id, version }) => [id, { version, contentHash: hashFromMeta(liveMeta(id)), maxPackage: maxPackage(id, version + 1), ...bytesEntry(liveMeta(id)) }]);
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
export function runUploads(plan, root, { put, packageExists, audioExists = packageExists, hasCover = (folder) => existsSync(join(folder, COVER_FILE)) }) {
    const done = [];
    const failures = [];
    for (const book of plan.filter((item) => item.outcome === Outcome.Upload)) {
        try {
            done.push({ id: book.id, version: uploadBook(book, root, { put, packageExists, audioExists, hasCover }) });
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

function uploadBook(book, root, { put, packageExists, audioExists = packageExists, hasCover }) {
    const folder = join(root, book.id);
    if (!existsSync(join(folder, packageName(book.version)))) throw new Error(`${book.id}: meta.json says v${book.version} but ${packageName(book.version)} is missing.`);
    putAudioPacks(book, folder, { put, packageExists: audioExists }); // Slice 265: the packs go up before the cover, package and meta.json (meta last, so a listed pack is always there)
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

/** The committed audio pack records of each content book, `[{ stem, id, packs: { language: version } }]` (books without an audio-cast.json or without records are left out). */
function readAudioRecords() {
    return readContentBooks().map((book) => {
        const castPath = join(CONTENT_ROOT, book.stem, AUDIO_CAST_FILE);
        return { stem: book.stem, id: book.id, packs: validPackVersions(recordsOf(castPath, book.stem)) };
    }).filter((book) => Object.keys(book.packs).length > 0);
}

/** The `audioPacks` records of one audio-cast.json; an unreadable file is warned about and skipped so one bad book does not stop the others. */
function recordsOf(castPath, stem) {
    if (!existsSync(castPath)) return {};
    try {
        return JSON.parse(readFileSync(castPath, 'utf8')).audioPacks ?? {};
    } catch (error) {
        console.warn(`warning: ${stem} ${AUDIO_CAST_FILE} is unreadable (${error.message}); no audio pack is built for it`);
        return {};
    }
}

/** Pure: `{ language: version }` for the records whose version is a positive integer (a missing or malformed one would read as NaN, forever behind). */
export function validPackVersions(records) {
    const entries = Object.entries(records ?? {}).map(([language, record]) => [language, Number(record?.version)]);
    return Object.fromEntries(entries.filter(([, version]) => Number.isInteger(version) && version > 0));
}

/** `--audio-behind`: prints one `<stem> <language>` line for each audio pack the live store lacks, so dino-store.yml fetches only those books' clips from Git LFS and packs them. */
async function printAudioBehind(args) {
    audioPacksBehind(readAudioRecords(), await fetchLive(args, process.env)).forEach(({ stem, languages }) => languages.forEach((language) => console.log(`${stem} ${language}`)));
}

async function main() {
    const args = process.argv.slice(2);
    if (args.includes('--check-content')) return checkContent();
    if (args.includes('--live-state')) return writeLiveState(args);
    if (args.includes('--audio-behind')) return printAudioBehind(args);
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
    pruneBuiltMetas(root, built, live); // also on --dry-run (it only edits the local built folder), so the plan matches the real run
    const plan = planStoreUploads(built, live);
    const deletions = args.includes('--delist') ? planDeletions(built, live, { contentIds: new Set(readContentBooks().map((book) => book.id)), isManaged: isManagedStory }) : [];
    if (dryRun) {
        plan.forEach((item) => console.log(`${item.outcome}: ${item.id} built v${item.version}, live ${item.liveVersion ?? 'none'}`));
        deletions.forEach((story) => console.log(`delist: ${story.id} (${story.title ?? 'untitled'}), no content folder`));
        return;
    }
    warnUnsentAudio(plan, root);
    checkDeletionLimit(deletions, live);
    const audioExists = (upload) => audioPackIsLive(live, upload); // Slice 265: the live list says which packs are in the bucket; no zip is downloaded to ask
    let uploaded = [];
    let refreshed = [];
    let delisted = [];
    const errors = [];
    for (const [step, assign] of [
        [() => runUploads(plan, root, { put: putToBucket, packageExists: packageInBucket, audioExists: audioExists }), (done) => { uploaded = done; }],
        [() => runMetaRefresh(plan, root, { put: putToBucket, liveMeta: metaInBucket, packageExists: packageInBucket, audioExists: audioExists }), (done) => { refreshed = done; }],
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
