// Slice 457: a child's favourite books on the Alexa skill, kept per Alexa account in the STATS D1 database (table `alexa_favourites`,
// worker/schema/stats.sql). The only things stored are Alexa's opaque user id and a book id: no name, device, household or time.
// Without the binding, a missing table or a D1 error every call here answers `null` (never throws), and the skill says favourites are
// not available right now. Kept free of enums and parameter properties so node's type stripping can run it in tests.

import { objectAt } from "./object-path.ts";

export const FAVOURITES_TABLE = "alexa_favourites";
export const D1_ERROR_LOG = "alexa favourites d1";
const MISSING_TABLE_PATTERN = /no such table/i;
export const MAX_FAVOURITES = 50;
const USER_ID_PATTERN = /^amzn1\.ask\.account\.[A-Za-z0-9_.-]{1,512}$/;
const BOOK_ID_PATTERN = /^[0-9a-f]{32}$/;

export const AddResult = { Added: "added", Already: "already", Full: "full" } as const;
export type AddResultName = (typeof AddResult)[keyof typeof AddResult];

/** The Alexa account id of a request (`context.System.user.userId`, else `session.user.userId`), or null when absent or not Alexa's format. */
export function alexaUserId(envelope: unknown): string | null {
  const id = objectAt(envelope, "context", "System", "user", "userId") ?? objectAt(envelope, "session", "user", "userId");
  return typeof id === "string" && USER_ID_PATTERN.test(id) ? id : null;
}

/** One log line for a failed D1 call: the error's name and whether the table is missing, never the account, a book, the SQL or its message. */
export function logD1Error(error: unknown): void {
  const name = error instanceof Error ? error.name : typeof error;
  const message = error instanceof Error ? error.message : "";
  console.warn(`${D1_ERROR_LOG} error=${name} table_missing=${MISSING_TABLE_PATTERN.test(message)}`);
}

/** One account's favourites, oldest first (the order they are played in); null when D1 cannot be read. */
export async function listFavourites(db: D1Database, userId: string): Promise<string[] | null> {
  try {
    const result = await db.prepare(`SELECT book FROM ${FAVOURITES_TABLE} WHERE user_id = ?1 ORDER BY rowid`).bind(userId).all<{ book: string }>();
    return (result.results ?? []).map((row) => row.book).filter((book) => BOOK_ID_PATTERN.test(book));
  } catch (error) {
    logD1Error(error); // the skill answers "favourites are not available"
    return null;
  }
}

/** Saves a book; `already` when it was saved before, `full` at MAX_FAVOURITES, null when D1 fails. */
export async function addFavourite(db: D1Database, userId: string, book: string): Promise<AddResultName | null> {
  if (!BOOK_ID_PATTERN.test(book)) return null;
  const current = await listFavourites(db, userId);
  if (current === null) return null;
  if (current.includes(book)) return AddResult.Already;
  if (current.length >= MAX_FAVOURITES) return AddResult.Full;
  try {
    await db.prepare(`INSERT OR IGNORE INTO ${FAVOURITES_TABLE} (user_id, book) VALUES (?1, ?2)`).bind(userId, book).run();
    return AddResult.Added;
  } catch (error) {
    logD1Error(error);
    return null;
  }
}

/** Removes a book; true when it was saved, false when it was not, null when D1 fails. */
export async function removeFavourite(db: D1Database, userId: string, book: string): Promise<boolean | null> {
  const current = await listFavourites(db, userId);
  if (current === null) return null;
  if (!current.includes(book)) return false;
  try {
    await db.prepare(`DELETE FROM ${FAVOURITES_TABLE} WHERE user_id = ?1 AND book = ?2`).bind(userId, book).run();
    return true;
  } catch (error) {
    logD1Error(error);
    return null;
  }
}
