// Slice 435 item 4 (code only until the owner's grant): uploads rendered videos to the channel.
//
//   node upload.mjs --out <render folder> --book <stem> --lang <code,...> [--privacy unlisted|public] [--ai-disclosure] [--yes]
//   node upload.mjs --authorize          the owner's one-time sign-in (the channel's Google account); saves the refresh token
//
// DRY RUN BY DEFAULT: without --yes it reads nothing from the secrets store, opens no connection and only prints what it would send.
// Unlisted by default; `--privacy public` is refused until the owner has said yes on the first video (--owner-approved).

import { createHash } from 'node:crypto';
import { createReadStream, existsSync, readFileSync, writeFileSync } from 'node:fs';
import { valueOf } from './lib/args.mjs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CHECK_FILE } from './lib/render-video.mjs';
import { playlistTitle } from './lib/metadata.mjs';
import { LANGUAGES } from './lib/strings.mjs';
import { PRIVACY, accessToken, addToPlaylist, authorize, ensurePlaylist, insertBody, isQuotaExceeded, readBack, setPrivacy, setThumbnail, uploadVideo } from './lib/youtube.mjs';

const EXIT_QUOTA_STOP = 2;
const SKIPPED_PLAYLIST = 'skipped';
export const UPLOADED_FILE ='uploaded.json'; // <out>/<book>/uploaded.json: language -> { hash (of video.mp4), id, thumbnail, playlist, done }; the git-ignored out folder

export function parseArgs(argv) {
  const options = { privacy: PRIVACY.unlisted, aiDisclosure: false, yes: false, ownerApproved: false, authorize: false, lang: 'all', book: null, out: null };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (['--out', '--book', '--lang', '--privacy'].includes(arg)) {
      options[arg.slice(2)] = valueOf(argv, i, arg);
      i += 1;
    } else if (arg === '--ai-disclosure') options.aiDisclosure = true;
    else if (arg === '--yes') options.yes = true;
    else if (arg === '--owner-approved') options.ownerApproved = true;
    else if (arg === '--authorize') options.authorize = true;
    else throw new Error(`Unknown argument ${arg}`);
  }
  languagesOf(options.lang);
  if (!Object.values(PRIVACY).includes(options.privacy)) throw new Error(`--privacy must be one of ${Object.values(PRIVACY).join(', ')}`);
  if (options.privacy === PRIVACY.public && !options.ownerApproved) throw new Error('Public uploads wait for the owner\'s yes on the first (unlisted) video: pass --owner-approved only after it is recorded in the slice file.');
  return options;
}

/** `all` or a comma list, trimmed; blank entries dropped and a code outside LANGUAGES refused. */
export function languagesOf(lang) {
  if (lang === 'all') return LANGUAGES;
  const codes = lang.split(',').map((code) => code.trim()).filter(Boolean);
  const unknown = codes.filter((code) => !LANGUAGES.includes(code));
  if (!codes.length || unknown.length) throw new Error(`--lang must be "all" or codes from ${LANGUAGES.join(', ')}${unknown.length ? ` (unknown: ${unknown.join(', ')})` : ''}`);
  return codes;
}

export function plan(options) {
  return languagesOf(options.lang).map((language) => {
    const dir = resolve(options.out, options.book, language);
    const files = { video: join(dir, 'video.mp4'), thumbnail: join(dir, 'thumbnail.jpg'), meta: join(dir, 'meta.json'), check: join(dir, CHECK_FILE) };
    const meta = existsSync(files.meta) ? JSON.parse(readFileSync(files.meta, 'utf8')) : null;
    const body = meta && insertBody(meta, { privacy: options.privacy, containsSyntheticMedia: options.aiDisclosure });
    return { language, dir, video: files.video, thumbnail: files.thumbnail, meta, playlist: playlistTitle(language), body, refusal: refusalOf(files, meta) };
  });
}

/** Why this language must not go up (null = fine): a missing file, or no passing, non-sample render check next to the video. */
export function refusalOf(files, meta) {
  const missing = Object.entries({ video: files.video, thumbnail: files.thumbnail, 'meta.json': files.meta }).filter(([, path]) => !existsSync(path)).map(([name]) => name);
  if (missing.length) return `missing ${missing.join(', ')}`;
  if (!existsSync(files.check)) return `no ${CHECK_FILE}: render this video again`;
  const check = JSON.parse(readFileSync(files.check, 'utf8'));
  if (!check.pass) return 'its render check did not PASS';
  if (check.sample) return `it is a SAMPLE render (first ${check.maxPages} pages)`;
  return meta ? null : 'missing meta.json';
}

function readRecord(file) {
  return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {};
}

/** SHA-256 of the video file's bytes: the record's identity for "this exact video is already up". */
export async function videoHash(file) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest('hex');
}

/**
 * The record entry to work on for a language, keyed on the video's content hash. Same hash: the entry carries on (done = skipped,
 * half done = resumed). A different hash (a re-rendered video, or an entry from before hashes were stored): a fresh entry, and
 * `replaced` names the old video, which stays on the channel until the owner deletes it by hand. Never an upload without a line.
 */
export function entryFor(record, language, hash) {
  const old = record[language];
  if (!old || !old.id || old.hash === hash) {
    record[language] = { ...(old ?? {}), hash };
    return { entry: record[language], replaced: null };
  }
  record[language] = { hash };
  return { entry: record[language], replaced: old.id };
}

/** The three steps run once each and are recorded after each, so a rerun never calls videos.insert twice and retries only what is missing. */
export async function uploadOne(auth, item, privacy, entry, save) {
  if (!entry.id) { entry.id = await uploadVideo(auth, item.video, item.body); entry.privacy = privacy; save(); }
  if (!entry.thumbnail) { await setThumbnail(auth, entry.id, item.thumbnail); entry.thumbnail = true; save(); }
  if (!entry.playlist) { entry.playlist = await playlistStep(auth, item, privacy, entry.id); save(); }
  const back = await readBack(auth, entry.id);
  console.log(`UPLOADED ${item.language} ${entry.id} ${JSON.stringify(back)}`);
  if (back.privacy !== privacy) throw new Error(`${item.language} ${entry.id}: asked for ${privacy}, YouTube shows ${back.privacy}`);
  entry.done = true;
  save();
}

/** Puts the video in its language's playlist: true when added, SKIPPED_PLAYLIST when the playlist is public and the video is not (it is added when the video is published). */
async function playlistStep(auth, item, privacy, videoId) {
  const playlistId = await ensurePlaylist(auth, item.playlist, item.language, privacy);
  if (!playlistId) {
    console.log(`PLAYLIST SKIPPED ${item.language}: "${item.playlist}" is public and ${videoId} is ${privacy}; it joins the playlist when it is made public.`);
    return SKIPPED_PLAYLIST;
  }
  await addToPlaylist(auth, playlistId, videoId);
  return true;
}

/**
 * A rerun with --privacy public after the owner's yes: an already uploaded video that is not public yet is made public with
 * videos.update (never uploaded again), then joins its (now public) playlist if it was kept out of it. Returns true when it changed.
 */
export async function publishIfNeeded(auth, item, entry, save) {
  const back = await readBack(auth, entry.id);
  if (back.privacy === PRIVACY.public) { entry.privacy = PRIVACY.public; save(); return false; }
  await setPrivacy(auth, entry.id, PRIVACY.public);
  if (entry.playlist !== true) { await addToPlaylist(auth, await ensurePlaylist(auth, item.playlist, item.language, PRIVACY.public), entry.id); entry.playlist = true; }
  const after = await readBack(auth, entry.id);
  if (after.privacy !== PRIVACY.public) throw new Error(`${item.language} ${entry.id}: made public, YouTube shows ${after.privacy}`);
  entry.privacy = PRIVACY.public;
  save();
  console.log(`PUBLISHED ${item.language} ${entry.id} (was ${back.privacy}) ${JSON.stringify(after)}`);
  return true;
}

function printDryRun(items, options) {
  for (const item of items) {
    console.log(`DRY RUN ${item.language}: ${item.refusal ? `REFUSED (${item.refusal})` : 'would upload'} ${item.video} as ${options.privacy}, then thumbnail and playlist "${item.playlist}"\n${JSON.stringify(item.body)}`);
  }
  console.log('Dry run only: nothing was sent. Add --yes to upload.');
}

/** One language: skip, publish (a done video asked for public), or upload/resume. */
async function handleItem(auth, item, options, entry, save) {
  if (!entry.done) return uploadOne(auth, item, options.privacy, entry, save);
  if (options.privacy === PRIVACY.public && entry.privacy !== PRIVACY.public && await publishIfNeeded(auth, item, entry, save)) return;
  console.log(`SKIP ${item.language}: this exact video is already up (${entry.id})`);
}

/** Returns the languages NOT finished because the daily quota ran out (empty = all done). */
export async function uploadAll(items, options, auth = accessToken()) {
  const refused = items.filter((i) => i.refusal);
  if (refused.length) throw new Error(`Nothing uploaded. Refused: ${refused.map((i) => `${i.language} (${i.refusal})`).join('; ')}`);
  const recordFile = join(resolve(options.out, options.book), UPLOADED_FILE);
  const record = readRecord(recordFile);
  const save = () => writeFileSync(recordFile, JSON.stringify(record, null, 2));
  for (const [index, item] of items.entries()) {
    const { entry, replaced } = entryFor(record, item.language, await videoHash(item.video));
    if (replaced) console.log(`REPLACED ${item.language}: the video changed since ${replaced} was uploaded; uploading it again as a NEW video. ${replaced} stays on the channel until you delete it.`);
    try {
      await handleItem(auth, item, options, entry, save);
    } catch (error) {
      if (!isQuotaExceeded(error)) throw error;
      const left = items.slice(index).map((i) => i.language);
      console.log(`STOPPED at ${item.language}: the daily YouTube quota is used up. Languages left: ${left.join(', ')}. What is up so far is in ${recordFile}; rerun after the quota resets (midnight Pacific time) and it resumes.`);
      return left;
    }
  }
  return [];
}

/** Returns the process exit code: 0 done, 2 when a quota stop left languages undone. */
export async function main(argv) {
  const options = parseArgs(argv);
  if (options.authorize) { await authorize(); return 0; }
  if (!options.out || !options.book) throw new Error('--out and --book are required');
  const items = plan(options);
  if (!options.yes) { printDryRun(items, options); return 0; }
  return (await uploadAll(items, options)).length ? EXIT_QUOTA_STOP : 0;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).then((code) => process.exit(code ?? 0), (error) => { console.error(error.message); process.exit(1); });
}
