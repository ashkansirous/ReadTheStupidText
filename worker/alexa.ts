// Slice 282: Dino Reader as an anonymous Alexa kid skill (custom skill, AudioPlayer) at /alexa (workers.dev: /dino/alexa).
// The catalogue is the store list this Worker already serves (/api/stories, Slice 281's listening tracks), English track only.
// No server state: Alexa owns playback, and every Play directive carries a token `v1|<book>|<lang>|<version>|<segmentStartMs>` that
// Alexa sends back with each later request, so pause/resume/next need nothing stored. No household, user, device or skill-instance
// id is kept and nothing is logged (worker/CLAUDE.md). Request signature checks live in alexa-verify.ts.
// Kept free of enums and parameter properties so node's type stripping can run it in tests.

import { SIGNATURE_HEADER, readCapped, verifyAlexaRequest } from "./alexa-verify.ts";
import type { AlexaEnvelope, VerifyOptions } from "./alexa-verify.ts";
import { STORY_ID_PATTERN } from "./download-keys.mjs";
import { LANGUAGE_PATTERN, MAX_SECONDS, matches } from "./events.ts";
import { trackOf } from "./sonos.ts";
import { escapeXml } from "./sonos-soap.ts";
import { DETAIL_ESTIMATED, DETAIL_REPORTED, SPEAKER_PLATFORM_ALEXA, recordSpeakerPlay } from "./speaker-usage.ts";
import type { SpeakerUsage } from "./speaker-usage.ts";
import { handleStories } from "./stories.ts";
import type { ListItem, ObjectServer } from "./stories.ts";

export const ALEXA_PATH = "/alexa";
export const BOOK_LANGUAGE = "en";
export const MAX_LISTED_BOOKS = 5;
export const TOKEN_VERSION = "v1";
const TOKEN_SEPARATOR = "|";
const TOKEN_PARTS = 5;
const MAX_BODY_BYTES = 262_144;
const MAX_STORY_VERSION = 1_000_000;
const MS_PER_SECOND = 1000;
const STORIES_PATH = "/api/stories";
const ALLOWED_METHODS = "POST";
const NOT_ALLOWED_STATUS = 405;
const REJECTED_STATUS = 400;
const OK_STATUS = 200;
const JSON_CONTENT_TYPE = "application/json;charset=UTF-8";
const RESPONSE_VERSION = "1.0";
const PLAY_BEHAVIOR = "REPLACE_ALL";
const MIN_WORD_MATCH = 0.75;

/** The request types, intent names and slot this skill reads (skill-package/ has the same names). */
export const RequestType = {
  Launch: "LaunchRequest",
  Intent: "IntentRequest",
  SessionEnded: "SessionEndedRequest",
  PlaybackStopped: "AudioPlayer.PlaybackStopped",
  PlaybackFinished: "AudioPlayer.PlaybackFinished",
  PlaybackStarted: "AudioPlayer.PlaybackStarted",
  PlaybackNearlyFinished: "AudioPlayer.PlaybackNearlyFinished",
  PlaybackFailed: "AudioPlayer.PlaybackFailed",
  PlayCommand: "PlaybackController.PlayCommandIssued",
  PauseCommand: "PlaybackController.PauseCommandIssued",
  NextCommand: "PlaybackController.NextCommandIssued",
  PreviousCommand: "PlaybackController.PreviousCommandIssued",
} as const;

export const IntentName = {
  ReadBook: "ReadBookIntent",
  Pause: "AMAZON.PauseIntent",
  Resume: "AMAZON.ResumeIntent",
  Stop: "AMAZON.StopIntent",
  Cancel: "AMAZON.CancelIntent",
  NavigateHome: "AMAZON.NavigateHomeIntent",
  Next: "AMAZON.NextIntent",
  Previous: "AMAZON.PreviousIntent",
  StartOver: "AMAZON.StartOverIntent",
  Help: "AMAZON.HelpIntent",
  Fallback: "AMAZON.FallbackIntent",
} as const;

export const BOOK_SLOT = "book";

/** What the skill says (the backend speaks to the user here; there is no other UI). Titles are inserted escaped. */
export const Speech = {
  Welcome: "Welcome to Dino Book Reader.",
  Choose: "Which book would you like?",
  CanRead: "I can read",
  NoBooks: "There are no books to read yet. Please come back soon.",
  NotFound: "I could not find that book.",
  Ambiguous: "More than one book matches.",
  NotAvailable: "That book is not available any more.",
  NothingPlaying: "There is nothing to continue.",
  NoNext: "That was the last book.",
  NoPrevious: "That is the first book.",
  Unavailable: "Sorry, I cannot get the books right now. Please try again later.",
  Goodbye: "Goodbye.",
  Reading: "Here is",
} as const;

type Track = NonNullable<ReturnType<typeof trackOf>>;

export interface AlexaEnv {
  readonly ALEXA_SKILL_ID?: string;
}

export interface PlayToken {
  readonly book: string;
  readonly language: string;
  readonly version: number;
  readonly segmentStartMs: number;
}

interface Directive {
  readonly type: string;
  readonly [key: string]: unknown;
}

interface Reply {
  readonly speech?: string;
  readonly reprompt?: string;
  readonly directives: readonly Directive[];
  readonly end: boolean;
}

interface Context {
  readonly envelope: AlexaEnvelope;
  readonly tracks: readonly Track[];
  readonly usage: SpeakerUsage | undefined;
  readonly httpRequest: Request;
}

type Obj = Record<string, unknown>;

function objectAt(value: unknown, ...path: string[]): unknown {
  let current: unknown = value;
  for (const key of path) {
    if (typeof current !== "object" || current === null) return undefined;
    current = (current as Obj)[key];
  }
  return current;
}

function isWhole(value: unknown, max: number): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 && value <= max;
}

// ---- token ----

export function tokenOf(track: Track, segmentStartMs: number): string {
  return [TOKEN_VERSION, track.storyId, track.language, track.version, Math.round(segmentStartMs)].join(TOKEN_SEPARATOR);
}

/** The fields of a token, each validated; anything else (foreign, malformed, out of range) is null and never trusted. */
export function parseToken(text: unknown): PlayToken | null {
  if (typeof text !== "string") return null;
  const parts = text.split(TOKEN_SEPARATOR);
  if (parts.length !== TOKEN_PARTS || parts[0] !== TOKEN_VERSION) return null;
  const [, book, language, version, start] = parts as [string, string, string, string, string];
  const versionNumber = /^\d{1,7}$/.test(version) ? Number(version) : Number.NaN;
  const startNumber = /^\d{1,9}$/.test(start) ? Number(start) : Number.NaN;
  if (!matches(STORY_ID_PATTERN, book) || !matches(LANGUAGE_PATTERN, language) || language !== BOOK_LANGUAGE) return null;
  if (!isWhole(versionNumber, MAX_STORY_VERSION) || !isWhole(startNumber, MAX_SECONDS * MS_PER_SECOND)) return null;
  return { book, language, version: versionNumber, segmentStartMs: startNumber };
}

// ---- catalogue ----

async function loadTracks(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer): Promise<Track[]> {
  const origin = new URL(request.url).origin;
  const response = await handleStories(new Request(`${origin}${pathPrefix}${STORIES_PATH}`, { method: "GET" }), env, pathPrefix, serve);
  if (!response.ok) throw new Error("catalogue");
  const body = (await response.json()) as { stories?: ListItem[] };
  const items = Array.isArray(body.stories) ? body.stories : [];
  return items.map((item) => trackOf(item, BOOK_LANGUAGE, origin)).filter((track): track is Track => track !== null);
}

function findByToken(tracks: readonly Track[], token: PlayToken | null): Track | null {
  return token === null ? null : (tracks.find((track) => track.storyId === token.book && track.language === token.language) ?? null);
}

function words(text: string): string[] {
  return text
    .toLowerCase()
    .replace(/[^\p{L}\p{N}\s]/gu, " ")
    .split(/\s+/)
    .filter((word) => word !== "");
}

/** Words that say nothing about which book is meant (articles, "story", "please"): never enough on their own to pick a title. */
const STOP_WORDS: ReadonlySet<string> = new Set(["the", "a", "an", "and", "of", "to", "in", "on", "at", "for", "with", "story", "stories", "book", "books", "please", "read", "play", "me", "my"]);

const withoutStopWords = (list: readonly string[]): string[] => list.filter((word) => !STOP_WORDS.has(word));

function wordScore(spoken: readonly string[], title: readonly string[]): number {
  const shared = spoken.filter((word) => title.includes(word)).length;
  return spoken.length === 0 ? 0 : shared / spoken.length;
}

const hasRun = (outer: readonly string[], run: readonly string[]): boolean => ` ${outer.join(" ")} `.includes(` ${run.join(" ")} `);

interface Candidate {
  readonly track: Track;
  readonly words: string[];
  readonly content: string[];
}

function bestScored(candidates: readonly Candidate[], heard: readonly string[]): Track[] {
  const scored = candidates.map((entry) => ({ track: entry.track, score: wordScore(heard, entry.content) }));
  const top = Math.max(0, ...scored.map((entry) => entry.score));
  return top < MIN_WORD_MATCH ? [] : scored.filter((entry) => entry.score === top).map((entry) => entry.track);
}

/**
 * The listed books a spoken name can mean, best tier only: the same words; else the spoken words (stop words left out) as a whole-word run
 * inside a title, or a whole title inside what was said; else the titles sharing most of the spoken words. One entry is a clear answer,
 * several are ambiguous (the caller lists them), none is "not found". A name made only of stop words matches only an identical title.
 */
export function findBooks(tracks: readonly Track[], spoken: string): Track[] {
  const heard = words(spoken);
  if (heard.length === 0) return [];
  const candidates = tracks.map((track) => ({ track, words: words(track.title), content: withoutStopWords(words(track.title)) })).filter((entry) => entry.words.length > 0);
  const exact = candidates.filter((entry) => entry.words.join(" ") === heard.join(" "));
  const key = withoutStopWords(heard);
  if (exact.length > 0 || key.length === 0) return exact.map((entry) => entry.track);
  const inside = candidates.filter((entry) => entry.content.length > 0 && (hasRun(entry.content, key) || hasRun(key, entry.content)));
  return inside.length > 0 ? inside.map((entry) => entry.track) : bestScored(candidates, key);
}

/** The one listed book a spoken name means, or null when none or several match. */
export function findBook(tracks: readonly Track[], spoken: string): Track | null {
  const found = findBooks(tracks, spoken);
  return found.length === 1 ? found[0]! : null;
}

// ---- replies ----

const ssml = (text: string): { type: string; ssml: string } => ({ type: "SSML", ssml: `<speak>${text}</speak>` });

function toResponse(reply: Reply): Obj {
  const response: Obj = { shouldEndSession: reply.end };
  if (reply.speech !== undefined) response.outputSpeech = ssml(reply.speech);
  if (reply.reprompt !== undefined) response.reprompt = { outputSpeech: ssml(reply.reprompt) };
  if (reply.directives.length > 0) response.directives = reply.directives;
  return { version: RESPONSE_VERSION, response };
}

/**
 * The answer to a PlaybackController request: AudioPlayer directives only. Amazon's reference says "The response can't include any of the
 * standard properties such as outputSpeech, card, or reprompt" (an error otherwise), and shouldEndSession is one of those standard fields,
 * so it is left out too.
 * https://developer.amazon.com/docs/alexa/custom-skills/playback-controller-interface-reference.html
 */
function toDirectivesOnly(reply: Reply): Obj {
  return { version: RESPONSE_VERSION, response: reply.directives.length > 0 ? { directives: reply.directives } : {} };
}

const EMPTY_ANSWER: Obj = { version: RESPONSE_VERSION, response: {} };

function playDirective(track: Track, offsetMs: number): Directive {
  const art = track.coverUrl === null ? {} : { art: { sources: [{ url: track.coverUrl }] } };
  return {
    type: "AudioPlayer.Play",
    playBehavior: PLAY_BEHAVIOR,
    audioItem: {
      stream: { url: track.listenUrl, token: tokenOf(track, offsetMs), offsetInMilliseconds: offsetMs },
      metadata: { title: track.title, subtitle: track.author ?? undefined, ...art },
    },
  };
}

const STOP_DIRECTIVE: Directive = { type: "AudioPlayer.Stop" };

function titleList(tracks: readonly Track[]): string {
  return tracks
    .slice(0, MAX_LISTED_BOOKS)
    .map((track) => escapeXml(track.title))
    .join(", ");
}

function listBooks(tracks: readonly Track[], intro: string): Reply {
  if (tracks.length === 0) return { speech: Speech.NoBooks, directives: [], end: true };
  const speech = `${intro} ${Speech.CanRead} ${titleList(tracks)}. ${Speech.Choose}`;
  return { speech, reprompt: Speech.Choose, directives: [], end: false };
}

function startBook(track: Track, offsetMs: number, announce: boolean): Reply {
  const speech = announce ? `${Speech.Reading} ${escapeXml(track.title)}.` : undefined;
  return { speech, directives: [playDirective(track, offsetMs)], end: true };
}

// ---- what is playing now ----

interface Playback {
  readonly track: Track;
  readonly offsetMs: number;
}

function currentPlayback(ctx: Context): Playback | null {
  const token = parseToken(objectAt(ctx.envelope, "context", "AudioPlayer", "token"));
  const track = findByToken(ctx.tracks, token);
  if (track === null) return null;
  const offset = objectAt(ctx.envelope, "context", "AudioPlayer", "offsetInMilliseconds");
  if (token !== null && token.version !== track.version) return { track, offsetMs: 0 }; // the book was re-cut since this segment started: the old offset means nothing in the new audio
  return { track, offsetMs: resumeOffset(offset, track.seconds * MS_PER_SECOND) };
}

/** Where a resume starts: Alexa's offset, or the beginning when there is none or it is at or past the end (a finished book starts over). */
function resumeOffset(offset: unknown, limitMs: number): number {
  if (typeof offset !== "number" || !Number.isFinite(offset)) return 0;
  const rounded = Math.max(Math.round(offset), 0);
  return rounded >= limitMs ? 0 : rounded;
}

// ---- actions (one thing each) ----

function resume(ctx: Context): Reply {
  const playing = currentPlayback(ctx);
  return playing === null ? listBooks(ctx.tracks, Speech.NothingPlaying) : startBook(playing.track, playing.offsetMs, false);
}

function pause(): Reply {
  return { directives: [STOP_DIRECTIVE], end: true };
}

function stop(): Reply {
  return { speech: Speech.Goodbye, directives: [STOP_DIRECTIVE], end: true };
}

function startOver(ctx: Context): Reply {
  const playing = currentPlayback(ctx);
  return playing === null ? listBooks(ctx.tracks, Speech.NothingPlaying) : startBook(playing.track, 0, false);
}

function step(ctx: Context, direction: number): Reply {
  const playing = currentPlayback(ctx);
  if (playing === null) return listBooks(ctx.tracks, Speech.NothingPlaying);
  const neighbour = ctx.tracks[ctx.tracks.indexOf(playing.track) + direction];
  // Past either end the speech is the whole answer and the session ends: no reprompt exists to keep it open, and a speaker button
  // (PlaybackController) drops speech anyway.
  if (neighbour === undefined) return { speech: direction > 0 ? Speech.NoNext : Speech.NoPrevious, directives: [], end: true };
  return startBook(neighbour, 0, true);
}

function readBook(ctx: Context): Reply {
  const spoken = objectAt(ctx.envelope, "request", "intent", "slots", BOOK_SLOT, "value");
  const found = typeof spoken === "string" ? findBooks(ctx.tracks, spoken) : [];
  if (found.length > 1) return listBooks(found, Speech.Ambiguous);
  return found[0] === undefined ? listBooks(ctx.tracks, Speech.NotFound) : startBook(found[0], 0, true);
}

function help(ctx: Context): Reply {
  return listBooks(ctx.tracks, Speech.Welcome);
}

// ---- one handler per request type and intent ----

const INTENT_HANDLERS: Readonly<Record<string, (ctx: Context) => Reply>> = {
  [IntentName.ReadBook]: readBook,
  [IntentName.Pause]: pause,
  [IntentName.Resume]: resume,
  [IntentName.Stop]: stop,
  [IntentName.Cancel]: stop,
  [IntentName.NavigateHome]: stop,
  [IntentName.Next]: (ctx) => step(ctx, 1),
  [IntentName.Previous]: (ctx) => step(ctx, -1),
  [IntentName.StartOver]: startOver,
  [IntentName.Help]: help,
  [IntentName.Fallback]: help,
};

function onLaunch(ctx: Context): Obj {
  return toResponse(listBooks(ctx.tracks, Speech.Welcome));
}

function onIntent(ctx: Context): Obj {
  const name = objectAt(ctx.envelope, "request", "intent", "name");
  const handler = typeof name === "string" ? INTENT_HANDLERS[name] : undefined;
  return toResponse((handler ?? help)(ctx));
}

const COMMAND_HANDLERS: Readonly<Record<string, (ctx: Context) => Reply>> = {
  [RequestType.PlayCommand]: resume,
  [RequestType.PauseCommand]: pause,
  [RequestType.NextCommand]: (ctx) => step(ctx, 1),
  [RequestType.PreviousCommand]: (ctx) => step(ctx, -1),
};

/** A playback command (a button on the speaker or a remote) may answer with directives only, never speech. */
function onCommand(ctx: Context, type: string): Obj {
  return toDirectivesOnly(COMMAND_HANDLERS[type]!(ctx));
}

/** Seconds actually played in the segment the event ends: offset minus the token's start, else the rest of the book (estimated). */
export function playedSeconds(token: PlayToken, track: { readonly seconds: number } | null, offsetMs: unknown): { seconds: number; detail: typeof DETAIL_REPORTED | typeof DETAIL_ESTIMATED } {
  if (typeof offsetMs === "number" && Number.isFinite(offsetMs)) {
    const played = Math.round((offsetMs - token.segmentStartMs) / MS_PER_SECOND);
    return { seconds: Math.min(Math.max(played, 0), track?.seconds ?? MAX_SECONDS), detail: DETAIL_REPORTED };
  }
  if (track === null) return { seconds: 0, detail: DETAIL_ESTIMATED }; // no offset and no catalogue: the book's length is unknown
  return { seconds: Math.max(track.seconds - Math.round(token.segmentStartMs / MS_PER_SECOND), 0), detail: DETAIL_ESTIMATED };
}

// Slice 289's table: one book_listened row per played segment, written when the segment ends. A paused-and-resumed book makes two rows.
async function countSegment(ctx: Context): Promise<void> {
  const token = parseToken(objectAt(ctx.envelope, "request", "token"));
  const track = findByToken(ctx.tracks, token);
  if (token === null) return; // the token already carries a validated book and language, so a book missing from the listing (or an unreadable catalogue) still counts
  const { seconds, detail } = playedSeconds(token, track, objectAt(ctx.envelope, "request", "offsetInMilliseconds"));
  const play = { platform: SPEAKER_PLATFORM_ALEXA, book: token.book, language: token.language, seconds, detail };
  const counting = recordSpeakerPlay(ctx.usage, play, ctx.httpRequest).catch(() => {
    // no-fallback: telemetry never changes the spoken answer, and a failure is never logged (ids and titles stay out of logs)
    return false;
  });
  if (ctx.usage?.defer === undefined) await counting;
  else ctx.usage.defer(counting); // the answer never waits for the telemetry read or the row write
}

async function onSegmentEnd(ctx: Context): Promise<Obj> {
  await countSegment(ctx);
  return EMPTY_ANSWER;
}

const COMMAND_TYPES: ReadonlySet<string> = new Set(Object.keys(COMMAND_HANDLERS));

async function answer(ctx: Context): Promise<Obj> {
  const type = objectAt(ctx.envelope, "request", "type");
  if (typeof type !== "string") return EMPTY_ANSWER;
  if (type === RequestType.Launch) return onLaunch(ctx);
  if (type === RequestType.Intent) return onIntent(ctx);
  if (isSegmentEnd(type)) return onSegmentEnd(ctx);
  if (COMMAND_TYPES.has(type)) return onCommand(ctx, type);
  return EMPTY_ANSWER; // started, nearly finished, failed, session ended and anything new: Alexa wants no speech and no directive
}

// ---- endpoint ----

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isAlexaPath(pathname: string): boolean {
  return pathname === ALEXA_PATH;
}

async function readBody(request: Request): Promise<Uint8Array | null> {
  const declared = Number(request.headers.get("Content-Length") ?? 0);
  if (declared > MAX_BODY_BYTES) return null;
  return readCapped(request.body, MAX_BODY_BYTES); // counts the bytes as they arrive, so a missing or false Content-Length changes nothing
}

function jsonResponse(body: Obj): Response {
  return new Response(JSON.stringify(body), { status: OK_STATUS, headers: { "Content-Type": JSON_CONTENT_TYPE, "Cache-Control": "no-store" } });
}

function rejected(): Response {
  return new Response(null, { status: REJECTED_STATUS });
}

/** Started, nearly finished, failed, session ended and anything new get an empty answer and never read the catalogue. */
function readsCatalogue(type: unknown): boolean {
  return typeof type === "string" && (type === RequestType.Launch || type === RequestType.Intent || isSegmentEnd(type) || COMMAND_TYPES.has(type));
}

function isSegmentEnd(type: string): boolean {
  return type === RequestType.PlaybackStopped || type === RequestType.PlaybackFinished;
}

async function tracksFor(type: unknown, request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer): Promise<readonly Track[]> {
  if (!readsCatalogue(type)) return [];
  try {
    return await loadTracks(request, env, pathPrefix, serve);
  } catch (error) {
    if (typeof type === "string" && isSegmentEnd(type)) return []; // a segment end still records its row from the token
    throw error;
  }
}

async function respond(request: Request, envelope: AlexaEnvelope, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage: SpeakerUsage | undefined): Promise<Response> {
  const type = objectAt(envelope, "request", "type");
  try {
    const tracks = await tracksFor(type, request, env, pathPrefix, serve);
    return jsonResponse(await answer({ envelope, tracks, usage, httpRequest: request }));
  } catch {
    // no-fallback: the spoken answer is the fallback; a failure is never logged (ids and titles stay out of logs)
    const apology: Reply = { speech: Speech.Unavailable, directives: [], end: true };
    return jsonResponse(type === RequestType.Launch || type === RequestType.Intent ? toResponse(apology) : EMPTY_ANSWER);
  }
}

/**
 * The Alexa endpoint: POST only; a request that fails the signature, certificate, timestamp or skill-id checks is HTTP 400 with no body.
 * `pathPrefix` is "" on the Dino host and "/dino" under workers.dev. `usage` (Slice 289) counts each segment played; without it nothing is counted.
 * `options` replaces the clock, roots and certificate fetch in tests.
 */
export async function handleAlexa(request: Request, env: { DOWNLOADS?: R2Bucket } & AlexaEnv, pathPrefix: string, serve: ObjectServer, usage?: SpeakerUsage, options: VerifyOptions = {}): Promise<Response> {
  if (request.method !== "POST") return new Response(null, { status: NOT_ALLOWED_STATUS, headers: { Allow: ALLOWED_METHODS } });
  if (request.headers.get(SIGNATURE_HEADER) === null) return rejected();
  const body = await readBody(request);
  if (body === null) return rejected();
  const envelope = await verifyAlexaRequest(request, body, { ...options, skillId: options.skillId ?? env.ALEXA_SKILL_ID ?? "" });
  return envelope === null ? rejected() : respond(request, envelope, env, pathPrefix, serve, usage);
}
