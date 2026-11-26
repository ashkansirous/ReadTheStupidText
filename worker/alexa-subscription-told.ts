// Slice 457 decision 15: the Alexa skill tells an account about the subscription books at most once a week. The one thing kept is the time
// it was last told, per opaque Alexa account id, in the STATS D1 database (table `alexa_subscription_told`, worker/schema/stats.sql);
// no name, device, household or speech. Fails open: without the binding or the table, or on any D1 error, the line is said every time.
// Kept free of enums and parameter properties so node's type stripping can run it in tests.

import { logD1Error } from "./alexa-favourites.ts";

export const SUBSCRIPTION_TOLD_TABLE = "alexa_subscription_told";
export const TOLD_INTERVAL_MS = 7 * 24 * 60 * 60 * 1000;

export const ToldState = { Tell: "tell", Quiet: "quiet", Unreadable: "unreadable" } as const;
export type ToldStateValue = (typeof ToldState)[keyof typeof ToldState];

/**
 * Whether the subscription line should be said: Tell (never told, or over a week ago), Quiet (told within the week) or Unreadable (no memory
 * or it cannot be read: the line is said, and nothing is written, so a broken table logs one warning, not two).
 */
export async function shouldTellSubscription(db: D1Database | undefined, userId: string | null, nowMs: number): Promise<ToldStateValue> {
  if (db === undefined || userId === null) return ToldState.Unreadable;
  try {
    const row = await db.prepare(`SELECT told_at FROM ${SUBSCRIPTION_TOLD_TABLE} WHERE user_id = ?1`).bind(userId).first<{ told_at: number }>();
    const due = row === null || typeof row.told_at !== "number" || nowMs - row.told_at >= TOLD_INTERVAL_MS || row.told_at > nowMs;
    return due ? ToldState.Tell : ToldState.Quiet;
  } catch (error) {
    logD1Error(error); // fail open: the line is said
    return ToldState.Unreadable;
  }
}

/** Remembers that the line was said now; a failure is logged (no account, no SQL) and ignored. */
export async function markSubscriptionTold(db: D1Database | undefined, userId: string | null, nowMs: number): Promise<void> {
  if (db === undefined || userId === null) return;
  try {
    await db.prepare(`INSERT INTO ${SUBSCRIPTION_TOLD_TABLE} (user_id, told_at) VALUES (?1, ?2) ON CONFLICT(user_id) DO UPDATE SET told_at = ?2`).bind(userId, nowMs).run();
  } catch (error) {
    logD1Error(error);
  }
}
