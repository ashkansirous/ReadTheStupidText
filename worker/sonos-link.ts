// Slice 438: every Sonos home links once through the Dino app (Sonos AppLink) and saves its story language. This file holds the whole link:
// the three SMAPI calls (getAppLink, getDeviceAuthToken, refreshAuthToken), the JSON calls the Dino app makes (confirm, unlink), the D1 rows
// (worker/schema/sonos-homes.sql) and the words of the "Story language" row. Sources: docs/plan/slices/438-dino-sonos-link-story-language.md (B1).
// A home is kept as hashes (SHA-256 of the household id, of the authToken and privateKey given to Sonos and of the subscription proof key) plus, since Slice 439, the subscription proof itself
// in the `proof` column (plain, reachable only by this Worker; the proof is asked of Apple or Google here and is never sent to Sonos, a speaker or the app). The household id, the tokens and
// the proof never reach a log. The page Sonos opens is site/dino/sonos-link.html.

import { isSubscriptionProduct } from "./products.ts";
import { parseProof } from "./entitlements/proof.ts";
import { SubscriptionAnswerKind } from "./entitlements/subscription.ts";
import type { SubscriptionSource } from "./entitlements/subscription.ts";
import { Store, SubscriptionState } from "./entitlements/types.ts";
import { FaultCode, FaultMessage, SoapFault, SonosAction, escapeXml, readBoundedText, resultResponse, tag } from "./sonos-soap.ts";
import type { LoginToken, SonosRequest } from "./sonos-soap.ts";

const HOME_TABLE = "sonos_homes";
const PENDING_TABLE = "sonos_pending_links";
const LINK_CODE_BYTES = 16;
const TOKEN_BYTES = 32;
const LINK_LIFETIME_MS = 3_600_000;
const DAY_MS = 86_400_000;
const IDLE_HOME_MS = 400 * DAY_MS;
const MAX_PENDING_PER_HOME = 3;
const MAX_HOUSEHOLD_LENGTH = 255;
const MAX_CALLBACK_LENGTH = 1024;
const LINK_CODE_PATTERN = /^[0-9a-f]{32}$/;
const PROOF_PATTERN = /^[A-Za-z0-9_.:=+/-]{8,512}$/;
const SONOS_SCHEME = /^sonos[a-z0-9+.-]*:$/;
const SONOS_HOST = "sonos.com";
const LINK_PAGE_PATH = "/dino/sonos-link.html";
const APP_URL_STRING_ID = "SIGN_IN";
const JSON_CONTENT_TYPE = "application/json";
const D1_ERROR_LOG = "sonos link d1";
const notLinkedDetail = (info: string, code: number): string => `<ExceptionInfo>${info}</ExceptionInfo><SonosError>${code}</SonosError>`;

/** The fifteen story languages a home can choose (the apps' languages, the same codes as the catalogue's). */
export const STORY_LANGUAGES: readonly string[] = ["en", "ar", "da", "de", "es", "fa", "fi", "fr", "ja", "nb", "nl", "pt", "ru", "sv", "zh"];

/** Slice 438: what handleSonos needs to know about links. `db` is the STATS binding; a missing one means no link can be checked and every call faults. */
export interface SonosLinks {
  readonly db: D1Database | undefined;
  /** Epoch milliseconds; tests pass a fixed clock. */
  readonly now?: () => number;
  /** Slice 439: asks the store whether a home's subscription proof is active (the paid gate's switch, limit and cache, built in index.ts). Absent = no home has a subscription. */
  readonly subscription?: SubscriptionSource;
}

/** A linked home as a browse sees it. `proof` is the subscription proof the app linked with (Slice 439), null for a home linked without one. */
export interface Home {
  readonly homeHash: string;
  readonly storyLanguage: string;
  readonly proof?: string | null;
}

/** The JSON operations the Dino app sends to `POST /sonos` with `Content-Type: application/json`. `renew` (Slice 439) swaps the stored proof when the subscription renews or changes. */
export const LinkOp = { Confirm: "confirm", Unlink: "unlink", Renew: "renew" } as const;

const NOT_SUBSCRIPTION_ERROR = "not_subscription";
const NO_HOME_ERROR = "unknown_home";
const INACTIVE_ERROR = "subscription_inactive";
const PROOF_TAKEN_ERROR = "proof_taken";
const PAID_BOOKS_OFF = "paid_books_off";
const SUBSCRIPTION_LOG = "sonos subscription";

/** True for a proof the subscription check can use: a store proof (not Windows, which reads its end on the device) whose product is a Dino subscription. A single-book or bundle proof is not one. */
export function isSubscriptionProof(proof: string): boolean {
  const parsed = parseProof(proof);
  return parsed !== null && parsed.store !== Store.Windows && isSubscriptionProduct(parsed.productId);
}

const UNKNOWN_CLIENT = "unknown";
const CLIENT_IP_HEADER = "CF-Connecting-IP";
const LAST_ANSWER_MS = 10 * 60_000;
const MAX_LAST_ANSWERS = 500;

/** The key the store-call limit counts a request under: the connection's IP, exactly as the paid gate keys its own limit (stories.ts); only the limiter sees it, nothing stores or logs it. */
export function clientKeyOf(request: Request): string {
  return request.headers.get(CLIENT_IP_HEADER) ?? UNKNOWN_CLIENT;
}

/**
 * Slice 439 (review 3, finding 1): the store-call key for a call that comes from Sonos (browse, getMediaURI, the media route). Those arrive from Sonos's cloud servers, so one IP stands for every home; a linked home
 * (made only by a store-checked confirm, so a fake one cannot be made) gets its own bucket. The app's own calls (confirm, renew) stay on clientKeyOf.
 */
export function homeKeyOf(home: Home): string {
  return `home:${home.homeHash}`;
}

/** What the store says about one subscription proof: `Active` (active or grace), `Inactive` (lapsed, unknown, not a subscription), `Limited` (over the client's store-call limit) or `Unavailable` (the store cannot be asked, or nothing can ask it). */
export const ProofCheck = { Active: "active", Inactive: "inactive", Limited: "limited", Unavailable: "unavailable", Off: "off" } as const;
export type ProofCheckName = (typeof ProofCheck)[keyof typeof ProofCheck];

function checkOfAnswer(kind: string, state: string | undefined): ProofCheckName {
  if (kind === SubscriptionAnswerKind.Off) return ProofCheck.Off;
  if (kind === SubscriptionAnswerKind.RateLimited) return ProofCheck.Limited;
  if (kind === SubscriptionAnswerKind.Status) return state === SubscriptionState.Active || state === SubscriptionState.Grace ? ProofCheck.Active : ProofCheck.Inactive;
  return kind === SubscriptionAnswerKind.BadProof || kind === SubscriptionAnswerKind.NoProof ? ProofCheck.Inactive : ProofCheck.Unavailable;
}

/** Asks the store (through the paid gate's source: its switch, store-call limit and cache) about one proof. The proof never reaches a log. */
export async function checkProof(proof: string, links: SonosLinks, clientKey: string): Promise<ProofCheckName> {
  if (links.subscription === undefined) return ProofCheck.Unavailable;
  try {
    const answer = await links.subscription(proof, clientKey);
    if (answer.kind !== SubscriptionAnswerKind.Status && answer.kind !== SubscriptionAnswerKind.NoProof) console.warn(`${SUBSCRIPTION_LOG} answer=${answer.kind}`);
    return checkOfAnswer(answer.kind, answer.status?.state);
  } catch (error) {
    // no-fallback: a store check that throws is "cannot ask" (the caller keeps the last answer or refuses); logged by error name only
    console.warn(`${SUBSCRIPTION_LOG} error=${error instanceof Error ? error.name : typeof error}`);
    return ProofCheck.Unavailable;
  }
}

// Slice 439 (review 1, finding 10): the last real answer per home, kept for LAST_ANSWER_MS. A store error or rate limit gives this answer back instead of flipping a paying home to the message.
const lastAnswers = new Map<string, { readonly at: number; readonly proof: string; readonly subscribed: boolean }>();

function rememberAnswer(home: Home, proof: string, subscribed: boolean, now: number): void {
  lastAnswers.delete(home.homeHash);
  if (lastAnswers.size >= MAX_LAST_ANSWERS) lastAnswers.delete(lastAnswers.keys().next().value as string);
  lastAnswers.set(home.homeHash, { at: now, proof, subscribed });
}

/** The last real answer for this home and this proof, while it is fresh; null when there is none. Never asks the store. */
function lastAnswer(home: Home, now: number): boolean | null {
  const held = lastAnswers.get(home.homeHash);
  return held !== undefined && held.proof === home.proof && now - held.at < LAST_ANSWER_MS ? held.subscribed : null;
}

/** What a browse knows about the home's subscription without asking the store (the poll for a changed catalogue uses it): the last real answer, else false. */
export function subscriptionKnown(home: Home, links: SonosLinks): boolean {
  return lastAnswer(home, clock(links)) ?? false;
}

/**
 * Whether the home may play the paid books: true while its proof is active (or in grace) and, with the paid-books switch off, always (review 3, finding 2: the app's own paid-file route is free then, so a linked home's
 * paid tracks play like everyone else's, linked with a proof or not); false for no source, no proof, or a lapsed or unknown subscription. A store that cannot be
 * asked, an error or a rate limit keeps the last real answer for LAST_ANSWER_MS, and answers null (unknown) when there is none (review 2, finding 5: the media route then asks the player to retry). The proof never reaches a log.
 */
export async function subscriptionAnswer(home: Home, links: SonosLinks, clientKey: string): Promise<boolean | null> {
  if (links.subscription === undefined) return false;
  const proof = home.proof ?? "";
  const check = await checkProof(proof, links, clientKey); // an empty proof still reads the switch first: Off answers before the proof is looked at
  if (check === ProofCheck.Off) return true;
  if (proof === "") return false;
  if (check === ProofCheck.Active || check === ProofCheck.Inactive) {
    rememberAnswer(home, proof, check === ProofCheck.Active, clock(links));
    return check === ProofCheck.Active;
  }
  return lastAnswer(home, clock(links));
}

/**
 * Review 3, finding 5: what the media route knows for a Range or HEAD request (a continuation of a play already started): the kept answer while it is fresh, so the store-call limit is spent once
 * per play and cannot cut a book mid-way; with none kept, or for the first request, the store is asked as `subscriptionAnswer` does.
 */
export async function subscriptionForPlayback(home: Home, links: SonosLinks, clientKey: string, continuation: boolean): Promise<boolean | null> {
  const kept = continuation ? lastAnswer(home, clock(links)) : null;
  return kept ?? subscriptionAnswer(home, links, clientKey);
}

/** True while the home's subscription is active; an unknown answer counts as false (a browse then lists the paid books as "In the Dino app"). */
export async function subscriptionActive(home: Home, links: SonosLinks, clientKey: string): Promise<boolean> {
  return (await subscriptionAnswer(home, links, clientKey)) ?? false;
}

/** Test seam: forgets every kept answer. */
export function resetSubscriptionAnswers(): void {
  lastAnswers.clear();
}

export function isLinkAction(action: string): boolean {
  return action === SonosAction.GetAppLink || action === SonosAction.GetDeviceAuthToken || action === SonosAction.RefreshAuthToken;
}

export function isJsonRequest(request: Request): boolean {
  return (request.headers.get("Content-Type") ?? "").toLowerCase().startsWith(JSON_CONTENT_TYPE);
}

function clock(links: SonosLinks): number {
  return (links.now ?? Date.now)();
}

async function sha256Hex(domain: string, value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(`${domain}:${value}`));
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

function randomHex(bytes: number): string {
  return [...crypto.getRandomValues(new Uint8Array(bytes))].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

const hashHome = (householdId: string): Promise<string> => sha256Hex("home", householdId);
const hashToken = (token: string): Promise<string> => sha256Hex("token", token);
const hashKey = (key: string): Promise<string> => sha256Hex("key", key);
const hashCode = (code: string): Promise<string> => sha256Hex("code", code);
const hashProof = (proof: string): Promise<string> => sha256Hex("proof", proof);

/** True for the one kind of URL Sonos hands us to come back to: its own app scheme (`sonos:`, `sonos-xyz:`) or an https URL on sonos.com. */
export function isSonosCallback(text: string | null): text is string {
  if (text === null || text.length === 0 || text.length > MAX_CALLBACK_LENGTH) return false;
  let url: URL;
  try {
    url = new URL(text);
  } catch {
    // no-fallback: a callback that does not parse is refused, never opened
    return false;
  }
  if (url.protocol === "https:") return url.hostname === SONOS_HOST || url.hostname.endsWith(`.${SONOS_HOST}`);
  return SONOS_SCHEME.test(url.protocol);
}

/** The callback with the link code added, the way Sonos wants the app to return (`code` and the `state` it already carries). */
export function callbackWithCode(callback: string, code: string): string {
  const url = new URL(callback);
  url.searchParams.set("code", code);
  return url.toString();
}

function validHousehold(householdId: string | null): householdId is string {
  return householdId !== null && householdId.length > 0 && householdId.length <= MAX_HOUSEHOLD_LENGTH;
}

/** A D1 failure is one log line with the error's name only (never the SQL, an id or a token) and a service-unavailable fault. */
export async function guarded<T>(work: () => Promise<T>): Promise<T> {
  try {
    return await work();
  } catch (error) {
    if (error instanceof SoapFault) throw error;
    console.warn(`${D1_ERROR_LOG} error=${error instanceof Error ? error.name : typeof error}`);
    throw new SoapFault(FaultCode.Unavailable, FaultMessage.Unavailable);
  }
}

function requireDb(links: SonosLinks): D1Database {
  if (links.db === undefined) throw new SoapFault(FaultCode.Unavailable, FaultMessage.Unavailable);
  return links.db;
}

/** Asks for a link: stores the pending row and returns the link code. The stored home (token, key, language) is not touched; only a confirmed, unexpired code replaces it (issueTokens). */
export async function startLink(db: D1Database, householdId: string, callback: string, now: number): Promise<string> {
  const code = randomHex(LINK_CODE_BYTES);
  await purgeExpired(db, now);
  const homeHash = await hashHome(householdId);
  // At most MAX_PENDING_PER_HOME pending rows per home: the insert, then the oldest rows beyond the cap go (one batch, so a loop of getAppLink calls cannot grow the table).
  await db.batch([
    db
      .prepare(`INSERT INTO ${PENDING_TABLE} (code_hash, home_hash, callback, story_language, proof_hash, expires_at) VALUES (?1, ?2, ?3, NULL, NULL, ?4)`)
      .bind(await hashCode(code), homeHash, callback, now + LINK_LIFETIME_MS),
    db
      .prepare(`DELETE FROM ${PENDING_TABLE} WHERE home_hash = ?1 AND code_hash NOT IN (SELECT code_hash FROM ${PENDING_TABLE} WHERE home_hash = ?1 ORDER BY expires_at DESC LIMIT ?2)`)
      .bind(homeHash, MAX_PENDING_PER_HOME),
  ]);
  return code;
}

export interface ConfirmBody {
  readonly code: string;
  readonly language: string;
  readonly proof: string | null;
}

/** The Dino app's answer: saves the language (and the subscription proof, when it has one) on the pending row; returns the Sonos URL to open and whether the proof was kept (review 3, finding 1: a database without the `proof` column keeps none), or null for an unknown or expired code. */
export async function confirmLink(db: D1Database, body: ConfirmBody, now: number): Promise<{ readonly callback: string; readonly proofStored: boolean } | null> {
  await purgeExpired(db, now);
  const proof = body.proof === null ? null : await hashProof(body.proof);
  const codeHash = await hashCode(body.code);
  let row: { callback: string } | null;
  let proofStored = body.proof !== null;
  try {
    row = await db
      .prepare(`UPDATE ${PENDING_TABLE} SET story_language = ?2, proof_hash = ?3, proof = ?5 WHERE code_hash = ?1 AND expires_at >= ?4 RETURNING callback`)
      .bind(codeHash, body.language, proof, now, body.proof)
      .first<{ callback: string }>();
  } catch (error) {
    if (!isMissingColumn(error)) throw error;
    // Review 2, finding 1: a database without the `proof` column still links; the row is written without the proof, so the home links as one without a subscription.
    console.warn(`${D1_ERROR_LOG} fallback=no-proof-column`);
    proofStored = false;
    row = await db
      .prepare(`UPDATE ${PENDING_TABLE} SET story_language = ?2, proof_hash = ?3 WHERE code_hash = ?1 AND expires_at >= ?4 RETURNING callback`)
      .bind(codeHash, body.language, proof, now)
      .first<{ callback: string }>();
  }
  return row === null ? null : { callback: callbackWithCode(row.callback, body.code), proofStored };
}

interface PendingRow {
  readonly home_hash: string;
  readonly story_language: string | null;
  readonly proof_hash: string | null;
}

export interface IssuedTokens {
  readonly authToken: string;
  readonly privateKey: string;
}

/** getDeviceAuthToken: success only once the app confirmed; a wrong home or unknown code is a permanent failure, an unconfirmed one asks Sonos to retry. */
export async function issueTokens(db: D1Database, householdId: string, linkCode: string, now: number): Promise<IssuedTokens> {
  await purgeExpired(db, now);
  const codeHash = await hashCode(linkCode);
  const homeHash = await hashHome(householdId);
  const pending = await db.prepare(`SELECT home_hash, story_language, proof_hash FROM ${PENDING_TABLE} WHERE code_hash = ?1 AND expires_at >= ?2`).bind(codeHash, now).first<PendingRow>();
  if (pending === null || pending.home_hash !== homeHash) throw notLinked(FaultCode.NotLinkedFailure, "NOT_LINKED_FAILURE", 6);
  if (pending.story_language === null) throw notLinked(FaultCode.NotLinkedRetry, "NOT_LINKED_RETRY", 5);
  // One token pair per link, and the link is never lost: the home row is written from the pending row and the pending row deleted in ONE batch (a D1 batch is one
  // transaction, so both happen or neither). Only the call whose insert changed a row issues tokens; a second call for the same code finds no pending row and fails.
  const tokens = { authToken: randomHex(TOKEN_BYTES), privateKey: randomHex(TOKEN_BYTES) };
  const results = await batchLink(db, { codeHash, homeHash }, await hashToken(tokens.authToken), await hashKey(tokens.privateKey), now);
  const written = results[results.length - 2];
  if (written === undefined || written.meta.changes === 0) throw notLinked(FaultCode.NotLinkedFailure, "NOT_LINKED_FAILURE", 6);
  return tokens;
}

/** Expired pending links and homes idle for 400 days go; every link call runs this first, so nothing outlives its time by more than the gap to the next link call. */
export async function purgeExpired(db: D1Database, now: number): Promise<void> {
  await db.batch([
    db.prepare(`DELETE FROM ${PENDING_TABLE} WHERE expires_at < ?1`).bind(now),
    db.prepare(`DELETE FROM ${HOME_TABLE} WHERE last_seen < ?1`).bind(now - IDLE_HOME_MS),
  ]);
}

function notLinked(code: typeof FaultCode.NotLinkedFailure | typeof FaultCode.NotLinkedRetry, info: string, sonosError: number): SoapFault {
  return new SoapFault(code, FaultMessage.NotLinked, notLinkedDetail(info, sonosError));
}

function isMissingColumn(error: unknown): boolean {
  return error instanceof Error && MISSING_COLUMN.test(error.message);
}

type LinkKey = { readonly codeHash: string; readonly homeHash: string };

// Review 2, finding 1: the batch writes the `proof` column; a database without it (the hand-run ALTER not done yet) gets the same batch without the column, so the link still works (with no proof).
async function batchLink(db: D1Database, key: LinkKey, tokenHash: string, keyHash: string, now: number): Promise<D1Result[]> {
  try {
    return await db.batch(linkStatements(db, key, tokenHash, keyHash, now, true));
  } catch (error) {
    if (!isMissingColumn(error)) throw error;
    console.warn(`${D1_ERROR_LOG} fallback=no-proof-column`);
    return db.batch(linkStatements(db, key, tokenHash, keyHash, now, false));
  }
}

// One home per subscription: a new home with the same proof replaces the old rows (Slice 415 decision 5). The home row is written once per household (a relink keeps `created_at`).
// A relink without a proof clears the stored proof and its hash (review 2, finding 4: the app forgets it too, so the two sides agree).
// The statements read the confirmed pending row themselves, so a call that finds none changes nothing; the upsert is always second to last (issueTokens reads its `changes`).
function linkStatements(db: D1Database, key: LinkKey, tokenHash: string, keyHash: string, now: number, withProof: boolean): D1PreparedStatement[] {
  const confirmed = `code_hash = ?1 AND home_hash = ?2 AND story_language IS NOT NULL AND expires_at >= ?3`;
  const replace = db
    .prepare(`DELETE FROM ${HOME_TABLE} WHERE home_hash <> ?2 AND proof_hash = (SELECT proof_hash FROM ${PENDING_TABLE} WHERE ${confirmed})`)
    .bind(key.codeHash, key.homeHash, now);
  const columns = withProof ? "proof_hash, proof" : "proof_hash";
  const updates = withProof ? "proof_hash = excluded.proof_hash, proof = excluded.proof" : "proof_hash = excluded.proof_hash";
  const upsert = db
    .prepare(
      `INSERT INTO ${HOME_TABLE} (home_hash, story_language, token_hash, key_hash, ${columns}, created_at, last_seen) ` +
        `SELECT home_hash, story_language, ?4, ?5, ${columns}, ?3, ?3 FROM ${PENDING_TABLE} WHERE ${confirmed} ` +
        `ON CONFLICT(home_hash) DO UPDATE SET story_language = excluded.story_language, token_hash = excluded.token_hash, key_hash = excluded.key_hash, ${updates}, last_seen = excluded.last_seen`,
    )
    .bind(key.codeHash, key.homeHash, now, tokenHash, keyHash);
  const consume = db.prepare(`DELETE FROM ${PENDING_TABLE} WHERE ${confirmed}`).bind(key.codeHash, key.homeHash, now);
  return [replace, upsert, consume];
}

/** The home a request's login token belongs to, or null when the token is not (or no longer) one. Notes a use at most once a day. */
export async function homeFor(db: D1Database, login: LoginToken, now: number): Promise<Home | null> {
  const homeHash = await hashHome(login.householdId);
  const row = await homeRow(db, "home_hash = ?1 AND token_hash = ?2", [homeHash, await hashToken(login.token)]);
  if (row === null) return null;
  if (now - row.last_seen >= DAY_MS) await db.prepare(`UPDATE ${HOME_TABLE} SET last_seen = ?2 WHERE home_hash = ?1`).bind(homeHash, now).run();
  return { homeHash, storyLanguage: row.story_language, proof: row.proof };
}

/** Slice 439: the home whose Sonos auth token this is (the media file route has no household id, only the token the player sends as `Authorization: Bearer`); null when it is not (or no longer) one. */
export async function homeForToken(db: D1Database, token: string): Promise<Home | null> {
  const row = await homeRow(db, "token_hash = ?1", [await hashToken(token)]);
  return row === null ? null : { homeHash: row.home_hash, storyLanguage: row.story_language, proof: row.proof };
}

interface HomeRow {
  readonly home_hash: string;
  readonly story_language: string;
  readonly last_seen: number;
  readonly proof: string | null;
}

const MISSING_COLUMN = /no such column|has no column named/i; // a SELECT or UPDATE says the first, an INSERT the second

// Slice 439 (review 1, finding 4): the `proof` column is added by hand (docs/DEPLOYING.md), so a Worker deployed before that step still reads the home: without the column the home has no proof (its paid books
// play the message) instead of every call faulting. The fallback is reported by one log line, error name only.
async function homeRow(db: D1Database, condition: string, bindings: readonly string[]): Promise<HomeRow | null> {
  try {
    return await db.prepare(`SELECT home_hash, story_language, last_seen, proof FROM ${HOME_TABLE} WHERE ${condition}`).bind(...bindings).first<HomeRow>();
  } catch (error) {
    if (!isMissingColumn(error)) throw error;
    console.warn(`${D1_ERROR_LOG} fallback=no-proof-column`);
    const row = await db.prepare(`SELECT home_hash, story_language, last_seen FROM ${HOME_TABLE} WHERE ${condition}`).bind(...bindings).first<Omit<HomeRow, "proof">>();
    return row === null ? null : { ...row, proof: null };
  }
}

/** refreshAuthToken: new tokens for the home whose current token and key match; null when they do not. */
export async function refreshTokens(db: D1Database, login: LoginToken, now: number): Promise<IssuedTokens | null> {
  await purgeExpired(db, now);
  const homeHash = await hashHome(login.householdId);
  const oldToken = await hashToken(login.token);
  const tokens = { authToken: randomHex(TOKEN_BYTES), privateKey: randomHex(TOKEN_BYTES) };
  const result = await db
    .prepare(`UPDATE ${HOME_TABLE} SET token_hash = ?4, key_hash = ?5, last_seen = ?6 WHERE home_hash = ?1 AND token_hash = ?2 AND key_hash = ?3`)
    .bind(homeHash, oldToken, await hashKey(login.key), await hashToken(tokens.authToken), await hashKey(tokens.privateKey), now)
    .run();
  return result.meta.changes === 1 ? tokens : null;
}

/** Saves the story language the grown-up picked in Sonos (`setlang:<code>`); false when the home is gone (unlinked meanwhile), so nothing was saved. */
export async function saveStoryLanguage(db: D1Database, home: Home, language: string): Promise<boolean> {
  const result = await db.prepare(`UPDATE ${HOME_TABLE} SET story_language = ?2 WHERE home_hash = ?1`).bind(home.homeHash, language).run();
  return result.meta.changes > 0;
}

/** Deletes the homes linked with this subscription proof, and any confirmed pending link carrying it; returns how many homes went. */
export async function unlinkProof(db: D1Database, proof: string): Promise<number> {
  const proofHash = await hashProof(proof);
  const [homes] = await db.batch([
    db.prepare(`DELETE FROM ${HOME_TABLE} WHERE proof_hash = ?1`).bind(proofHash),
    db.prepare(`DELETE FROM ${PENDING_TABLE} WHERE proof_hash = ?1`).bind(proofHash),
  ]);
  return homes?.meta.changes ?? 0;
}

/** True when some home is linked with this subscription proof. */
export async function holdsProof(db: D1Database, proof: string): Promise<boolean> {
  return (await db.prepare(`SELECT 1 AS held FROM ${HOME_TABLE} WHERE proof_hash = ?1 LIMIT 1`).bind(await hashProof(proof)).first()) !== null;
}

/**
 * Slice 439: the subscription renewed or changed, so the app sends the new proof with the old one. The home holding the old proof gets the new proof (and its hash). It changes nothing and deletes no
 * home when no home holds the old proof, or when another home already holds the new one (one subscription, one home: that is a relink's job, never a renew's). Returns how many homes were updated.
 * The caller has checked the new proof with the store first (renewCall).
 */
export async function renewProof(db: D1Database, oldProof: string, newProof: string): Promise<number> {
  const oldHash = await hashProof(oldProof);
  const newHash = await hashProof(newProof);
  const free = `?1 = ?2 OR NOT EXISTS (SELECT 1 FROM ${HOME_TABLE} WHERE proof_hash = ?2)`;
  const result = await db.prepare(`UPDATE ${HOME_TABLE} SET proof = ?3, proof_hash = ?2 WHERE proof_hash = ?1 AND (${free})`).bind(oldHash, newHash, newProof).run();
  return result.meta.changes;
}

/** The home of a browse request, or the not-logged-in fault Sonos answers with its re-authorize prompt (Client.AuthTokenExpired). */
export async function requireHome(request: SonosRequest, links: SonosLinks): Promise<Home> {
  const db = requireDb(links);
  const home = request.login === null ? null : await guarded(() => homeFor(db, request.login as LoginToken, clock(links)));
  if (home === null) throw new SoapFault(FaultCode.AuthTokenExpired, FaultMessage.NotLoggedIn);
  return home;
}

function tokensXml(tokens: IssuedTokens): string {
  return [tag("authToken", tokens.authToken), tag("privateKey", tokens.privateKey)].join("");
}

// The page Sonos opens: the Dino site's link page with the link code. It is reached by its file path, which needs no Worker route (site/dino/sonos-link.html).
function linkPageUrl(origin: string, code: string): string {
  return `${origin}${LINK_PAGE_PATH}?code=${code}`;
}

function appLinkXml(origin: string, code: string): string {
  const url = escapeXml(linkPageUrl(origin, code));
  const deviceLink = `<deviceLink><regUrl>${url}</regUrl>${tag("linkCode", code)}${tag("showLinkCode", false)}</deviceLink>`;
  return `<authorizeAccount><appUrl>${url}</appUrl>${tag("appUrlStringId", APP_URL_STRING_ID)}${deviceLink}</authorizeAccount>`;
}

async function getAppLink(request: SonosRequest, links: SonosLinks, origin: string): Promise<Response> {
  if (!validHousehold(request.householdId) || !isSonosCallback(request.callbackPath)) throw new SoapFault(FaultCode.Unknown, FaultMessage.BadLink);
  const db = requireDb(links);
  const householdId = request.householdId;
  const callback = request.callbackPath;
  const code = await guarded(() => startLink(db, householdId, callback, clock(links)));
  return resultResponse(SonosAction.GetAppLink, appLinkXml(origin, code));
}

async function getDeviceAuthToken(request: SonosRequest, links: SonosLinks): Promise<Response> {
  if (!validHousehold(request.householdId) || request.linkCode === null || !LINK_CODE_PATTERN.test(request.linkCode)) throw notLinked(FaultCode.NotLinkedFailure, "NOT_LINKED_FAILURE", 6);
  const db = requireDb(links);
  const householdId = request.householdId;
  const linkCode = request.linkCode;
  const tokens = await guarded(() => issueTokens(db, householdId, linkCode, clock(links)));
  return resultResponse(SonosAction.GetDeviceAuthToken, tokensXml(tokens));
}

async function refreshAuthToken(request: SonosRequest, links: SonosLinks): Promise<Response> {
  const db = requireDb(links);
  const login = request.login;
  const tokens = login === null ? null : await guarded(() => refreshTokens(db, login, clock(links)));
  if (tokens === null) throw new SoapFault(FaultCode.AuthTokenExpired, FaultMessage.NotLoggedIn);
  return resultResponse(SonosAction.RefreshAuthToken, tokensXml(tokens));
}

/** The three SMAPI link calls (the caller checked `isLinkAction`). */
export function answerLinkAction(request: SonosRequest, links: SonosLinks, origin: string): Promise<Response> {
  if (request.action === SonosAction.GetAppLink) return getAppLink(request, links, origin);
  if (request.action === SonosAction.GetDeviceAuthToken) return getDeviceAuthToken(request, links);
  return refreshAuthToken(request, links);
}

function jsonResponse(status: number, body: Readonly<Record<string, unknown>>): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": JSON_CONTENT_TYPE, "Cache-Control": "no-store" } });
}

function badRequest(): Response {
  return jsonResponse(400, { error: "bad_request" });
}

function stringField(body: unknown, name: string): string | null {
  const value = typeof body === "object" && body !== null ? (body as Record<string, unknown>)[name] : undefined;
  return typeof value === "string" ? value : null;
}

function confirmBodyOf(body: unknown): ConfirmBody | null {
  const code = stringField(body, "code");
  const language = stringField(body, "language");
  const proof = stringField(body, "proof");
  if (code === null || !LINK_CODE_PATTERN.test(code) || language === null || !STORY_LANGUAGES.includes(language)) return null;
  if (proof !== null && !PROOF_PATTERN.test(proof)) return null;
  return { code, language, proof };
}

/** The refusal for a proof the store did not confirm as active: 403 for a lapsed or unknown one, 429 over the client's store-call limit, 503 when the store cannot be asked; null for an active one. */
function refusalOf(check: ProofCheckName): Response | null {
  if (check === ProofCheck.Active) return null;
  if (check === ProofCheck.Inactive) return jsonResponse(403, { error: INACTIVE_ERROR });
  return check === ProofCheck.Limited ? jsonResponse(429, { error: "rate_limited" }) : jsonResponse(503, { error: "unavailable" });
}

async function confirmCall(body: unknown, db: D1Database, links: SonosLinks, request: Request): Promise<Response> {
  const sent = confirmBodyOf(body);
  if (sent === null) return badRequest();
  if (sent.proof !== null && !isSubscriptionProof(sent.proof)) return jsonResponse(400, { error: NOT_SUBSCRIPTION_ERROR });
  // Review 1, finding 3: the store confirms the proof before it is kept, so a made-up proof never becomes a stored one.
  // Review 2, finding 2: with the paid-books switch off the store is not asked and the proof is not stored: the home links with the free books, exactly as one without a subscription.
  const check = sent.proof === null ? ProofCheck.Active : await checkProof(sent.proof, links, clientKeyOf(request));
  const refused = check === ProofCheck.Off ? null : refusalOf(check);
  if (refused !== null) return refused;
  const confirm = check === ProofCheck.Off ? { ...sent, proof: null } : sent;
  const linked = await confirmLink(db, confirm, clock(links));
  return linked === null ? jsonResponse(404, { error: "unknown_link" }) : jsonResponse(200, { callback: linked.callback, proofStored: linked.proofStored });
}

async function unlinkCall(body: unknown, db: D1Database, now: number): Promise<Response> {
  const proof = stringField(body, "proof");
  if (proof === null || !PROOF_PATTERN.test(proof)) return badRequest();
  await purgeExpired(db, now);
  return jsonResponse(200, { removed: await unlinkProof(db, proof) });
}

async function renewCall(body: unknown, db: D1Database, links: SonosLinks, request: Request): Promise<Response> {
  const oldProof = stringField(body, "oldProof");
  const newProof = stringField(body, "newProof");
  if (oldProof === null || newProof === null || !PROOF_PATTERN.test(oldProof) || !PROOF_PATTERN.test(newProof)) return badRequest();
  if (!isSubscriptionProof(oldProof) || !isSubscriptionProof(newProof)) return jsonResponse(400, { error: NOT_SUBSCRIPTION_ERROR });
  await purgeExpired(db, clock(links));
  if (!(await holdsProof(db, oldProof))) return jsonResponse(404, { error: NO_HOME_ERROR });
  // Review 1, finding 1: the stored proof is replaced only by one the store confirms as active; anything else refuses and changes nothing.
  // Review 2, finding 2: with the paid-books switch off there is nothing to swap; the app reads this status as "nothing to do" (never a 503 it would retry).
  const check = await checkProof(newProof, links, clientKeyOf(request));
  if (check === ProofCheck.Off) return jsonResponse(200, { renewed: 0, status: PAID_BOOKS_OFF });
  const unconfirmed = refusalOf(check);
  if (unconfirmed !== null) return unconfirmed;
  const renewed = await renewProof(db, oldProof, newProof);
  return renewed === 0 ? jsonResponse(409, { error: PROOF_TAKEN_ERROR }) : jsonResponse(200, { renewed });
}

async function parseJson(request: Request): Promise<unknown> {
  const text = await readBoundedText(request);
  if (text === null) return null;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    // no-fallback: a body that is not JSON is a bad request, answered below
    return null;
  }
}

/** The Dino app's calls (`confirm`, `unlink`): JSON in, JSON out; a failure of D1 is 503 and logged by name only. */
export async function handleLinkApi(request: Request, links: SonosLinks): Promise<Response> {
  const body = await parseJson(request);
  const op = stringField(body, "op");
  if (op !== LinkOp.Confirm && op !== LinkOp.Unlink && op !== LinkOp.Renew) return badRequest();
  if (links.db === undefined) return jsonResponse(503, { error: "unavailable" });
  try {
    if (op === LinkOp.Confirm) return await confirmCall(body, links.db, links, request);
    return op === LinkOp.Renew ? await renewCall(body, links.db, links, request) : await unlinkCall(body, links.db, clock(links));
  } catch (error) {
    console.warn(`${D1_ERROR_LOG} error=${error instanceof Error ? error.name : typeof error}`);
    return jsonResponse(503, { error: "unavailable" });
  }
}

interface StoryLanguageWords {
  /** "Story language: {name}" */
  readonly row: string;
  readonly tap: string;
  readonly current: string;
}

// The words of the story-language row and its picker, in the 15 app languages. English and Farsi are the mock's (docs/design/sonos/, SET-Home, SET-Pick, SET-FA-Home); the rest are translated from them.
const WORDS: Readonly<Record<string, StoryLanguageWords>> = {
  en: { row: "Story language: {name}", tap: "Tap to change · saved for your home", current: "current" },
  fa: { row: "زبان قصه‌ها: {name}", tap: "برای تغییر بزنید · برای خانه‌ی شما ذخیره می‌شود", current: "فعلی" },
  ar: { row: "لغة القصص: {name}", tap: "اضغط للتغيير · محفوظة لمنزلك", current: "الحالية" },
  da: { row: "Historiernes sprog: {name}", tap: "Tryk for at ændre · gemt for dit hjem", current: "nuværende" },
  de: { row: "Sprache der Geschichten: {name}", tap: "Zum Ändern tippen · für Ihr Zuhause gespeichert", current: "aktuell" },
  es: { row: "Idioma de los cuentos: {name}", tap: "Toca para cambiar · guardado para tu hogar", current: "actual" },
  fi: { row: "Tarinoiden kieli: {name}", tap: "Vaihda napauttamalla · tallennettu kotiisi", current: "nykyinen" },
  fr: { row: "Langue des histoires : {name}", tap: "Touchez pour changer · enregistrée pour votre foyer", current: "actuelle" },
  ja: { row: "お話の言語：{name}", tap: "タップして変更 · このホームに保存されます", current: "現在" },
  nb: { row: "Historienes språk: {name}", tap: "Trykk for å endre · lagret for hjemmet ditt", current: "gjeldende" },
  nl: { row: "Taal van de verhalen: {name}", tap: "Tik om te wijzigen · bewaard voor jouw huis", current: "huidig" },
  pt: { row: "Idioma das histórias: {name}", tap: "Toque para mudar · guardado para a sua casa", current: "atual" },
  ru: { row: "Язык историй: {name}", tap: "Нажмите, чтобы изменить · сохранено для вашего дома", current: "текущий" },
  sv: { row: "Berättelsernas språk: {name}", tap: "Tryck för att ändra · sparat för ditt hem", current: "nuvarande" },
  zh: { row: "故事语言：{name}", tap: "点按更改 · 已为你的家保存", current: "当前" },
};

// The mock names these two in English as "Farsi" and "Norwegian" (SET-Pick); the platform's language data says "Persian" and "Norwegian Bokmål".
const MOCK_ENGLISH_NAMES: Readonly<Record<string, string>> = { fa: "Farsi", nb: "Norwegian" };
const ENGLISH = "en";

/** A language's name in the phone's language (`label`), with the mock's English names for Farsi and Norwegian; `platformName` is the platform's answer. */
export function languageNameFor(code: string, label: string, platformName: string): string {
  return (label === ENGLISH ? MOCK_ENGLISH_NAMES[code] : undefined) ?? platformName;
}

function wordsFor(label: string): StoryLanguageWords {
  return WORDS[label] ?? (WORDS.en as StoryLanguageWords);
}

/** "Story language: English" in the phone's language (`label`), with the language named in that language. */
export function storyLanguageTitle(label: string, languageName: string): string {
  return wordsFor(label).row.replace("{name}", languageName);
}

/** "Tap to change · saved for your home". */
export function storyLanguageTap(label: string): string {
  return wordsFor(label).tap;
}

/** The word under the saved language in the picker ("English · current"). */
export function currentWord(label: string): string {
  return wordsFor(label).current;
}
