// The free Dino Reader story store (Slice 92, Decisions 94-99). Stories are uploaded by hand into the
// `DOWNLOADS` R2 bucket (layout in download-keys.mjs: dinoreader/stories/<id>/{meta.json, cover.jpg,
// story-v<n>.dino}). This answers:
//   GET /api/stories                  the list of complete stories (60 s listing cache)
//   GET /api/stories/<id>/cover       the cover picture
//   GET /api/stories/<id>/download    the newest package; add ?v=<n> for that version, which is cached for a day
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
  storyMetaKey,
  storyPackageFileName,
  storyPackageKey,
} from "./download-keys.mjs";
import type { ServedFile } from "./downloads";

export const STORIES_API_PATH = "/api/stories";
const STORY_PATH_PREFIX = "/api/stories/";
const COVER_SEGMENT = "cover";
const DOWNLOAD_SEGMENT = "download";
const VERSION_PARAM = "v";

/** The list response format; the app reads `stories` and ignores fields it does not know. */
export const LIST_SCHEMA_VERSION = 1;

const JPEG_CONTENT_TYPE = "image/jpeg";
const PACKAGE_CONTENT_TYPE = "application/octet-stream";
const LIST_CACHE_CONTROL = "public, max-age=60";
const COVER_CACHE = "public, max-age=3600";
const META_READ_BATCH = 6;
const MAX_STORIES = 200;
const MAX_TITLE_LENGTH = 200;
const MAX_LANGUAGE_LENGTH = 12;
const ALLOWED_METHODS = "GET, HEAD";
const PACKAGE_FILE = /^story-v(\d+)\.dino$/;
const POSITIVE_INTEGER = /^[1-9]\d{0,8}$/;

const ErrorCode = {
  Unavailable: "stories-unavailable",
  NotFound: "not-found",
  StoryUnavailable: "story-unavailable",
} as const;

/** What `meta.json` holds, checked. `schemaVersion` is the `.dino` format the package is in. */
export interface StoryMeta {
  readonly schemaVersion: number;
  readonly title: string;
  readonly language: string;
  readonly version: number | null;
  readonly pageCount: number | null;
  readonly minutes: number | null;
  readonly added: string | null;
}

/** One complete story: its checked meta, the package version it offers, and whether a cover was uploaded. */
export interface StoryEntry {
  readonly id: string;
  readonly meta: StoryMeta;
  readonly version: number;
  readonly hasCover: boolean;
}

interface Folder {
  hasMeta: boolean;
  hasCover: boolean;
  versions: number[];
}

/** The `<id>` and file name of a key under the stories prefix, or null for any other key. */
export function parseStoryKey(key: string): { id: string; file: string } | null {
  if (!key.startsWith(STORIES_PREFIX)) return null;
  const parts = key.slice(STORIES_PREFIX.length).split("/");
  const id = parts[0] ?? "";
  const file = parts[1] ?? "";
  return parts.length === 2 && STORY_ID_PATTERN.test(id) && file !== "" ? { id, file } : null;
}

/** The version a `story-v<n>.dino` file name carries, or null for any other name. */
export function packageVersionOf(file: string): number | null {
  const match = PACKAGE_FILE.exec(file);
  const digits = match?.[1] ?? "";
  return POSITIVE_INTEGER.test(digits) ? Number(digits) : null;
}

function emptyFolder(): Folder {
  return { hasMeta: false, hasCover: false, versions: [] };
}

/** Groups listed keys by story id; keys that are not a story file are ignored. */
export function groupFolders(keys: readonly string[]): Map<string, Folder> {
  const folders = new Map<string, Folder>();
  for (const key of keys) {
    const parsed = parseStoryKey(key);
    if (parsed === null) continue;
    const folder = folders.get(parsed.id) ?? emptyFolder();
    const version = packageVersionOf(parsed.file);
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

function boundedText(value: unknown, max: number): string | null {
  return typeof value === "string" && value.trim() !== "" && value.length <= max ? value : null;
}

/** A checked meta, or null when a required field is missing or malformed (the story is then not offered). */
export function parseMeta(parsed: unknown): StoryMeta | null {
  if (typeof parsed !== "object" || parsed === null) return null;
  const raw = parsed as Record<string, unknown>;
  const schemaVersion = positiveIntegerOrNull(raw["schemaVersion"]);
  const title = boundedText(raw["title"], MAX_TITLE_LENGTH);
  const language = boundedText(raw["language"], MAX_LANGUAGE_LENGTH);
  if (schemaVersion === null || title === null || language === null) return null;
  return {
    schemaVersion,
    title,
    language,
    version: positiveIntegerOrNull(raw["version"]),
    pageCount: positiveIntegerOrNull(raw["pageCount"]),
    minutes: positiveIntegerOrNull(raw["minutes"]),
    added: boundedText(raw["added"], MAX_TITLE_LENGTH),
  };
}

/** The package version to offer: meta's own `version` when that file is there, else the highest uploaded; null when meta names a missing one. */
export function offeredVersion(meta: StoryMeta, uploaded: readonly number[]): number | null {
  if (meta.version !== null) return uploaded.includes(meta.version) ? meta.version : null;
  return uploaded.length === 0 ? null : Math.max(...uploaded);
}

/** Newest first by `added`, then by title and id so equal dates stay in one order. */
export function compareEntries(a: StoryEntry, b: StoryEntry): number {
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
  readonly coverUrl: string | null;
  readonly downloadUrl: string;
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
  return meta === null || version === null ? null : { id, meta, version, hasCover: folder.hasCover };
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

// Without ?v= the newest package is sent and never kept; with it, that exact version is immutable.
function serveDownload(request: Request, bucket: R2Bucket, entry: StoryEntry, version: number | null, serve: ObjectServer): Promise<Response> {
  return serve(request, bucket, packageFile(entry.id, version ?? entry.version), version === null ? NO_CACHE : VERSIONED_CACHE);
}

function serveStoryFile(request: Request, bucket: R2Bucket, entry: StoryEntry, segment: string, serve: ObjectServer): Promise<Response> {
  const version = requestedVersion(new URL(request.url));
  if (version === "invalid" || (segment !== COVER_SEGMENT && segment !== DOWNLOAD_SEGMENT)) {
    return Promise.resolve(errorResponse(ErrorCode.NotFound, 404));
  }
  return segment === COVER_SEGMENT ? serveCover(request, bucket, entry, serve) : serveDownload(request, bucket, entry, version, serve);
}

async function route(request: Request, bucket: R2Bucket, pathPrefix: string, serve: ObjectServer): Promise<Response> {
  const { pathname } = new URL(request.url);
  if (pathname === STORIES_API_PATH) {
    const entries = await currentEntries(bucket);
    return entries === null ? errorResponse(ErrorCode.Unavailable, 503) : listResponse(entries, pathPrefix, request.method === "HEAD");
  }
  const [id = "", segment = "", ...extra] = pathname.slice(STORY_PATH_PREFIX.length).split("/");
  if (!STORY_ID_PATTERN.test(id) || extra.length > 0) return errorResponse(ErrorCode.NotFound, 404);
  let entry: StoryEntry | null;
  try {
    entry = await freshEntry(bucket, id);
  } catch {
    return errorResponse(ErrorCode.Unavailable, 503);
  }
  return entry === null ? errorResponse(ErrorCode.NotFound, 404) : serveStoryFile(request, bucket, entry, segment, serve);
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
): Promise<Response> {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS } });
  }
  if (!env.DOWNLOADS) return errorResponse(ErrorCode.Unavailable, 503);
  const response = await route(request, env.DOWNLOADS, pathPrefix, serve);
  if (request.method !== "HEAD" || response.status < 400) return response;
  return new Response(null, { status: response.status, headers: response.headers });
}
