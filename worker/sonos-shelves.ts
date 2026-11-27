// Slice 437: the shelves of one language's books on Sonos (Bedtime, New, ages, Free, A-Z) and the ids that name them. Pure: no I/O.
// Ids carry the language (`lang:<code>`, `lang:<code>/<shelf>`) so a speaker, which sends no Accept-Language, still browses in it.

import { Shelf } from "./sonos-labels.ts";
import type { ShelfName } from "./sonos-labels.ts";
import type { Track } from "./sonos.ts";

export const LANGUAGE_ID_PREFIX = "lang:";
const SHELF_SEPARATOR = "/";
const LIST_SEPARATOR = ",";
const NEW_TAG = "New";
const BEDTIME_MAX_SECONDS = 600;
const YOUNG_MAX_AGE = 5;
const OLDER_MIN_AGE = 6;
const LEADING_THE = /^the\s+/i;
const ENGLISH = "en";
const SAFE_CODE = /^[A-Za-z0-9_-]{1,64}$/;
const BOOK_SHELVES: readonly ShelfName[] = [Shelf.Bedtime, Shelf.New, Shelf.Ages35, Shelf.Ages69, Shelf.Free, Shelf.All];

export interface ShelfRow {
  readonly shelf: ShelfName;
  readonly tracks: readonly Track[];
}

export interface ShelfTarget {
  readonly language: string;
  /** Null for the language's own folder (`lang:<code>`). */
  readonly shelf: ShelfName | null;
  /** Language codes the parent listed first (only on Other languages); empty otherwise. */
  readonly firstLanguages: readonly string[];
}

export function languageFolderId(language: string): string {
  return `${LANGUAGE_ID_PREFIX}${language}`;
}

export function shelfId(language: string, shelf: ShelfName, firstLanguages: readonly string[] = []): string {
  const base = `${languageFolderId(language)}${SHELF_SEPARATOR}${shelf}`;
  return firstLanguages.length === 0 ? base : `${base}${SHELF_SEPARATOR}${firstLanguages.join(LIST_SEPARATOR)}`;
}

function shelfNameOf(text: string | undefined): ShelfName | null {
  return (Object.values(Shelf) as string[]).includes(text ?? "") ? (text as ShelfName) : null;
}

/** What a `lang:...` id names, or null for any other or malformed id. */
export function parseShelfId(id: string | null): ShelfTarget | null {
  if (id === null || !id.startsWith(LANGUAGE_ID_PREFIX)) return null;
  const [language = "", shelfText, listText, ...extra] = id.slice(LANGUAGE_ID_PREFIX.length).split(SHELF_SEPARATOR);
  if (extra.length > 0 || !SAFE_CODE.test(language)) return null;
  const shelf = shelfText === undefined ? null : shelfNameOf(shelfText);
  if (shelfText !== undefined && shelf === null) return null;
  const firstLanguages = listText === undefined ? [] : listText.split(LIST_SEPARATOR);
  if (listText !== undefined && (shelf !== Shelf.Others || firstLanguages.some((code) => !SAFE_CODE.test(code)))) return null;
  return { language, shelf, firstLanguages };
}

function collatorFor(language: string): Intl.Collator {
  try {
    return new Intl.Collator(language);
  } catch {
    // no-fallback: a code the platform's collation does not know sorts as English
    return new Intl.Collator(ENGLISH);
  }
}

function sortKey(track: Track, language: string): string {
  return language === ENGLISH ? track.title.replace(LEADING_THE, "") : track.title;
}

/** A to Z in the language's own alphabet (English ignores a leading "The"); equal titles keep a fixed order by story id. */
export function sortAlphabetically(tracks: readonly Track[], language: string): Track[] {
  const collator = collatorFor(language);
  return [...tracks].sort((a, b) => collator.compare(sortKey(a, language), sortKey(b, language)) || a.storyId.localeCompare(b.storyId));
}

function byShortest(tracks: readonly Track[], language: string): Track[] {
  const alphabetical = sortAlphabetically(tracks, language);
  return alphabetical.sort((a, b) => a.seconds - b.seconds);
}

function byNewest(tracks: readonly Track[], language: string): Track[] {
  const alphabetical = sortAlphabetically(tracks, language);
  return alphabetical.sort((a, b) => (b.added ?? "").localeCompare(a.added ?? ""));
}

/** The books of one shelf, in the shelf's order. `books` are one language's tracks. */
export function shelfTracks(books: readonly Track[], shelf: ShelfName, language: string): Track[] {
  switch (shelf) {
    case Shelf.Bedtime:
      return byShortest(books.filter((track) => track.seconds <= BEDTIME_MAX_SECONDS), language);
    case Shelf.New:
      return byNewest(books.filter((track) => track.tags.includes(NEW_TAG)), language);
    case Shelf.Ages35:
      return sortAlphabetically(books.filter((track) => track.ageMin !== null && track.ageMin <= YOUNG_MAX_AGE), language);
    case Shelf.Ages69:
      return sortAlphabetically(books.filter((track) => track.ageMax !== null && track.ageMax >= OLDER_MIN_AGE), language);
    case Shelf.Free:
      // Slice 439 (review 1, finding 11): a paid book a subscription opens (`unlocked`) is not a free book, so it stays off this shelf.
      return sortAlphabetically(books.filter((track) => !track.paid && track.unlocked !== true), language);
    case Shelf.All:
      return sortAlphabetically(books, language);
    default:
      return [];
  }
}

/** The shelves a language shows, in the mock's order, each with its books; a shelf with no book is left out. */
export function shelvesOf(books: readonly Track[], language: string): ShelfRow[] {
  return BOOK_SHELVES.map((shelf) => ({ shelf, tracks: shelfTracks(books, shelf, language) })).filter((row) => row.tracks.length > 0);
}

/** The languages Other languages lists: every language with books but `language`, the parent's own list first, then by name in the parent's language. */
export function otherLanguages(codes: readonly string[], language: string, firstLanguages: readonly string[], nameOf: (code: string) => string): string[] {
  const others = codes.filter((code) => code !== language);
  const first = firstLanguages.filter((code) => others.includes(code));
  const collator = collatorFor(language);
  const rest = others.filter((code) => !first.includes(code)).sort((a, b) => collator.compare(nameOf(a), nameOf(b)) || a.localeCompare(b));
  return [...new Set([...first, ...rest])];
}
