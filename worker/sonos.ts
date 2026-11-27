// Slice 283: Dino Reader as an anonymous Sonos music service (SMAPI over SOAP) at /sonos (workers.dev: /dino/sonos). The
// catalogue is the store list this Worker already serves (/api/stories, Slice 281's listening tracks): only a book that has a
// track in a language appears, under that language. No household state and no logging of ids or titles (worker/CLAUDE.md).
// Browse tree (Slice 437): root opens in the first Accept-Language language that has books (its shelves: Bedtime, New, ages, Free, A-Z;
// the phone's second language; Other languages), else lists the languages; `lang:<code>` and `lang:<code>/<shelf>` carry the language.

import { PAID_ACCESS, handleStories } from "./stories.ts";
import type { CatalogueRuntime, ListItem, ObjectServer } from "./stories.ts";
import { FaultCode, FaultMessage, SoapFault, SonosAction, acceptedLanguages, escapeXml, faultResponse, parseSonosRequest, readBoundedText, resultResponse, tag } from "./sonos-soap.ts";
import type { SonosActionName, SonosRequest } from "./sonos-soap.ts";
import { fitArt, folderArtUrl, languageTileUrl, squareCoverPath } from "./sonos-art.ts";
import { Shelf, bookSubtitle, languageNameIn, languageSubtitle, shelfSubtitle, shelfTitle } from "./sonos-labels.ts";
import type { ShelfName } from "./sonos-labels.ts";
import { languageFolderId, otherLanguages, parseShelfId, shelfId, shelfTracks, shelvesOf } from "./sonos-shelves.ts";
import type { ShelfTarget } from "./sonos-shelves.ts";
import { SPEAKER_PLATFORM_SONOS, recordSpeakerFault } from "./speaker-usage.ts";
import type { SpeakerUsage } from "./speaker-usage.ts";

export const SONOS_PATH = "/sonos";
const ROOT_ID = "root";
const TRACK_PREFIX = "track:";
const ID_SEPARATOR = ":";
const SAFE_PART = /^[A-Za-z0-9_-]{1,64}$/;
const TRACK_MIME = "audio/mp4";
const ITEM_TRACK = "track";
const ITEM_CONTAINER = "container";
const ARTIST_FALLBACK = "Dino: Kids Books & Stories";
const ALBUM_NAME = "Dino: Kids Books & Stories";
const ACCEPT_LANGUAGE_HEADER = "Accept-Language";
const MAX_TITLE = 64;
const DEFAULT_PAGE = 100;
const MAX_PAGE = 500;
const POLL_SECONDS = 600;
const STORIES_PATH = "/api/stories";
const ALLOWED_METHODS = "POST";
const NOT_ALLOWED_STATUS = 405;
const NO_FAVORITES = "0";
const MEDIA_METADATA = "mediaMetadata";
const MEDIA_COLLECTION = "mediaCollection";

/** Each book language's name in that language, for the folder list; an unknown code shows in capitals. */
const LANGUAGE_NAMES: Readonly<Record<string, string>> = {
  en: "English",
  ar: "العربية",
  da: "Dansk",
  de: "Deutsch",
  es: "Español",
  fa: "فارسی",
  fi: "Suomi",
  fr: "Français",
  ja: "日本語",
  nb: "Norsk",
  nl: "Nederlands",
  pt: "Português",
  ru: "Русский",
  sv: "Svenska",
  zh: "中文",
};

export interface Track {
  readonly language: string;
  readonly storyId: string;
  readonly title: string;
  readonly author: string | null;
  readonly seconds: number;
  readonly version: number;
  readonly coverUrl: string | null;
  /** Slice 437: the cover link asking for the square crop Sonos shows (null when it would pass 128 characters). */
  readonly squareCoverUrl: string | null;
  readonly listenUrl: string;
  /** Slice 437: what the shelves sort and filter on. */
  readonly tags: readonly string[];
  readonly ageMin: number | null;
  readonly ageMax: number | null;
  readonly added: string | null;
  /** A paid book is listed ("In the Dino app") but not playable on this anonymous service. */
  readonly paid: boolean;
}

interface Page {
  readonly items: readonly string[];
  readonly total: number;
}

function clip(text: string, max: number): string {
  const chars = [...text];
  return chars.length <= max ? text : `${chars.slice(0, max - 1).join("")}…`;
}

export function trackOf(item: ListItem, language: string, origin: string): Track | null {
  const pack = item.audio?.[language];
  if (pack === undefined || pack === null || pack.listenSeconds === undefined || pack.listenUrl === undefined) return null;
  if (typeof item.id !== "string" || !SAFE_PART.test(item.id) || !SAFE_PART.test(language)) return null;
  const title = item.titles?.[language] ?? item.title;
  if (typeof title !== "string" || typeof pack.listenSeconds !== "number" || typeof pack.listenUrl !== "string") return null;
  const cover = typeof item.coverUrl === "string" ? item.coverUrl : null;
  return {
    language,
    storyId: item.id,
    title,
    author: item.author,
    seconds: pack.listenSeconds,
    version: pack.version,
    coverUrl: fitArt(cover === null ? null : `${origin}${cover}`),
    squareCoverUrl: fitArt(cover === null ? null : `${origin}${squareCoverPath(cover)}`),
    listenUrl: `${origin}${pack.listenUrl}`,
    tags: item.tags ?? [],
    ageMin: item.ageMin ?? null,
    ageMax: item.ageMax ?? null,
    added: item.added ?? null,
    paid: item.access === PAID_ACCESS,
  };
}

function tracksOf(items: readonly ListItem[], origin: string): Track[] {
  const tracks: Track[] = [];
  for (const item of items) {
    for (const language of Object.keys(item.audio ?? {})) {
      const track = trackOf(item, language, origin);
      if (track !== null) tracks.push(track);
    }
  }
  return tracks;
}

// The catalogue is this Worker's own list, asked for through the same handler the apps use, so the rules for what is offered live in one place.
// That handler caches the built list per bucket for 60 s and shares one rebuild between concurrent callers (stories.ts currentEntries), so a SOAP call does not re-list R2 or re-read meta.json.
// Slice 283 F1/F2: `catalogue` lets that handler serve the last good list while it rebuilds and keep the list in the colo's Cache API, so a cold isolate does not rebuild before answering Sonos.
export async function loadTracks(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, catalogue?: CatalogueRuntime): Promise<Track[]> {
  const origin = new URL(request.url).origin;
  const listRequest = new Request(`${origin}${pathPrefix}${STORIES_PATH}`, { method: "GET" });
  const response = await handleStories(listRequest, env, pathPrefix, serve, undefined, undefined, undefined, catalogue);
  if (!response.ok) throw new SoapFault(FaultCode.Unavailable, FaultMessage.NoCatalogue);
  const body = (await response.json()) as { stories?: ListItem[] };
  return tracksOf(Array.isArray(body.stories) ? body.stories : [], origin);
}

export function languageIds(tracks: readonly Track[]): string[] {
  return [...new Set(tracks.map((track) => track.language))].sort();
}

export function languageName(code: string): string {
  return LANGUAGE_NAMES[code] ?? code.toUpperCase();
}

export function trackId(track: Track): string {
  return `${TRACK_PREFIX}${track.language}${ID_SEPARATOR}${track.storyId}`;
}

export function findTrack(tracks: readonly Track[], id: string | null): Track | null {
  if (id === null || !id.startsWith(TRACK_PREFIX)) return null;
  const [language, storyId, extra] = id.slice(TRACK_PREFIX.length).split(ID_SEPARATOR);
  if (extra !== undefined) return null;
  return tracks.find((track) => track.language === language && track.storyId === storyId) ?? null;
}

function requireTrack(tracks: readonly Track[], id: string | null): Track {
  const track = findTrack(tracks, id);
  if (track === null) throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NoSuchItem);
  return track;
}

/** One folder's row as the browse and the catalog token both see it: id, words, picture and the books under it, in order. */
interface FolderView {
  readonly id: string;
  readonly title: string;
  readonly subtitle: string;
  readonly art: string | null;
  readonly canPlay: boolean;
  readonly books: readonly Track[];
}

function shelfView(origin: string, language: string, shelf: ShelfName, books: readonly Track[]): FolderView {
  const canPlay = shelf !== Shelf.Others && playable(books).length > 0;
  return { id: shelfId(language, shelf), title: shelfTitle(language, shelf), subtitle: shelfSubtitle(language, shelf, books.length), art: folderArtUrl(origin, shelf), canPlay, books };
}

function tracksOfLanguage(tracks: readonly Track[], language: string): Track[] {
  return tracks.filter((track) => track.language === language);
}

/** The shelves of one language (empty ones left out), then its Other languages folder when the catalogue has another language. */
function languageViews(tracks: readonly Track[], origin: string, language: string, firstLanguages: readonly string[] = []): FolderView[] {
  const shelves = shelvesOf(tracksOfLanguage(tracks, language), language).map((row) => shelfView(origin, language, row.shelf, row.tracks));
  const others = otherLanguages(languageIds(tracks), language, firstLanguages, (code) => languageNameIn(code, language));
  if (others.length === 0) return shelves;
  const folder = shelfView(origin, language, Shelf.Others, []);
  return [...shelves, { ...folder, id: shelfId(language, Shelf.Others, firstLanguages.filter((code) => others.includes(code))), subtitle: shelfSubtitle(language, Shelf.Others, others.length) }];
}

// A value that changes whenever anything Sonos shows changes: a book or its track added, replaced or removed, a title, author,
// cover, track URL, length, tag, age range, date or access edited (meta.json edits need no version bump), and every shelf's id,
// words, picture and book order. FNV-1a over those lines.
export function catalogToken(tracks: readonly Track[], origin = ""): string {
  const trackLines = tracks.map((track) =>
    JSON.stringify([track.language, track.storyId, track.version, track.seconds, track.title, track.author, track.coverUrl, track.squareCoverUrl, track.listenUrl, track.tags, track.ageMin, track.ageMax, track.added, track.paid]),
  );
  const folderLines = languageIds(tracks).flatMap((language) =>
    languageViews(tracks, origin, language).map((view) => JSON.stringify([view.id, view.title, view.subtitle, view.art, view.canPlay, view.books.map(trackId), languageTileUrl(origin, language)])),
  );
  const lines = [...trackLines, ...folderLines].sort();
  let hash = 0x811c9dc5;
  for (const char of lines.join("\n")) {
    hash = Math.imul(hash ^ char.codePointAt(0)!, 0x01000193) >>> 0;
  }
  return `${tracks.length}-${hash.toString(16)}`;
}

function trackFields(track: Track): string {
  const subtitle = bookSubtitle(track, track.language);
  const art = track.squareCoverUrl ?? track.coverUrl;
  const meta = [
    tag("artist", clip(track.author ?? ARTIST_FALLBACK, MAX_TITLE)),
    tag("album", clip(subtitle === "" ? ALBUM_NAME : subtitle, MAX_TITLE)),
    tag("duration", track.seconds),
    art === null ? "" : tag("albumArtURI", art),
    tag("canPlay", !track.paid),
    tag("canSkip", true),
  ].join("");
  return [tag("id", trackId(track)), tag("itemType", ITEM_TRACK), tag("title", clip(track.title, MAX_TITLE)), tag("mimeType", TRACK_MIME), `<trackMetadata>${meta}</trackMetadata>`].join("");
}

function trackItem(track: Track): string {
  return `<${MEDIA_METADATA}>${trackFields(track)}</${MEDIA_METADATA}>`;
}

/** A folder row: title, the line under it (as `artist`), its square picture, and whether Play queues all of it. */
function folderItem(view: Pick<FolderView, "id" | "title" | "subtitle" | "art" | "canPlay">): string {
  const fields = [
    tag("id", view.id),
    tag("itemType", ITEM_CONTAINER),
    tag("title", clip(view.title, MAX_TITLE)),
    view.subtitle === "" ? "" : tag("artist", clip(view.subtitle, MAX_TITLE)),
    view.art === null ? "" : tag("albumArtURI", view.art),
    tag("canPlay", view.canPlay),
    tag("canEnumerate", true),
  ].join("");
  return `<${MEDIA_COLLECTION}>${fields}</${MEDIA_COLLECTION}>`;
}

/** The plain row of the language list a request with no usable Accept-Language gets: the language's own name, nothing under it. */
function languageItem(code: string, origin: string): string {
  return folderItem({ id: languageFolderId(code), title: languageName(code), subtitle: "", art: languageTileUrl(origin, code), canPlay: false });
}

/** A language row among shelves: own name on top, "<name in the parent's language> · N stories" under it. */
function languageRow(code: string, parentLanguage: string, tracks: readonly Track[], origin: string): string {
  const count = tracksOfLanguage(tracks, code).length;
  return folderItem({ id: languageFolderId(code), title: languageName(code), subtitle: languageSubtitle(code, parentLanguage, count), art: languageTileUrl(origin, code), canPlay: false });
}

interface Browse {
  readonly tracks: readonly Track[];
  readonly origin: string;
  /** The request's Accept-Language, best first: read by `root` only; every other id carries its own language. */
  readonly languages: readonly string[];
}

function playable(tracks: readonly Track[]): Track[] {
  return tracks.filter((track) => !track.paid);
}

function pageOfItems(items: readonly string[]): Page {
  return { items, total: items.length };
}

function noSuchItem(): SoapFault {
  return new SoapFault(FaultCode.ItemNotFound, FaultMessage.NoSuchItem);
}

/** Root for a parent whose phone language has books: its shelves, the phone's second language, then Other languages. */
function rootShelves(browse: Browse, language: string): Page {
  const { tracks, origin } = browse;
  const available = browse.languages.filter((code) => tracksOfLanguage(tracks, code).length > 0);
  const second = available.find((code) => code !== language);
  const views = languageViews(tracks, origin, language, available.filter((code) => code !== language));
  const others = views.filter((view) => view.id.startsWith(shelfId(language, Shelf.Others)));
  const shelves = views.filter((view) => !others.includes(view));
  return pageOfItems([...shelves.map(folderItem), ...(second === undefined ? [] : [languageRow(second, language, tracks, origin)]), ...others.map(folderItem)]);
}

function browseRoot(browse: Browse, recursive: boolean): Page {
  const { tracks, origin } = browse;
  if (recursive) return pageOfItems(playable(tracks).map(trackItem));
  const language = browse.languages.find((code) => tracksOfLanguage(tracks, code).length > 0);
  if (language !== undefined) return rootShelves(browse, language);
  return pageOfItems(languageIds(tracks).map((code) => languageItem(code, origin)));
}

function browseOtherLanguages(browse: Browse, target: ShelfTarget): Page {
  const codes = otherLanguages(languageIds(browse.tracks), target.language, target.firstLanguages, (code) => languageNameIn(code, target.language));
  return pageOfItems(codes.map((code) => languageRow(code, target.language, browse.tracks, browse.origin)));
}

function browseTarget(browse: Browse, target: ShelfTarget, recursive: boolean): Page {
  const books = tracksOfLanguage(browse.tracks, target.language);
  if (books.length === 0) throw noSuchItem();
  if (target.shelf === Shelf.Others) return recursive ? pageOfItems([]) : browseOtherLanguages(browse, target);
  const wanted = target.shelf === null ? books : shelfTracks(books, target.shelf, target.language);
  if (recursive) return pageOfItems(playable(wanted).map(trackItem));
  if (target.shelf !== null) return pageOfItems(wanted.map(trackItem));
  return rootLikeShelves(browse, target.language);
}

/** `lang:<code>`: the language's shelves and its Other languages folder (no second-language row: that is the root's alone). */
function rootLikeShelves(browse: Browse, language: string): Page {
  return pageOfItems(languageViews(browse.tracks, browse.origin, language).map(folderItem));
}

function browseOf(browse: Browse, id: string, recursive: boolean): Page {
  if (id === ROOT_ID) return browseRoot(browse, recursive);
  const target = parseShelfId(id);
  if (target === null) throw noSuchItem();
  return browseTarget(browse, target, recursive);
}

function pageOf(source: Page, request: SonosRequest): string {
  const start = Math.min(request.index ?? 0, source.total);
  const wanted = Math.min(Math.max(request.count ?? DEFAULT_PAGE, 0), MAX_PAGE);
  const items = source.items.slice(start, start + wanted);
  return [tag("index", start), tag("count", items.length), tag("total", source.total), ...items].join("");
}

function getMetadata(browse: Browse, request: SonosRequest): Response {
  const page = browseOf(browse, request.id ?? ROOT_ID, request.recursive);
  return resultResponse(SonosAction.GetMetadata, pageOf(page, request));
}

/** A favourite saved on `lang:<code>` still plays: Play queues that language's free books (the recursive browse of the id). */
function savedLanguageItem(browse: Browse, language: string): string {
  const canPlay = playable(tracksOfLanguage(browse.tracks, language)).length > 0;
  return folderItem({ id: languageFolderId(language), title: languageName(language), subtitle: "", art: languageTileUrl(browse.origin, language), canPlay });
}

/** The folder a `lang:` id names, as the row the browse shows for it. */
function folderOfId(browse: Browse, id: string | null): string {
  const target = parseShelfId(id);
  if (target === null || tracksOfLanguage(browse.tracks, target.language).length === 0) throw noSuchItem();
  if (target.shelf === null) return savedLanguageItem(browse, target.language);
  const prefix = shelfId(target.language, target.shelf);
  const view = languageViews(browse.tracks, browse.origin, target.language, target.firstLanguages).find((candidate) => candidate.id.startsWith(prefix));
  if (view === undefined) throw noSuchItem();
  return folderItem(view);
}

function getExtendedMetadata(browse: Browse, request: SonosRequest): Response {
  const track = findTrack(browse.tracks, request.id);
  return resultResponse(SonosAction.GetExtendedMetadata, track !== null ? trackItem(track) : folderOfId(browse, request.id));
}

function getMediaMetadata(tracks: readonly Track[], request: SonosRequest): Response {
  return resultResponse(SonosAction.GetMediaMetadata, trackFields(requireTrack(tracks, request.id)));
}

// Slice 436: getMediaURI is not a play count (Sonos sends it for a queued next track as well); plays are counted from the player's own report at /timePlayed (sonos-reporting.ts). An unplayable request is a fault row (A3).
async function getMediaUri(tracks: readonly Track[], request: SonosRequest, usage: SpeakerUsage | undefined, httpRequest: Request): Promise<Response> {
  const track = findTrack(tracks, request.id);
  if (track === null) {
    const faulting = recordSpeakerFault(usage, SPEAKER_PLATFORM_SONOS, httpRequest).catch(() => false);
    if (usage?.defer === undefined) await faulting;
    else usage.defer(faulting);
    throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NoSuchItem);
  }
  // Slice 437 A1: a paid book is listed with canPlay false. Sonos documents no fault for an unplayable track; Client.LoginUnsupported and the login faults make the app ask the user to upgrade or sign in, and this service has no accounts, so a call that reaches here (a queue or favourite made elsewhere) gets Client.ItemNotFound, which starts no login flow (owner's sandbox decides, Not yet verified).
  if (track.paid) throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NotPlayable);
  return resultResponse(SonosAction.GetMediaURI, escapeXml(track.listenUrl));
}

function getLastUpdate(browse: Browse): Response {
  const inner = [tag("catalog", catalogToken(browse.tracks, browse.origin)), tag("favorites", NO_FAVORITES), tag("pollInterval", POLL_SECONDS)].join("");
  return resultResponse(SonosAction.GetLastUpdate, inner);
}

// Only `root` reads `browse.languages` (the request's Accept-Language); every other id carries its language, because a speaker sends no header (worker/CLAUDE.md).
async function answer(browse: Browse, request: SonosRequest, usage: SpeakerUsage | undefined, httpRequest: Request): Promise<Response> {
  const action = request.action as SonosActionName;
  const { tracks } = browse;
  switch (action) {
    case SonosAction.GetMetadata:
      return getMetadata(browse, request);
    case SonosAction.GetExtendedMetadata:
      return getExtendedMetadata(browse, request);
    case SonosAction.GetMediaMetadata:
      return getMediaMetadata(tracks, request);
    case SonosAction.GetMediaURI:
      return getMediaUri(tracks, request, usage, httpRequest);
    case SonosAction.GetLastUpdate:
      return getLastUpdate(browse);
    default:
      throw new SoapFault(FaultCode.Unknown, FaultMessage.Unsupported);
  }
}

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isSonosPath(pathname: string): boolean {
  return pathname === SONOS_PATH;
}

async function respond(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage: SpeakerUsage | undefined, catalogue: CatalogueRuntime | undefined): Promise<Response> {
  const text = await readBoundedText(request);
  if (text === null) throw new SoapFault(FaultCode.Unknown, FaultMessage.TooLarge);
  const parsed = parseSonosRequest(text);
  if (parsed === null) throw new SoapFault(FaultCode.Unknown, FaultMessage.NotSoap);
  const browse = { tracks: await loadTracks(request, env, pathPrefix, serve, catalogue), origin: new URL(request.url).origin, languages: acceptedLanguages(request.headers.get(ACCEPT_LANGUAGE_HEADER)) };
  return answer(browse, parsed, usage, request);
}

/** The Sonos SMAPI endpoint: POST only, every failure a SOAP fault with HTTP 500. `pathPrefix` is "" on the Dino host and "/dino" under workers.dev. `usage` (Slice 436) records an unplayable getMediaURI as a fault row; plays are counted at /timePlayed, not here. */
export async function handleSonos(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage?: SpeakerUsage, catalogue?: CatalogueRuntime): Promise<Response> {
  if (request.method !== "POST") return new Response(null, { status: NOT_ALLOWED_STATUS, headers: { Allow: ALLOWED_METHODS } });
  try {
    return await respond(request, env, pathPrefix, serve, usage, catalogue);
  } catch (error) {
    // no-fallback: the fault is the answer; a failure is never logged here (ids and titles stay out of logs; stories.ts logs only a catalogue rebuild's error name)
    return faultResponse(error instanceof SoapFault ? error : new SoapFault(FaultCode.Unavailable, FaultMessage.Unavailable));
  }
}
