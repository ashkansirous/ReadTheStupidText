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
import { LABEL_LANGUAGES, Shelf, bookSubtitle, languageNameIn, languageSubtitle, shelfSubtitle, shelfTitle, storiesLabel } from "./sonos-labels.ts";
import type { ShelfName } from "./sonos-labels.ts";
import { languageFolderId, otherLanguages, parseShelfId, shelfId, shelfTracks, shelvesOf } from "./sonos-shelves.ts";
import type { ShelfTarget } from "./sonos-shelves.ts";
import { SEARCH_ID, isSearchCategory, searchCategories, searchTracks } from "./sonos-search.ts";
import type { CategoryRow, SearchHit } from "./sonos-search.ts";
import type { ServedFile } from "./downloads.ts";
import { STORY_LANGUAGES, answerLinkAction, currentWord, guarded, handleLinkApi, homeForToken, homeKeyOf, isJsonRequest, isLinkAction, languageNameFor, requireHome, saveStoryLanguage, storyLanguageTap, storyLanguageTitle, subscriptionActive, subscriptionForPlayback, subscriptionKnown } from "./sonos-link.ts";
import type { Home, SonosLinks } from "./sonos-link.ts";
import { SPEAKER_PLATFORM_SONOS, recordSpeakerFault } from "./speaker-usage.ts";
import type { SpeakerUsage } from "./speaker-usage.ts";

export const SONOS_PATH = "/sonos";
// Slice 439: the spoken "subscribe in the Dino app" message, one file per story language, rendered once with the apps' narrator voices (tools/dino/sonos-subscribe) and kept in R2 under SUBSCRIBE_KEY_PREFIX.
const SUBSCRIBE_PATH_PREFIX = `${SONOS_PATH}/subscribe/`;
const SUBSCRIBE_FILE = /^([a-z]{2})\.m4a$/;
const SUBSCRIBE_KEY_PREFIX = "dinoreader/sonos/subscribe-";
const SUBSCRIBE_CACHE = "public, max-age=3600";
const SUBSCRIBE_UNAVAILABLE = "story-unavailable";
const FALLBACK_MESSAGE_LANGUAGE = "en";
const HTTP_HEADERS_TAG = "httpHeaders";
// Slice 439 (review 1, finding 2): a paid book's audio is served by this Worker at `/sonos/media/<language>/<storyId>.m4a`. The player sends the home's own Sonos auth token there (`Authorization: Bearer`, the
// `httpHeaders` entry of getMediaURI, https://docs.sonos.com/docs/getmediauri); the route finds the home by that token, asks the store about the home's stored proof, and streams the book or the subscribe message.
// The proof itself never leaves the Worker.
const MEDIA_PATH_PREFIX = `${SONOS_PATH}/media/`;
const MEDIA_FILE = /^([A-Za-z0-9_-]{1,64})\/([A-Za-z0-9_-]{1,64})\.m4a$/;
const AUTHORIZATION_HEADER = "Authorization";
const BEARER_PREFIX = "Bearer ";
const PRIVATE_NO_STORE = "private, no-store";
const RANGE_HEADER = "Range";
const FORWARDED_HEADERS = [RANGE_HEADER, "If-Range", "If-None-Match", "If-Modified-Since"];
const STATUS_UNAUTHORIZED = 401;
const STATUS_NOT_FOUND = 404;
const STATUS_UNAVAILABLE = 503;
const STATUS_BAD_GATEWAY = 502;
const ROOT_ID = "root";
const TRACK_PREFIX = "track:";
const ID_SEPARATOR = ":";
const SAFE_PART = /^[A-Za-z0-9_-]{1,64}$/;
const TRACK_MIME = "audio/mp4";
const ITEM_TRACK = "track";
const ITEM_CONTAINER = "container";
const ITEM_SEARCH = "search";
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
// Slice 438: a linked home's folder ids carry the story language and the phone's language (`lang:fa-en/bedtime`), because a speaker sends no Accept-Language;
// `pick:<label>` is the story-language picker and `setlang:<code>:<label>` saves the choice (a browse of it answers with the new root).
const LABEL_JOIN = "-";
const PICK_PREFIX = "pick:";
const SET_PREFIX = "setlang:";
const FALLBACK_LABEL = "en";
const PART_SEPARATOR = " · ";

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
  /** Slice 439: a paid book the linked home's active subscription opens (set only on the copy a browse works with; `paid` is then false). */
  readonly unlocked?: boolean;
  /** Slice 439 (review 1, finding 9): a paid book of a linked home without an active subscription; it is listed playable so a press on it plays the spoken subscribe message (set only on the copy a browse works with; `paid` stays true). */
  readonly subscribePrompt?: boolean;
  /** Slice 438: the book's title in each language it has one, for the phone-language title under a story language's row (absent in older callers). */
  readonly altTitles?: Readonly<Record<string, string>>;
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
    altTitles: { [item.language]: item.title, ...(item.titles ?? {}) },
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

/** The language code a folder id carries: the story language alone, or `<story>-<label>` when the phone's language (the words) differs. */
function idLanguage(language: string, label: string): string {
  return label === language ? language : `${language}${LABEL_JOIN}${label}`;
}

/** Splits an id's language code into the story language and the label language (the label is the story language when the id has none). */
function splitLanguage(code: string): { readonly language: string; readonly label: string } {
  const [language = "", label] = code.split(LABEL_JOIN);
  return { language, label: label ?? language };
}

/** `label` is the language of the words (the phone's), `language` the books' language; the anonymous browse has them equal. */
function shelfView(origin: string, language: string, label: string, shelf: ShelfName, books: readonly Track[]): FolderView {
  const canPlay = shelf !== Shelf.Others && playable(books).length > 0;
  return { id: shelfId(idLanguage(language, label), shelf), title: shelfTitle(label, shelf), subtitle: shelfSubtitle(label, shelf, books.length), art: folderArtUrl(origin, shelf), canPlay, books };
}

function tracksOfLanguage(tracks: readonly Track[], language: string): Track[] {
  return tracks.filter((track) => track.language === language);
}

/** The shelves of one language (empty ones left out), then its Other languages folder when the catalogue has another language. */
function languageViews(tracks: readonly Track[], origin: string, language: string, firstLanguages: readonly string[] = [], label: string = language, withOthers = true): FolderView[] {
  const shelves = shelvesOf(tracksOfLanguage(tracks, language), language).map((row) => shelfView(origin, language, label, row.shelf, row.tracks));
  const others = withOthers ? otherLanguages(languageIds(tracks), language, firstLanguages, (code) => languageNameIn(code, label)) : [];
  if (others.length === 0) return shelves;
  const folder = shelfView(origin, language, label, Shelf.Others, []);
  return [...shelves, { ...folder, id: shelfId(idLanguage(language, label), Shelf.Others, firstLanguages.filter((code) => others.includes(code))), subtitle: shelfSubtitle(label, Shelf.Others, others.length) }];
}

// A value that changes whenever anything Sonos shows changes: a book or its track added, replaced or removed, a title, author,
// cover, track URL, length, tag, age range, date or access edited (meta.json edits need no version bump), and every shelf's id,
// words, picture and book order. FNV-1a over those lines.
export function catalogToken(tracks: readonly Track[], origin = ""): string {
  const trackLines = tracks.map((track) =>
    JSON.stringify([track.language, track.storyId, track.version, track.seconds, track.title, track.author, track.coverUrl, track.squareCoverUrl, track.listenUrl, track.tags, track.ageMin, track.ageMax, track.added, track.paid, track.unlocked === true, track.subscribePrompt === true]),
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

/**
 * The line under a book. In the story language's own words it is "Ages 3–5 · 6 min · Free"; with a phone language that differs and a title in it,
 * the title leads and the age part drops: "The Lion and the Mouse · 6 min · Free" (Slice 438, SET-Saved).
 */
function bookLine(track: Track, label: string): string {
  const title = label === track.language ? undefined : track.altTitles?.[label];
  if (title === undefined || title === track.title) return bookSubtitle(track, label);
  return `${title}${PART_SEPARATOR}${bookSubtitle({ ...track, ageMin: null, ageMax: null }, label)}`;
}

function trackFields(track: Track, label: string = track.language, subtitleOverride?: string): string {
  const subtitle = subtitleOverride ?? bookLine(track, label);
  const art = track.squareCoverUrl ?? track.coverUrl;
  const meta = [
    tag("artist", clip(track.author ?? ARTIST_FALLBACK, MAX_TITLE)),
    tag("album", clip(subtitle === "" ? ALBUM_NAME : subtitle, MAX_TITLE)),
    tag("duration", track.seconds),
    art === null ? "" : tag("albumArtURI", art),
    tag("canPlay", !track.paid || track.subscribePrompt === true),
    tag("canSkip", true),
  ].join("");
  return [tag("id", trackId(track)), tag("itemType", ITEM_TRACK), tag("title", clip(track.title, MAX_TITLE)), tag("mimeType", TRACK_MIME), `<trackMetadata>${meta}</trackMetadata>`].join("");
}

function trackItem(track: Track, label: string = track.language, subtitleOverride?: string): string {
  return `<${MEDIA_METADATA}>${trackFields(track, label, subtitleOverride)}</${MEDIA_METADATA}>`;
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
  /** Slice 438: the linked home (its saved story language), or null for a browse with no link checks (tests, older callers). */
  readonly home: Home | null;
  /** Slice 439: "" on the Dino host, "/dino" under workers.dev; the spoken message's address needs it. */
  readonly pathPrefix: string;
}

/** The language of the words for a browse: the request's first Accept-Language the labels cover, else English. */
function labelOfRequest(browse: Browse): string {
  return browse.languages.find((code) => LABEL_LANGUAGES.includes(code)) ?? FALLBACK_LABEL;
}

/** A linked home's story language: the saved one while it has books, else the phone's first language with books, else the first language of the catalogue. */
function storyLanguageOf(browse: Browse, home: Home): string {
  const { tracks } = browse;
  const has = (code: string): boolean => tracksOfLanguage(tracks, code).length > 0;
  return [home.storyLanguage, ...browse.languages].find(has) ?? languageIds(tracks)[0] ?? home.storyLanguage;
}

/** The label language for a track asked for by id: the anonymous browse words it in the book's language, a linked one in the phone's. */
function trackLabel(browse: Browse, track: Track): string {
  return browse.home === null ? track.language : labelOfRequest(browse);
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

/** A language's name in the phone's language as the story-language row and picker word it (the mock says "Farsi" and "Norwegian" in English). */
function nameIn(code: string, label: string): string {
  return languageNameFor(code, label, languageNameIn(code, label));
}

/** Slice 438 root for a linked home: the "Story language: <name>" row, then the shelves of the saved language in the phone's words. No Other languages row. */
function linkedRoot(browse: Browse, home: Home, recursive: boolean): Page {
  const { tracks, origin } = browse;
  const language = storyLanguageOf(browse, home);
  const label = labelOfRequest(browse);
  if (recursive) return pageOfItems(playable(tracksOfLanguage(tracks, language)).map((track) => trackItem(track, label)));
  const row = folderItem({ id: `${PICK_PREFIX}${label}`, title: storyLanguageTitle(label, nameIn(language, label)), subtitle: storyLanguageTap(label), art: languageTileUrl(origin, language), canPlay: false });
  return pageOfItems([row, ...languageViews(tracks, origin, language, [], label, false).map(folderItem)]);
}

/** The picker: the saved language first ("English · current"), then the phone's languages, then the rest by name in the phone's language ("Farsi · 8 stories"). */
function browsePick(browse: Browse, home: Home, label: string): Page {
  const current = storyLanguageOf(browse, home);
  const codes = [current, ...otherLanguages(languageIds(browse.tracks), current, browse.languages, (code) => nameIn(code, label))];
  const row = (code: string): string => {
    const detail = code === current ? currentWord(label) : storiesLabel(label, tracksOfLanguage(browse.tracks, code).length);
    return folderItem({ id: `${SET_PREFIX}${code}${ID_SEPARATOR}${label}`, title: languageName(code), subtitle: `${nameIn(code, label)}${PART_SEPARATOR}${detail}`, art: languageTileUrl(browse.origin, code), canPlay: false });
  };
  return pageOfItems(codes.map(row));
}

function browseRoot(browse: Browse, recursive: boolean): Page {
  const { tracks, origin } = browse;
  if (browse.home !== null) return linkedRoot(browse, browse.home, recursive);
  if (recursive) return pageOfItems(playable(tracks).map((track) => trackItem(track)));
  const language = browse.languages.find((code) => tracksOfLanguage(tracks, code).length > 0);
  if (language !== undefined) return rootShelves(browse, language);
  return pageOfItems(languageIds(tracks).map((code) => languageItem(code, origin)));
}

function browseOtherLanguages(browse: Browse, target: ShelfTarget, label: string): Page {
  const codes = otherLanguages(languageIds(browse.tracks), target.language, target.firstLanguages, (code) => languageNameIn(code, label));
  return pageOfItems(codes.map((code) => languageRow(code, label, browse.tracks, browse.origin)));
}

function browseTarget(browse: Browse, target: ShelfTarget, label: string, recursive: boolean): Page {
  const books = tracksOfLanguage(browse.tracks, target.language);
  if (books.length === 0) throw noSuchItem();
  if (target.shelf === Shelf.Others) {
    if (browse.home !== null) throw noSuchItem(); // a linked home has one story language and no Other languages row
    return recursive ? pageOfItems([]) : browseOtherLanguages(browse, target, label);
  }
  const wanted = target.shelf === null ? books : shelfTracks(books, target.shelf, target.language);
  if (recursive) return pageOfItems(playable(wanted).map((track) => trackItem(track, label)));
  if (target.shelf !== null) return pageOfItems(wanted.map((track) => trackItem(track, label)));
  return rootLikeShelves(browse, target.language, label);
}

/** `lang:<code>`: the language's shelves and its Other languages folder (no second-language row: that is the root's alone; a linked home has no Other languages folder). */
function rootLikeShelves(browse: Browse, language: string, label: string): Page {
  return pageOfItems(languageViews(browse.tracks, browse.origin, language, [], label, browse.home === null).map(folderItem));
}

/** What a `lang:` id names: the shelf target for the story language, and the language of the words the id carries. */
function labelledTarget(id: string | null): { readonly target: ShelfTarget; readonly label: string } | null {
  const raw = parseShelfId(id);
  if (raw === null) return null;
  const { language, label } = splitLanguage(raw.language);
  return { target: { ...raw, language }, label };
}

function browseOf(browse: Browse, id: string, recursive: boolean): Page {
  if (id === ROOT_ID) return browseRoot(browse, recursive);
  if (id === SEARCH_ID) return searchContainer(browse);
  if (id.startsWith(PICK_PREFIX) && browse.home !== null) return browsePick(browse, browse.home, id.slice(PICK_PREFIX.length));
  const named = labelledTarget(id);
  if (named === null) throw noSuchItem();
  return browseTarget(browse, named.target, named.label, recursive);
}

/** Slice 440: the search container a desktop app browses: one `search` item per category (the same ids as the presentation map's). */
function searchContainer(browse: Browse): Page {
  const row = (category: CategoryRow): string => `<${MEDIA_COLLECTION}>${tag("id", category.id)}${tag("title", category.name)}${tag("itemType", ITEM_SEARCH)}</${MEDIA_COLLECTION}>`;
  return pageOfItems(searchCategories(labelOfRequest(browse)).map(row));
}

/** The story language a search's Stories category looks in: a linked home's saved one, else the phone's first language with books, else the catalogue's first. */
function searchLanguageOf(browse: Browse): string {
  if (browse.home !== null) return storyLanguageOf(browse, browse.home);
  return browse.languages.find((code) => tracksOfLanguage(browse.tracks, code).length > 0) ?? languageIds(browse.tracks)[0] ?? FALLBACK_LABEL;
}

function searchRow(hit: SearchHit, label: string): string {
  const subtitle = hit.withLanguage ? `${languageNameIn(hit.track.language, label)}${PART_SEPARATOR}${bookSubtitle(hit.track, label)}` : undefined;
  return trackItem(hit.track, label, subtitle);
}

/** Slice 440: `search` (id = category, term, index, count); an unknown category is an unknown item, an empty term an empty page. */
function search(browse: Browse, request: SonosRequest): Response {
  if (!isSearchCategory(request.id)) throw noSuchItem();
  const label = labelOfRequest(browse);
  const hits = searchTracks(browse.tracks, request.id, request.term, searchLanguageOf(browse));
  return resultResponse(SonosAction.Search, pageOf(pageOfItems(hits.map((hit) => searchRow(hit, label))), request));
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
  const named = labelledTarget(id);
  if (named === null || tracksOfLanguage(browse.tracks, named.target.language).length === 0) throw noSuchItem();
  const { target, label } = named;
  if (target.shelf === null) return savedLanguageItem(browse, target.language);
  const prefix = shelfId(idLanguage(target.language, label), target.shelf);
  const view = languageViews(browse.tracks, browse.origin, target.language, target.firstLanguages, label, browse.home === null).find((candidate) => candidate.id.startsWith(prefix));
  if (view === undefined) throw noSuchItem();
  return folderItem(view);
}

function getExtendedMetadata(browse: Browse, request: SonosRequest): Response {
  const track = findTrack(browse.tracks, request.id);
  return resultResponse(SonosAction.GetExtendedMetadata, track !== null ? trackItem(track, trackLabel(browse, track)) : folderOfId(browse, request.id));
}

function getMediaMetadata(browse: Browse, request: SonosRequest): Response {
  const track = requireTrack(browse.tracks, request.id);
  return resultResponse(SonosAction.GetMediaMetadata, trackFields(track, trackLabel(browse, track)));
}

// Slice 436: getMediaURI is not a play count (Sonos sends it for a queued next track as well); plays are counted from the player's own report at /timePlayed (sonos-reporting.ts). An unplayable request is a fault row (A3).
async function getMediaUri(browse: Browse, request: SonosRequest, usage: SpeakerUsage | undefined, httpRequest: Request): Promise<Response> {
  const track = findTrack(browse.tracks, request.id);
  if (track === null) {
    const faulting = recordSpeakerFault(usage, SPEAKER_PLATFORM_SONOS, httpRequest).catch(() => false);
    if (usage?.defer === undefined) await faulting;
    else usage.defer(faulting);
    throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NoSuchItem);
  }
  // Slice 437 A1: an anonymous call for a paid book (a queue or favourite made elsewhere) gets Client.ItemNotFound, which starts no login flow; this service has no accounts there (owner's sandbox decides, Not yet verified).
  // Slice 439: a linked home's paid book answers with this Worker's media URL and the home's own Sonos auth token as the `Authorization` header (Sonos holds that token already). The media route asks the store at play time and
  // streams the story or the spoken "subscribe in the Dino app" message; getMediaURI itself never calls the store (review 1, finding 10) and never carries the subscription proof (finding 2).
  if (track.paid) {
    const token = request.login?.token;
    if (browse.home === null || token === undefined) throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NotPlayable);
    return withHttpHeader(resultResponse(SonosAction.GetMediaURI, escapeXml(mediaUrl(browse, track))), AUTHORIZATION_HEADER, `${BEARER_PREFIX}${token}`);
  }
  return resultResponse(SonosAction.GetMediaURI, escapeXml(track.listenUrl));
}

function mediaUrl(browse: Browse, track: Track): string {
  return `${browse.origin}${browse.pathPrefix}${MEDIA_PATH_PREFIX}${track.language}/${track.storyId}.m4a`;
}

function spokenLanguage(language: string): string {
  return STORY_LANGUAGES.includes(language) ? language : FALLBACK_MESSAGE_LANGUAGE;
}

/** The language and book id a `/sonos/media/<language>/<storyId>.m4a` path names, or null for any other path. */
function mediaOf(pathname: string): { readonly language: string; readonly storyId: string } | null {
  if (!pathname.startsWith(MEDIA_PATH_PREFIX)) return null;
  const found = MEDIA_FILE.exec(pathname.slice(MEDIA_PATH_PREFIX.length));
  return found === null ? null : { language: found[1] as string, storyId: found[2] as string };
}

function bearerOf(request: Request): string | null {
  const header = request.headers.get(AUTHORIZATION_HEADER) ?? "";
  const token = header.startsWith(BEARER_PREFIX) ? header.slice(BEARER_PREFIX.length).trim() : "";
  return token === "" ? null : token;
}

function privately(response: Response): Response {
  const headers = new Headers(response.headers);
  headers.set("Cache-Control", PRIVATE_NO_STORE);
  return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
}

/** The story's own request, with only the headers a ranged or conditional read needs; the subscription was checked here, so the paid gate is not asked again. */
function listenRequestOf(request: Request, track: Track): Request {
  const headers = new Headers();
  for (const name of FORWARDED_HEADERS) {
    const value = request.headers.get(name);
    if (value !== null) headers.set(name, value);
  }
  return new Request(track.listenUrl, { method: request.method, headers });
}

interface MediaWorld {
  readonly env: { DOWNLOADS?: R2Bucket };
  readonly pathPrefix: string;
  readonly serve: ObjectServer;
  readonly catalogue: CatalogueRuntime | undefined;
  readonly links: SonosLinks | undefined;
}

async function mediaHome(request: Request, links: SonosLinks | undefined): Promise<Home | null> {
  const token = bearerOf(request);
  if (links?.db === undefined || token === null) return null;
  const db = links.db;
  return guarded(() => homeForToken(db, token));
}

// Slice 439 (review 1, finding 2): the media route. 401 for a token that is not a linked home's, 404 for a book that is not paid, the story for an active (or grace) subscription, else the subscribe message.
async function serveMedia(request: Request, world: MediaWorld, wanted: { readonly language: string; readonly storyId: string }): Promise<Response> {
  const home = await mediaHome(request, world.links);
  if (home === null || world.links === undefined) return new Response(null, { status: STATUS_UNAUTHORIZED });
  const tracks = await loadTracks(request, world.env, world.pathPrefix, world.serve, world.catalogue);
  const track = tracks.find((candidate) => candidate.paid && candidate.language === wanted.language && candidate.storyId === wanted.storyId);
  if (track === undefined) return new Response(null, { status: STATUS_NOT_FOUND });
  // Review 3, finding 5: only the first request of a play asks the store; a Range or HEAD request is a continuation and uses the kept answer, so the store-call limit cannot cut a book mid-play.
  const continuation = request.method === "HEAD" || request.headers.has(RANGE_HEADER);
  const subscribed = await subscriptionForPlayback(home, world.links, homeKeyOf(home), continuation);
  // Review 2, finding 5: a store that cannot be asked (and no kept answer) is not "not subscribed": the player is told to retry, never given the subscribe clip.
  if (subscribed === null) return new Response(null, { status: STATUS_UNAVAILABLE });
  if (!subscribed) return privately(await serveSubscribeForMedia(request, world.env, world.serve, spokenLanguage(track.language)));
  return privately(await handleStories(listenRequestOf(request, track), world.env, world.pathPrefix, world.serve, undefined, undefined, undefined, world.catalogue));
}

async function answerMedia(request: Request, world: MediaWorld, wanted: { readonly language: string; readonly storyId: string }): Promise<Response> {
  try {
    return await serveMedia(request, world, wanted);
  } catch {
    // no-fallback: a failure of D1 or the catalogue is a plain 503 here (the player retries); guarded() already logged the error name
    return new Response(null, { status: STATUS_UNAVAILABLE });
  }
}

/** The response with one `httpHeaders` entry added after the result (resultResponse has no place for it). */
async function withHttpHeader(response: Response, name: string, value: string): Promise<Response> {
  const entry = `<${HTTP_HEADERS_TAG}><httpHeader>${tag("header", name)}${tag("value", value)}</httpHeader></${HTTP_HEADERS_TAG}>`;
  const closing = `</${SonosAction.GetMediaURI}Response>`;
  const text = (await response.text()).replace(closing, `${entry}${closing}`);
  return new Response(text, { status: response.status, headers: response.headers });
}

/** True for the one file route this module serves besides SMAPI: `/sonos/subscribe/<language>.m4a`. */
function subscribeLanguageOf(pathname: string): string | null {
  if (!pathname.startsWith(SUBSCRIBE_PATH_PREFIX)) return null;
  const language = SUBSCRIBE_FILE.exec(pathname.slice(SUBSCRIBE_PATH_PREFIX.length))?.[1];
  return language !== undefined && STORY_LANGUAGES.includes(language) ? language : null;
}

function serveSubscribeMessage(request: Request, env: { DOWNLOADS?: R2Bucket }, serve: ObjectServer, language: string): Promise<Response> {
  if (env.DOWNLOADS === undefined) return Promise.resolve(new Response(null, { status: 404 }));
  const fileName = `subscribe-${language}.m4a`;
  const file: ServedFile = { key: `${SUBSCRIBE_KEY_PREFIX}${language}.m4a`, contentType: TRACK_MIME, fileName, disposition: "inline", unavailable: SUBSCRIBE_UNAVAILABLE };
  return serve(request, env.DOWNLOADS, file, SUBSCRIBE_CACHE);
}

const SUBSCRIBE_MISSING_LOG = "[sonos] subscribe clip missing";

/** True when `serve` could not find the clip: the 502 `story-unavailable` of a missing R2 object, or the 404 of a Worker with no bucket. */
function clipMissing(response: Response): boolean {
  return response.status === STATUS_NOT_FOUND || response.status === STATUS_BAD_GATEWAY;
}

/**
 * Review 3, finding 6: the clip a paid book's media route plays to a home without a subscription. A language whose clip is not in R2 yet plays the English one; with that missing too the answer is a plain
 * 503 (the player retries), never a 404 or a 502 error body; each miss is logged by language only.
 */
async function serveSubscribeForMedia(request: Request, env: { DOWNLOADS?: R2Bucket }, serve: ObjectServer, language: string): Promise<Response> {
  const wanted = await serveSubscribeMessage(request, env, serve, language);
  if (!clipMissing(wanted)) return wanted;
  console.warn(`${SUBSCRIBE_MISSING_LOG} language=${language}`);
  if (language === FALLBACK_MESSAGE_LANGUAGE) return new Response(null, { status: STATUS_UNAVAILABLE });
  const english = await serveSubscribeMessage(request, env, serve, FALLBACK_MESSAGE_LANGUAGE);
  if (!clipMissing(english)) return english;
  console.warn(`${SUBSCRIBE_MISSING_LOG} language=${FALLBACK_MESSAGE_LANGUAGE}`);
  return new Response(null, { status: STATUS_UNAVAILABLE });
}

function getLastUpdate(browse: Browse): Response {
  // Slice 438: a linked home's saved language is part of the token, so its root is fetched again after the language changes (the catalogue itself is unchanged).
  const token = catalogToken(browse.tracks, browse.origin);
  const inner = [tag("catalog", browse.home === null ? token : `${token}${LABEL_JOIN}${browse.home.storyLanguage}`), tag("favorites", NO_FAVORITES), tag("pollInterval", POLL_SECONDS)].join("");
  return resultResponse(SonosAction.GetLastUpdate, inner);
}

// Only `root` reads `browse.languages` (the request's Accept-Language); every other id carries its language, because a speaker sends no header (worker/CLAUDE.md).
/** `setlang:<code>:<label>` as the story language and the words' language it names, or null for any other id. */
function parseSetLanguage(id: string | null): { readonly code: string; readonly label: string } | null {
  if (id === null || !id.startsWith(SET_PREFIX)) return null;
  const [code = "", label = "", extra] = id.slice(SET_PREFIX.length).split(ID_SEPARATOR);
  return extra !== undefined || !SAFE_PART.test(code) || !SAFE_PART.test(label) ? null : { code, label };
}

// Slice 438: browsing `setlang:<code>:<label>` saves that language for the home and answers with the root in it (the words stay in the phone's language, `label`).
async function chooseLanguage(browse: Browse, request: SonosRequest, links: SonosLinks | undefined): Promise<Response> {
  const chosen = parseSetLanguage(request.id);
  const db = links?.db;
  if (chosen === null || browse.home === null || db === undefined || tracksOfLanguage(browse.tracks, chosen.code).length === 0) throw noSuchItem();
  const home = browse.home;
  const stored = await guarded(() => saveStoryLanguage(db, home, chosen.code));
  if (!stored) throw new SoapFault(FaultCode.AuthTokenExpired, FaultMessage.NotLoggedIn);
  const saved = { ...browse, home: { ...home, storyLanguage: chosen.code }, languages: [chosen.label] };
  return getMetadata(saved, { ...request, id: ROOT_ID });
}

async function answer(browse: Browse, request: SonosRequest, usage: SpeakerUsage | undefined, httpRequest: Request, links: SonosLinks | undefined): Promise<Response> {
  const action = request.action as SonosActionName;
  switch (action) {
    case SonosAction.GetMetadata:
      return parseSetLanguage(request.id) === null ? getMetadata(browse, request) : chooseLanguage(browse, request, links);
    case SonosAction.GetExtendedMetadata:
      return getExtendedMetadata(browse, request);
    case SonosAction.GetMediaMetadata:
      return getMediaMetadata(browse, request);
    case SonosAction.GetMediaURI:
      return getMediaUri(browse, request, usage, httpRequest);
    case SonosAction.GetLastUpdate:
      return getLastUpdate(browse);
    case SonosAction.Search:
      return search(browse, request);
    default:
      throw new SoapFault(FaultCode.Unknown, FaultMessage.Unsupported);
  }
}

/** True for the paths this module answers (after the `/dino` prefix is stripped): the SMAPI endpoint and, since Slice 439, the spoken subscribe message files and the paid-book media route. */
export function isSonosPath(pathname: string): boolean {
  return pathname === SONOS_PATH || subscribeLanguageOf(pathname) !== null || mediaOf(pathname) !== null;
}

/**
 * Slice 439: a linked home whose subscription is active sees its paid books as plain books (`unlocked`); a linked home without one sees them as paid but playable (`subscribePrompt`: a press plays the spoken
 * message, review 1 finding 9); an anonymous browse (no home) sees them as paid and not playable.
 */
function unlockPaid(tracks: readonly Track[], subscribed: boolean, linked: boolean): readonly Track[] {
  if (!linked) return tracks;
  return tracks.map((track) => {
    if (!track.paid) return track;
    return subscribed ? { ...track, paid: false, unlocked: true } : { ...track, subscribePrompt: true };
  });
}

/**
 * Whether the home's subscription is active, as the call needs it. Only the calls that show a book's state ask the store (its answer is cached by the subscription source, and a store error keeps the last answer):
 * getMediaURI needs none (the media route asks at play time) and the poll for a changed catalogue (getLastUpdate) only reads the last answer, so a poll never reaches the store (review 1, finding 10).
 */
async function isSubscribed(action: string, home: Home | null, links: SonosLinks | undefined): Promise<boolean> {
  if (home === null || links === undefined || action === SonosAction.GetMediaURI) return false;
  return action === SonosAction.GetLastUpdate ? subscriptionKnown(home, links) : subscriptionActive(home, links, homeKeyOf(home));
}

async function respond(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage: SpeakerUsage | undefined, catalogue: CatalogueRuntime | undefined, links: SonosLinks | undefined): Promise<Response> {
  const text = await readBoundedText(request);
  if (text === null) throw new SoapFault(FaultCode.Unknown, FaultMessage.TooLarge);
  const parsed = parseSonosRequest(text);
  if (parsed === null) throw new SoapFault(FaultCode.Unknown, FaultMessage.NotSoap);
  const origin = new URL(request.url).origin;
  if (isLinkAction(parsed.action)) {
    if (links === undefined) throw new SoapFault(FaultCode.Unknown, FaultMessage.Unsupported);
    return answerLinkAction(parsed, links, origin);
  }
  // Slice 438: with links, every other call needs the home's login token (an unlinked home gets Client.AuthTokenExpired, which makes Sonos ask the grown-up to link again).
  const home = links === undefined ? null : await requireHome(parsed, links);
  const subscribed = await isSubscribed(parsed.action, home, links);
  const tracks = unlockPaid(await loadTracks(request, env, pathPrefix, serve, catalogue), subscribed, home !== null);
  const browse = { tracks, origin, languages: acceptedLanguages(request.headers.get(ACCEPT_LANGUAGE_HEADER)), home, pathPrefix };
  return answer(browse, parsed, usage, request, links);
}

/**
 * The Sonos SMAPI endpoint: POST only, every failure a SOAP fault with HTTP 500. `pathPrefix` is "" on the Dino host and "/dino" under workers.dev. `usage` (Slice 436) records an unplayable getMediaURI as a fault row; plays are counted at /timePlayed, not here.
 * `links` (Slice 438) turns the link on: the Dino app's JSON calls (confirm, unlink) are answered here too, and without `links` the service stays the anonymous one of Slice 283.
 */
export async function handleSonos(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage?: SpeakerUsage, catalogue?: CatalogueRuntime, links?: SonosLinks): Promise<Response> {
  const spoken = subscribeLanguageOf(new URL(request.url).pathname);
  const media = mediaOf(new URL(request.url).pathname);
  if (spoken !== null || media !== null) {
    if (request.method !== "GET" && request.method !== "HEAD") return new Response(null, { status: NOT_ALLOWED_STATUS, headers: { Allow: "GET, HEAD" } });
    return spoken !== null ? serveSubscribeMessage(request, env, serve, spoken) : answerMedia(request, { env, pathPrefix, serve, catalogue, links }, media as { language: string; storyId: string });
  }
  if (request.method !== "POST") return new Response(null, { status: NOT_ALLOWED_STATUS, headers: { Allow: ALLOWED_METHODS } });
  if (links !== undefined && isJsonRequest(request)) return handleLinkApi(request, links);
  try {
    return await respond(request, env, pathPrefix, serve, usage, catalogue, links);
  } catch (error) {
    // no-fallback: the fault is the answer; a failure is never logged here (ids and titles stay out of logs; stories.ts logs only a catalogue rebuild's error name)
    return faultResponse(error instanceof SoapFault ? error : new SoapFault(FaultCode.Unavailable, FaultMessage.Unavailable));
  }
}
