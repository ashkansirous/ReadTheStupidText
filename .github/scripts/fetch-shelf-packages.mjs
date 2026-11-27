#!/usr/bin/env node
/**
 * Slice 404, Decision 5: fetches the book packages the store upload ALREADY made, for the full-shelf test hook of the store pictures. Never builds one.
 *
 * `node .github/scripts/fetch-shelf-packages.mjs <out folder>` reads the live store list (`/api/stories`, public), and for each book of
 * `tools/dino/full-shelf-books.json` that the list sells as paid (a free book is downloaded by the app from the live store itself) reads its stored package
 * `dinoreader/stories/<id>/story-v<n>.dino` from the downloads bucket with `wrangler r2 object get --remote` (the same read `publish-store-books.mjs` does; it needs CLOUDFLARE_API_TOKEN and
 * CLOUDFLARE_ACCOUNT_ID, or a wrangler login on this PC) and writes it to `<out folder>/<id>/story-v<n>.dino`. A package already there is kept only when its size equals the list's `bytes`; a download goes to `<file>.part` and is renamed on success. Exit 1, naming the book, when a chosen book is not in the list or its package cannot be read (each wrangler call has a time limit).
 */

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { BUCKET_NAME, wranglerCommand } from './r2-wrangler.mjs';

const DEFAULT_LIST_URL = 'https://dinoreader.sirous.uk/api/stories';
const PAID = 'paid';
const INCLUDE_FREE_FLAG = '--include-free'; // Slice 434: the Windows VM is offline, so its full shelf needs the free books' packages too
const LISTING_FILE = 'shelf-listing.json'; // Slice 434: id -> { min, max, access, productId, unlockedBy } from the live list, next to the package folders; TestStore.ShelfListingOf reads its copy in the guest
const WRANGLER_TIMEOUT_MS = 4 * 60 * 1000;
const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const BOOKS_FILE = join(REPO_ROOT, 'tools', 'dino', 'full-shelf-books.json');

/** Pure: the packages to fetch, one `{ id, title, version, key }` per chosen book the list sells as paid; `missing` names chosen ids the list does not hold (the hook skips them too). */
export function packagesToFetch(list, chosen, includeFree = false) {
    const byId = new Map((list.stories ?? []).map((story) => [story.id, story]));
    const packages = [];
    const missing = [];
    for (const book of chosen) {
        const story = byId.get(book.id);
        if (!story) missing.push(book.id);
        else if (includeFree || story.access === PAID) packages.push({ id: book.id, title: story.title, version: story.version, bytes: story.bytes, key: `dinoreader/stories/${book.id}/story-v${story.version}.dino` });
    }
    return { packages, missing };
}

/** Pure: the sale fields `StoreClient`'s list parser reads (`access`, `productId`, `unlockedBy`), kept only when well formed; a free or older-Worker line has none. */
function saleOf(story) {
    if (story.access !== PAID) return {};
    const sale = { access: PAID };
    if (typeof story.productId === 'string' && story.productId.trim()) sale.productId = story.productId.trim();
    if (Array.isArray(story.unlockedBy)) sale.unlockedBy = story.unlockedBy.filter((product) => typeof product === 'string' && product.trim());
    return sale;
}

/**
 * Pure (Slice 434): what the live list says about each fetched book, `{ <id>: { min?, max?, access?, productId?, unlockedBy? } }`: the age range (only when complete) and, for a paid book, the sale fields the app's own list parser
 * reads, so the offline VM's test store shows each card as the live store does (price, subscription lock, or Free). The list carries no price: the platform store supplies it.
 */
export function shelfListingOf(list, packages) {
    const byId = new Map((list.stories ?? []).map((story) => [story.id, story]));
    const listing = {};
    for (const { id } of packages) {
        const story = byId.get(id) ?? {};
        const entry = { ...saleOf(story) };
        if (Number.isInteger(story.ageMin) && Number.isInteger(story.ageMax)) {
            entry.min = story.ageMin;
            entry.max = story.ageMax;
        }
        if (Object.keys(entry).length > 0) listing[id] = entry;
    }
    return listing;
}

/** Pure: a package already on disk is reused only when its size is exactly the live list's byte count for the book (a partial or stale file never is). */
export function isReusable(fileSize, listedBytes) {
    return Number.isInteger(listedBytes) && listedBytes > 0 && fileSize === listedBytes;
}

/** Pure: one line for a `::error::` annotation (newlines would end it), the tail of what wrangler printed. */
export function oneLine(text) {
    return text.replace(/\s+/g, ' ').trim().slice(-300);
}

function fetchPackage(item, outFolder) {
    const target = join(outFolder, item.id, `story-v${item.version}.dino`);
    if (existsSync(target) && isReusable(statSync(target).size, item.bytes)) {
        console.log(`kept ${target}`);
        return;
    }
    mkdirSync(dirname(target), { recursive: true });
    const partial = `${target}.part`;
    rmSync(partial, { force: true });
    const { command, args } = wranglerCommand(['r2', 'object', 'get', `${BUCKET_NAME}/${item.key}`, '--remote', '--file', partial]);
    const result = spawnSync(command, args, { encoding: 'utf8', env: process.env, maxBuffer: 1024 * 1024, timeout: WRANGLER_TIMEOUT_MS });
    if (result.error || result.status !== 0) {
        rmSync(partial, { force: true });
        const why = result.error ? result.error.message : `wrangler exited ${result.status}`;
        throw new Error(`the stored package of "${item.title}" (${item.key}) could not be read (${why}): ${oneLine(`${result.stdout ?? ''}${result.stderr ?? ''}`)}`);
    }
    rmSync(target, { force: true });
    renameSync(partial, target);
    console.log(`fetched ${item.key} -> ${target}`);
}

async function main() {
    const outFolder = process.argv[2];
    if (!outFolder) throw new Error(`usage: node fetch-shelf-packages.mjs <out folder> [${INCLUDE_FREE_FLAG}]`);
    const includeFree = process.argv.includes(INCLUDE_FREE_FLAG);
    const response = await fetch(process.env.DINO_STORIES_URL ?? DEFAULT_LIST_URL);
    if (!response.ok) throw new Error(`the live store list answered ${response.status}`);
    const chosen = JSON.parse(readFileSync(BOOKS_FILE, 'utf8')).books;
    const list = await response.json();
    const { packages, missing } = packagesToFetch(list, chosen, includeFree);
    if (missing.length > 0) throw new Error(`chosen book(s) not in the live store list (${DEFAULT_LIST_URL}), so the shelf would have fewer than 12 books: ${missing.join(', ')}`);
    for (const item of packages) fetchPackage(item, resolve(outFolder));
    writeFileSync(join(resolve(outFolder), LISTING_FILE), JSON.stringify(shelfListingOf(list, packages), null, 2));
    console.log(`${packages.length} package(s)${includeFree ? ' (paid and free)' : ' (paid only)'} ready in ${resolve(outFolder)}`);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
    main().catch((error) => {
        console.error(`::error::${error.message.replace(/\s+/g, ' ').trim()}`);
        process.exit(1);
    });
}
