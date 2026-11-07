// Slice 283: Dino Reader as an anonymous Sonos music service (SMAPI over SOAP) at /sonos (workers.dev: /dino/sonos). The
// catalogue is the store list this Worker already serves (/api/stories, Slice 281's listening tracks): only a book that has a
// track in a language appears, under that language. No household state and no logging of ids or titles (worker/CLAUDE.md).
// Browse tree: root -> one folder per language (own name) -> the books of that language, each one playable track.

import { handleStories } from "./stories.ts";
import type { ListItem, ObjectServer } from "./stories.ts";
import { FaultCode, FaultMessage, SoapFault, SonosAction, escapeXml, faultResponse, parseSonosRequest, readBoundedText, resultResponse, tag } from "./sonos-soap.ts";
import type { SonosActionName, SonosRequest } from "./sonos-soap.ts";

export const SONOS_PATH = "/sonos";
const ROOT_ID = "root";
const LANGUAGE_PREFIX = "lang:";
const TRACK_PREFIX = "track:";
const ID_SEPARATOR = ":";
const SAFE_PART = /^[A-Za-z0-9_-]{1,64}$/;
const TRACK_MIME = "audio/mp4";
const ITEM_TRACK = "track";
const ITEM_CONTAINER = "container";
const ARTIST_FALLBACK = "Dino Reader";
const ALBUM_NAME = "Dino Reader";
const MAX_TITLE = 64;
const MAX_ART_URI = 128;
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

interface Track {
  readonly language: string;
  readonly storyId: string;
  readonly title: string;
  readonly author: string | null;
  readonly seconds: number;
  readonly version: number;
  readonly coverUrl: string | null;
  readonly listenUrl: string;
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
  const art = typeof item.coverUrl === "string" ? `${origin}${item.coverUrl}` : null;
  return {
    language,
    storyId: item.id,
    title,
    author: item.author,
    seconds: pack.listenSeconds,
    version: pack.version,
    coverUrl: art !== null && art.length <= MAX_ART_URI ? art : null,
    listenUrl: `${origin}${pack.listenUrl}`,
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
async function loadTracks(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer): Promise<Track[]> {
  const origin = new URL(request.url).origin;
  const listRequest = new Request(`${origin}${pathPrefix}${STORIES_PATH}`, { method: "GET" });
  const response = await handleStories(listRequest, env, pathPrefix, serve);
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

function findTrack(tracks: readonly Track[], id: string | null): Track | null {
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

/** The language code of a `lang:<code>` id, else null. */
function languageOfId(id: string | null): string | null {
  return id !== null && id.startsWith(LANGUAGE_PREFIX) ? id.slice(LANGUAGE_PREFIX.length) : null;
}

// A value that changes whenever anything Sonos shows changes: a book or its track added, replaced or removed, or a title, author,
// cover or track URL or length edited (meta.json edits need no version bump). FNV-1a over every shown field of the offered tracks.
export function catalogToken(tracks: readonly Track[]): string {
  const lines = tracks
    .map((track) => JSON.stringify([track.language, track.storyId, track.version, track.seconds, track.title, track.author, track.coverUrl, track.listenUrl]))
    .sort();
  let hash = 0x811c9dc5;
  for (const char of lines.join("\n")) {
    hash = Math.imul(hash ^ char.codePointAt(0)!, 0x01000193) >>> 0;
  }
  return `${lines.length}-${hash.toString(16)}`;
}

function trackFields(track: Track): string {
  const meta = [
    tag("artist", clip(track.author ?? ARTIST_FALLBACK, MAX_TITLE)),
    tag("album", ALBUM_NAME),
    track.coverUrl === null ? "" : tag("albumArtURI", track.coverUrl),
    tag("duration", track.seconds),
  ].join("");
  return [tag("id", trackId(track)), tag("itemType", ITEM_TRACK), tag("title", clip(track.title, MAX_TITLE)), tag("mimeType", TRACK_MIME), `<trackMetadata>${meta}</trackMetadata>`].join("");
}

function trackItem(track: Track): string {
  return `<${MEDIA_METADATA}>${trackFields(track)}</${MEDIA_METADATA}>`;
}

function languageItem(code: string): string {
  const fields = [tag("id", `${LANGUAGE_PREFIX}${code}`), tag("itemType", ITEM_CONTAINER), tag("title", clip(languageName(code), MAX_TITLE)), tag("canPlay", false)].join("");
  return `<${MEDIA_COLLECTION}>${fields}</${MEDIA_COLLECTION}>`;
}

function browse(tracks: readonly Track[], id: string, recursive: boolean): Page {
  if (id === ROOT_ID) {
    const items = recursive ? tracks.map(trackItem) : languageIds(tracks).map(languageItem);
    return { items, total: items.length };
  }
  const language = languageOfId(id);
  const books = tracks.filter((track) => track.language === language);
  if (language === null || books.length === 0) throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NoSuchItem);
  return { items: books.map(trackItem), total: books.length };
}

function pageOf(source: Page, request: SonosRequest): string {
  const start = Math.min(request.index ?? 0, source.total);
  const wanted = Math.min(Math.max(request.count ?? DEFAULT_PAGE, 0), MAX_PAGE);
  const items = source.items.slice(start, start + wanted);
  return [tag("index", start), tag("count", items.length), tag("total", source.total), ...items].join("");
}

function getMetadata(tracks: readonly Track[], request: SonosRequest): Response {
  const page = browse(tracks, request.id ?? ROOT_ID, request.recursive);
  return resultResponse(SonosAction.GetMetadata, pageOf(page, request));
}

function getExtendedMetadata(tracks: readonly Track[], request: SonosRequest): Response {
  const track = findTrack(tracks, request.id);
  if (track !== null) return resultResponse(SonosAction.GetExtendedMetadata, trackItem(track));
  const language = languageOfId(request.id);
  if (language === null || !tracks.some((candidate) => candidate.language === language)) throw new SoapFault(FaultCode.ItemNotFound, FaultMessage.NoSuchItem);
  return resultResponse(SonosAction.GetExtendedMetadata, languageItem(language));
}

function getMediaMetadata(tracks: readonly Track[], request: SonosRequest): Response {
  return resultResponse(SonosAction.GetMediaMetadata, trackFields(requireTrack(tracks, request.id)));
}

function getMediaUri(tracks: readonly Track[], request: SonosRequest): Response {
  return resultResponse(SonosAction.GetMediaURI, escapeXml(requireTrack(tracks, request.id).listenUrl));
}

function getLastUpdate(tracks: readonly Track[]): Response {
  const inner = [tag("catalog", catalogToken(tracks)), tag("favorites", NO_FAVORITES), tag("pollInterval", POLL_SECONDS)].join("");
  return resultResponse(SonosAction.GetLastUpdate, inner);
}

function answer(tracks: readonly Track[], request: SonosRequest): Response {
  const action = request.action as SonosActionName;
  switch (action) {
    case SonosAction.GetMetadata:
      return getMetadata(tracks, request);
    case SonosAction.GetExtendedMetadata:
      return getExtendedMetadata(tracks, request);
    case SonosAction.GetMediaMetadata:
      return getMediaMetadata(tracks, request);
    case SonosAction.GetMediaURI:
      return getMediaUri(tracks, request);
    case SonosAction.GetLastUpdate:
      return getLastUpdate(tracks);
    default:
      throw new SoapFault(FaultCode.Unknown, FaultMessage.Unsupported);
  }
}

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isSonosPath(pathname: string): boolean {
  return pathname === SONOS_PATH;
}

async function respond(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer): Promise<Response> {
  const text = await readBoundedText(request);
  if (text === null) throw new SoapFault(FaultCode.Unknown, FaultMessage.TooLarge);
  const parsed = parseSonosRequest(text);
  if (parsed === null) throw new SoapFault(FaultCode.Unknown, FaultMessage.NotSoap);
  return answer(await loadTracks(request, env, pathPrefix, serve), parsed);
}

/** The Sonos SMAPI endpoint: POST only, every failure a SOAP fault with HTTP 500. `pathPrefix` is "" on the Dino host and "/dino" under workers.dev. */
export async function handleSonos(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer): Promise<Response> {
  if (request.method !== "POST") return new Response(null, { status: NOT_ALLOWED_STATUS, headers: { Allow: ALLOWED_METHODS } });
  try {
    return await respond(request, env, pathPrefix, serve);
  } catch (error) {
    // no-fallback: the fault is the answer; a failure is never logged (ids and titles stay out of logs)
    return faultResponse(error instanceof SoapFault ? error : new SoapFault(FaultCode.Unavailable, FaultMessage.Unavailable));
  }
}
