// Slice 282: Dino Reader as an anonymous Alexa kid skill (custom skill, AudioPlayer) at /alexa (workers.dev: /dino/alexa).
// The catalogue is the store list this Worker already serves (/api/stories, Slice 281's listening tracks), English track only.
// No server state: Alexa owns playback, and every Play directive carries a token `v1|<book>|<lang>|<version>|<segmentStartMs>` that
// Alexa sends back with each later request, so pause/resume/next need nothing stored. No household, user, device or skill-instance
// id is kept and nothing is logged (worker/CLAUDE.md). Request signature checks live in alexa-verify.ts.
// Slice 457: the one thing stored is a child's favourites, per opaque Alexa account id in D1 (alexa-favourites.ts). A token may carry a
// sixth part `f` (playing the favourites in a row). Only books whose track plays are ever sent as a Play; the logs hold intent and outcome only.
// Kept free of enums and parameter properties so node's type stripping can run it in tests.

import { AddResult, addFavourite, alexaUserId, listFavourites, removeFavourite } from "./alexa-favourites.ts";
import { SIGNATURE_HEADER, readCapped, verifyAlexaRequest } from "./alexa-verify.ts";
import type { AlexaEnvelope, VerifyOptions } from "./alexa-verify.ts";
import { STORY_ID_PATTERN } from "./download-keys.mjs";
import { LANGUAGE_PATTERN, MAX_SECONDS, matches } from "./events.ts";
import { objectAt } from "./object-path.ts";
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
const FAVOURITES_FLAG = "f"; // an optional sixth token part: this book is playing as part of "play my favourites"
const CLIP_FLAG = "c"; // the other sixth token part: the "what next?" clip queued after this book (Slice 457 item 7); the token still names the book just played
export const CLIP_FOLDER = "/dino/alexa-clips"; // static assets (site/dino/alexa-clips/), the same path on the Dino host and on workers.dev
const CLIP_FILE_PLAIN = "what-next.m4a";
const CLIP_FILE_WITH_FAVOURITES = "what-next-with-favourites.m4a";
const CLIP_TITLE = "Dino Bedtime Stories";
const MAX_LIST_OFFSET = 10_000;
const NUMBER_WORDS = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"];
const MIN_FUZZY_LENGTH = 4; // words shorter than this must match exactly; a first-word prefix needs at least this many letters
const LONG_WORD_LENGTH = 8; // 4-7 letters allow one slip, 8 or more allow two
const FUZZY_QUALITY = 0.9; // a spelling-slack or prefix match ranks below an exact word, so "hood" prefers Riding Hood over Good Enough
const SLOT_ID_MATCH = "ER_SUCCESS_MATCH";
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
const ENQUEUE_BEHAVIOR = "ENQUEUE";
const MIN_WORD_MATCH = 0.75;
export const LIST_OFFSET_ATTRIBUTE = "listOffset"; // session attribute: where "more books" continues
export const PENDING_ATTRIBUTE = "pending"; // session attribute: the favourites action a "which book?" question belongs to
export const PendingAction = { Save: "save", Remove: "remove" } as const;
const LAUNCH_INTENT = "Launch"; // the log name of a LaunchRequest
const UNKNOWN_INTENT = "unknown";
const DEFAULT_OUTCOME = "ok";
const LIST_PAGE_OUTCOME = "list_page";
const RESUMABLE_ACTIVITIES: ReadonlySet<string> = new Set(["PLAYING", "BUFFER_UNDERRUN"]); // AudioPlayer activities a favourites answer carries on from; a paused story stays paused (speech only)

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
  BookTitle: "BookTitleIntent",
  ListBooks: "ListBooksIntent",
  MoreBooks: "MoreBooksIntent",
  AddFavourite: "AddFavouriteIntent",
  PlayFavourites: "PlayFavouritesIntent",
  PlayFavourite: "PlayFavouriteIntent",
  ListFavourites: "ListFavouritesIntent",
  RemoveFavourite: "RemoveFavouriteIntent",
  Pause: "AMAZON.PauseIntent",
  Resume: "AMAZON.ResumeIntent",
  Stop: "AMAZON.StopIntent",
  Cancel: "AMAZON.CancelIntent",
  NavigateHome: "AMAZON.NavigateHomeIntent",
  Next: "AMAZON.NextIntent",
  Previous: "AMAZON.PreviousIntent",
  StartOver: "AMAZON.StartOverIntent",
  Repeat: "AMAZON.RepeatIntent", // Slice 457 decision 12: the one word a small child says for "again"
  LoopOn: "AMAZON.LoopOnIntent",
  LoopOff: "AMAZON.LoopOffIntent",
  ShuffleOn: "AMAZON.ShuffleOnIntent", // "Alexa, shuffle": a random favourite
  ShuffleOff: "AMAZON.ShuffleOffIntent",
  Help: "AMAZON.HelpIntent",
  Fallback: "AMAZON.FallbackIntent",
} as const;

export const BOOK_SLOT = "book";
export const TITLE_SLOT = "title";

/** What the skill says (the backend speaks to the user here; there is no other UI). Titles are inserted escaped. */
export const Speech = {
  Welcome: "Welcome to Dino Bedtime Stories.",
  Choose: "Which book would you like?",
  CanRead: "I can read",
  NoBooks: "There are no books to read yet. Please come back soon.",
  NotFound: "I could not find that book.",
  Ambiguous: "More than one book matches.",
  YouCanHear: "You can listen to",
  WithSubscription: "These come with the Dino subscription in the Dino app:",
  MoreWithSubscription: "with the Dino subscription in the Dino app.", // "1 more comes ..." / "2 more come ..." / "2 books come ..." (the prompt already says 'more books')
  SubscriptionBook: "comes with the Dino subscription in the Dino app.",
  AllBooks: "That's all the books.",
  SayName: "Say a book's name, or say 'more books'.",
  SayNameOrFavourites: "Say a book's name, say 'more books', or say 'play my favourites'.",
  AfterStory: "After a story, say: Alexa, repeat. Or: Alexa, next.",
  AfterStoryWithFavourites: "After a story, say: Alexa, repeat. Or: Alexa, next. Or: Alexa, shuffle.",
  Help: "You can say a book's name, 'what books do you have', 'more books', 'add this to my favourites', 'play my favourites', 'what are my favourites' or 'remove' and a book's name from my favourites. While a story plays, say 'Alexa, ask Dino Bedtime Stories for' and a book's name. Or say 'Alexa, repeat' to hear it again, or 'Alexa, next' for a new story.",
  HelpShuffle: "Say 'Alexa, shuffle' for a favourite.",
  HelpAlso: "You can also say pause, resume or previous.",
  FavouritesOff: "Favourites are not available right now.",
  NoFavourites: "You have no favourites yet. While a story plays, say 'Alexa, ask Dino Bedtime Stories to save this story'.",
  NoFavouritePlays: "None of your favourites can play right now.",
  YourFavourites: "Your favourites are",
  SaveWhat: "Tell me which book to save, or ask me while a story plays.",
  Saved: "Saved to your favourites:",
  AlreadySaved: "is already in your favourites.",
  FavouritesFull: "Your favourites are full. Remove one first.",
  Removed: "Removed from your favourites:",
  NotInFavourites: "is not in your favourites.",
  FavouriteSubscription: "I can only save books that play right now.",
  Sorry: "Sorry, I did not catch that.",
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
  /** Slice 457: the D1 database that holds the favourites table (the same `STATS` binding as the dashboard). */
  readonly STATS?: D1Database;
}

/** What the Worker's index tells the skill that the list cannot: whether paid books are gated (`paidBooks.enabled`); unknown means gated. */
export interface AlexaServices {
  readonly paidBooksEnabled?: () => Promise<boolean>;
  /** A number in [0, 1) for "play a favourite"; tests pin it. */
  readonly random?: () => number;
}

export interface PlayToken {
  readonly book: string;
  readonly language: string;
  readonly version: number;
  readonly segmentStartMs: number;
  /** Present (true) only when the book plays as part of "play my favourites". */
  readonly favourites?: true;
  /** Present (true) only on the "what next?" clip's token; `book` is then the book that was just played. */
  readonly clip?: true;
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
  /** Slice 457: the one-word result logged with the intent (a book id, never what was said). */
  readonly outcome?: string;
  readonly session?: Obj;
}

interface Catalogue {
  /** Books whose track plays: free, or paid while the paid-books switch is off (the listen route then answers 206 with no proof). */
  readonly tracks: readonly Track[];
  /** Books that are paid while the switch is on: the listen route answers them 402, so they are never sent as a Play. */
  readonly locked: readonly Track[];
}

interface Context {
  readonly envelope: AlexaEnvelope;
  readonly tracks: readonly Track[];
  readonly locked: readonly Track[];
  readonly usage: SpeakerUsage | undefined;
  readonly httpRequest: Request;
  readonly db: D1Database | undefined;
  readonly random: () => number;
}

type Obj = Record<string, unknown>;

function isWhole(value: unknown, max: number): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 && value <= max;
}

// ---- token ----

export function tokenOf(track: Track, segmentStartMs: number, favourites = false): string {
  const parts = [TOKEN_VERSION, track.storyId, track.language, track.version, Math.round(segmentStartMs)];
  return (favourites ? [...parts, FAVOURITES_FLAG] : parts).join(TOKEN_SEPARATOR);
}

/** The token of the "what next?" clip: the book just played (its token's fields), a start of 0 and the clip flag. */
export function clipTokenOf(played: PlayToken): string {
  return [TOKEN_VERSION, played.book, played.language, played.version, 0, CLIP_FLAG].join(TOKEN_SEPARATOR);
}

/** The fields of a token, each validated; anything else (foreign, malformed, out of range) is null and never trusted. */
export function parseToken(text: unknown): PlayToken | null {
  if (typeof text !== "string") return null;
  const parts = text.split(TOKEN_SEPARATOR);
  const flag = parts.length === TOKEN_PARTS + 1 ? parts[TOKEN_PARTS] : undefined;
  const flagged = flag === FAVOURITES_FLAG;
  const clip = flag === CLIP_FLAG;
  if ((parts.length !== TOKEN_PARTS && !flagged && !clip) || parts[0] !== TOKEN_VERSION) return null;
  const [, book, language, version, start] = parts as [string, string, string, string, string];
  const versionNumber = /^\d{1,7}$/.test(version) ? Number(version) : Number.NaN;
  const startNumber = /^\d{1,9}$/.test(start) ? Number(start) : Number.NaN;
  if (!matches(STORY_ID_PATTERN, book) || !matches(LANGUAGE_PATTERN, language) || language !== BOOK_LANGUAGE) return null;
  if (!isWhole(versionNumber, MAX_STORY_VERSION) || !isWhole(startNumber, MAX_SECONDS * MS_PER_SECOND)) return null;
  const base = { book, language, version: versionNumber, segmentStartMs: startNumber };
  if (clip) return { ...base, clip: true };
  return flagged ? { ...base, favourites: true } : base;
}

// ---- catalogue ----

// The list says which books have a listening track (trackOf drops the rest, so a missing track is in no list and never played) and which are
// paid. The listen route (stories.ts paidDenial) answers a paid book 402 to a caller with no proof while `paidBooks.enabled` is on, and the
// skill never has a proof, so a paid book is "locked" while that switch is on (or cannot be read: fail closed) and plays otherwise. The track
// itself is never fetched to find out.
async function loadCatalogue(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, services: AlexaServices): Promise<Catalogue> {
  const origin = new URL(request.url).origin;
  const response = await handleStories(new Request(`${origin}${pathPrefix}${STORIES_PATH}`, { method: "GET" }), env, pathPrefix, serve);
  if (!response.ok) throw new Error("catalogue");
  const body = (await response.json()) as { stories?: ListItem[] };
  const items = Array.isArray(body.stories) ? body.stories : [];
  const gated = items.some((item) => item.access === "paid") && (await paidGateOn(services));
  const tracks: Track[] = [];
  const locked: Track[] = [];
  for (const item of items) {
    const track = trackOf(item, BOOK_LANGUAGE, origin);
    if (track !== null) (gated && item.access === "paid" ? locked : tracks).push(track);
  }
  return { tracks, locked };
}

async function paidGateOn(services: AlexaServices): Promise<boolean> {
  if (services.paidBooksEnabled === undefined) return true;
  try {
    return await services.paidBooksEnabled();
  } catch {
    // no-fallback: an unreadable switch means paid books stay locked, which is the safe side (no Play that the listen route would refuse)
    return true;
  }
}

function findByToken(tracks: readonly Track[], token: PlayToken | null): Track | null {
  return token === null ? null : (tracks.find((track) => track.storyId === token.book && track.language === token.language) ?? null);
}

/** Lower-case words, with digits said as words ("3" is "three"), so a title and what was heard compare the same way. */
function words(text: string): string[] {
  return text
    .toLowerCase()
    .replace(/[^\p{L}\p{N}\s]/gu, " ")
    .split(/\s+/)
    .filter((word) => word !== "")
    .map((word) => (/^\d{1,2}$/.test(word) ? (NUMBER_WORDS[Number(word)] ?? word) : word));
}

/** Singular and plural are the same word ("bears" = "bear"); "ss" endings and short words keep their letters. */
function stem(word: string): string {
  return word.length > 3 && word.endsWith("s") && !word.endsWith("ss") ? word.slice(0, -1) : word;
}

function editDistance(a: string, b: string): number {
  let previous = Array.from({ length: b.length + 1 }, (_, index) => index);
  for (let row = 1; row <= a.length; row++) {
    const current = [row];
    for (let column = 1; column <= b.length; column++) {
      const cost = a[row - 1] === b[column - 1] ? 0 : 1;
      current[column] = Math.min(previous[column]! + 1, current[column - 1]! + 1, previous[column - 1]! + cost);
    }
    previous = current;
  }
  return previous[b.length]!;
}

/** How many letters may differ: none under 4 letters, one for 4-7, two from 8. */
function slack(length: number): number {
  if (length < MIN_FUZZY_LENGTH) return 0;
  return length < LONG_WORD_LENGTH ? 1 : 2;
}

/** 1 for the same word (plural or not), FUZZY_QUALITY for a small spelling slip or a first-word prefix ("goldy"), else 0. */
function wordQuality(heard: string, titleWord: string, isFirst: boolean): number {
  const a = stem(heard);
  const b = stem(titleWord);
  if (a === b) return 1;
  if (editDistance(a, b) <= slack(Math.min(a.length, b.length))) return FUZZY_QUALITY;
  if (!isFirst || heard.length < MIN_FUZZY_LENGTH) return 0;
  const front = titleWord.slice(0, heard.length);
  return editDistance(heard, front) <= slack(heard.length) ? FUZZY_QUALITY : 0;
}

/** Words that say nothing about which book is meant (articles, "story", "please"): never enough on their own to pick a title. */
const STOP_WORDS: ReadonlySet<string> = new Set(["the", "a", "an", "and", "of", "to", "in", "on", "at", "for", "with", "story", "stories", "book", "books", "please", "read", "play", "me", "my"]);

const withoutStopWords = (list: readonly string[]): string[] => list.filter((word) => !STOP_WORDS.has(word));

interface Scored<T> {
  readonly track: T;
  /** Mean quality of the spoken words (1 each when exact, FUZZY_QUALITY when slack or prefix, 0 when not in the title). */
  readonly score: number;
  /** The share of the title's own words that were said (1 = the whole title). */
  readonly coverage: number;
}

function scoreTitle<T extends Track>(track: T, heard: readonly string[]): Scored<T> | null {
  const title = withoutStopWords(words(track.title));
  if (title.length === 0) return null;
  const qualities = heard.map((word) => Math.max(...title.map((titleWord, index) => wordQuality(word, titleWord, index === 0))));
  const matchedQualities = qualities.filter((quality) => quality > 0);
  const covered = title.filter((titleWord, index) => heard.some((word) => wordQuality(word, titleWord, index === 0) > 0)).length;
  const whole = covered === title.length;
  // A whole title said is a hit however many extra words came with it ("goldilocks and the three bears from dino"); its score is then the
  // mean over the words that matched, so noise words do not rank it below a book that was only partly said.
  if (!whole && matchedQualities.length / heard.length < MIN_WORD_MATCH) return null;
  const score = matchedQualities.reduce((sum, quality) => sum + quality, 0) / (whole ? matchedQualities.length : heard.length);
  return { track, score, coverage: covered / title.length };
}

/** The top-scoring books; when the whole title of one of them was said, only those, else every tie (the caller asks which). */
function topScored<T extends Track>(scored: readonly Scored<T>[]): T[] {
  const best = Math.max(0, ...scored.map((entry) => entry.score));
  const tied = scored.filter((entry) => best - entry.score < 1e-9);
  const whole = tied.filter((entry) => entry.coverage === 1);
  return (whole.length > 0 ? whole : tied).map((entry) => entry.track);
}

/**
 * The books a spoken name can mean (Slice 457 decision 1): word by word with the title's content words (stop words left out), where a word
 * counts when it is the same (digits as number words, plural = singular), differs by a small spelling slip (1 letter for 4-7, 2 for 8 or
 * more) or is the start of the title's first word ("goldy"); at least 75% of the spoken words must count (unless the title's every content word was said: extra words then change nothing). The best score wins; a whole
 * title said beats a title only partly said; equal books are all returned (several = ambiguous, none = "not found"). A name made only of
 * stop words matches only an identical title.
 */
export function findBooks<T extends Track>(tracks: readonly T[], spoken: string): T[] {
  const heard = words(spoken);
  if (heard.length === 0) return [];
  const key = withoutStopWords(heard);
  if (key.length === 0) return tracks.filter((track) => words(track.title).join(" ") === heard.join(" "));
  const scored = tracks.map((track) => scoreTitle(track, key)).filter((entry): entry is Scored<T> => entry !== null);
  return topScored(scored);
}

/** The one listed book a spoken name means, or null when none or several match. */
export function findBook<T extends Track>(tracks: readonly T[], spoken: string): T | null {
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
  const answer: Obj = { version: RESPONSE_VERSION, response };
  if (reply.session !== undefined) answer.sessionAttributes = reply.session;
  return answer;
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

interface PlayOptions {
  /** The book plays as part of "play my favourites": its token says so, so next and previous walk the favourites. */
  readonly favourites?: boolean;
  /** Queue after the stream with this token instead of replacing it (the next favourite, sent while the last one nears its end). */
  readonly enqueueAfter?: string;
}

function playDirective(track: Track, offsetMs: number, options: PlayOptions = {}): Directive {
  const art = track.coverUrl === null ? {} : { art: { sources: [{ url: track.coverUrl }] } };
  const queued = options.enqueueAfter === undefined ? {} : { expectedPreviousToken: options.enqueueAfter };
  return {
    type: "AudioPlayer.Play",
    playBehavior: options.enqueueAfter === undefined ? PLAY_BEHAVIOR : ENQUEUE_BEHAVIOR,
    audioItem: {
      stream: { url: track.listenUrl, token: tokenOf(track, offsetMs, options.favourites === true), offsetInMilliseconds: offsetMs, ...queued },
      metadata: { title: track.title, subtitle: track.author ?? undefined, ...art },
    },
  };
}

const STOP_DIRECTIVE: Directive = { type: "AudioPlayer.Stop" };

const titlesOf = (tracks: readonly Track[]): string => tracks.map((track) => escapeXml(track.title)).join(", ");

const reply = (speech: string, outcome: string, extra: Partial<Reply> = {}): Reply => ({ speech, reprompt: Speech.Choose, directives: [], end: false, outcome, ...extra });

const allBooks = (ctx: Context): Track[] => [...ctx.tracks, ...ctx.locked];

function pageOffset(ctx: Context): number {
  const offset = objectAt(ctx.envelope, "session", "attributes", LIST_OFFSET_ATTRIBUTE);
  return isWhole(offset, MAX_LIST_OFFSET) ? offset : 0;
}

/** "1 more comes with the Dino subscription ...", "2 more come ...", and "2 books come ..." when no free book was named. */
function hiddenPaidSpeech(count: number, afterFree: boolean): string {
  if (afterFree) return `${count} more ${count === 1 ? "comes" : "come"} ${Speech.MoreWithSubscription}`;
  return `${count} ${count === 1 ? "book comes" : "books come"} ${Speech.MoreWithSubscription}`;
}

/**
 * One page (MAX_LISTED_BOOKS) of every book, the ones that play first and then the ones that come with the subscription, then what to say
 * next. The next page's start goes back as a session attribute (Alexa returns it with the next request), so the Worker keeps nothing.
 */
function listPage(ctx: Context, offset: number, intro: string, prompt: string = Speech.SayName, nameLocked: boolean = false): Reply {
  const books = allBooks(ctx);
  if (books.length === 0) return { speech: Speech.NoBooks, directives: [], end: true, outcome: "no_books" };
  const page = books.slice(offset, offset + MAX_LISTED_BOOKS);
  const free = page.filter((book) => ctx.tracks.includes(book));
  const paid = nameLocked ? page.filter((book) => ctx.locked.includes(book)) : [];
  const count = offset === 0 ? `I have ${books.length} ${books.length === 1 ? "book" : "books"}.` : "";
  const heard = free.length > 0 ? `${Speech.YouCanHear} ${titlesOf(free)}.` : "";
  // The launch and "what books do you have" only count the paid books; "more books" continues at the first one and names them.
  const next = nameLocked ? offset + page.length : offset + free.length;
  // Counted only when every book after the named ones is a paid one; free books beyond the page are reached by "more books" instead.
  const hidden = nameLocked || next < ctx.tracks.length ? 0 : books.length - next;
  const sold = paid.length > 0 ? `${Speech.WithSubscription} ${titlesOf(paid)}.` : hidden > 0 ? hiddenPaidSpeech(hidden, free.length > 0) : "";
  const end = next >= books.length ? Speech.AllBooks : "";
  const speech = [intro, count, heard, sold, end, prompt].filter((part) => part !== "").join(" ");
  return { speech, reprompt: prompt, directives: [], end: false, outcome: `${LIST_PAGE_OUTCOME}:${offset}`, session: { [LIST_OFFSET_ATTRIBUTE]: next } };
}

/** Name the books that match and ask which; `pending` makes the next bare title finish a save or a remove instead of playing the book. */
function chooseFrom(books: readonly Track[], intro: string, pending?: string): Reply {
  const speech = `${intro} ${Speech.CanRead} ${titlesOf(books.slice(0, MAX_LISTED_BOOKS))}. ${Speech.Choose}`;
  return reply(speech, "ambiguous", pending === undefined ? {} : { session: { [PENDING_ATTRIBUTE]: pending } });
}

function startBook(track: Track, offsetMs: number, announce: boolean, favourites = false): Reply {
  const speech = announce ? `${Speech.Reading} ${escapeXml(track.title)}.` : undefined;
  return { speech, directives: [playDirective(track, offsetMs, { favourites })], end: true, outcome: `play:${track.storyId}` };
}

/** A paid book was asked for while paid books are gated: say so, and offer the ones that do play. Never a Play directive. */
function subscriptionBook(ctx: Context, track: Track): Reply {
  const free = ctx.tracks.slice(0, MAX_LISTED_BOOKS);
  const offer = free.length > 0 ? ` ${Speech.YouCanHear} ${titlesOf(free)}.` : "";
  return reply(`${escapeXml(track.title)} ${Speech.SubscriptionBook}${offer} ${Speech.Choose}`, `paid:${track.storyId}`);
}

// ---- what is playing now ----

interface Playback {
  readonly track: Track;
  readonly offsetMs: number;
  readonly favourites: boolean;
  /** The "what next?" clip is the stream: `track` is the book just played, and a resume or start over means that book, from the start. */
  readonly clip: boolean;
}

function currentPlayback(ctx: Context): Playback | null {
  const token = parseToken(objectAt(ctx.envelope, "context", "AudioPlayer", "token"));
  const track = findByToken(ctx.tracks, token); // a book that is locked now (or gone) is not played again from an old token
  if (track === null) return null;
  const favourites = token?.favourites === true;
  if (token?.clip === true) return { track, offsetMs: 0, favourites: false, clip: true }; // the offset belongs to the clip, not the book
  const offset = objectAt(ctx.envelope, "context", "AudioPlayer", "offsetInMilliseconds");
  if (token !== null && token.version !== track.version) return { track, offsetMs: 0, favourites, clip: false }; // the book was re-cut since this segment started: the old offset means nothing in the new audio
  return { track, offsetMs: resumeOffset(offset, track.seconds * MS_PER_SECOND), favourites, clip: false };
}

/** Where a resume starts: Alexa's offset, or the beginning when there is none or it is at or past the end (a finished book starts over). */
function resumeOffset(offset: unknown, limitMs: number): number {
  if (typeof offset !== "number" || !Number.isFinite(offset)) return 0;
  const rounded = Math.max(Math.round(offset), 0);
  return rounded >= limitMs ? 0 : rounded;
}

/**
 * A spoken answer that ends the session (a favourite saved, listed or removed) while a story was going: Alexa stops the stream for the
 * request, so the same answer also carries a Play at the offset it stopped at, and the story goes on after the speech. Nothing playing
 * (or the stream finished or stopped by the child): the answer is unchanged.
 */
function keepPlaying(ctx: Context, answer: Reply): Reply {
  const activity = objectAt(ctx.envelope, "context", "AudioPlayer", "playerActivity");
  const playing = typeof activity === "string" && RESUMABLE_ACTIVITIES.has(activity) ? currentPlayback(ctx) : null;
  if (playing === null || playing.clip || !answer.end || answer.directives.length > 0) return answer; // a favourites answer during the clip does not bring the book back
  return { ...answer, directives: [playDirective(playing.track, playing.offsetMs, { favourites: playing.favourites })] };
}

const keepingPlaying = (handler: Handler): Handler => async (ctx) => keepPlaying(ctx, await handler(ctx));

// ---- actions (one thing each) ----

function resume(ctx: Context): Reply {
  const playing = currentPlayback(ctx);
  return playing === null ? listPage(ctx, 0, Speech.NothingPlaying) : startBook(playing.track, playing.offsetMs, false, playing.favourites);
}

function pause(): Reply {
  return { directives: [STOP_DIRECTIVE], end: true };
}

function stop(): Reply {
  return { speech: Speech.Goodbye, directives: [STOP_DIRECTIVE], end: true };
}

function startOver(ctx: Context): Reply {
  const playing = currentPlayback(ctx);
  return playing === null ? listPage(ctx, 0, Speech.NothingPlaying) : startBook(playing.track, 0, false, playing.favourites);
}

/** The books next and previous walk through: the favourites (in the order they were saved, only those that play) for a favourites token, else the whole playable list. */
async function queueOf(ctx: Context, playing: Playback): Promise<{ queue: readonly Track[]; favourites: boolean }> {
  if (!playing.favourites) return { queue: ctx.tracks, favourites: false };
  const saved = await favouriteTracks(ctx);
  return saved !== null && saved.includes(playing.track) ? { queue: saved, favourites: true } : { queue: ctx.tracks, favourites: false };
}

/** From the "what next?" clip: the book after (or before) the one just played, wrapping round, never the one just played. */
function otherBook(queue: readonly Track[], played: Track, direction: number): Track | undefined {
  const others = queue.filter((track) => track !== played);
  if (others.length === 0) return undefined;
  const behind = queue.slice(0, queue.indexOf(played) + 1).filter((track) => track !== played).length; // the books before the played one
  return direction > 0 ? others[behind % others.length] : others[(behind - 1 + others.length) % others.length];
}

async function step(ctx: Context, direction: number): Promise<Reply> {
  const playing = currentPlayback(ctx);
  if (playing === null) return listPage(ctx, 0, Speech.NothingPlaying);
  const { queue, favourites } = await queueOf(ctx, playing);
  // The whole list wraps (a child's "next" never dead-ends, as after the clip); a favourites run keeps its ends.
  const neighbour = playing.clip || !favourites ? otherBook(queue, playing.track, direction) : queue[queue.indexOf(playing.track) + direction];
  // Past either end the speech is the whole answer and the session ends: no reprompt exists to keep it open, and a speaker button
  // (PlaybackController) drops speech anyway.
  if (neighbour === undefined) return { speech: direction > 0 ? Speech.NoNext : Speech.NoPrevious, directives: [], end: true, outcome: "no_neighbour" };
  return startBook(neighbour, 0, true, favourites);
}

/** The id of a slot the model resolved to one of its listed values (Alexa's entity resolution), else null. */
function resolvedBookId(slot: unknown): string | null {
  const authorities = objectAt(slot, "resolutions", "resolutionsPerAuthority");
  if (!Array.isArray(authorities)) return null;
  for (const authority of authorities) {
    const id = objectAt(authority, "status", "code") === SLOT_ID_MATCH ? objectAt(authority, "values", "0", "value", "id") : undefined;
    if (typeof id === "string") return id;
  }
  return null;
}

/** The books a slot names: the listed value Alexa resolved it to, else a fuzzy match of what was heard. Slot text is matched and dropped, never logged. */
function namedBooks(ctx: Context, slotName: string): Track[] {
  const slot = objectAt(ctx.envelope, "request", "intent", "slots", slotName);
  const books = allBooks(ctx);
  const id = resolvedBookId(slot);
  const resolved = id === null ? undefined : books.find((book) => book.storyId === id);
  if (resolved !== undefined) return [resolved];
  const spoken = objectAt(slot, "value");
  return typeof spoken === "string" ? findBooks(books, spoken) : [];
}

function openBooks(ctx: Context, found: readonly Track[]): Reply {
  if (found.length > 1) return chooseFrom(found, Speech.Ambiguous);
  const book = found[0];
  if (book === undefined) return { ...listPage(ctx, 0, Speech.NotFound), outcome: "not_found" };
  return ctx.locked.includes(book) ? subscriptionBook(ctx, book) : startBook(book, 0, true);
}

const readBook = (ctx: Context): Reply => openBooks(ctx, namedBooks(ctx, BOOK_SLOT));

/** A bare title: it answers a pending "which book?" for a save or a remove, else it opens the book. */
async function readTitle(ctx: Context): Promise<Reply> {
  const pending = objectAt(ctx.envelope, "session", "attributes", PENDING_ATTRIBUTE);
  if (pending === PendingAction.Save) return keepPlaying(ctx, await addFavouriteBook(ctx));
  if (pending === PendingAction.Remove) return keepPlaying(ctx, await removeFavouriteBook(ctx));
  return openBooks(ctx, namedBooks(ctx, TITLE_SLOT));
}

function listBooks(ctx: Context): Reply {
  return listPage(ctx, 0, "");
}

function moreBooks(ctx: Context): Reply {
  return listPage(ctx, pageOffset(ctx), "", Speech.SayName, true);
}

async function help(ctx: Context): Promise<Reply> {
  const playable = await hasPlayableFavourite(ctx); // "shuffle" plays a favourite: only said when one can play
  const intro = [Speech.Help, ...(playable ? [Speech.HelpShuffle] : []), Speech.HelpAlso].join(" ");
  return { ...listPage(ctx, 0, intro), outcome: "help" };
}

function fallback(ctx: Context): Reply {
  return { ...listPage(ctx, 0, Speech.Sorry), outcome: "fallback" };
}

// ---- favourites (Slice 457 decision 6) ----

interface Store {
  readonly db: D1Database;
  readonly userId: string;
}

function storeOf(ctx: Context): Store | null {
  const userId = alexaUserId(ctx.envelope);
  return ctx.db === undefined || userId === null ? null : { db: ctx.db, userId };
}

const favouritesOff = (): Reply => ({ speech: Speech.FavouritesOff, directives: [], end: true, outcome: "favourites_unavailable" });

/** The saved books that play now, in the order they were saved; null when favourites cannot be read. */
async function favouriteTracks(ctx: Context): Promise<Track[] | null> {
  const store = storeOf(ctx);
  const ids = store === null ? null : await listFavourites(store.db, store.userId);
  if (ids === null) return null;
  return ids.map((id) => ctx.tracks.find((track) => track.storyId === id)).filter((track): track is Track => track !== undefined);
}

async function hasFavourites(ctx: Context): Promise<boolean> {
  const store = storeOf(ctx);
  const ids = store === null ? null : await listFavourites(store.db, store.userId);
  return ids !== null && ids.length > 0;
}

async function hasPlayableFavourite(ctx: Context): Promise<boolean> {
  return ((await favouriteTracks(ctx)) ?? []).length > 0;
}

/** The book to save: the one named, else the one playing or last played (Alexa sends its token with every request). */
function bookToSave(ctx: Context): Track[] {
  const named = namedBooks(ctx, TITLE_SLOT);
  if (named.length > 0 || objectAt(ctx.envelope, "request", "intent", "slots", TITLE_SLOT, "value") !== undefined) return named;
  const playing = currentPlayback(ctx);
  return playing === null ? [] : [playing.track];
}

async function addFavouriteReply(store: Store, book: Track): Promise<Reply> {
  const title = escapeXml(book.title);
  const result = await addFavourite(store.db, store.userId, book.storyId);
  if (result === null) return favouritesOff();
  const line = { [AddResult.Added]: `${Speech.Saved} ${title}.`, [AddResult.Already]: `${title} ${Speech.AlreadySaved}`, [AddResult.Full]: Speech.FavouritesFull }[result];
  return { speech: line, directives: [], end: true, outcome: `favourite_${result}:${book.storyId}` };
}

async function addFavouriteBook(ctx: Context): Promise<Reply> {
  const store = storeOf(ctx);
  if (store === null) return favouritesOff();
  const found = bookToSave(ctx);
  if (found.length === 0) return reply(Speech.SaveWhat, "favourite_none");
  if (found.length > 1) return chooseFrom(found, Speech.Ambiguous, PendingAction.Save);
  const book = found[0]!;
  return ctx.locked.includes(book) ? reply(`${escapeXml(book.title)} ${Speech.SubscriptionBook} ${Speech.FavouriteSubscription}`, `favourite_paid:${book.storyId}`) : addFavouriteReply(store, book);
}

async function playFavourites(ctx: Context): Promise<Reply> {
  const saved = await favouriteTracks(ctx);
  if (saved === null) return favouritesOff();
  const first = saved[0];
  if (first !== undefined) return startBook(first, 0, true, true);
  const none = (await hasFavourites(ctx)) ? Speech.NoFavouritePlays : Speech.NoFavourites;
  return { speech: none, directives: [], end: true, outcome: "favourites_none" };
}

/** "Play a favourite": one random saved book that plays (not the one just played, when there is a choice); the "what next?" clip follows it. */
async function playRandomFavourite(ctx: Context): Promise<Reply> {
  const saved = await favouriteTracks(ctx);
  if (saved === null) return favouritesOff();
  return randomFavouriteFrom(ctx, saved);
}

/** "Alexa, shuffle" (decision 12): a random favourite, or, with no favourite that plays, a new playable book (never the one just played). */
async function shuffleOn(ctx: Context): Promise<Reply> {
  const saved = await favouriteTracks(ctx);
  if (saved !== null && saved.length > 0) return randomFavouriteFrom(ctx, saved);
  const playing = currentPlayback(ctx);
  if (playing === null) return listPage(ctx, 0, Speech.NothingPlaying);
  const other = otherBook(ctx.tracks, playing.track, 1);
  if (other === undefined) return { speech: Speech.NoNext, directives: [], end: true, outcome: "no_neighbour" };
  return startBook(other, 0, true);
}

/** "Loop off" and "shuffle off" have nothing to turn off: no speech, and a story that was playing goes on. */
const nothingToTurnOff = (ctx: Context): Reply => keepPlaying(ctx, { directives: [], end: true, outcome: "nothing_to_turn_off" });

async function randomFavouriteFrom(ctx: Context, saved: readonly Track[]): Promise<Reply> {
  const justPlayed = currentPlayback(ctx)?.track;
  const others = saved.filter((track) => track !== justPlayed);
  const choices = others.length > 0 ? others : saved;
  const pick = choices[Math.min(Math.floor(ctx.random() * choices.length), choices.length - 1)];
  if (pick !== undefined) return startBook(pick, 0, true);
  const none = (await hasFavourites(ctx)) ? Speech.NoFavouritePlays : Speech.NoFavourites;
  return { speech: none, directives: [], end: true, outcome: "favourites_none" };
}

async function listFavouriteBooks(ctx: Context): Promise<Reply> {
  const store = storeOf(ctx);
  const ids = store === null ? null : await listFavourites(store.db, store.userId);
  if (ids === null) return favouritesOff();
  const books = ids.map((id) => allBooks(ctx).find((book) => book.storyId === id)).filter((book): book is Track => book !== undefined);
  if (books.length === 0) return { speech: Speech.NoFavourites, directives: [], end: true, outcome: "favourites_none" };
  return { speech: `${Speech.YourFavourites} ${titlesOf(books)}.`, directives: [], end: true, outcome: "favourites_list" };
}

async function removeFavouriteBook(ctx: Context): Promise<Reply> {
  const store = storeOf(ctx);
  if (store === null) return favouritesOff();
  const found = namedBooks(ctx, TITLE_SLOT);
  if (found.length > 1) return chooseFrom(found, Speech.Ambiguous, PendingAction.Remove);
  const book = found[0];
  if (book === undefined) return { ...listPage(ctx, 0, Speech.NotFound), outcome: "not_found" };
  const removed = await removeFavourite(store.db, store.userId, book.storyId);
  if (removed === null) return favouritesOff();
  const speech = removed ? `${Speech.Removed} ${escapeXml(book.title)}.` : `${escapeXml(book.title)} ${Speech.NotInFavourites}`;
  return { speech, directives: [], end: true, outcome: `favourite_${removed ? "removed" : "absent"}:${book.storyId}` };
}

// ---- one handler per request type and intent ----

type Handler = (ctx: Context) => Reply | Promise<Reply>;

const INTENT_HANDLERS: Readonly<Record<string, Handler>> = {
  [IntentName.ReadBook]: readBook,
  [IntentName.BookTitle]: readTitle,
  [IntentName.ListBooks]: listBooks,
  [IntentName.MoreBooks]: moreBooks,
  [IntentName.AddFavourite]: keepingPlaying(addFavouriteBook),
  [IntentName.PlayFavourites]: playFavourites,
  [IntentName.PlayFavourite]: playRandomFavourite,
  [IntentName.ListFavourites]: keepingPlaying(listFavouriteBooks),
  [IntentName.RemoveFavourite]: keepingPlaying(removeFavouriteBook),
  [IntentName.Pause]: pause,
  [IntentName.Resume]: resume,
  [IntentName.Stop]: stop,
  [IntentName.Cancel]: stop,
  [IntentName.NavigateHome]: stop,
  [IntentName.Next]: (ctx) => step(ctx, 1),
  [IntentName.Previous]: (ctx) => step(ctx, -1),
  [IntentName.StartOver]: startOver,
  [IntentName.Repeat]: startOver, // decision 12: during a book it restarts it; during or after the end-of-story clip it replays the book just played
  [IntentName.LoopOn]: startOver,
  [IntentName.LoopOff]: nothingToTurnOff,
  [IntentName.ShuffleOn]: shuffleOn,
  [IntentName.ShuffleOff]: nothingToTurnOff,
  [IntentName.Help]: help,
  [IntentName.Fallback]: fallback,
};

/** Slice 457 decision 7: one line per request, the intent and what happened (a book id, a page, a favourite action), never anything that was said. */
function logOutcome(intent: string, outcome: string | undefined): void {
  console.log(`alexa intent=${intent} outcome=${outcome ?? DEFAULT_OUTCOME}`);
}

async function onLaunch(ctx: Context): Promise<Obj> {
  const saved = await hasFavourites(ctx);
  const hint = (await hasPlayableFavourite(ctx)) ? Speech.AfterStoryWithFavourites : Speech.AfterStory;
  const prompt = `${saved ? Speech.SayNameOrFavourites : Speech.SayName} ${hint}`;
  const launched = listPage(ctx, 0, Speech.Welcome, prompt);
  logOutcome(LAUNCH_INTENT, launched.outcome);
  return toResponse(launched);
}

async function onIntent(ctx: Context): Promise<Obj> {
  const name = objectAt(ctx.envelope, "request", "intent", "name");
  const known = typeof name === "string" && Object.hasOwn(INTENT_HANDLERS, name);
  const handler = known ? INTENT_HANDLERS[name]! : help;
  const answered = await handler(ctx);
  logOutcome(known ? name : UNKNOWN_INTENT, answered.outcome);
  return toResponse(answered);
}

const COMMAND_HANDLERS: Readonly<Record<string, Handler>> = {
  [RequestType.PlayCommand]: resume,
  [RequestType.PauseCommand]: pause,
  [RequestType.NextCommand]: (ctx) => step(ctx, 1),
  [RequestType.PreviousCommand]: (ctx) => step(ctx, -1),
};

/** A playback command (a button on the speaker or a remote) may answer with directives only, never speech. */
async function onCommand(ctx: Context, type: string): Promise<Obj> {
  return toDirectivesOnly(await COMMAND_HANDLERS[type]!(ctx));
}

/** The ENQUEUE of the "what next?" clip behind the stream with token `current` (a skill cannot speak after audio ends, so the clip is the question). */
function clipDirective(ctx: Context, played: PlayToken, current: string, withFavourites: boolean): Directive {
  const url = `${new URL(ctx.httpRequest.url).origin}${CLIP_FOLDER}/${withFavourites ? CLIP_FILE_WITH_FAVOURITES : CLIP_FILE_PLAIN}`;
  const stream = { url, token: clipTokenOf(played), offsetInMilliseconds: 0, expectedPreviousToken: current };
  return { type: "AudioPlayer.Play", playBehavior: ENQUEUE_BEHAVIOR, audioItem: { stream, metadata: { title: CLIP_TITLE } } };
}

async function enqueueClip(ctx: Context, played: PlayToken, current: string): Promise<Obj> {
  const withFavourites = await hasPlayableFavourite(ctx); // saved but locked or gone books must not make the child say "shuffle" into silence
  logOutcome(RequestType.PlaybackNearlyFinished, `clip:${played.book}:${withFavourites ? "favourites" : "plain"}`);
  return toDirectivesOnly({ directives: [clipDirective(ctx, played, current, withFavourites)], end: true });
}

/**
 * While a book nears its end: in "play my favourites" the next favourite is queued behind it (ENQUEUE, expectedPreviousToken is the stream
 * now playing); a single book, or the last favourite, gets the "what next?" clip queued instead (Slice 457 item 7). The clip itself, and
 * anything else, gets an empty answer.
 */
async function onNearlyFinished(ctx: Context): Promise<Obj> {
  const current = objectAt(ctx.envelope, "request", "token");
  const token = parseToken(current);
  if (token === null || token.clip === true || typeof current !== "string") return EMPTY_ANSWER;
  if (token.favourites !== true) return enqueueClip(ctx, token, current);
  const saved = await favouriteTracks(ctx);
  const here = saved?.findIndex((track) => track.storyId === token.book) ?? -1;
  const next = here < 0 ? undefined : saved?.[here + 1];
  if (next === undefined) return enqueueClip(ctx, token, current);
  return toDirectivesOnly({ directives: [playDirective(next, 0, { favourites: true, enqueueAfter: current })], end: true });
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
  if (token === null || token.clip === true) return; // the clip is not a listen to any book; the token already carries a validated book and language, so a book missing from the listing (or an unreadable catalogue) still counts
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
  if (type === RequestType.PlaybackNearlyFinished) return onNearlyFinished(ctx);
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

/**
 * Started, failed, session ended and anything new get an empty answer and never read the catalogue. A nearly-finished event of a book reads
 * it (to queue the next favourite, or to pick the clip by the favourites that can play); the clip's own nearly-finished gets nothing.
 */
function readsCatalogue(type: unknown, envelope: AlexaEnvelope): boolean {
  if (typeof type !== "string") return false;
  const token = parseToken(objectAt(envelope, "request", "token"));
  if (type === RequestType.PlaybackNearlyFinished) return token !== null && token.clip !== true;
  if (isSegmentEnd(type) && token?.clip === true) return false; // the clip's own end counts no listen, so it needs no catalogue
  return type === RequestType.Launch || type === RequestType.Intent || isSegmentEnd(type) || COMMAND_TYPES.has(type);
}

function isSegmentEnd(type: string): boolean {
  return type === RequestType.PlaybackStopped || type === RequestType.PlaybackFinished;
}

const NO_BOOKS: Catalogue = { tracks: [], locked: [] };

async function catalogueFor(type: unknown, envelope: AlexaEnvelope, request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, services: AlexaServices): Promise<Catalogue> {
  if (!readsCatalogue(type, envelope)) return NO_BOOKS;
  try {
    return await loadCatalogue(request, env, pathPrefix, serve, services);
  } catch (error) {
    if (typeof type === "string" && (isSegmentEnd(type) || type === RequestType.PlaybackNearlyFinished)) return NO_BOOKS; // a segment end still records its row from the token; a nearly-finished then queues the plain clip
    throw error;
  }
}

async function respond(request: Request, envelope: AlexaEnvelope, env: { DOWNLOADS?: R2Bucket } & AlexaEnv, pathPrefix: string, serve: ObjectServer, usage: SpeakerUsage | undefined, services: AlexaServices): Promise<Response> {
  const type = objectAt(envelope, "request", "type");
  try {
    const { tracks, locked } = await catalogueFor(type, envelope, request, env, pathPrefix, serve, services);
    return jsonResponse(await answer({ envelope, tracks, locked, usage, httpRequest: request, db: env.STATS, random: services.random ?? Math.random }));
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
export async function handleAlexa(request: Request, env: { DOWNLOADS?: R2Bucket } & AlexaEnv, pathPrefix: string, serve: ObjectServer, usage?: SpeakerUsage, options: VerifyOptions = {}, services: AlexaServices = {}): Promise<Response> {
  if (request.method !== "POST") return new Response(null, { status: NOT_ALLOWED_STATUS, headers: { Allow: ALLOWED_METHODS } });
  if (request.headers.get(SIGNATURE_HEADER) === null) return rejected();
  const body = await readBody(request);
  if (body === null) return rejected();
  const envelope = await verifyAlexaRequest(request, body, { ...options, skillId: options.skillId ?? env.ALEXA_SKILL_ID ?? "" });
  return envelope === null ? rejected() : respond(request, envelope, env, pathPrefix, serve, usage, services);
}
