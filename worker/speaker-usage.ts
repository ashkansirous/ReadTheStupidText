// Slice 289: a smart-speaker play (Sonos now, Alexa in Slice 282) counted on the server, because a speaker streams and runs no Dino app.
// One play is one `book_listened` row in the same dataset and layout as the apps' counts (events.ts `toDataPoint`), with the speaker's
// name as the platform, no app version and the book's length as an estimated number of seconds (the speaker reports no played time).
// Nothing about the home is read: no household, speaker or account id, no IP; the country is the connection's, like an app row.
// Switched off with the apps' own switch (`telemetry.enabled`, passed in as the same check). Platform-neutral on purpose: the caller names the platform.
// Like events.ts this file is kept free of enums and parameter properties so node's type stripping can run it in tests.

import { LANGUAGE_PATTERN, MAX_SECONDS, NO_CONTEXT, NO_COUNT, NO_EXTRA_FIELDS, SHELF_SOURCE, countryOf, matches, requestCountry, toDataPoint } from "./events.ts";
import type { TelemetryCheck, UsageEvent } from "./events.ts";
import { STORY_ID_PATTERN } from "./download-keys.mjs";

export const SPEAKER_PLATFORM_SONOS = "sonos";
export const BOOK_LISTENED = "book_listened";
/** The row's detail: the seconds are the book's length, not a measured listening time (the dashboard hatches them). */
export const DETAIL_ESTIMATED = "estimated";
const NO_VERSION = "";
const FULL_WEIGHT = 1;
const ISO_DAY_LENGTH = 10;

/** One play of one book in one language on a speaker platform. `seconds` is the book's length. */
export interface SpeakerPlay {
  readonly platform: string;
  readonly book: string;
  readonly language: string;
  readonly seconds: number;
}

/** What a caller needs to count: the usage dataset (`DINO_USAGE`) and the config's telemetry switch. */
export interface SpeakerUsage {
  readonly dataset: AnalyticsEngineDataset | undefined;
  readonly telemetryEnabled: TelemetryCheck;
  /** Hands the counting to the runtime to finish after the answer is sent (the Worker's `ctx.waitUntil`). Without it the caller awaits the count. */
  readonly defer?: (work: Promise<unknown>) => void;
}

function wholeSecondsOrNone(seconds: number): number {
  const whole = Math.round(seconds);
  return Number.isFinite(whole) && whole >= 0 && whole <= MAX_SECONDS ? whole : NO_COUNT;
}

/** The count a play becomes, or null when its book or language is not a store id or a language code (the row would be refused like an app's). */
export function speakerEvent(play: SpeakerPlay, now: Date): UsageEvent | null {
  if (!matches(STORY_ID_PATTERN, play.book) || !matches(LANGUAGE_PATTERN, play.language)) return null;
  return {
    name: BOOK_LISTENED,
    book: play.book,
    language: play.language,
    source: SHELF_SOURCE,
    hasRecording: false, // blob5: the apps set it when the user recorded their own voice for the book; a speaker plays the pre-made audio only
    version: NO_VERSION,
    platform: play.platform,
    day: now.toISOString().slice(0, ISO_DAY_LENGTH),
    sampleRate: FULL_WEIGHT,
    ...NO_EXTRA_FIELDS,
    detail: DETAIL_ESTIMATED,
    seconds: wholeSecondsOrNone(play.seconds),
  };
}

/**
 * Writes one row for the play. Never throws: with no usage wiring, no dataset, telemetry off or the switch unreadable, nothing
 * is written. It does wait for the telemetry switch (a config read, cached for a minute), so the caller decides whether to await
 * it before answering. Returns whether a row was written (tests use it).
 */
export async function recordSpeakerPlay(usage: SpeakerUsage | undefined, play: SpeakerPlay, request: Request, now: Date = new Date()): Promise<boolean> {
  if (usage === undefined || usage.dataset === undefined) return false;
  const enabled = await usage.telemetryEnabled().catch(() => false);
  const event = enabled ? speakerEvent(play, now) : null;
  if (event === null) return false;
  try {
    usage.dataset.writeDataPoint(toDataPoint(event, { ...NO_CONTEXT, country: countryOf(requestCountry(request)) }));
  } catch {
    // no-fallback: a failed count must never fail the listener's play; counts are best effort like the apps'
    return false;
  }
  return true;
}
