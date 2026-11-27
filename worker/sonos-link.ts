// Slice 438: every Sonos home links once through the Dino app (Sonos AppLink) and saves its story language. This file holds the whole link:
// the three SMAPI calls (getAppLink, getDeviceAuthToken, refreshAuthToken), the JSON calls the Dino app makes (confirm, unlink), the D1 rows
// (worker/schema/sonos-homes.sql) and the words of the "Story language" row. Sources: docs/plan/slices/438-dino-sonos-link-story-language.md (B1).
// A home is kept as hashes only: SHA-256 of the household id, of the authToken and privateKey given to Sonos and of the subscription proof key;
// the household id, the tokens and the proof never reach a log. The page Sonos opens is site/dino/sonos-link.html.

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
}

/** A linked home as a browse sees it. */
export interface Home {
  readonly homeHash: string;
  readonly storyLanguage: string;
}

/** The JSON operations the Dino app sends to `POST /sonos` with `Content-Type: application/json`. */
export const LinkOp = { Confirm: "confirm", Unlink: "unlink" } as const;

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

/** The Dino app's answer: saves the language (and the subscription proof, when it has one) on the pending row; returns the Sonos URL to open, or null for an unknown or expired code. */
export async function confirmLink(db: D1Database, body: ConfirmBody, now: number): Promise<string | null> {
  await purgeExpired(db, now);
  const proof = body.proof === null ? null : await hashProof(body.proof);
  const row = await db
    .prepare(`UPDATE ${PENDING_TABLE} SET story_language = ?2, proof_hash = ?3 WHERE code_hash = ?1 AND expires_at >= ?4 RETURNING callback`)
    .bind(await hashCode(body.code), body.language, proof, now)
    .first<{ callback: string }>();
  return row === null ? null : callbackWithCode(row.callback, body.code);
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
  const results = await db.batch(linkStatements(db, { codeHash, homeHash }, await hashToken(tokens.authToken), await hashKey(tokens.privateKey), now));
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

// One home per subscription: a new home with the same proof replaces the old rows (Slice 415 decision 5). The home row is written once per household (a relink keeps `created_at`).
// The statements read the confirmed pending row themselves, so a call that finds none changes nothing; the upsert is always second to last (issueTokens reads its `changes`).
function linkStatements(db: D1Database, key: { readonly codeHash: string; readonly homeHash: string }, tokenHash: string, keyHash: string, now: number): D1PreparedStatement[] {
  const confirmed = `code_hash = ?1 AND home_hash = ?2 AND story_language IS NOT NULL AND expires_at >= ?3`;
  const replace = db
    .prepare(`DELETE FROM ${HOME_TABLE} WHERE home_hash <> ?2 AND proof_hash = (SELECT proof_hash FROM ${PENDING_TABLE} WHERE ${confirmed})`)
    .bind(key.codeHash, key.homeHash, now);
  const upsert = db
    .prepare(
      `INSERT INTO ${HOME_TABLE} (home_hash, story_language, token_hash, key_hash, proof_hash, created_at, last_seen) ` +
        `SELECT home_hash, story_language, ?4, ?5, proof_hash, ?3, ?3 FROM ${PENDING_TABLE} WHERE ${confirmed} ` +
        `ON CONFLICT(home_hash) DO UPDATE SET story_language = excluded.story_language, token_hash = excluded.token_hash, key_hash = excluded.key_hash, proof_hash = COALESCE(excluded.proof_hash, ${HOME_TABLE}.proof_hash), last_seen = excluded.last_seen`,
    )
    .bind(key.codeHash, key.homeHash, now, tokenHash, keyHash);
  const consume = db.prepare(`DELETE FROM ${PENDING_TABLE} WHERE ${confirmed}`).bind(key.codeHash, key.homeHash, now);
  return [replace, upsert, consume];
}

/** The home a request's login token belongs to, or null when the token is not (or no longer) one. Notes a use at most once a day. */
export async function homeFor(db: D1Database, login: LoginToken, now: number): Promise<Home | null> {
  const homeHash = await hashHome(login.householdId);
  const row = await db
    .prepare(`SELECT story_language, last_seen FROM ${HOME_TABLE} WHERE home_hash = ?1 AND token_hash = ?2`)
    .bind(homeHash, await hashToken(login.token))
    .first<{ story_language: string; last_seen: number }>();
  if (row === null) return null;
  if (now - row.last_seen >= DAY_MS) await db.prepare(`UPDATE ${HOME_TABLE} SET last_seen = ?2 WHERE home_hash = ?1`).bind(homeHash, now).run();
  return { homeHash, storyLanguage: row.story_language };
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

async function confirmCall(body: unknown, db: D1Database, now: number): Promise<Response> {
  const confirm = confirmBodyOf(body);
  if (confirm === null) return badRequest();
  const callback = await confirmLink(db, confirm, now);
  return callback === null ? jsonResponse(404, { error: "unknown_link" }) : jsonResponse(200, { callback });
}

async function unlinkCall(body: unknown, db: D1Database, now: number): Promise<Response> {
  const proof = stringField(body, "proof");
  if (proof === null || !PROOF_PATTERN.test(proof)) return badRequest();
  await purgeExpired(db, now);
  return jsonResponse(200, { removed: await unlinkProof(db, proof) });
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
  if (op !== LinkOp.Confirm && op !== LinkOp.Unlink) return badRequest();
  if (links.db === undefined) return jsonResponse(503, { error: "unavailable" });
  try {
    return op === LinkOp.Confirm ? await confirmCall(body, links.db, clock(links)) : await unlinkCall(body, links.db, clock(links));
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
