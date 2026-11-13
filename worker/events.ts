// Dino Reader's anonymous usage counts (Slice 133, Decisions 140-142). The app batches a session's counts into one
//   POST /api/events    { "schemaVersion": 1, "device"?: { osMajor, formFactor, arch, cores, ram }, "events": [ { name, book, language, source, hasRecording, version, platform, day, sampleRate, detail?, session?, seq? } ] }
// and this writes each valid one to the Workers Analytics Engine dataset bound as DINO_USAGE (`dino_usage`). Nothing about the
// caller is stored or written except the country (Slice 166: the two-letter code Cloudflare derives from the connection, `request.cf.country`;
// never the IP, never sent by the app) and the optional `device` object's coarse closed values: no user agent, and no id except (Slice 250) the random per-launch session id below, which is new at every cold start and tied to no install, device or person; the only fields kept are the ones named below, each checked
// against a closed list or a strict pattern, so a changed or hostile client can add no free text. When the remote config
// (Slice 132) has telemetry off or missing, the request is answered like a success and nothing is written. Likewise (Slice 179) an android or ios event whose device.arch is x64 or x86 is dropped with the same 204: real phones are ARM, so it is a robot or emulator. Only Dino Reader's
// host and its /dino/ prefix on workers.dev route here (index.ts), which also supplies the telemetry check (it reads the
// config object). Like appconfig.ts this file imports values only from download-keys.mjs so worker/events.test.mjs can run it
// under node's type stripping: keep it free of enums and parameter properties.

import { JSON_CONTENT_TYPE, NO_CACHE, STORY_ID_PATTERN } from "./download-keys.mjs";

export const EVENTS_PATH = "/api/events";

/** The request format; the Worker reads `events` and ignores fields it does not know. */
export const EVENTS_SCHEMA_VERSION = 1;

/** One request carries at most this many counts. */
export const MAX_EVENTS_PER_REQUEST = 50;

const MAX_BODY_BYTES = 32 * 1024;
const ALLOWED_METHODS = "POST";
const OWN_BOOK = "own";
export const SHELF_SOURCE = "shelf";
const NO_BOOK = "none";
const PLATFORMS = ["android", "ios", "windows"] as const;
const BOOK_SOURCES = [SHELF_SOURCE, OWN_BOOK] as const;
const NO_DETAIL = "";
// A wait count says nothing about a language (Slice 148): the app sends this "undetermined" code, the only one the Worker takes for it.
const NO_LANGUAGE = "und";
const STORE_FILTERS = ["none", "age", "author", "age+author"] as const;
// Slice 191 adds "restore" (one per book a backup restore put back). Slice 178: file imports and mixed books; older apps send the plain first four, newer ones "<kind>:<page bucket>".
const OWN_BOOK_KINDS = ["photo", "pdf", "scan", "typed", "docx", "txt", "mixed", "restore"] as const;
const PAGE_BUCKETS = ["1", "2-5", "6-10", "11+"] as const;
// The app's supported reading languages (UiLanguage.Supported in DinoReader.Core); UsageEventTests fails when this list and the app's drift apart.
const READING_LANGUAGES = ["en", "fa", "fr", "es", "pt", "zh", "ar", "ru", "ja", "da", "fi", "sv", "nb", "nl", "de"] as const;
const VOICE_CHANGES = ["character_voice", "flavour", "narrator"] as const;
/** The waits the phone apps' overlay can show (WaitStage in the app, Slice 148); both products count them, and a test keeps this list and the app's `UsageWaitStage` the same. */
const WAIT_STAGES = ["preparing_voice", "stalled", "downloading_model", "translating", "reading_pictures", "saving", "getting_file"] as const;
const WAIT_DETAIL_SEPARATOR = ":";
// The first-run intro (Slice 155; Slice 173 added the story languages step; Slice 228 the two places and the read step): six steps, each tried or skipped; it starts from the first run or a replay and ends finished or skipped all.
// Slice 362: the new first run's six steps (language, story_languages, books, listen, voice, corner) count as intro steps too.
const INTRO_STEPS = ["places", "read", "store", "record", "add", "languages", "language", "story_languages", "books", "listen", "voice", "corner"] as const;
const INTRO_ACTIONS = ["tried", "skipped"] as const;
const INTRO_STARTS = ["first_run", "replay"] as const;
const INTRO_ENDS = ["finished", "skipped_all"] as const;
// Story languages (Slice 173): the set saved at "<count of story languages>:<where>", and a voice pack download's outcome.
const STORY_LANGUAGE_COUNTS = ["1", "2", "3"] as const;
const STORY_LANGUAGE_WHERE = ["intro", "settings"] as const;
const PACK_OUTCOMES = ["done", "failed", "cancelled"] as const;
const READ_VOICES = ["premade", "partial", "device:no_pack", "device:pack_failed", "device:text_mismatch", "device:clip_missing"] as const;
// Slice 273: the `fallback` count, one per (area, reason) a feature fell back for. Both apps send the same two closed lists (the app's FallbackArea and FallbackReason; a test keeps them the same). Later parts append inside their own block.
export const FALLBACK_AREAS = [
  "premade_audio",
  // part B: speech, voices and translation
  "speech", "translation",
  // part C: packs, OCR, documents, store, config and UI language
  "pack", "ocr", "document", "store", "config", "ui_language",
  // part D: platform gaps and the swallowed-error class
  "window", "font", "library",
  // Slice 275
  "input",
  // Slice 279
  "ui",
] as const;
export const FALLBACK_REASONS = [
  "no_pack", "pack_failed", "text_mismatch", "clip_missing", "partial",
  // part B, speech
  "neural_not_ready", "neural_unavailable", "stream_unavailable", "engine_build_failed", "segments_unsupported", "phone_cannot_speak", "no_voice", "silent_retake", "silent_sibling", "silent_gave_up", "quiet_retake", "quiet_sibling", "quiet_gave_up",
  "voice_not_in_roster", "recording_unplayable", "phone_language_missing",
  // part B, translation
  "translation_timeout", "translation_pack_missing", "translation_error", "stream_plan_failed", "stream_partial", "translation_model_missing", "translation_skipped", "translation_pack_failed",
  // part C
  "pack_sync_failed", "storyteller_failed", "recast_failed", "catalogue_failed",
  "ocr_builtin_no_pack", "ocr_builtin_failed", "ocr_platform_fallback", "ocr_pack_failed", "scanner_start_failed",
  "pdf_import_failed", "store_unreachable", "update_check_failed", "config_cached", "config_default", "ui_language_unsupported",
  // part D
  "storyteller_pack_missing", "animal_clip_unplayable", "clip_unplayable", "clip_speed_unset", "in_app_update_failed", "window_frame_failed", "displays_unreadable", "saved_position_moved", "dpi_defaulted",
  "font_fallback", "shelf_order_unreadable", "story_unreadable", "language_detect_failed", "page_read_failed", "intro_state_reset",
  // Slice 275
  "ocr_crop_degraded", "uia_unavailable", "store_failed", "preference_unsaved",
  // Slice 279
  "tap_failed", "layout_failed",
  // Slice 351: the real causes behind clip_missing (no_pack and pack_failed are above), and the two ways an animal clip failed
  "file_missing", "no_clip", "animal_clip_copy_failed", "animal_clip_play_failed",
  // Slice 359: a book's drawn back cover is named but its picture file is missing
  "back_cover_missing",
  // Slice 360: a read had no sound for a missing pack (the Wi-Fi wait is gone; its reason stays so old rows parse)
  "waiting_wifi", "pack_missing_on_play", "onboarding_try_not_deleted",
] as const;
const MAX_COUNT = 1_000_000;
export const MAX_SECONDS = 86_400;
export const NO_COUNT = -1;
// Slice 313: a shelf book was behind the store: "<result>:<from version>:<to version>", like "updated:5:6" (the app's BookUpdateResult; versions only).
const BOOK_UPDATE_RESULTS = ["staged", "updated", "skipped_edited", "failed"] as const;
const BOOK_UPDATE_VERSION = /^\d{1,4}$/;
const AUDIO_PACK_OUTCOMES = ["started", "done", "failed:network", "failed:size", "failed:unzip", "failed:clip", "failed:missing", "failed:storage", "cancelled"] as const;
// Slice 250: why a read stopped (the app's ReadStopReason; a test keeps the two lists the same). `window_stopped` is accepted though no caller emits it yet.
const READ_STOP_REASONS = ["left_book", "finished", "cancelled", "language_changed", "restarted", "window_stopped", "skipped", "failed", "other"] as const;

// ---- Screens, taps, settings and read origins (Slice 352) ----------------------------------------------------------------
// Closed lists only (no free text, no typed text, no own-book title, no id). The app's UiScreens / UiScreenActions / UiControls / UiSettings / ReadOrigins in DinoReader.Core must be the same lists:
// UiUsageTests fails when they drift. The screen a count was made on is also stored on every row as blob20 ("none" for an old build or a count made before any page).
// A `tap` or `screen` count's detail is "<screen>:<control or action>"; `setting_changed` is "<setting>:<value>"; app_backgrounded / app_resumed carry the screen alone.
export const SCREENS = [
  "intro_1", "intro_2", "intro_3", "intro_4", "first_run", "first_books", "shelf", "reading", "corner_books", "corner_store", "corner_settings", "gate", "story_languages", "language", "reading_speed", "animal_sounds",
  "record_intro", "record", "backup", "usage_counts", "about", "report_problem", "update", "close_question", "drop_language",
] as const;
export const SCREEN_ACTIONS = ["shown", "next", "back", "skip", "close"] as const;
export const CONTROLS = [
  "play", "pause", "stop", "next_page", "previous_page", "sentence", "back", "close", "done", "cancel", "confirm", "next", "skip", "skip_all", "try_it", "continue", "retry", "save", "restore", "send", "copy_code", "update", "download", "delete",
  "filter", "language", "voice", "speed", "mode", "book", "book_more", "read_now", "record", "open_corner", "tab_books", "tab_store", "tab_settings", "settings_row", "toggle", "reorder", "pick", "dismiss", "gate_key", "see_stories",
] as const;
export const SETTINGS = ["usage_counts", "animal_sounds", "reading_speed", "reading_mode", "app_language"] as const;
export const SETTING_SWITCH = ["on", "off"] as const;
export const SPEED_BUCKETS = ["slow", "normal", "fast"] as const;
export const READING_MODES = ["text", "picture", "picture_text"] as const;
export const READ_ORIGINS = ["wizard", "shelf", "corner", "resume", "next_book"] as const;
const SETTING_VALUES: Readonly<Record<string, readonly string[]>> = {
  usage_counts: SETTING_SWITCH,
  animal_sounds: SETTING_SWITCH,
  reading_speed: SPEED_BUCKETS,
  reading_mode: READING_MODES,
  app_language: READING_LANGUAGES,
};
export const NO_SCREEN = "none";

// ---- Session id (Slice 250) ----------------------------------------------------------------------------------------------
// A random id the app makes at every cold start and keeps only in memory, plus the count's order inside that session. Neither is tied to an
// install, a device or a person. Both are optional (older builds, RTST) and checked on their own: a malformed one is dropped from the row, never the event.
export const SESSION_PATTERN = /^[0-9a-f]{32}$/;
export const NO_SESSION = "";
/** The sequence a row gets when the build sent none (a real one is never negative), like the device buckets' "no data". */
export const NO_SEQ = -1;
const MAX_SEQ = 1_000_000;

// ---- Who and when (Slice 351) --------------------------------------------------------------------------------------------
// Every Dino count may carry four closed context fields: the app's screen language (blob17, the 15 app languages), the install-age and launch-count buckets (blob18, blob19; worked out on
// the device from a first-launch day that is never sent, so there is still no install id) and the whole seconds since the launch started (double6). A missing or invalid one is stored as
// "none" / -1 and keeps the event, like the device block. blob20 is the screen (Slice 352, below).
export const NO_CONTEXT_FIELD = "none";
export const INSTALL_AGES = ["first", "d0", "d1_7", "d8_30", "d31_plus"] as const;
export const LAUNCH_NUMBERS = ["1", "2_5", "6_20", "21_plus"] as const;
export const MAX_SINCE_LAUNCH = 86_400;

// ---- Device context (Slice 166) ------------------------------------------------------------------------------------------
// Both apps send the coarse device facts once per batch in an optional `device` object; the Worker checks each field against a closed
// list, stamps it on every point of the batch, and adds the country. A build without the object, or a field that is missing or not on
// its list, gives empty fields: it never drops a count. The country is `none` for no value, `XX`, `T1` (Tor) or anything not two capital letters.

const NO_COUNTRY = "none";
const COUNTRY_PATTERN = /^[A-Z]{2}$/;
const UNKNOWN_COUNTRIES = ["XX", "T1"] as const;
const DEVICE_FORM_FACTORS = ["phone", "tablet", "desktop", "other"] as const;
const DEVICE_ARCHES = ["arm64", "x64", "x86", "arm", "other"] as const;
const DEVICE_CORES = ["1-4", "5-8", "9+"] as const;
const CORE_LOWER_BOUNDS = [1, 5, 9] as const;
const DEVICE_RAM = ["<3", "3-4", "4-6", "6-8", "8+"] as const;
const RAM_LOWER_BOUNDS = [0, 3, 4, 6, 8] as const;
/** The double a bucket gets when the build sent none (a real lower bound is never negative), so "no data" never reads as "<3 GB". */
const NO_BUCKET_BOUND = -1;
const NO_DEVICE_FIELD = "";
// Slice 179: real phones and tablets are ARM, so an Android or iOS batch from an Intel device is Play's pre-launch robot or an emulator (the app's own gate misses some); it is answered like a success and nothing is written.
const PHONE_PLATFORMS = ["android", "ios"] as const;
const NON_PHONE_ARCHES = ["x64", "x86"] as const;
const MIN_DEVICE_OS_MAJOR = 1;
const MAX_DEVICE_OS_MAJOR = 99;

/** The checked device facts of a batch; every field is empty when missing or invalid. */
export interface DeviceContext {
  readonly osMajor: string;
  readonly formFactor: string;
  readonly arch: string;
  readonly cores: string;
  readonly ram: string;
}

/** What a batch adds to each of its points: the country and the checked device facts. */
export interface BatchContext {
  readonly country: string;
  readonly device: DeviceContext;
}

const EMPTY_DEVICE: DeviceContext = { osMajor: NO_DEVICE_FIELD, formFactor: NO_DEVICE_FIELD, arch: NO_DEVICE_FIELD, cores: NO_DEVICE_FIELD, ram: NO_DEVICE_FIELD };

/** The context of a batch with no country and no device object. */
export const NO_CONTEXT: BatchContext = { country: NO_COUNTRY, device: EMPTY_DEVICE };

function listedOrEmpty(list: readonly string[], value: unknown): string {
  return oneOf(list, value) ? value : NO_DEVICE_FIELD;
}

function osMajorOrEmpty(value: unknown): string {
  const valid = Number.isInteger(value) && (value as number) >= MIN_DEVICE_OS_MAJOR && (value as number) <= MAX_DEVICE_OS_MAJOR;
  return valid ? String(value) : NO_DEVICE_FIELD;
}

/** The checked `device` object of a batch: each field on its list, else empty; a missing or non-object value is all empty. */
export function parseDevice(raw: unknown): DeviceContext {
  if (!isRecord(raw)) return EMPTY_DEVICE;
  return {
    osMajor: osMajorOrEmpty(raw["osMajor"]),
    formFactor: listedOrEmpty(DEVICE_FORM_FACTORS, raw["formFactor"]),
    arch: listedOrEmpty(DEVICE_ARCHES, raw["arch"]),
    cores: listedOrEmpty(DEVICE_CORES, raw["cores"]),
    ram: listedOrEmpty(DEVICE_RAM, raw["ram"]),
  };
}

/** True for a count from an Android or iOS build on an Intel (x64/x86) device: a robot or emulator, never a real phone (Slice 179). */
export function isRobotCount(raw: unknown, device: DeviceContext): boolean {
  return isRecord(raw) && oneOf(PHONE_PLATFORMS, raw["platform"]) && oneOf(NON_PHONE_ARCHES, device.arch);
}

/** The country to record: two capital letters other than the "unknown" and Tor codes, else `none`. */
export function countryOf(raw: unknown): string {
  return matches(COUNTRY_PATTERN, raw) && !oneOf(UNKNOWN_COUNTRIES, raw) ? raw : NO_COUNTRY;
}

function lowerBoundOf(list: readonly string[], bounds: readonly number[], value: string): number {
  const index = list.indexOf(value);
  return index < 0 ? NO_BUCKET_BOUND : bounds[index]!;
}

/** The lower bound (1, 5 or 9) of a core bucket, or -1 when empty. */
export const coreBound = (cores: string): number => lowerBoundOf(DEVICE_CORES, CORE_LOWER_BOUNDS, cores);

/** The lower bound in GB (0, 3, 4, 6 or 8) of a RAM bucket, or -1 when empty. */
export const ramBound = (ram: string): number => lowerBoundOf(DEVICE_RAM, RAM_LOWER_BOUNDS, ram);

/** What an event name allows: the sources it may come from and the one extra value (`detail`) it carries. */
interface EventRule {
  readonly sources: readonly string[];
  readonly detail: (value: unknown) => boolean;
  /** When set, the only language the event may carry. */
  readonly language?: string;
  /** Slice 273: true for the events whose `count` and `deviceCount` are kept (`fallback`, `read_voice`); every other event's numbers are dropped. */
  readonly counts?: boolean;
  /** Slice 288: true for the events whose `seconds` (whole seconds of audio played, 0 to a day) is kept (`read_paused`, `read_stopped`). */
  readonly seconds?: boolean;
}

const emptyDetail = (value: unknown): boolean => value === undefined || value === NO_DETAIL;
const detailIn = (list: readonly string[]) => (value: unknown): boolean => oneOf(list, value);

// A wait count's detail is "<stage>:<duration bucket>", like "stalled:2-5" (Slice 148): both halves come from closed lists.
const waitDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  return parts.length === 2 && oneOf(WAIT_STAGES, parts[0]) && oneOf(RTST_DURATION_BUCKETS, parts[1]);
};

// An own-book count's detail is a kind alone (older apps) or "<kind>:<page bucket>", like "pdf:6-10" (Slice 178): both halves come from closed lists.
const ownBookDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  if (parts.length === 1) return oneOf(OWN_BOOK_KINDS, parts[0]);
  return parts.length === 2 && oneOf(OWN_BOOK_KINDS, parts[0]) && oneOf(PAGE_BUCKETS, parts[1]);
};

// An intro step count's detail is "<step>:<action>", like "store:tried" (Slice 155): both halves come from closed lists.
const introStepDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  return parts.length === 2 && oneOf(INTRO_STEPS, parts[0]) && oneOf(INTRO_ACTIONS, parts[1]);
};

// A story-languages count's detail is "<count>:<where>", like "2:settings" (Slice 173): both halves come from closed lists.
const storyLanguagesDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  return parts.length === 2 && oneOf(STORY_LANGUAGE_COUNTS, parts[0]) && oneOf(STORY_LANGUAGE_WHERE, parts[1]);
};

const bookUpdateDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  return parts.length === 3 && oneOf(BOOK_UPDATE_RESULTS, parts[0]) && matches(BOOK_UPDATE_VERSION, parts[1]) && matches(BOOK_UPDATE_VERSION, parts[2]);
};

// A fallback count's detail is "<area>:<reason>", like "premade_audio:partial" (Slice 273): both halves come from closed lists.
const fallbackDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  return parts.length === 2 && oneOf(FALLBACK_AREAS, parts[0]) && oneOf(FALLBACK_REASONS, parts[1]);
};

// Slice 352: a read's origin (book_opened, read_started) is empty for an older build, else one of READ_ORIGINS.
const originDetail = (value: unknown): boolean => emptyDetail(value) || oneOf(READ_ORIGINS, value);

// Slice 352: "<left>:<right>", both halves on closed lists.
const pairDetail = (left: readonly string[], right: readonly string[]) => (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  return parts.length === 2 && oneOf(left, parts[0]) && oneOf(right, parts[1]);
};

// Slice 352: a changed setting, "<setting>:<value>", the value from that setting's own list.
const settingDetail = (value: unknown): boolean => {
  const parts = typeof value === "string" ? value.split(WAIT_DETAIL_SEPARATOR) : [];
  const allowed = parts.length === 2 && Object.hasOwn(SETTING_VALUES, parts[0]!) ? SETTING_VALUES[parts[0]!]! : [];
  return parts.length === 2 && oneOf(allowed, parts[1]);
};

// Slice 133 events carry no detail; Slice 134 added the rest. Anything not named here is dropped.
const EVENT_RULES: Readonly<Record<string, EventRule>> = {
  book_opened: { sources: BOOK_SOURCES, detail: originDetail },
  book_finished: { sources: BOOK_SOURCES, detail: emptyDetail },
  store_download: { sources: [SHELF_SOURCE], detail: detailIn(STORE_FILTERS) },
  own_book_added: { sources: [OWN_BOOK], detail: ownBookDetail },
  recording_made: { sources: BOOK_SOURCES, detail: detailIn(PAGE_BUCKETS) },
  voice_changed: { sources: [NO_BOOK], detail: detailIn(VOICE_CHANGES) },
  reading_language_changed: { sources: [NO_BOOK], detail: detailIn(READING_LANGUAGES) },
  app_opened: { sources: [NO_BOOK], detail: emptyDetail },
  wait_shown: { sources: [NO_BOOK], detail: waitDetail, language: NO_LANGUAGE },
  wait_cancelled: { sources: [NO_BOOK], detail: waitDetail, language: NO_LANGUAGE },
  intro_started: { sources: [NO_BOOK], detail: detailIn(INTRO_STARTS) },
  intro_step: { sources: [NO_BOOK], detail: introStepDetail },
  intro_ended: { sources: [NO_BOOK], detail: detailIn(INTRO_ENDS) },
  story_languages_set: { sources: [NO_BOOK], detail: storyLanguagesDetail },
  story_language_added: { sources: [NO_BOOK], detail: emptyDetail },
  story_language_switched: { sources: [...BOOK_SOURCES, NO_BOOK], detail: emptyDetail },
  voice_pack_download: { sources: [NO_BOOK], detail: detailIn(PACK_OUTCOMES) },
  // Slice 250: playback events, each about a book (a shelf book or the grown-up's own); only read_stopped has a detail.
  read_started: { sources: BOOK_SOURCES, detail: originDetail },
  read_paused: { sources: BOOK_SOURCES, detail: emptyDetail, seconds: true },
  read_resumed: { sources: BOOK_SOURCES, detail: emptyDetail },
  read_stopped: { sources: BOOK_SOURCES, detail: detailIn(READ_STOP_REASONS), seconds: true },
  book_left: { sources: BOOK_SOURCES, detail: emptyDetail },
  // Slice 288: a reading reached 30 s of played audio (one per reading); `seconds` is double5 on read_paused / read_stopped only (an app's book_listened carries none and one sent to /api/events is ignored; only a speaker platform's row (Slice 289, speaker-usage.ts, platform `sonos`, detail `estimated`) carries an estimated `seconds`, so always filter hours on blob1 and blob7).
  book_listened: { sources: BOOK_SOURCES, detail: emptyDetail },
  // Slice 267: which voice a read used, and how one pre-made audio pack download went; closed lists only (docs/analytics/dino-voice-queries.md).
  read_voice: { sources: BOOK_SOURCES, detail: detailIn(READ_VOICES), counts: true },
  audio_pack_download: { sources: [SHELF_SOURCE], detail: detailIn(AUDIO_PACK_OUTCOMES) },
  // Slice 313: a shelf book was behind the store; one count per book and outcome (result:from:to).
  book_update: { sources: [SHELF_SOURCE], detail: bookUpdateDetail },
  // Slice 273: a feature fell back (area:reason); about a book or about no book. Its `count` is how many times in that session.
  fallback: { sources: [...BOOK_SOURCES, NO_BOOK], detail: fallbackDetail, counts: true },
  // Slice 352: where the app was and what was pressed; all about no book, closed lists only.
  screen: { sources: [NO_BOOK], detail: pairDetail(SCREENS, SCREEN_ACTIONS) },
  tap: { sources: [NO_BOOK], detail: pairDetail(SCREENS, CONTROLS) },
  setting_changed: { sources: [NO_BOOK], detail: settingDetail },
  app_backgrounded: { sources: [NO_BOOK], detail: detailIn([...SCREENS, NO_SCREEN]) },
  app_resumed: { sources: [NO_BOOK], detail: detailIn([...SCREENS, NO_SCREEN]) },
};
export const LANGUAGE_PATTERN = /^[a-z]{2,3}(-[A-Za-z]{2,4})?$/;
/** The version pattern both phone endpoints check (Slice 227 shares it with `/api/crash`). */
export const VERSION_PATTERN = /^\d{1,6}(\.\d{1,6}){1,3}$/;
// Slice 167: the project default version of a build never stamped by CI, i.e. built on a desk. A backstop for the apps' own test-build gate:
// such a count is dropped, not written, in both datasets.
const UNSTAMPED_VERSIONS: readonly string[] = ["1.0", "1.0.0", "1.0.0.0"];
// Slice 219: any version whose major part is 0 (including 0.0.0 and 0.0.0.0) is a pre-1.0 dev build, never a store release.
const PRE_RELEASE_MAJOR = 0;
const MIN_SAMPLE_RATE = 0.001;

/** True for a build never stamped by CI or a pre-1.0 dev build, and for anything that is not a well-formed version (Slice 223); the Worker drops such a count or report. */
export function isUnstampedVersion(version: unknown): boolean {
  if (!matches(VERSION_PATTERN, version)) return true;
  const majorPart = version.split(".")[0] ?? "";
  return UNSTAMPED_VERSIONS.includes(version) || Number.parseInt(majorPart, 10) === PRE_RELEASE_MAJOR;
}
const DAY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const ErrorCode = {
  BadRequest: "bad-request",
  TooMany: "too-many-events",
  TooLarge: "body-too-large",
  Unavailable: "events-unavailable",
} as const;

/** A count that passed every check; these are exactly the fields that reach the dataset. */
export interface UsageEvent {
  readonly name: string;
  readonly book: string;
  readonly language: string;
  readonly source: string;
  readonly hasRecording: boolean;
  readonly version: string;
  readonly platform: string;
  readonly day: string;
  readonly sampleRate: number;
  /** The event's one extra closed value (see EVENT_RULES); empty for the reader events. */
  readonly detail: string;
  /** Slice 250: the per-launch session id (32 hex digits), or empty when the build sent none or a malformed one. */
  readonly session: string;
  /** Slice 250: the count's order inside its session (0 or more), or -1 when there is no valid session id or sequence. */
  readonly seq: number;
  /** Slice 273: how many times (a fallback in its session) or how many pieces were read by clips (read_voice), or -1 when the build sent none. */
  readonly count: number;
  /** Slice 273: how many pieces a read gave to the device voice (read_voice), or -1 when none was sent. */
  readonly deviceCount: number;
  /** Slice 288: whole seconds of audio played (read_paused, read_stopped), or -1 when none was sent or it was out of range. */
  readonly seconds: number;
  /** Slice 351: the app's screen language (one of the 15), or "none". */
  readonly uiLang: string;
  /** Slice 351: install age bucket (INSTALL_AGES), or "none". */
  readonly installAge: string;
  /** Slice 351: launch count bucket (LAUNCH_NUMBERS), or "none". */
  readonly launchNo: string;
  /** Slice 351: whole seconds since the launch started (0 to a day), or -1. */
  readonly sinceLaunch: number;
  /** Slice 352: the screen the app was on (SCREENS), or "none". Stored as blob20. */
  readonly screen: string;
}

/** The optional fields of a count, all "none" (a caller that has none of them, like a speaker row, spreads this and sets only what it has). Typed on UsageEvent, so a new field there fails to compile wherever a row is built. */
export const NO_EXTRA_FIELDS: Pick<UsageEvent, "detail" | "session" | "seq" | "count" | "deviceCount" | "seconds" | "uiLang" | "installAge" | "launchNo" | "sinceLaunch" | "screen"> = {
  screen: NO_SCREEN,
  uiLang: NO_CONTEXT_FIELD,
  installAge: NO_CONTEXT_FIELD,
  launchNo: NO_CONTEXT_FIELD,
  sinceLaunch: NO_COUNT,
  detail: NO_DETAIL,
  session: NO_SESSION,
  seq: NO_SEQ,
  count: NO_COUNT,
  deviceCount: NO_COUNT,
  seconds: NO_COUNT,
};

/** What this module needs from the Worker's bindings (`DINO_USAGE` is declared in wrangler.jsonc). */
export interface EventsEnv {
  readonly DINO_USAGE?: AnalyticsEngineDataset;
  /** Slice 251: TestFlight builds' counts (`dino_usage_beta`), never mixed into `DINO_USAGE`. */
  readonly DINO_USAGE_BETA?: AnalyticsEngineDataset;
}

/** Slice 251: TestFlight batches post to their own route, which only writes `DINO_USAGE_BETA`. A Worker without it answers 404, so a beta batch can never reach `dino_usage`. */
export const BETA_EVENTS_PATH = "/api/events/beta";
const DINO_PATH_PREFIX = "/dino";
/** A `channel: "beta"` body on the production route is refused, so a beta batch that reaches it by mistake is never counted as production. */
const BETA_CHANNEL = "beta";

/** True while the remote config has telemetry on; false when it is off or missing. Rejects when the config cannot be read (answered 503, so the app retries). */
export type TelemetryCheck = () => Promise<boolean>;

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function oneOf(list: readonly string[], value: unknown): value is string {
  return typeof value === "string" && list.includes(value);
}

export function matches(pattern: RegExp, value: unknown): value is string {
  return typeof value === "string" && pattern.test(value);
}

function isRealDay(value: unknown): value is string {
  const parts = typeof value === "string" ? DAY_PATTERN.exec(value) : null;
  if (parts === null) return false;
  const [year, month, day] = [Number(parts[1]), Number(parts[2]), Number(parts[3])];
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

// A shelf book is named by its package id; an own book never carries one, and a count about no book names none, so the source and the book must agree.
function bookMatchesSource(book: unknown, source: unknown): book is string {
  if (source === OWN_BOOK) return book === OWN_BOOK;
  if (source === NO_BOOK) return book === NO_BOOK;
  return matches(STORY_ID_PATTERN, book);
}

function ruleFor(name: unknown): EventRule | null {
  return typeof name === "string" && Object.hasOwn(EVENT_RULES, name) ? EVENT_RULES[name]! : null;
}

/** The checked count in `raw`, or null when any field is missing, of the wrong type or outside its list or pattern. */
export function parseEvent(raw: unknown): UsageEvent | null {
  if (!isRecord(raw)) return null;
  const { name, book, language, source, hasRecording, version, platform, day, sampleRate } = raw;
  const rule = ruleFor(name);
  if (rule === null || !rule.detail(raw["detail"])) return null;
  const detail = typeof raw["detail"] === "string" ? raw["detail"] : NO_DETAIL;
  const valid =
    typeof name === "string" &&
    oneOf(rule.sources, source) &&
    bookMatchesSource(book, source) &&
    matches(LANGUAGE_PATTERN, language) &&
    (rule.language === undefined || language === rule.language) &&
    typeof hasRecording === "boolean" &&
    matches(VERSION_PATTERN, version) &&
    !isUnstampedVersion(version as string) &&
    oneOf(PLATFORMS, platform) &&
    isRealDay(day) &&
    typeof sampleRate === "number" &&
    sampleRate >= MIN_SAMPLE_RATE &&
    sampleRate <= 1;
  if (!valid) return null;
  const session = matches(SESSION_PATTERN, raw["session"]) ? raw["session"] : NO_SESSION;
  const seq = session !== NO_SESSION && isSeq(raw["seq"]) ? raw["seq"] : NO_SEQ;
  return { name, book, language, source, hasRecording, version, platform, day, sampleRate, detail, session, seq, count: rule.counts ? countOrNone(raw["count"]) : NO_COUNT, deviceCount: rule.counts ? countOrNone(raw["deviceCount"]) : NO_COUNT, seconds: rule.seconds ? secondsOrNone(raw["seconds"]) : NO_COUNT, ...contextOf(raw) };
}

/** Slice 351: the who-and-when fields of a count, each on its closed list or whole-number range, else "none" / -1 (never drops the event). */
function contextOf(raw: Record<string, unknown>): Pick<UsageEvent, "uiLang" | "installAge" | "launchNo" | "sinceLaunch" | "screen"> {
  const since = raw["sinceLaunch"];
  return {
    screen: oneOf(SCREENS, raw["screen"]) ? raw["screen"] : NO_SCREEN, // Slice 352: blob20
    uiLang: oneOf(READING_LANGUAGES, raw["uiLang"]) ? raw["uiLang"] : NO_CONTEXT_FIELD,
    installAge: oneOf(INSTALL_AGES, raw["installAge"]) ? raw["installAge"] : NO_CONTEXT_FIELD,
    launchNo: oneOf(LAUNCH_NUMBERS, raw["launchNo"]) ? raw["launchNo"] : NO_CONTEXT_FIELD,
    sinceLaunch: Number.isInteger(since) && (since as number) >= 0 && (since as number) <= MAX_SINCE_LAUNCH ? (since as number) : NO_COUNT,
  };
}

/** A count's optional number, 0 to a million, else -1: a malformed one is dropped from the row, never the event. */
function countOrNone(value: unknown): number {
  return Number.isInteger(value) && (value as number) >= 0 && (value as number) <= MAX_COUNT ? (value as number) : NO_COUNT;
}

/** A count's played seconds, 0 to a day, else -1: a malformed one is dropped from the row, never the event. */
function secondsOrNone(value: unknown): number {
  return Number.isInteger(value) && (value as number) >= 0 && (value as number) <= MAX_SECONDS ? (value as number) : NO_COUNT;
}

function isSeq(value: unknown): value is number {
  return Number.isInteger(value) && (value as number) >= 0 && (value as number) <= MAX_SEQ;
}

/** The Analytics Engine data point for a count: the name is the sampling index, every field a blob (blob10-blob15 are the batch's country and device facts, Slice 166; blob16 is the per-launch session id, Slice 250, empty for a build that sent none), and double1 is the count's weight (1 / the sample rate the app counted at), so SUM(double1) estimates the true total; double2 is the count's order inside its session (-1 for none); double3 and double4 (Slice 273) are the count's `count` and `deviceCount` (-1 for none); double5 (Slice 288) is the played `seconds` of a read_paused or read_stopped (-1 for none). Analytics Engine takes up to 20 blobs and 20 doubles, so there is room left. */
export function toDataPoint(event: UsageEvent, context: BatchContext = NO_CONTEXT): AnalyticsEngineDataPoint {
  const { country, device } = context;
  return {
    indexes: [event.name],
    blobs: [
      event.name, event.book, event.language, event.source, event.hasRecording ? "yes" : "no", event.version, event.platform, event.day, event.detail,
      country, device.osMajor, device.formFactor, device.arch, device.cores, device.ram,
      event.session,
      event.uiLang, event.installAge, event.launchNo, // blob17-blob19 (Slice 351)
      event.screen, // blob20 (Slice 352): the screen the count was made on
    ],
    doubles: [1 / event.sampleRate, event.seq, event.count, event.deviceCount, event.seconds, event.sinceLaunch],
  };
}

export function reply(status: number, code?: string): Response {
  const headers: Record<string, string> = { "Cache-Control": NO_CACHE };
  if (code === undefined) return new Response(null, { status, headers });
  headers["Content-Type"] = JSON_CONTENT_TYPE;
  return new Response(JSON.stringify({ code }), { status, headers });
}

// Reads at most `maxBytes` (the events cap by default; Slice 227's crash reports allow more): the stream is cut off as soon as it passes the cap, so a chunked or lying request is never buffered whole.
export async function readCappedText(request: Request, maxBytes: number = MAX_BODY_BYTES): Promise<string | "too-large"> {
  if (request.body === null) return "";
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > maxBytes) {
      await reader.cancel();
      return "too-large";
    }
    chunks.push(value);
  }
  return new TextDecoder().decode(await new Blob(chunks).arrayBuffer());
}

async function readBody(request: Request): Promise<unknown | "too-large" | "invalid"> {
  const declared = Number(request.headers.get("Content-Length") ?? 0);
  if (declared > MAX_BODY_BYTES) return "too-large";
  const text = await readCappedText(request);
  if (text === "too-large") return text;
  try {
    return JSON.parse(text);
  } catch {
    return "invalid";
  }
}

/** True only for the exact beta path, plain or under the workers.dev `/dino` prefix; any other path (a lookalike) is the production route. */
function isBetaPath(pathname: string): boolean {
  return pathname === BETA_EVENTS_PATH || pathname === DINO_PATH_PREFIX + BETA_EVENTS_PATH;
}

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isEventsPath(pathname: string): boolean {
  return pathname === EVENTS_PATH || pathname === BETA_EVENTS_PATH;
}

/** Turns one raw count into a data point; null when the count is invalid. */
type PointMaker = (raw: unknown, context: BatchContext) => AnalyticsEngineDataPoint | null;

// The connection's country as Cloudflare saw it (undefined in tests and local dev, where there is no `cf`); nothing else about the connection is read.
export function requestCountry(request: Request): unknown {
  return (request as { cf?: { country?: unknown } }).cf?.country;
}

// The shared request handling (method, telemetry switch, binding, size caps, batch limit); the two products differ only in the dataset and in what a valid count is.
async function handleBatch(request: Request, dataset: AnalyticsEngineDataset | undefined, telemetryEnabled: TelemetryCheck, makePoint: PointMaker, refuseBetaChannel = false): Promise<Response> {
  if (request.method !== "POST") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS } });
  }
  // The cheap checks come first: a switched-off or unconfigured endpoint never reads the body.
  const enabled = await telemetryEnabled().catch(() => null);
  if (enabled === null) return reply(503, ErrorCode.Unavailable);
  if (!enabled) return reply(204);
  if (!dataset) return reply(503, ErrorCode.Unavailable);
  const body = await readBody(request);
  if (body === "too-large") return reply(413, ErrorCode.TooLarge);
  const events = isRecord(body) ? body["events"] : undefined;
  if (body === "invalid" || !Array.isArray(events)) return reply(400, ErrorCode.BadRequest);
  if (events.length > MAX_EVENTS_PER_REQUEST) return reply(413, ErrorCode.TooMany);
  if (refuseBetaChannel && isRecord(body) && body["channel"] === BETA_CHANNEL) return reply(400, ErrorCode.BadRequest);
  const context = { country: countryOf(requestCountry(request)), device: parseDevice(isRecord(body) ? body["device"] : undefined) };
  for (const raw of events) {
    if (isRobotCount(raw, context.device)) continue;
    const point = makePoint(raw, context);
    if (point !== null) dataset.writeDataPoint(point);
  }
  return reply(204);
}

/** Answers POST /api/events: 204 when taken or deliberately dropped, 400/413 for a body that is not a batch of counts. */
export function handleEvents(request: Request, env: EventsEnv, telemetryEnabled: TelemetryCheck): Promise<Response> {
  const beta = isBetaPath(new URL(request.url).pathname);
  return handleBatch(request, beta ? env.DINO_USAGE_BETA : env.DINO_USAGE, telemetryEnabled, (raw, context) => {
    const event = parseEvent(raw);
    return event === null ? null : toDataPoint(event, context);
  }, !beta);
}

// ---- Read The Stupid Text's phone counts (Slice 137, Decisions 143-145) -------------------------------------------------
// The same pipe as Dino's, its own dataset (`rtst_usage`, binding RTST_USAGE) and its own closed event set. A count is a name
// plus closed-list fields; no free text, no id, nothing finer than the day. Each event names the fields it requires and the
// ones it may carry; any other field, or a value outside its list or pattern, drops the whole count.

/** The Analytics Engine binding for the phone's counts (`rtst_usage`, declared in both wrangler configs). */
export interface RtstEventsEnv {
  readonly RTST_USAGE?: AnalyticsEngineDataset;
}

const YES = "yes";
const NO = "no";
const RTST_MODES = ["original", "translation"] as const;
const RTST_ENGINES = ["phone", "supertonic", "piper", "melo", "kokoro", "windows"] as const;
const RTST_SOURCES = ["selection", "clipboard", "hotkey", "panel"] as const;
const RTST_LENGTH_BUCKETS = ["<200", "200-1000", "1000-5000", ">5000"] as const;
const RTST_STAGES = ["model_download", "translate", "first_audio"] as const;
const RTST_OUTCOMES = ["done", "cancelled", "failed"] as const;
const RTST_DURATION_BUCKETS = ["<0.5", "0.5-1", "1-2", "2-5", "5-10", "10-20", ">20"] as const;
const OS_MAJOR_FIELD = "osMajor";
const MIN_OS_MAJOR = 1;
const MAX_OS_MAJOR = 99;

/** The blob order of a count after its envelope; a field a count does not carry is an empty blob. New fields are only ever appended (Slice 140 added `source`, Slice 139 `early`, Slice 148 `waitStage`). */
export const RTST_FIELDS = ["language", "sourceLanguage", "mode", "engine", "voiceDefault", "lengthBucket", "osMajor", "stage", "outcome", "durationBucket", "source", "early", "waitStage"] as const;
// Slice 273: the fallback count's fields. The dataset has 20 blobs and RTST uses all of them, so these have no blob of their own: `fallbackArea` is stored in the `stage` blob and `fallbackReason` in the `outcome` blob (a fallback count carries neither), and `count` in double4.
const RTST_FALLBACK_FIELDS = ["fallbackArea", "fallbackReason", "count"] as const;
const RTST_ALL_FIELDS = [...RTST_FIELDS, ...RTST_FALLBACK_FIELDS] as const;
type RtstField = (typeof RTST_ALL_FIELDS)[number];
type FieldValue = string | number | boolean;

const isOsMajor = (value: unknown): boolean => Number.isInteger(value) && (value as number) >= MIN_OS_MAJOR && (value as number) <= MAX_OS_MAJOR;

const RTST_VALIDATORS: Readonly<Record<RtstField, (value: unknown) => boolean>> = {
  language: (value) => matches(LANGUAGE_PATTERN, value),
  sourceLanguage: (value) => matches(LANGUAGE_PATTERN, value),
  mode: (value) => oneOf(RTST_MODES, value),
  engine: (value) => oneOf(RTST_ENGINES, value),
  voiceDefault: (value) => typeof value === "boolean",
  lengthBucket: (value) => oneOf(RTST_LENGTH_BUCKETS, value),
  osMajor: isOsMajor,
  stage: (value) => oneOf(RTST_STAGES, value),
  outcome: (value) => oneOf(RTST_OUTCOMES, value),
  durationBucket: (value) => oneOf(RTST_DURATION_BUCKETS, value),
  source: (value) => oneOf(RTST_SOURCES, value),
  early: (value) => typeof value === "boolean",
  waitStage: (value) => oneOf(WAIT_STAGES, value),
  fallbackArea: (value) => oneOf(FALLBACK_AREAS, value),
  fallbackReason: (value) => oneOf(FALLBACK_REASONS, value),
  count: (value) => Number.isInteger(value) && (value as number) >= 0 && (value as number) <= MAX_COUNT,
};

interface RtstRule {
  readonly required: readonly RtstField[];
  readonly optional: readonly RtstField[];
}

const RTST_EVENT_RULES: Readonly<Record<string, RtstRule>> = {
  app_opened: { required: ["language", "osMajor"], optional: [] },
  read_started: { required: ["language", "mode", "engine", "voiceDefault", "lengthBucket"], optional: ["sourceLanguage", "source"] },
  translate_offered: { required: ["sourceLanguage", "language"], optional: [] },
  translate_chosen: { required: ["language"], optional: ["sourceLanguage"] },
  voice_changed: { required: ["language", "engine"], optional: [] },
  timing: { required: ["stage", "outcome", "durationBucket"], optional: ["sourceLanguage", "language", "early"] },
  wait_shown: { required: ["waitStage", "durationBucket"], optional: [] },
  wait_cancelled: { required: ["waitStage", "durationBucket"], optional: [] },
  fallback: { required: ["fallbackArea", "fallbackReason"], optional: ["language", "count"] },
};

/** A phone count that passed every check: the envelope plus the closed fields its event allows. */
export interface RtstEvent {
  readonly name: string;
  readonly version: string;
  readonly platform: string;
  readonly day: string;
  readonly sampleRate: number;
  readonly fields: Readonly<Partial<Record<RtstField, FieldValue>>>;
}

function rtstRuleFor(name: unknown): RtstRule | null {
  return typeof name === "string" && Object.hasOwn(RTST_EVENT_RULES, name) ? RTST_EVENT_RULES[name]! : null;
}

function hasValidEnvelope(raw: Record<string, unknown>): boolean {
  const { version, platform, day, sampleRate } = raw;
  return (
    matches(VERSION_PATTERN, version) &&
    !isUnstampedVersion(version as string) &&
    oneOf(PLATFORMS, platform) &&
    isRealDay(day) &&
    typeof sampleRate === "number" &&
    sampleRate >= MIN_SAMPLE_RATE &&
    sampleRate <= 1
  );
}

// The event's fields, or null when one is missing, invalid or not allowed for this event.
function rtstFieldsOf(raw: Record<string, unknown>, rule: RtstRule): RtstEvent["fields"] | null {
  const allowed: readonly RtstField[] = [...rule.required, ...rule.optional];
  const present = RTST_ALL_FIELDS.filter((field) => raw[field] !== undefined);
  if (present.some((field) => !allowed.includes(field))) return null;
  if (rule.required.some((field) => raw[field] === undefined)) return null;
  if (present.some((field) => !RTST_VALIDATORS[field](raw[field]))) return null;
  return Object.fromEntries(present.map((field) => [field, raw[field] as FieldValue]));
}

/** The checked phone count in `raw`, or null when anything about it is outside the closed lists. */
export function parseRtstEvent(raw: unknown): RtstEvent | null {
  if (!isRecord(raw)) return null;
  const rule = rtstRuleFor(raw["name"]);
  if (rule === null || !hasValidEnvelope(raw)) return null;
  const fields = rtstFieldsOf(raw, rule);
  if (fields === null) return null;
  return { name: raw["name"] as string, version: raw["version"] as string, platform: raw["platform"] as string, day: raw["day"] as string, sampleRate: raw["sampleRate"] as number, fields };
}

function rtstBlob(value: FieldValue | undefined): string {
  if (value === undefined) return "";
  if (typeof value === "boolean") return value ? YES : NO;
  return String(value);
}

// A fallback count has no stage or outcome: its area and reason are stored in those two blobs (Slice 273).
function fallbackFieldFor(event: RtstEvent, field: RtstField): FieldValue | undefined {
  if (field === "stage") return event.fields.fallbackArea;
  return field === "outcome" ? event.fields.fallbackReason : undefined;
}

// The device's OS version fills the count's osMajor blob on every event (Slice 166); an old build sends it only on app_opened, as the event's own field.
function rtstFieldBlob(event: RtstEvent, device: DeviceContext, field: RtstField): string {
  const own = rtstBlob(event.fields[field] ?? fallbackFieldFor(event, field));
  return field === OS_MAJOR_FIELD && own === "" ? device.osMajor : own;
}

/**
 * The data point of a phone count: the name is the sampling index, the envelope and fields are blobs (blob5-blob17), then the batch's country,
 * form factor and architecture (blob18-blob20, Slice 166). The doubles are the count's weight (1 / sampleRate), the core bucket's lower bound
 * (1, 5, 9) and the RAM bucket's lower bound in GB (0, 3, 4, 6, 8); -1 when the build sent none; double4 (Slice 273) is a fallback count's `count` (-1 for none).
 */
export function toRtstDataPoint(event: RtstEvent, context: BatchContext = NO_CONTEXT): AnalyticsEngineDataPoint {
  const { country, device } = context;
  return {
    indexes: [event.name],
    blobs: [event.name, event.version, event.platform, event.day, ...RTST_FIELDS.map((field) => rtstFieldBlob(event, device, field)), country, device.formFactor, device.arch],
    doubles: [1 / event.sampleRate, coreBound(device.cores), ramBound(device.ram), Number(event.fields.count ?? NO_COUNT)],
  };
}

/** Answers POST /api/events on Read The Stupid Text's host: the phone's closed event set into the RTST_USAGE dataset. */
export function handleRtstEvents(request: Request, env: RtstEventsEnv, telemetryEnabled: TelemetryCheck): Promise<Response> {
  if (isBetaPath(new URL(request.url).pathname)) return Promise.resolve(new Response(null, { status: 404 })); // RTST has no beta route
  return handleBatch(request, env.RTST_USAGE, telemetryEnabled, (raw, context) => {
    const event = parseRtstEvent(raw);
    return event === null ? null : toRtstDataPoint(event, context);
  });
}
