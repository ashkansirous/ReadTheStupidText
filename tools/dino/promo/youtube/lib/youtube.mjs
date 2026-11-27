// YouTube Data API v3 calls for the uploader (context7 /websites/developers_google_youtube: videos.insert resumable
// upload, thumbnails.set, playlists, playlistItems.insert, OAuth refresh at https://oauth2.googleapis.com/token). Plain fetch,
// no client library. Nothing here runs without `upload.mjs --yes`; the dry run builds the request bodies only.

import { createReadStream, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { createHash, randomBytes } from 'node:crypto';
import { dirname, relative, resolve, isAbsolute } from 'node:path';
import { Readable } from 'node:stream';
import { fileURLToPath } from 'node:url';

const DEFAULT_SECRETS_FOLDER = 'D:/secrets/dinoreader';
const REPO_ROOT = fileURLToPath(new URL('../../../../../', import.meta.url));
const AUTHORIZE_TIMEOUT_MS = 5 * 60 * 1000;
const TOKEN_EXPIRY_MARGIN_MS = 5 * 60 * 1000;
const HTTP_UNAUTHORIZED = 401;
const HTTP_FORBIDDEN = 403;
const HTTP_BAD_REQUEST = 400;
const HTTP_RESUME_INCOMPLETE = 308;
const HTTP_NOT_FOUND = 404;
const RETRYABLE_STATUSES = new Set([500, 502, 503, 504]); // every other 4xx/5xx is permanent (resumable-upload docs)
const MAX_UPLOAD_RESUMES = 5;
const BACKOFF_BASE_MS = 1000;
const BACKOFF_CAP_MS = 60 * 1000;
const PLAYLIST_FETCH_LIMIT = 50;
export const SECRETS_FOLDER = process.env.DINO_SECRETS_FOLDER || DEFAULT_SECRETS_FOLDER;
export const CLIENT_FILE = `${SECRETS_FOLDER}/youtube-oauth-client.json`;
export const TOKEN_FILE = process.env.YOUTUBE_TOKEN_FILE || `${SECRETS_FOLDER}/youtube-token.json`;
export const SCOPES = ['https://www.googleapis.com/auth/youtube']; // covers upload, thumbnails and playlists; a second scope would only widen consent
const AUTH_URL = 'https://accounts.google.com/o/oauth2/v2/auth';
const TOKEN_URL = 'https://oauth2.googleapis.com/token';
const API = 'https://www.googleapis.com/youtube/v3';
const UPLOAD_API = 'https://www.googleapis.com/upload/youtube/v3';
const CATEGORY_EDUCATION = '27';
export const PRIVACY = { unlisted: 'unlisted', public: 'public', private: 'private' };

/** Pure: the videos.insert body. Always made for kids; the AI flag is upload.mjs --ai-disclosure, default false (policy note: false for these illustrated, non-realistic books). */
export function insertBody(meta, { privacy, containsSyntheticMedia }) {
  return {
    snippet: {
      title: meta.title, description: meta.description, tags: meta.tags, categoryId: CATEGORY_EDUCATION,
      defaultLanguage: meta.defaultLanguage, defaultAudioLanguage: meta.defaultAudioLanguage,
    },
    status: { privacyStatus: privacy, selfDeclaredMadeForKids: true, containsSyntheticMedia, embeddable: true },
  };
}

export function readClient(path = CLIENT_FILE) {
  const json = JSON.parse(readFileSync(path, 'utf8'));
  const client = json.installed ?? json.web;
  if (!client?.client_id) throw new Error(`${path} holds no OAuth client`);
  return client;
}

async function tokenRequest(client, params) {
  const response = await fetch(TOKEN_URL, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams({ client_id: client.client_id, client_secret: client.client_secret ?? '', ...params }) });
  const body = await response.json();
  if (!response.ok) throw new Error(`Token endpoint ${response.status}: ${body.error_description ?? body.error}`);
  return body;
}

/** A token source: `get()` returns a cached access token and refreshes it before it expires; `get(true)` forces a refresh (after a 401). */
export function accessToken(now = () => Date.now()) {
  let cached = null;
  return {
    async get(force = false) {
      if (!force && cached && cached.expiresAt - now() > TOKEN_EXPIRY_MARGIN_MS) return cached.token;
      const saved = JSON.parse(readFileSync(TOKEN_FILE, 'utf8'));
      const fresh = await tokenRequest(readClient(), { grant_type: 'refresh_token', refresh_token: saved.refresh_token });
      cached = { token: fresh.access_token, expiresAt: now() + (fresh.expires_in ?? 0) * 1000 };
      return cached.token;
    },
  };
}

/** The grant's token file must never sit inside the repo (a committed refresh token is a leaked channel). */
export function assertTokenOutsideRepo(file = TOKEN_FILE, repoRoot = REPO_ROOT) {
  const rel = relative(resolve(repoRoot), resolve(file));
  if (rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))) throw new Error(`${file} is inside the repo; point YOUTUBE_TOKEN_FILE at the secrets folder.`);
}

/** True for the daily-cap answers: 403 quotaExceeded, or uploadLimitExceeded (which YouTube sends as 400 or 403). The batch stops cleanly and a rerun resumes. */
export function isQuotaExceeded(error) {
  const body = error?.body ?? '';
  if (error?.status === HTTP_FORBIDDEN) return /quotaExceeded|uploadLimitExceeded/.test(body);
  return error?.status === HTTP_BAD_REQUEST && /uploadLimitExceeded/.test(body);
}

const base64Url = (buffer) => buffer.toString('base64url');

/** The owner's one-time grant: a loopback redirect with PKCE; saves the refresh token to TOKEN_FILE. Run only by the owner (a sign-in as the channel's account). */
export async function authorize(print = console.log) {
  assertTokenOutsideRepo();
  mkdirSync(dirname(TOKEN_FILE), { recursive: true }); // before the one-time code is spent
  const client = readClient();
  const verifier = base64Url(randomBytes(48));
  const state = base64Url(randomBytes(16));
  const { server, port, codePromise } = await listenForCode(state);
  const redirect = `http://127.0.0.1:${port}`;
  const url = `${AUTH_URL}?${new URLSearchParams({
    client_id: client.client_id, redirect_uri: redirect, response_type: 'code', scope: SCOPES.join(' '), access_type: 'offline', prompt: 'consent', state,
    code_challenge: base64Url(createHash('sha256').update(verifier).digest()), code_challenge_method: 'S256',
  })}`;
  print(`Open this address, sign in as the channel's account and choose the channel:\n${url}`);
  const code = await codePromise;
  server.close();
  const token = await tokenRequest(client, { grant_type: 'authorization_code', code, redirect_uri: redirect, code_verifier: verifier });
  if (!token.refresh_token) throw new Error('Google returned no refresh token.');
  writeFileSync(TOKEN_FILE, JSON.stringify({ refresh_token: token.refresh_token }, null, 2));
  print(`Saved the refresh token to ${TOKEN_FILE}. Commit it with the manage-secrets skill.`);
}

function listenForCode(expectedState) {
  return new Promise((resolveListen) => {
    let deliver;
    const codePromise = new Promise((resolveCode, rejectCode) => {
      deliver = { resolveCode, rejectCode };
      setTimeout(() => rejectCode(new Error('No sign-in within 5 minutes; run --authorize again.')), AUTHORIZE_TIMEOUT_MS).unref();
    });
    const server = createServer((request, response) => {
      const query = new URL(request.url, 'http://127.0.0.1').searchParams;
      response.end('You can close this tab and go back to the terminal.');
      if (query.get('state') !== expectedState) return;
      if (query.get('code')) deliver.resolveCode(query.get('code'));
      else deliver.rejectCode(new Error(`Authorisation refused: ${query.get('error')}`));
    });
    server.listen(0, '127.0.0.1', () => resolveListen({ server, port: server.address().port, codePromise }));
  });
}

/** One authorised call; a 401 refreshes the token once and retries (bodies here are strings or buffers, so they can be resent). */
async function call(auth, method, url, body, headers = {}) {
  let response = await fetch(url, { method, headers: { Authorization: `Bearer ${await auth.get()}`, ...headers }, body });
  if (response.status === HTTP_UNAUTHORIZED) response = await fetch(url, { method, headers: { Authorization: `Bearer ${await auth.get(true)}`, ...headers }, body });
  if (!response.ok) {
    const text = await response.text(); // the whole body: reason codes are matched against all of it, only the message is cut
    throw Object.assign(new Error(`${method} ${url.split('?')[0]} answered ${response.status}: ${text.slice(0, 400)}`), { status: response.status, body: text });
  }
  return response;
}

/** videos.insert, resumable, step one: a session with the metadata. Returns the session address. A 500/502/503/504 or a dropped connection retries with the same backoff as the upload (no video exists before the session does). */
export async function startSession(token, size, body, io = { sleep: pause }) {
  const request = () => call(token, 'POST', `${UPLOAD_API}/videos?uploadType=resumable&part=snippet,status`, JSON.stringify(body), {
    'Content-Type': 'application/json; charset=UTF-8', 'X-Upload-Content-Length': String(size), 'X-Upload-Content-Type': 'video/mp4',
  });
  const start = await withBackoff(request, io);
  const sessionUrl = start.headers.get('location');
  if (!sessionUrl) throw new Error('videos.insert gave no upload session address');
  return sessionUrl;
}

/** Runs `request`, retrying a retryable status or a network error up to MAX_UPLOAD_RESUMES times with backoffMs; anything else throws at once. */
async function withBackoff(request, io) {
  for (let retries = 0; ; retries += 1) {
    try {
      return await request();
    } catch (error) {
      const retryable = error.status === undefined || RETRYABLE_STATUSES.has(error.status);
      if (!retryable || retries >= MAX_UPLOAD_RESUMES) throw error;
      await io.sleep(backoffMs(retries));
    }
  }
}

async function uploadError(file, response) {
  const text = await response.text();
  return Object.assign(new Error(`Upload of ${file} answered ${response.status}: ${text.slice(0, 400)}`), { status: response.status, body: text });
}

/** Retry-After in seconds -> milliseconds (undefined when absent or not a number). */
export function retryAfterMs(response) {
  const seconds = Number(response.headers.get('retry-after'));
  return Number.isFinite(seconds) && response.headers.get('retry-after') !== null ? seconds * 1000 : undefined;
}

/** Exponential backoff (1 s, 2 s, 4 s ... capped), replaced by the server's Retry-After when it sent one. */
export function backoffMs(retries, retryAfter) {
  return retryAfter ?? Math.min(BACKOFF_BASE_MS * 2 ** retries, BACKOFF_CAP_MS);
}

async function putStep(auth, sessionUrl, file, size, offset) {
  const headers = { Authorization: `Bearer ${await auth.get()}`, 'Content-Length': String(size - offset), 'Content-Type': 'video/mp4' };
  if (offset > 0) headers['Content-Range'] = `bytes ${offset}-${size - 1}/${size}`;
  return fetch(sessionUrl, { method: 'PUT', headers, body: Readable.toWeb(createReadStream(file, { start: offset })), duplex: 'half' });
}

/** The resumable upload's status query: an authorised, empty PUT with `Content-Range: bytes * / total`. */
async function statusStep(auth, sessionUrl, size) {
  return fetch(sessionUrl, { method: 'PUT', headers: { Authorization: `Bearer ${await auth.get()}`, 'Content-Length': '0', 'Content-Range': `bytes */${size}` } });
}

/** What one answer means: { id } done; { expired } 404; { incomplete, offset, retryAfter }; { retry, error, retryAfter } for 500/502/503/504 and 401 (token refreshed); anything else is permanent and throws. */
async function classify(auth, file, response) {
  if (response.ok) return { id: (await response.json()).id };
  if (response.status === HTTP_NOT_FOUND) return { expired: true };
  if (response.status === HTTP_RESUME_INCOMPLETE) {
    const held = /bytes=0-(\d+)/.exec(response.headers.get('range') ?? '');
    return { incomplete: true, offset: held ? Number(held[1]) + 1 : 0, retryAfter: retryAfterMs(response) };
  }
  const error = await uploadError(file, response);
  if (response.status === HTTP_UNAUTHORIZED) await auth.get(true);
  if (response.status === HTTP_UNAUTHORIZED || RETRYABLE_STATUSES.has(response.status)) return { retry: true, error, retryAfter: retryAfterMs(response) };
  throw error;
}

/** One request, a dropped connection counted as a retry. */
async function attemptStep(auth, file, request) {
  try {
    return await classify(auth, file, await request());
  } catch (error) {
    if (error.status !== undefined) throw error; // a permanent answer
    return { retry: true, error };
  }
}

/**
 * The resumable upload loop. A dropped connection or a 500/502/503/504 asks the session how far it got and sends the rest; a 404
 * means the session expired, so a new session starts from byte 0 (one more videos.insert, said in the log). Every retry,
 * the new session included, counts against the same bound; other errors are permanent. `io` = { sleep, log } for tests.
 */
export async function uploadVideo(auth, file, body, io = { sleep: pause, log: console.log }) {
  const size = statSync(file).size;
  const state = { sessionUrl: await startSession(auth, size, body, io), offset: 0, needStatus: false };
  for (let retries = 0; ;) {
    const request = state.needStatus ? () => statusStep(auth, state.sessionUrl, size) : () => putStep(auth, state.sessionUrl, file, size, state.offset);
    const step = await attemptStep(auth, file, request);
    if (step.id) return step.id;
    if (step.incomplete && state.needStatus) {
      state.offset = step.offset;
      state.needStatus = false;
      if (step.retryAfter) await io.sleep(step.retryAfter);
      continue;
    }
    if (retries >= MAX_UPLOAD_RESUMES) throw step.error ?? new Error(`Upload of ${file} did not finish after ${MAX_UPLOAD_RESUMES} retries`);
    await io.sleep(backoffMs(retries, step.retryAfter));
    retries += 1;
    await prepareRetry({ auth, file, size, body, step, state, io, retries });
  }
}

/** After a failed step: a 404 starts a new session from byte 0 (said in the log), anything else asks the session how far it got. */
async function prepareRetry({ auth, file, size, body, step, state, io, retries }) {
  if (!step.expired) { state.needStatus = true; return; }
  io.log(`SESSION EXPIRED for ${file}: starting a NEW upload session from byte 0; this spends one more videos.insert of the daily quota (retry ${retries} of ${MAX_UPLOAD_RESUMES}).`);
  state.sessionUrl = await startSession(auth, size, body, io);
  state.offset = 0;
  state.needStatus = false;
}

const pause = (ms) => new Promise((done) => setTimeout(done, ms));

export async function setThumbnail(token, videoId, file) {
  await call(token, 'POST', `${UPLOAD_API}/thumbnails/set?videoId=${videoId}`, readFileSync(file), { 'Content-Type': 'image/jpeg' });
}

/** Walks every page of the channel's playlists (nextPageToken) until one has this title; null when none does. */
async function findPlaylist(token, title) {
  let pageToken = '';
  do {
    const page = await (await call(token, 'GET', `${API}/playlists?part=snippet,status&mine=true&maxResults=${PLAYLIST_FETCH_LIMIT}${pageToken ? `&pageToken=${encodeURIComponent(pageToken)}` : ''}`)).json();
    const found = page.items?.find((p) => p.snippet.title === title);
    if (found) return found;
    pageToken = page.nextPageToken ?? '';
  } while (pageToken);
  return null;
}

/**
 * The channel's playlist with this title - one per language. Created with the video's own privacy, so an unlisted
 * video never shows in a public playlist; an existing playlist goes public only when this upload is public (needs --owner-approved).
 * An unlisted video never goes into an already public playlist: that returns null (the caller skips the playlist step and says so).
 */
export async function ensurePlaylist(token, title, language, privacy) {
  const found = await findPlaylist(token, title);
  const foundPublic = found?.status?.privacyStatus === PRIVACY.public;
  if (found && foundPublic && privacy !== PRIVACY.public) return null;
  if (found && !(privacy === PRIVACY.public && !foundPublic)) return found.id;
  const headers = { 'Content-Type': 'application/json' };
  if (found) {
    await call(token, 'PUT', `${API}/playlists?part=snippet,status`, JSON.stringify({ id: found.id, snippet: { title, defaultLanguage: language }, status: { privacyStatus: privacy } }), headers);
    return found.id;
  }
  const created = await (await call(token, 'POST', `${API}/playlists?part=snippet,status`, JSON.stringify({ snippet: { title, defaultLanguage: language }, status: { privacyStatus: privacy } }), headers)).json();
  return created.id;
}

export async function addToPlaylist(token, playlistId, videoId) {
  await call(token, 'POST', `${API}/playlistItems?part=snippet`, JSON.stringify({ snippet: { playlistId, resourceId: { kind: 'youtube#video', videoId } } }), { 'Content-Type': 'application/json' });
}

const UPDATABLE_STATUS = ['embeddable', 'license', 'publicStatsViewable', 'selfDeclaredMadeForKids', 'containsSyntheticMedia'];

/** videos.update (docs: videos/update, 50 units): sets only privacyStatus. The call deletes any status property it omits, so the current writable ones are read first and sent back unchanged. */
export async function setPrivacy(token, videoId, privacy) {
  const json = await (await call(token, 'GET', `${API}/videos?part=status&id=${videoId}`)).json();
  const current = json.items?.[0]?.status;
  if (!current) throw new Error(`Video ${videoId} not found`);
  const kept = Object.fromEntries(UPDATABLE_STATUS.filter((key) => current[key] !== undefined).map((key) => [key, current[key]]));
  const body = { id: videoId, status: { ...kept, privacyStatus: privacy } };
  await call(token, 'PUT', `${API}/videos?part=status`, JSON.stringify(body), { 'Content-Type': 'application/json' });
}

/** Reads a video back: title, language, kids flag, privacy (the Verification line's read-back). */
export async function readBack(token, videoId) {
  const json = await (await call(token, 'GET', `${API}/videos?part=snippet,status&id=${videoId}`)).json();
  const item = json.items?.[0];
  if (!item) throw new Error(`Video ${videoId} not found`);
  return { title: item.snippet.title, language: item.snippet.defaultLanguage, madeForKids: item.status.madeForKids, selfDeclaredMadeForKids: item.status.selfDeclaredMadeForKids, privacy: item.status.privacyStatus };
}
