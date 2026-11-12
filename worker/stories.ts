// The free Dino Reader story store (Slice 92, Decisions 94-99). Stories are uploaded by hand into the
// `DOWNLOADS` R2 bucket (layout in download-keys.mjs: dinoreader/stories/<id>/{meta.json, cover.jpg,
// story-v<n>.dino}). This answers:
//   GET /api/stories                  the list of complete stories (60 s listing cache)
//   GET /api/stories/<id>/cover       the cover picture
//   GET /api/stories/<id>/download    the newest package; add ?v=<n> for that version, which is cached for a day
//   GET /api/stories/<id>/audio/<lang>?v=<n>   Slice 246 B2: the audio pack of that book language (audio-<lang>-v<n>.zip), only when meta.json's `audio` lists exactly
//                                     that language and version and the zip is in the folder; a missing or different ?v= is a 404. Immutable per version, cached for a day
//   GET /api/stories/<id>/listen/<lang>?v=<n>   Slice 281: the one listening track of that book language (listen-<lang>-v<n>.m4a, audio/mp4, inline, Range for smart speakers), only when meta.json's
//                                     `audio` lists that language at that version WITH `listenSeconds` and the .m4a is in the folder; same 404 rules as the audio route
// A folder counts only when its meta.json and at least one package are both there (meta.json goes up
// last, the APK-manifest convention), so a half upload is never offered. Nothing about the caller is
// read or stored. Only Dino Reader's host and its /dino/ prefix on workers.dev route here (index.ts).
// This file imports values only from download-keys.mjs, and takes the byte streamer as a parameter, so
// worker/downloads.test.mjs can run it under node's type stripping (an extensionless import of
// downloads.ts would not resolve there). Keep it free of enums and parameter properties.

import {
  JSON_CONTENT_TYPE,
  LISTING_CACHE_MS,
  LIST_PAGE_SIZE,
  MAX_LIST_PAGES,
  NO_CACHE,
  STORIES_PREFIX,
  STORY_COVER_FILE,
  STORY_ID_PATTERN,
  STORY_META_FILE,
  VERSIONED_CACHE,
  storyCoverKey,
  storyFolder,
  storyMetaKey,
  storyPackageFileName,
  storyPackageKey,
} from "./download-keys.mjs";
import type { ServedFile } from "./downloads";
import { GateAnswer } from "./entitlements/gate.ts";
import type { PaidGate } from "./entitlements/gate.ts";
import { PROOF_HEADER } from "./entitlements/proof.ts";
import { isProductId, productsUnlocking } from "./products.ts";

export const STORIES_API_PATH = "/api/stories";
const STORY_PATH_PREFIX = "/api/stories/";
const COVER_SEGMENT = "cover";
const DOWNLOAD_SEGMENT = "download";
const AUDIO_SEGMENT = "audio";
const LISTEN_SEGMENT = "listen";
const VERSION_PARAM = "v";

/** The list response format; the app reads `stories` and ignores fields it does not know. */
export const LIST_SCHEMA_VERSION = 1;

const JPEG_CONTENT_TYPE = "image/jpeg";
const PACKAGE_CONTENT_TYPE = "application/octet-stream";
const AUDIO_CONTENT_TYPE = "application/zip";
const LISTEN_CONTENT_TYPE = "audio/mp4";
const LIST_CACHE_CONTROL = "public, max-age=60";
const COVER_CACHE = "public, max-age=3600";
const META_READ_BATCH = 6;
const MAX_STORIES = 200;
const MAX_TITLE_LENGTH = 200;
const MAX_LANGUAGE_LENGTH = 12;
const MAX_CREDITS_LENGTH = 200;
const MAX_AUTHOR_LENGTH = 100;
const MAX_AGE = 18;
const MAX_TITLE_LANGUAGES = 20;
const LANGUAGE_CODE = /^[a-z]{2,3}(-[a-z0-9]{2,8})?$/;
const ALLOWED_METHODS = "GET, HEAD";
const PACKAGE_FILE = /^story-v(\d+)\.dino$/;
const AUDIO_FILE = /^audio-([a-z]{2,3}(?:-[a-z0-9]{2,8})?)-v(\d+)\.zip$/;
const LISTEN_FILE = /^listen-([a-z]{2,3}(?:-[a-z0-9]{2,8})?)-v(\d+)\.m4a$/;
const MAX_AUDIO_LANGUAGES = 40;
const MAX_LISTEN_SECONDS = 86_400;
const MAX_AUDIO_BYTES = 2_000_000_000;
const POSITIVE_INTEGER = /^[1-9]\d{0,8}$/;

/** Slice 260: the closed set of store tags, in the order a card shows them. Parsing is case-sensitive. */
export const STORE_TAGS = ["Hot", "Popular", "Recommended", "New"] as const;
export type StoreTag = (typeof STORE_TAGS)[number];

/** Slice 346: whether a book is sold. Closed set; a meta without `access` (or with anything but "free") is paid, as a book's `book.json` is. */
export const BOOK_ACCESS = ["free", "paid"] as const;
export type BookAccess = (typeof BOOK_ACCESS)[number];

const ErrorCode = {
  Unavailable: "stories-unavailable",
  NotFound: "not-found",
  StoryUnavailable: "story-unavailable",
  PaymentRequired: "payment-required",
  PurchaseNotValid: "purchase-not-valid",
  CannotVerify: "cannot-verify",
  RateLimited: "rate-limited",
} as const;

const HTTP_PAYMENT_REQUIRED = 402;
const HTTP_FORBIDDEN = 403;

/** What `meta.json` holds, checked. `schemaVersion` is the `.dino` format the package is in. */
export interface StoryMeta {
  readonly schemaVersion: number;
  readonly title: string;
  readonly language: string;
  readonly version: number | null;
  readonly pageCount: number | null;
  readonly minutes: number | null;
  readonly added: string | null;
  /** The optional one-line credit the store card shows ("Beatrix Potter, 1902"); older entries have none. */
  readonly credits: string | null;
  /** Slice 112: the age range the story suits (both set or both null; integers 0-18, min <= max). */
  readonly ageMin: number | null;
  readonly ageMax: number | null;
  /** Slice 112: the author the store filters by ("Beatrix Potter"). */
  readonly author: string | null;
  /** Slice 159: the title per language code ("fa": "..."), the store builder's copy of the book's bundled translations; null when the entry has none. */
  readonly titles: Readonly<Record<string, string>> | null;
  /** Slice 192: the credit line per language code, the store builder's copy of the book's `creditsByLanguage`; null when the entry has none (the card then shows `credits`). */
  readonly creditsByLanguage: Readonly<Record<string, string>> | null;
  /** Slice 246 B2: the audio pack each book language has (pack version and zip size), the store builder's copy of the book's pack record; null when the book has none. Old apps ignore it. */
  readonly audio: Readonly<Record<string, AudioPackInfo>> | null;
  /** Slice 260: the store tags the user set in the book's `book.json`, only the four known names, deduplicated, in list order (Hot, Popular, Recommended, New); empty when none. */
  readonly tags: readonly StoreTag[];
  /** Slice 260: the size of the `story-v<n>.dino` package in bytes; null when absent or not a positive integer. */
  readonly bytes: number | null;
  /** Slice 346: free or paid, and the store product id of a paid book (null when it has none). */
  readonly access: BookAccess;
  readonly productId: string | null;
}

/** One audio pack of a book language: `audio-<lang>-v<version>.zip`; `bytes` (null when unknown) lets the app show a size. */
export interface AudioPackInfo {
  readonly version: number;
  readonly bytes: number | null;
  /** Slice 281: the length in whole seconds of the book's `listen-<lang>-v<version>.m4a` track; the key is absent when the book has no track (or, on an offered entry, when the track is not in the folder). */
  readonly listenSeconds?: number;
}

/** One complete story: its checked meta, the package version it offers, and whether a cover was uploaded. */
export interface StoryEntry {
  readonly id: string;
  readonly meta: StoryMeta;
  readonly version: number;
  readonly hasCover: boolean;
  /** The audio packs meta.json lists AND the folder holds (a listed pack that was never uploaded is not offered); absent or null = none. */
  readonly audio?: Readonly<Record<string, AudioPackInfo>> | null;
}

interface Folder {
  hasMeta: boolean;
  hasCover: boolean;
  versions: number[];
  audio: Record<string, number[]>;
  /** Slice 281: the versions of each language's `listen-<lang>-v<n>.m4a` track in the folder. */
  listen: Record<string, number[]>;
}

/** The `<id>` and file name of a key under the stories prefix, or null for any other key. */
export function parseStoryKey(key: string): { id: string; file: string } | null {
  if (!key.startsWith(STORIES_PREFIX)) return null;
  const parts = key.slice(STORIES_PREFIX.length).split("/");
  const id = parts[0] ?? "";
  const file = parts[1] ?? "";
  return parts.length === 2 && STORY_ID_PATTERN.test(id) && file !== "" ? { id, file } : null;
}

/** The language and version an `audio-<lang>-v<n>.zip` file name carries, or null for any other name. */
export function audioPackOf(file: string): { language: string; version: number } | null {
  const match = AUDIO_FILE.exec(file);
  const language = match?.[1] ?? "";
  const digits = match?.[2] ?? "";
  return language !== "" && POSITIVE_INTEGER.test(digits) ? { language, version: Number(digits) } : null;
}

/** Slice 281: the language and version a `listen-<lang>-v<n>.m4a` file name carries, or null for any other name. */
export function listenTrackOf(file: string): { language: string; version: number } | null {
  const match = LISTEN_FILE.exec(file);
  const language = match?.[1] ?? "";
  const digits = match?.[2] ?? "";
  return language !== "" && POSITIVE_INTEGER.test(digits) ? { language, version: Number(digits) } : null;
}

/** The version a `story-v<n>.dino` file name carries, or null for any other name. */
export function packageVersionOf(file: string): number | null {
  const match = PACKAGE_FILE.exec(file);
  const digits = match?.[1] ?? "";
  return POSITIVE_INTEGER.test(digits) ? Number(digits) : null;
}

function emptyFolder(): Folder {
  return { hasMeta: false, hasCover: false, versions: [], audio: {}, listen: {} };
}

/** Groups listed keys by story id; keys that are not a story file are ignored. */
export function groupFolders(keys: readonly string[]): Map<string, Folder> {
  const folders = new Map<string, Folder>();
  for (const key of keys) {
    const parsed = parseStoryKey(key);
    if (parsed === null) continue;
    const folder = folders.get(parsed.id) ?? emptyFolder();
    const version = packageVersionOf(parsed.file);
    const audio = audioPackOf(parsed.file);
    if (audio !== null) (folder.audio[audio.language] ??= []).push(audio.version);
    const listen = listenTrackOf(parsed.file);
    if (listen !== null) (folder.listen[listen.language] ??= []).push(listen.version);
    if (parsed.file === STORY_META_FILE) folder.hasMeta = true;
    else if (parsed.file === STORY_COVER_FILE) folder.hasCover = true;
    else if (version !== null) folder.versions.push(version);
    folders.set(parsed.id, folder);
  }
  return folders;
}

/** The ids whose folder has a meta.json and at least one package, sorted so the list is stable. */
export function completeIds(folders: ReadonlyMap<string, Folder>): string[] {
  return [...folders.entries()]
    .filter(([, folder]) => folder.hasMeta && folder.versions.length > 0)
    .map(([id]) => id)
    .sort();
}

function positiveIntegerOrNull(value: unknown): number | null {
  return typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= 1_000_000_000 ? value : null;
}

/** Slice 112: a valid age range, or null for both ends when either is missing, not a 0-18 integer, or min > max. */
export function parseAgeRange(rawMin: unknown, rawMax: unknown): { ageMin: number | null; ageMax: number | null } {
  const valid = (v: unknown): v is number => typeof v === "number" && Number.isInteger(v) && v >= 0 && v <= MAX_AGE;
  return valid(rawMin) && valid(rawMax) && rawMin <= rawMax ? { ageMin: rawMin, ageMax: rawMax } : { ageMin: null, ageMax: null };
}

/** Slice 246 B2: the one rule for an audio language key, used by parseAudio and the audio route: exactly the app's lower-case code (no trimming, no case folding), so meta key, URL and zip name agree. */
export function isAudioLanguage(code: string): boolean {
  return LANGUAGE_CODE.test(code);
}

/** Slice 246 B2: the `audio` map of a meta.json (language code to {version, bytes}); bad codes and versions are dropped, null when nothing is left. */
export function parseAudio(raw: unknown): Record<string, AudioPackInfo> | null {
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return null;
  const kept: Record<string, AudioPackInfo> = {};
  for (const [code, value] of Object.entries(raw)) {
    const pack = typeof value === "object" && value !== null ? (value as Record<string, unknown>) : {};
    const version = positiveIntegerOrNull(pack["version"]);
    const bytes = positiveIntegerOrNull(pack["bytes"]);
    const sized = bytes !== null && bytes <= MAX_AUDIO_BYTES ? bytes : null;
    const seconds = positiveIntegerOrNull(pack["listenSeconds"]);
    const listen = seconds !== null && seconds <= MAX_LISTEN_SECONDS ? { listenSeconds: seconds } : {};
    if (isAudioLanguage(code) && version !== null && Object.keys(kept).length < MAX_AUDIO_LANGUAGES) kept[code] = { version, bytes: sized, ...listen };
  }
  return Object.keys(kept).length > 0 ? kept : null;
}

/** Slice 281: a listed pack as offered: the listening track is kept only when its .m4a is in the folder at the pack's version. */
function withHeldTrack(language: string, pack: AudioPackInfo, folder: Folder): AudioPackInfo {
  if (pack.listenSeconds === undefined || folder.listen[language]?.includes(pack.version) === true) return pack;
  return { version: pack.version, bytes: pack.bytes };
}

/** The packs meta.json lists that the folder really holds; null when none. */
export function offeredAudio(meta: StoryMeta, folder: Folder): Record<string, AudioPackInfo> | null {
  const listed = Object.entries(meta.audio ?? {})
    .filter(([language, pack]) => folder.audio[language]?.includes(pack.version) === true)
    .map(([language, pack]) => [language, withHeldTrack(language, pack, folder)] as const);
  return listed.length > 0 ? Object.fromEntries(listed) : null;
}

function boundedText(value: unknown, max: number): string | null {
  if (typeof value !== "string") return null;
  const trimmed = value.trim();
  return trimmed !== "" && trimmed.length <= max ? trimmed : null;
}

/** Slice 159: the language-to-title map, with bad codes and empty or over-long titles dropped and the count capped; null when nothing is left. */
export function parseTitles(raw: unknown): Record<string, string> | null {
  return parseLanguageTexts(raw, MAX_TITLE_LENGTH);
}

/** Slice 192: the language-to-credit-line map, checked like the titles but capped at the credit length (200); null when nothing is left. */
export function parseCreditsByLanguage(raw: unknown): Record<string, string> | null {
  return parseLanguageTexts(raw, MAX_CREDITS_LENGTH);
}

function parseLanguageTexts(raw: unknown, maxLength: number): Record<string, string> | null {
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return null;
  const kept: Record<string, string> = {};
  for (const [code, value] of Object.entries(raw)) {
    const language = code.trim().toLowerCase();
    const title = boundedText(value, maxLength);
    if (LANGUAGE_CODE.test(language) && title !== null && Object.keys(kept).length < MAX_TITLE_LANGUAGES) kept[language] = title;
  }
  return Object.keys(kept).length > 0 ? kept : null;
}

/** Slice 260: the known tag names of a `tags` array, deduplicated and in list order; a wrong-typed value, an unknown or wrong-case name is dropped. */
export function parseTags(raw: unknown): StoreTag[] {
  if (!Array.isArray(raw)) return [];
  return STORE_TAGS.filter((tag) => raw.includes(tag));
}

/** A checked meta, or null when a required field is missing or malformed (the story is then not offered). */
export function parseMeta(parsed: unknown): StoryMeta | null {
  if (typeof parsed !== "object" || parsed === null) return null;
  const raw = parsed as Record<string, unknown>;
  const schemaVersion = positiveIntegerOrNull(raw["schemaVersion"]);
  const title = boundedText(raw["title"], MAX_TITLE_LENGTH);
  const language = boundedText(raw["language"], MAX_LANGUAGE_LENGTH);
  if (schemaVersion === null || title === null || language === null) return null;
  const ages = parseAgeRange(raw["ageMin"], raw["ageMax"]);
  return {
    schemaVersion,
    title,
    language,
    version: positiveIntegerOrNull(raw["version"]),
    pageCount: positiveIntegerOrNull(raw["pageCount"]),
    minutes: positiveIntegerOrNull(raw["minutes"]),
    added: boundedText(raw["added"], MAX_TITLE_LENGTH),
    credits: boundedText(raw["credits"], MAX_CREDITS_LENGTH),
    ageMin: ages.ageMin,
    ageMax: ages.ageMax,
    author: boundedText(raw["author"], MAX_AUTHOR_LENGTH),
    titles: parseTitles(raw["titles"]),
    creditsByLanguage: parseCreditsByLanguage(raw["creditsByLanguage"]),
    audio: parseAudio(raw["audio"]),
    tags: parseTags(raw["tags"]),
    bytes: positiveIntegerOrNull(raw["bytes"]),
    access: parseAccess(raw["access"]),
    productId: parseProductId(raw["productId"]),
  };
}

/** Slice 346: a meta without an `access` field is free (a listing published before the field existed stays open); only an explicit non-"free" value, "paid" or a wrong one, is paid. */
export function parseAccess(raw: unknown): "free" | "paid" {
  return raw === undefined || raw === null || raw === "free" ? "free" : "paid";
}

/** Slice 346: a store product id as the stores carry it, else null. */
export function parseProductId(raw: unknown): string | null {
  return typeof raw === "string" && isProductId(raw) ? raw : null;
}

/** The package version to offer: meta's own `version` when that file is there, else the highest uploaded; null when meta names a missing one. */
export function offeredVersion(meta: StoryMeta, uploaded: readonly number[]): number | null {
  if (meta.version !== null) return uploaded.includes(meta.version) ? meta.version : null;
  return uploaded.length === 0 ? null : Math.max(...uploaded);
}

/** Slice 260: tagged books first, then newest first by `added`, then by title and id so equal dates stay in one order. */
export function compareEntries(a: StoryEntry, b: StoryEntry): number {
  const byTagged = Number(b.meta.tags.length > 0) - Number(a.meta.tags.length > 0);
  if (byTagged !== 0) return byTagged;
  const byDate = (b.meta.added ?? "").localeCompare(a.meta.added ?? "");
  return byDate !== 0 ? byDate : a.meta.title.localeCompare(b.meta.title) || a.id.localeCompare(b.id);
}

export interface ListItem {
  readonly id: string;
  readonly title: string;
  readonly language: string;
  readonly schemaVersion: number;
  readonly version: number;
  readonly pageCount: number | null;
  readonly minutes: number | null;
  readonly credits: string | null;
  readonly ageMin: number | null;
  readonly ageMax: number | null;
  readonly author: string | null;
  readonly titles: Readonly<Record<string, string>> | null;
  readonly creditsByLanguage: Readonly<Record<string, string>> | null;
  /** Slice 246 B2: the packs on offer per language (version, size, link); null when the book has none. */
  readonly audio: Readonly<Record<string, AudioPackInfo & { readonly url: string; readonly listenUrl?: string }>> | null;
  /** Slice 260: the store tags in list order (empty when none) and the package size in bytes (null when unknown). */
  readonly tags: readonly StoreTag[];
  readonly bytes: number | null;
  /** Slice 346: free or paid; the book's own product id and every product that unlocks it (its own plus each bundle that lists it). */
  readonly access: BookAccess;
  readonly productId: string | null;
  readonly unlockedBy: readonly string[];
  readonly coverUrl: string | null;
  readonly downloadUrl: string;
}

function audioItem(base: string, language: string, pack: AudioPackInfo): AudioPackInfo & { url: string; listenUrl?: string } {
  const query = `/${language}?${VERSION_PARAM}=${pack.version}`;
  const item = { ...pack, url: `${base}${AUDIO_SEGMENT}${query}` };
  return pack.listenSeconds === undefined ? item : { ...item, listenUrl: `${base}${LISTEN_SEGMENT}${query}` };
}

function audioItems(entry: StoryEntry, base: string): Record<string, AudioPackInfo & { url: string; listenUrl?: string }> | null {
  const packs = Object.entries(entry.audio ?? {});
  if (packs.length === 0) return null;
  return Object.fromEntries(packs.map(([language, pack]) => [language, audioItem(base, language, pack)]));
}

/** The list line for one story. `pathPrefix` is "" on the Dino host and "/dino" on workers.dev. */
export function toListItem(entry: StoryEntry, pathPrefix: string): ListItem {
  const base = `${pathPrefix}${STORY_PATH_PREFIX}${entry.id}/`;
  const versioned = `?${VERSION_PARAM}=${entry.version}`;
  return {
    id: entry.id,
    title: entry.meta.title,
    language: entry.meta.language,
    schemaVersion: entry.meta.schemaVersion,
    version: entry.version,
    pageCount: entry.meta.pageCount,
    minutes: entry.meta.minutes,
    credits: entry.meta.credits,
    ageMin: entry.meta.ageMin,
    ageMax: entry.meta.ageMax,
    author: entry.meta.author,
    titles: entry.meta.titles,
    creditsByLanguage: entry.meta.creditsByLanguage,
    audio: audioItems(entry, base),
    tags: entry.meta.tags,
    bytes: entry.meta.bytes,
    access: entry.meta.access,
    productId: entry.meta.productId,
    unlockedBy: entry.meta.access === "paid" ? productsUnlocking(entry.id, entry.meta.productId) : [],
    coverUrl: entry.hasCover ? `${base}${COVER_SEGMENT}${versioned}` : null,
    downloadUrl: `${base}${DOWNLOAD_SEGMENT}${versioned}`,
  };
}

interface CachedStories {
  readonly at: number;
  readonly entries: readonly StoryEntry[];
}

/** Per-isolate listing cache, keyed by bucket object so a test's fake never sees another's. */
const cache = new WeakMap<R2Bucket, CachedStories>();

async function listStoryKeys(bucket: R2Bucket, prefix: string = STORIES_PREFIX): Promise<string[]> {
  const keys: string[] = [];
  let cursor: string | undefined;
  for (let page = 0; page < MAX_LIST_PAGES; page += 1) {
    const result = await bucket.list({ prefix, cursor, limit: LIST_PAGE_SIZE });
    keys.push(...result.objects.map((object) => object.key));
    if (!result.truncated) break;
    cursor = result.cursor;
  }
  return keys;
}

// A missing or malformed meta.json means "not offered"; a failed R2 read throws, so the whole list is
// "unavailable" (503, not cached) instead of silently dropping the stories that could not be read.
async function readMeta(bucket: R2Bucket, id: string): Promise<StoryMeta | null> {
  const object = await bucket.get(storyMetaKey(id));
  if (!object) return null;
  try {
    return parseMeta(await object.json());
  } catch {
    return null;
  }
}

async function entryFor(bucket: R2Bucket, id: string, folder: Folder): Promise<StoryEntry | null> {
  const meta = await readMeta(bucket, id);
  const version = meta === null ? null : offeredVersion(meta, folder.versions);
  return meta === null || version === null ? null : { id, meta, version, hasCover: folder.hasCover, audio: offeredAudio(meta, folder) };
}

async function buildEntries(bucket: R2Bucket): Promise<StoryEntry[]> {
  const folders = groupFolders(await listStoryKeys(bucket));
  const ids = completeIds(folders).slice(0, MAX_STORIES);
  const entries: (StoryEntry | null)[] = [];
  for (let start = 0; start < ids.length; start += META_READ_BATCH) {
    const batch = ids.slice(start, start + META_READ_BATCH);
    entries.push(...(await Promise.all(batch.map((id) => entryFor(bucket, id, folders.get(id) ?? emptyFolder())))));
  }
  return entries.filter((entry): entry is StoryEntry => entry !== null).sort(compareEntries);
}

const pending = new WeakMap<R2Bucket, Promise<readonly StoryEntry[] | null>>();

async function refreshEntries(bucket: R2Bucket): Promise<readonly StoryEntry[] | null> {
  try {
    const entries = await buildEntries(bucket);
    cache.set(bucket, { at: Date.now(), entries });
    return entries;
  } catch {
    return null;
  } finally {
    pending.delete(bucket);
  }
}

/** Complete stories, cached for a minute; concurrent callers share one rebuild. Null when the bucket can't be read (never "empty"); a failed read is not cached. */
function currentEntries(bucket: R2Bucket): Promise<readonly StoryEntry[] | null> {
  const cached = cache.get(bucket);
  if (cached && Date.now() - cached.at < LISTING_CACHE_MS) return Promise.resolve(cached.entries);
  const inFlight = pending.get(bucket);
  if (inFlight) return inFlight;
  const started = refreshEntries(bucket);
  pending.set(bucket, started);
  return started;
}

// A cover or download is checked against the story's own folder, not the cached listing, so a story that was
// just uploaded or replaced is served at once. A failed R2 read throws and becomes a 503.
async function freshEntry(bucket: R2Bucket, id: string): Promise<StoryEntry | null> {
  const folder = groupFolders(await listStoryKeys(bucket, `${STORIES_PREFIX}${id}/`)).get(id);
  return folder === undefined ? null : entryFor(bucket, id, folder);
}

function jsonResponse(body: unknown, status: number, cacheControl: string): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": JSON_CONTENT_TYPE, "Cache-Control": cacheControl } });
}

function errorResponse(code: string, status: number): Response {
  return jsonResponse({ code }, status, NO_CACHE);
}

function listResponse(entries: readonly StoryEntry[], pathPrefix: string, isHead: boolean): Response {
  const body = { schemaVersion: LIST_SCHEMA_VERSION, stories: entries.map((entry) => toListItem(entry, pathPrefix)) };
  const response = jsonResponse(body, 200, LIST_CACHE_CONTROL);
  return isHead ? new Response(null, { status: 200, headers: response.headers }) : response;
}

/** Streams an R2 object with Range and conditional support (downloads.ts `serveObject`), passed in by index.ts. */
export type ObjectServer = (request: Request, bucket: R2Bucket, file: ServedFile, cacheControl: string) => Promise<Response>;

function requestedVersion(url: URL): number | null | "invalid" {
  const text = url.searchParams.get(VERSION_PARAM);
  if (text === null) return null;
  return POSITIVE_INTEGER.test(text) ? Number(text) : "invalid";
}

function coverFile(id: string): ServedFile {
  return { key: storyCoverKey(id), contentType: JPEG_CONTENT_TYPE, fileName: STORY_COVER_FILE, disposition: "inline", unavailable: ErrorCode.StoryUnavailable };
}

function packageFile(id: string, version: number): ServedFile {
  return {
    key: storyPackageKey(id, version),
    contentType: PACKAGE_CONTENT_TYPE,
    fileName: storyPackageFileName(version),
    disposition: "attachment",
    unavailable: ErrorCode.StoryUnavailable,
  };
}

// cover.jpg is one key per story that the owner may replace, so a cover is never cached as immutable, ?v= or not.
function serveCover(request: Request, bucket: R2Bucket, entry: StoryEntry, serve: ObjectServer): Promise<Response> {
  if (!entry.hasCover) return Promise.resolve(errorResponse(ErrorCode.NotFound, 404));
  return serve(request, bucket, coverFile(entry.id), COVER_CACHE);
}

/** Slice 346: while the paid-books switch is on, a paid book's files are never kept by a shared cache (a proof-bearing 200 must not be served to a caller with no proof). With the switch off every response and header is origin/main's. */
export const PAID_CACHE = "private, no-store";

// Without ?v= the newest package is sent and never kept; with it, that exact version is immutable.
function serveDownload(request: Request, bucket: R2Bucket, entry: StoryEntry, version: number | null, serve: ObjectServer): Promise<Response> {
  return serve(request, bucket, packageFile(entry.id, version ?? entry.version), version === null ? NO_CACHE : VERSIONED_CACHE);
}

// Slice 246 B2: the pack of one book language at one version. Both must be what meta.json lists and the folder holds (entry.audio), so a stale or guessed pair is a 404.
function serveAudio(request: Request, bucket: R2Bucket, entry: StoryEntry, language: string, serve: ObjectServer): Promise<Response> {
  const version = requestedVersion(new URL(request.url));
  const offered = isAudioLanguage(language) ? entry.audio?.[language] : undefined;
  if (version === null || version === "invalid" || offered === undefined || offered.version !== version) return Promise.resolve(errorResponse(ErrorCode.NotFound, 404));
  return serve(request, bucket, audioFile(entry.id, language, version), VERSIONED_CACHE);
}

function audioFile(id: string, language: string, version: number): ServedFile {
  const fileName = `audio-${language}-v${version}.zip`;
  return { key: `${storyFolder(id)}${fileName}`, contentType: AUDIO_CONTENT_TYPE, fileName, disposition: "attachment", unavailable: ErrorCode.StoryUnavailable };
}

// Slice 281: the one listening track of a book language, for smart speakers. Same rule as the pack: meta lists that language at that version with a track length AND the .m4a is in the folder.
// Range, HEAD and If-Range are serveObject's (downloads.ts), passed in as `serve`.
function serveListen(request: Request, bucket: R2Bucket, entry: StoryEntry, language: string, serve: ObjectServer): Promise<Response> {
  const version = requestedVersion(new URL(request.url));
  const offered = isAudioLanguage(language) ? entry.audio?.[language] : undefined;
  if (version === null || version === "invalid" || offered === undefined || offered.version !== version || offered.listenSeconds === undefined) {
    return Promise.resolve(errorResponse(ErrorCode.NotFound, 404));
  }
  return serve(request, bucket, listenFile(entry.id, language, version), VERSIONED_CACHE);
}

function listenFile(id: string, language: string, version: number): ServedFile {
  const fileName = `listen-${language}-v${version}.m4a`;
  return { key: `${storyFolder(id)}${fileName}`, contentType: LISTEN_CONTENT_TYPE, fileName, disposition: "inline", unavailable: ErrorCode.StoryUnavailable };
}

function serveStoryFile(request: Request, bucket: R2Bucket, entry: StoryEntry, segment: string, serve: ObjectServer): Promise<Response> {
  const version = requestedVersion(new URL(request.url));
  if (version === "invalid" || (segment !== COVER_SEGMENT && segment !== DOWNLOAD_SEGMENT)) {
    return Promise.resolve(errorResponse(ErrorCode.NotFound, 404));
  }
  return segment === COVER_SEGMENT ? serveCover(request, bucket, entry, serve) : serveDownload(request, bucket, entry, version, serve);
}

// Slice 346: the files of a paid book (package, audio packs, listening track; not the cover or the list) need a purchase proof while
// `paidBooks.enabled` is on. 402 = no proof, 403 = the store does not confirm it (or could not be asked). With the switch off, or when
// the gate cannot read it (the gate counts that as off), nothing is asked and the response is origin/main's: `Gate.Open`; `Gate.Passed` is a
// proof the store confirmed, whose file is then sent with PAID_CACHE.
const Gate = { Open: "open", Passed: "passed" } as const;

async function paidDenial(request: Request, entry: StoryEntry, segment: string, paid: PaidGate | undefined): Promise<Response | typeof Gate.Open | typeof Gate.Passed> {
  const isFile = segment === DOWNLOAD_SEGMENT || segment === AUDIO_SEGMENT || segment === LISTEN_SEGMENT;
  if (paid === undefined || !isFile || entry.meta.access !== "paid") return Gate.Open;
  let answer: string;
  try {
    answer = await paid(request.headers.get(PROOF_HEADER), { id: entry.id, productId: entry.meta.productId }, request.headers.get("CF-Connecting-IP") ?? UNKNOWN_CLIENT);
  } catch {
    return errorResponse(ErrorCode.Unavailable, 503); // the switch is on but a store check broke unexpectedly; never guess "owned"
  }
  if (answer === GateAnswer.Off) return Gate.Open;
  if (answer === GateAnswer.Allowed) return Gate.Passed;
  if (answer === GateAnswer.RateLimited) return errorResponse(ErrorCode.RateLimited, HTTP_TOO_MANY_REQUESTS); // over the per-client limit on store calls: never "owned"
  if (answer === GateAnswer.NoProof) return errorResponse(ErrorCode.PaymentRequired, HTTP_PAYMENT_REQUIRED);
  return errorResponse(answer === GateAnswer.CannotVerify ? ErrorCode.CannotVerify : ErrorCode.PurchaseNotValid, HTTP_FORBIDDEN);
}

/** Slice 346: what the ticket source answers: a ticket, none (switch off, no Microsoft keys or no limiter: 404) or over the per-client limit (429). */
export type WindowsTicketAnswer = { readonly ticket: string } | { readonly none: true } | { readonly limited: true };

/** Slice 346: hands the Windows app the short-lived Entra ID ticket it needs to make a Microsoft Store ID key; `clientKey` only keys the rate limit and is never stored. */
export type WindowsTicketSource = (clientKey: string) => Promise<WindowsTicketAnswer>;

export const WINDOWS_TICKET_PATH = "/api/stories/windows-ticket";
const UNKNOWN_CLIENT = "unknown";
const HTTP_TOO_MANY_REQUESTS = 429;

async function ticketResponse(request: Request, ticket: WindowsTicketSource | undefined): Promise<Response> {
  let answer: WindowsTicketAnswer = { none: true };
  try {
    answer = ticket === undefined ? answer : await ticket(request.headers.get("CF-Connecting-IP") ?? UNKNOWN_CLIENT);
  } catch {
    answer = { none: true }; // no ticket: the app then cannot make a Store ID key, so a Windows book stays unowned (never "owned")
  }
  if ("limited" in answer) return errorResponse(ErrorCode.RateLimited, HTTP_TOO_MANY_REQUESTS);
  return "ticket" in answer ? jsonResponse({ ticket: answer.ticket }, 200, NO_CACHE) : errorResponse(ErrorCode.NotFound, 404);
}

async function route(request: Request, bucket: R2Bucket, pathPrefix: string, serve: ObjectServer, paid?: PaidGate, ticket?: WindowsTicketSource): Promise<Response> {
  const { pathname } = new URL(request.url);
  if (pathname === STORIES_API_PATH) {
    const entries = await currentEntries(bucket);
    return entries === null ? errorResponse(ErrorCode.Unavailable, 503) : listResponse(entries, pathPrefix, request.method === "HEAD");
  }
  if (pathname === WINDOWS_TICKET_PATH) return ticketResponse(request, ticket);
  const [id = "", segment = "", ...extra] = pathname.slice(STORY_PATH_PREFIX.length).split("/");
  const withLanguage = (segment === AUDIO_SEGMENT || segment === LISTEN_SEGMENT) && extra.length === 1;
  const audioLanguage = withLanguage ? (extra[0] ?? "") : null; // audio/<lang> and listen/<lang> are the routes with a third part
  if (!STORY_ID_PATTERN.test(id) || (extra.length > 0 && audioLanguage === null)) return errorResponse(ErrorCode.NotFound, 404);
  let entry: StoryEntry | null;
  try {
    entry = await freshEntry(bucket, id);
  } catch {
    return errorResponse(ErrorCode.Unavailable, 503);
  }
  if (entry === null) return errorResponse(ErrorCode.NotFound, 404);
  const gate = await paidDenial(request, entry, segment, paid);
  if (gate instanceof Response) return gate;
  return serveFile(request, bucket, { entry, segment, audioLanguage }, gate === Gate.Passed ? privateServer(serve) : serve);
}

// Slice 346: the same server with the file's cache rule replaced by PAID_CACHE.
function privateServer(serve: ObjectServer): ObjectServer {
  return (request, bucket, file, _cache) => serve(request, bucket, file, PAID_CACHE);
}

function serveFile(request: Request, bucket: R2Bucket, target: { readonly entry: StoryEntry; readonly segment: string; readonly audioLanguage: string | null }, serve: ObjectServer): Promise<Response> {
  const { entry, segment, audioLanguage } = target;
  if (audioLanguage === null) return serveStoryFile(request, bucket, entry, segment, serve);
  return segment === LISTEN_SEGMENT ? serveListen(request, bucket, entry, audioLanguage, serve) : serveAudio(request, bucket, entry, audioLanguage, serve);
}

/** True for every path this module answers (after the `/dino` prefix is stripped). */
export function isStoryPath(pathname: string): boolean {
  return pathname === STORIES_API_PATH || pathname.startsWith(STORY_PATH_PREFIX);
}

/**
 * Routes /api/stories, /api/stories/<id>/cover and /api/stories/<id>/download. `pathPrefix` is "" on the
 * Dino host and "/dino" under workers.dev, so the list's links work where they were fetched. A HEAD error
 * carries no body.
 */
export async function handleStories(
  request: Request,
  env: { DOWNLOADS?: R2Bucket },
  pathPrefix: string,
  serve: ObjectServer,
  paid?: PaidGate,
  ticket?: WindowsTicketSource,
): Promise<Response> {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS } });
  }
  if (!env.DOWNLOADS) return errorResponse(ErrorCode.Unavailable, 503);
  const response = await route(request, env.DOWNLOADS, pathPrefix, serve, paid, ticket);
  if (request.method !== "HEAD" || response.status < 400) return response;
  return new Response(null, { status: response.status, headers: response.headers });
}
