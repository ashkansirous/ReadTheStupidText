// Slice 435: fast tests for the renderer's pure parts (no Chrome, ffmpeg or network). Run: node --test "*.test.mjs" inside this folder.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkVideo } from './lib/check.mjs';
import { closingStill, openingStill, pageStill, textSizePx } from './lib/frame.mjs';
import { playlistTitle, videoMetadata } from './lib/metadata.mjs';
import { groupByPage, piecesOfLanguage, placePieces, splitPages } from './lib/pages.mjs';
import { LANGUAGES, STRINGS, fill } from './lib/strings.mjs';
import { FPS, PAGE_GAP_SEC, SENTENCE_GAP_SEC, buildTimeline, sceneFrames } from './lib/timeline.mjs';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { concatFileLine, materialise } from './lib/tools.mjs';
import { checkedList, parseArgs as parseVoiceArgs, voiceLines } from './voice-lines.mjs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { CHECK_FILE } from './lib/render-video.mjs';
import { assertTokenOutsideRepo, backoffMs, ensurePlaylist, insertBody, isQuotaExceeded, uploadVideo } from './lib/youtube.mjs';
import { UPLOADED_FILE, entryFor, languagesOf, publishIfNeeded, uploadAll, main as mainUpload, parseArgs as parseUpload, plan as planUpload, refusalOf, videoHash } from './upload.mjs';
import { voicePath } from './lib/render-video.mjs';
import { fileURLToPath } from 'node:url';
import { parseArgs as parseRender } from './render.mjs';

const STORY_MD = 'One. Two.\n\nThree.\n---\n"Four?" said Lion. Five & six.\n---\n';
const ENGLISH = [
  { index: 0, text: 'One.', clip: 'a.m4a' }, { index: 1, text: 'Two.', clip: 'b.m4a' }, { index: 2, text: 'Three.', clip: 'c.m4a' },
  { index: 3, text: 'Four?', clip: 'd.m4a' }, { index: 4, text: 'said Lion.', clip: 'e.m4a' }, { index: 5, text: 'Five & six.', clip: 'f.m4a' },
];

test('splitPages splits on --- lines and trims', () => {
  assert.deepEqual(splitPages(STORY_MD), ['One. Two.\n\nThree.', '"Four?" said Lion. Five & six.']);
});

test('placePieces finds each piece on its page and notes paragraph breaks', () => {
  const placed = placePieces(splitPages(STORY_MD), ENGLISH);
  assert.deepEqual(placed.map((p) => p.page), [0, 0, 0, 1, 1, 1]);
  assert.deepEqual(placed.map((p) => p.breakBefore), [false, false, true, false, false, false]);
});

test('placePieces fails on a piece that is on no page', () => {
  assert.throws(() => placePieces(splitPages(STORY_MD), [{ index: 0, text: 'Nowhere', clip: 'x' }]), /not in any story page/);
});

test('piecesOfLanguage takes the English page and refuses a count mismatch', () => {
  const placed = placePieces(splitPages(STORY_MD), ENGLISH);
  const translated = ENGLISH.map((e) => ({ ...e, text: `T${e.index}` }));
  assert.equal(piecesOfLanguage(placed, translated, 'de')[3].page, 1);
  assert.throws(() => piecesOfLanguage(placed, translated.slice(1), 'de'), /manifest entries/);
});

const timelineInput = () => ({
  pages: [
    { page: 0, pieces: [{ index: 0, clip: 'a', sec: 2 }, { index: 1, clip: 'b', sec: 3.333 }] },
    { page: 1, pieces: [{ index: 2, clip: 'c', sec: 1.5 }] },
  ],
});

test('timeline uses the listening track gaps and is frame-exact', () => {
  const t = buildTimeline(timelineInput());
  const gaps = t.audio.filter((a) => 'silence' in a).map((a) => a.silence);
  assert.ok(gaps.includes(SENTENCE_GAP_SEC) && gaps.includes(PAGE_GAP_SEC));
  assert.equal(t.totalFrames, Math.round(t.totalSec * FPS));
  assert.equal(t.scenes.map(sceneFrames).reduce((a, b) => a + b, 0), t.totalFrames);
  assert.deepEqual(t.scenes.map((s) => s.kind), ['opening', 'page', 'page', 'closing']);
});

test('timeline lengthens the cards for a spoken line', () => {
  const quiet = buildTimeline(timelineInput());
  const spoken = buildTimeline({ ...timelineInput(), closingVoice: { file: 'v', sec: 12 } });
  assert.ok(spoken.totalSec > quiet.totalSec);
});

function stillsFor(pieces) {
  return pieces.map((p) => ({ pieceIndex: p.index, html: pageStill({ language: 'en', pictureUrl: 'file:///x.jpg', pieces, activeIndex: p.index }) }));
}
const pageOne = () => ENGLISH.slice(0, 3).map((e) => ({ ...e, breakBefore: e.index === 2 }));
const auditOf = (entries) => entries.map((e) => ({ kind: 'clip', index: e.index, file: e.clip }));

test('checkVideo passes a faithful video', () => {
  assert.deepEqual(checkVideo({ entries: pageOne(), stills: stillsFor(pageOne()), audio: auditOf(pageOne()) }), []);
});

test('checkVideo catches a highlighted text that is not the manifest text, a wrong clip and a wrong order', () => {
  const entries = pageOne();
  const stills = stillsFor(entries);
  stills[1].html = stills[1].html.replace('>Two.<', '>Too.<');
  const audio = auditOf(entries);
  audio[0].file = 'wrong.m4a';
  const problems = checkVideo({ entries, stills, audio });
  assert.ok(problems.some((p) => p.includes('not the manifest text')));
  assert.ok(problems.some((p) => p.includes('audio plays wrong.m4a')));
  assert.ok(checkVideo({ entries, stills: stillsFor(entries).reverse(), audio: auditOf(entries) }).some((p) => p.includes('highlight order')));
});

test('page text is escaped and survives the round trip the check reads', () => {
  const entries = [{ index: 0, text: 'Fish & <chips> "ok"', clip: 'a' }];
  assert.deepEqual(checkVideo({ entries, stills: stillsFor(entries), audio: auditOf(entries) }), []);
});

test('text never goes below the TV floor and shrinks with length', () => {
  assert.ok(textSizePx(100) > textSizePx(500));
  assert.ok(textSizePx(2000) >= 40);
});

test('cards carry the address, the QR only when given, and the grown-ups line', () => {
  const closing = closingStill({ language: 'de', backCoverUrl: 'x', credits: 'c', aiLine: 'a', grownUps: STRINGS.de.grownUps, qrSvg: '<svg></svg>' });
  assert.match(closing, /dinoreader\.sirous\.uk/);
  assert.match(closing, /<svg>/);
  assert.match(closing, /Für Erwachsene/);
  assert.doesNotMatch(openingStill({ language: 'en', coverUrl: 'x', title: 'T', author: 'A', qrSvg: null }), /<svg/);
  assert.match(openingStill({ language: 'ar', coverUrl: 'x', title: 'T', author: 'A', qrSvg: null }), /dir="rtl"/);
});

test('all 15 languages carry every string and the closing line promises nothing to buy', () => {
  assert.equal(LANGUAGES.length, 15);
  for (const language of LANGUAGES) {
    const s = STRINGS[language];
    for (const key of ['name', 'grownUps', 'ai', 'kids', 'intro', 'playlist']) assert.ok(s[key]?.length > 0, `${language}.${key}`);
    assert.match(s.intro, /\{title\}/);
    assert.match(s.playlist, /\{name\}/);
    assert.doesNotMatch(s.grownUps, /\d|€|\$|£/);
  }
  assert.equal(fill('a {x}', { x: 'b' }), 'a b');
});

const STORY = { title: 'Title', credits: 'Credit', creditsByLanguage: {}, titles: Object.fromEntries(LANGUAGES.map((l) => [l, `T-${l}`])), stem: 'book' };

test('metadata is in the video language and within YouTube limits', () => {
  for (const language of LANGUAGES) {
    const meta = videoMetadata(STORY, language);
    assert.ok(meta.title.length <= 100 && meta.description.length <= 5000);
    assert.ok(meta.description.includes(STRINGS[language].grownUps));
    assert.ok(playlistTitle(language).includes(STRINGS[language].name));
  }
  assert.equal(videoMetadata(STORY, 'zh').defaultLanguage, 'zh-Hans');
});

test('the upload body is always made for kids and unlisted by default', () => {
  const body = insertBody(videoMetadata(STORY, 'en'), { privacy: 'unlisted', containsSyntheticMedia: false });
  assert.equal(body.status.selfDeclaredMadeForKids, true);
  assert.equal(body.status.privacyStatus, 'unlisted');
  assert.equal(parseUpload(['--out', 'o', '--book', 'b']).yes, false);
});

test('public upload is refused until the owner approved', () => {
  assert.throws(() => parseUpload(['--privacy', 'public']), /owner/);
  assert.equal(parseUpload(['--privacy', 'public', '--owner-approved']).privacy, 'public');
});

test('render needs an --out folder and rejects unknown flags', () => {
  assert.throws(() => parseRender([]), /--out/);
  assert.throws(() => parseRender(['--out', 'x', '--wat']), /Unknown/);
  assert.equal(parseRender(['--out', 'x', '--no-qr']).noQr, true);
  assert.equal(typeof planUpload, 'function');
});

test('dry run makes no network call, reads no secrets and writes nothing', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'yt-dry-'));
  const lang = join(dir, 'book', 'en');
  mkdirSync(lang, { recursive: true });
  writeFileSync(join(lang, 'meta.json'), JSON.stringify(videoMetadata(STORY, 'en')));
  const realFetch = globalThis.fetch;
  globalThis.fetch = () => { throw new Error('network call in a dry run'); };
  try {
    await mainUpload(['--out', dir, '--book', 'book', '--lang', 'en']);
  } finally {
    globalThis.fetch = realFetch;
  }
  assert.equal(existsSync(join(dir, 'book', UPLOADED_FILE)), false);
});

test('an option without a value is refused up front', () => {
  assert.throws(() => parseUpload(['--lang']), /--lang needs a value/);
  assert.throws(() => parseUpload(['--out', '--book', 'b']), /--out needs a value/);
});

test('upload refuses a sample, a failed check, a missing check and missing files', () => {
  const dir = mkdtempSync(join(tmpdir(), 'yt-ref-'));
  const files = { video: join(dir, 'v.mp4'), thumbnail: join(dir, 't.jpg'), meta: join(dir, 'm.json'), check: join(dir, CHECK_FILE) };
  assert.match(refusalOf(files, {}), /missing video/);
  for (const path of [files.video, files.thumbnail, files.meta]) writeFileSync(path, 'x');
  assert.match(refusalOf(files, {}), /no check\.json/);
  writeFileSync(files.check, JSON.stringify({ pass: false, sample: false }));
  assert.match(refusalOf(files, {}), /did not PASS/);
  writeFileSync(files.check, JSON.stringify({ pass: true, sample: true, maxPages: 2 }));
  assert.match(refusalOf(files, {}), /SAMPLE/);
  writeFileSync(files.check, JSON.stringify({ pass: true, sample: false }));
  assert.equal(refusalOf(files, {}), null);
});

test('render rejects a NaN or zero --max-pages', () => {
  assert.throws(() => parseRender(['--out', 'x', '--max-pages', 'abc']), /--max-pages/);
  assert.throws(() => parseRender(['--out', 'x', '--max-pages', '0']), /--max-pages/);
  assert.equal(parseRender(['--out', 'x', '--max-pages', '2']).maxPages, 2);
});

test('the check fails when a page shows fewer sentences than its group', () => {
  const entries = [{ index: 0, text: 'One.', clip: 'a' }, { index: 1, text: 'Two.', clip: 'b' }];
  const html = '<span class="piece on" data-piece="0">One.</span>';
  const problems = checkVideo({ entries, stills: [{ pieceIndex: 0, html, pageIndexes: [0, 1] }, { pieceIndex: 1, html: '<span class="piece on" data-piece="1">Two.</span>', pageIndexes: [1] }], audio: [{ kind: 'clip', index: 0, file: 'a' }, { kind: 'clip', index: 1, file: 'b' }] });
  assert.ok(problems.some((p) => /the page holds/.test(p)));
});

test('quota errors are recognised, and a token file inside the repo is refused', () => {
  assert.equal(isQuotaExceeded({ status: 403, body: '{"error":{"errors":[{"reason":"quotaExceeded"}]}}' }), true);
  assert.equal(isQuotaExceeded({ status: 403, body: 'forbidden' }), false);
  assert.throws(() => assertTokenOutsideRepo('/repo/sub/youtube-token.json', '/repo'), /inside the repo/);
  assert.doesNotThrow(() => assertTokenOutsideRepo('/secrets/youtube-token.json', '/repo'));
});

test('uploadLimitExceeded counts as the quota stop whether YouTube sends 400 or 403', () => {
  assert.equal(isQuotaExceeded({ status: 400, body: '{"error":{"errors":[{"reason":"uploadLimitExceeded"}]}}' }), true);
  assert.equal(isQuotaExceeded({ status: 403, body: '{"reason":"uploadLimitExceeded"}' }), true);
  assert.equal(isQuotaExceeded({ status: 400, body: '{"reason":"quotaExceeded"}' }), false);
  assert.equal(isQuotaExceeded({ status: 400, body: 'badRequest' }), false);
});

test('a re-rendered video uploads again as a new one (named), an unchanged one is skipped', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'yt-hash-'));
  const file = join(dir, 'video.mp4');
  writeFileSync(file, 'first render');
  const first = await videoHash(file);
  writeFileSync(file, 'second render');
  const second = await videoHash(file);
  assert.notEqual(first, second);
  const record = { en: { hash: first, id: 'OLD', thumbnail: true, playlist: true, done: true } };
  const same = entryFor(record, 'en', first);
  assert.equal(same.replaced, null);
  assert.equal(same.entry.done, true);
  const changed = entryFor(record, 'en', second);
  assert.equal(changed.replaced, 'OLD');
  assert.deepEqual(changed.entry, { hash: second });
  assert.equal(entryFor({}, 'de', second).replaced, null);
  assert.equal(entryFor({ de: { hash: first } }, 'de', second).replaced, null); // nothing was uploaded yet: just carry on
});

test('render options without a value are refused, not a TypeError', () => {
  assert.throws(() => parseRender(['--out', 'x', '--book']), /--book needs a value/);
  assert.throws(() => parseRender(['--lang', '--out', 'x']), /--lang needs a value/);
  assert.throws(() => parseRender(['--out']), /--out needs a value/);
  assert.throws(() => parseRender(['--out', 'x', '--max-pages']), /--max-pages needs a value/);
});

test('the voice clip path is the platform file path of the voice folder', () => {
  const expected = fileURLToPath(new URL('./voice/en/grown-ups.m4a', import.meta.url));
  assert.equal(voicePath('en', 'grown-ups'), expected);
  assert.doesNotMatch(voicePath('en', 'x'), /%20|^[/\\][A-Za-z]:/);
});

test('placePieces places by sequence: a repeated short sentence cannot pull a later piece to the wrong spot', () => {
  const pages = splitPages('Run. Then he sat down. Run.\n---\nThe end.');
  const entries = [{ index: 0, text: 'Run.' }, { index: 1, text: 'Run.' }, { index: 2, text: 'The end.' }];
  assert.throws(() => placePieces(pages, entries), /unspoken words/); // piece 1 would jump over "Then he sat down."
  const ok = placePieces(pages, [{ index: 0, text: 'Run.' }, { index: 1, text: 'Then he sat down.' }, { index: 2, text: 'Run.' }, { index: 3, text: 'The end.' }]);
  assert.deepEqual(ok.map((p) => p.page), [0, 0, 0, 1]);
});

test('placePieces fails when a whole page tail is skipped between two pieces', () => {
  const pages = splitPages('One. Two.\n---\nThree.');
  assert.throws(() => placePieces(pages, [{ index: 0, text: 'One.' }, { index: 1, text: 'Three.' }]), /unspoken words/);
});

// Resumable upload (docs: using_resumable_upload_protocol): fetch is stubbed, nothing leaves the machine.
function scriptedUpload(answers) {
  const calls = [];
  const realFetch = globalThis.fetch;
  globalThis.fetch = async (url, init) => {
    calls.push({ url: String(url), method: init.method, headers: init.headers, body: init.body });
    const next = answers.shift();
    if (next instanceof Error) throw next;
    return new Response(next.body ?? '', { status: next.status, headers: next.headers ?? {} });
  };
  return { calls, restore: () => { globalThis.fetch = realFetch; } };
}

const SESSION = { status: 200, headers: { location: 'https://upload.example/session1' } };
const DONE = { status: 200, body: JSON.stringify({ id: 'VID1' }) };
const AUTH = { get: async () => 'tok' };

async function runUpload(answers) {
  const file = join(mkdtempSync(join(tmpdir(), 'yt-up-')), 'v.mp4');
  writeFileSync(file, 'abcdefghij');
  const stub = scriptedUpload(answers);
  const sleeps = [];
  const logs = [];
  try {
    const id = await uploadVideo(AUTH, file, {}, { sleep: async (ms) => { sleeps.push(ms); }, log: (line) => logs.push(line) });
    return { id, sleeps, logs, calls: stub.calls };
  } catch (error) {
    return { error, sleeps, logs, calls: stub.calls };
  } finally { stub.restore(); }
}

test('only 500, 502, 503 and 504 are retried; other errors are permanent', async () => {
  const permanent = await runUpload([SESSION, { status: 400, body: 'bad' }]);
  assert.equal(permanent.error.status, 400);
  assert.equal(permanent.calls.length, 2);
  const notImplemented = await runUpload([SESSION, { status: 501, body: 'no' }]);
  assert.equal(notImplemented.error.status, 501);
  const retried = await runUpload([SESSION, { status: 503 }, { status: 308, headers: { range: 'bytes=0-4' } }, { status: 502 }, { status: 308, headers: { range: 'bytes=0-6' } }, DONE]);
  assert.equal(retried.id, 'VID1');
  assert.equal(retried.calls.at(-1).headers['Content-Range'], 'bytes 7-9/10');
  assert.equal(retried.calls.filter((c) => c.method === 'POST').length, 1); // never a second videos.insert
});

test('the status query and the resume PUT carry the bearer token, and the query has Content-Length 0', async () => {
  const run = await runUpload([SESSION, { status: 503 }, { status: 308 }, DONE]);
  const [, put, status, resume] = run.calls;
  assert.equal(put.headers.Authorization, 'Bearer tok');
  assert.equal(status.headers.Authorization, 'Bearer tok');
  assert.equal(status.headers['Content-Length'], '0');
  assert.equal(status.headers['Content-Range'], 'bytes */10');
  assert.equal(resume.headers.Authorization, 'Bearer tok');
});

test('retries back off exponentially and a 308 Retry-After is honoured; the retries are bounded', async () => {
  const run = await runUpload([SESSION, { status: 503 }, { status: 308, headers: { 'retry-after': '3' } }, { status: 503 }, { status: 308 }, DONE]);
  assert.deepEqual(run.sleeps, [1000, 3000, 2000]);
  const endless = await runUpload([SESSION, ...Array.from({ length: 12 }, () => ({ status: 503 }))]);
  assert.equal(endless.error.status, 503);
  assert.equal(endless.sleeps.length, 5);
  assert.equal(backoffMs(0), 1000);
  assert.equal(backoffMs(2), 4000);
  assert.equal(backoffMs(20), 60000);
  assert.equal(backoffMs(1, 7000), 7000);
});

test('a 404 means the session expired: a new session from byte 0, said in the run line and counted in the bound', async () => {
  const run = await runUpload([SESSION, { status: 503 }, { status: 404 }, { status: 200, headers: { location: 'https://upload.example/session2' } }, DONE]);
  assert.equal(run.id, 'VID1');
  assert.equal(run.calls.filter((c) => c.method === 'POST').length, 2);
  assert.equal(run.calls.at(-1).url, 'https://upload.example/session2');
  assert.equal(run.calls.at(-1).headers['Content-Range'], undefined);
  assert.ok(run.logs.some((l) => /NEW upload session from byte 0.*one more videos\.insert/.test(l)));
  const always = await runUpload([SESSION, ...Array.from({ length: 6 }, () => [{ status: 404 }, SESSION]).flat()]);
  assert.equal(always.logs.length, 5);
  assert.ok(always.error);
});

test('the quota reason is matched against the whole error body, not its first 400 characters', async () => {
  const body = `{"error":{"message":"${'x'.repeat(900)}","errors":[{"reason":"uploadLimitExceeded"}]}}`;
  const run = await runUpload([{ status: 400, body }]);
  assert.equal(run.error.body.length, body.length);
  assert.ok(run.error.message.length < 600);
  assert.equal(isQuotaExceeded(run.error), true);
});

const json = (value, status = 200) => ({ status, body: JSON.stringify(value) });

test('startSession retries 500/502/503/504 and a dropped connection with the same backoff; 400 is permanent', async () => {
  const run = await runUpload([{ status: 502 }, new Error('socket hang up'), { status: 503 }, SESSION, DONE]);
  assert.equal(run.id, 'VID1');
  assert.deepEqual(run.sleeps, [1000, 2000, 4000]);
  assert.equal(run.calls.filter((c) => c.method === 'POST').length, 4);
  const permanent = await runUpload([{ status: 400, body: 'bad' }]);
  assert.equal(permanent.error.status, 400);
  assert.equal(permanent.calls.length, 1);
  const endless = await runUpload(Array.from({ length: 12 }, () => ({ status: 500 })));
  assert.equal(endless.error.status, 500);
  assert.equal(endless.sleeps.length, 5);
});

test('an unlisted video never goes into a public playlist; a non-public one is used', async () => {
  const playlists = (privacyStatus) => json({ items: [{ id: 'PL1', snippet: { title: 'T' }, status: { privacyStatus } }] });
  const publicStub = scriptedUpload([playlists('public')]);
  try { assert.equal(await ensurePlaylist(AUTH, 'T', 'en', 'unlisted'), null); } finally { publicStub.restore(); }
  const privateStub = scriptedUpload([playlists('private')]);
  try { assert.equal(await ensurePlaylist(AUTH, 'T', 'en', 'unlisted'), 'PL1'); } finally { privateStub.restore(); }
});

test('after the owner yes a rerun makes an uploaded unlisted video public with videos.update, without uploading again', async () => {
  const status = { privacyStatus: 'unlisted', selfDeclaredMadeForKids: true, madeForKids: true, embeddable: true, uploadStatus: 'processed' };
  const video = (privacyStatus) => json({ items: [{ snippet: { title: 'T', defaultLanguage: 'en' }, status: { ...status, privacyStatus } }] });
  const stub = scriptedUpload([
    video('unlisted'), video('unlisted'), { status: 200, body: '{}' },
    json({ items: [{ id: 'PL1', snippet: { title: 'T' }, status: { privacyStatus: 'unlisted' } }] }), { status: 200, body: '{}' }, { status: 200, body: '{}' }, video('public'),
  ]);
  const entry = { hash: 'h', id: 'VID1', thumbnail: true, playlist: 'skipped', done: true, privacy: 'unlisted' };
  try {
    assert.equal(await publishIfNeeded(AUTH, { language: 'en', playlist: 'T' }, entry, () => {}), true);
  } finally { stub.restore(); }
  const put = stub.calls.find((c) => c.method === 'PUT' && c.url.includes('/videos?part=status'));
  assert.deepEqual(JSON.parse(put.body), { id: 'VID1', status: { embeddable: true, selfDeclaredMadeForKids: true, privacyStatus: 'public' } });
  assert.equal(stub.calls.some((c) => c.url.includes('uploadType=resumable')), false);
  assert.equal(entry.privacy, 'public');
  assert.equal(entry.playlist, true);
});

test('a quota stop returns the languages left (the process then exits 2)', async () => {
  const out = mkdtempSync(join(tmpdir(), 'yt-quota-'));
  mkdirSync(join(out, 'book'));
  const items = ['en', 'de'].map((language) => {
    const video = join(out, `${language}.mp4`);
    writeFileSync(video, `video ${language}`);
    return { language, video, thumbnail: video, playlist: 'T', body: {}, refusal: null };
  });
  const stub = scriptedUpload([{ status: 403, body: '{"error":{"errors":[{"reason":"quotaExceeded"}]}}' }]);
  try {
    assert.deepEqual(await uploadAll(items, { out, book: 'book', privacy: 'unlisted' }, AUTH), ['en', 'de']);
  } finally { stub.restore(); }
});

test('--lang is trimmed and validated', () => {
  assert.deepEqual(languagesOf(' en, de ,'), ['en', 'de']);
  assert.throws(() => languagesOf('en,xx'), /unknown: xx/);
  assert.throws(() => languagesOf(' , '), /--lang must be/);
  assert.throws(() => parseUpload(['--lang', 'zz']), /--lang must be/);
});

test('a re-rendered picture or clip is copied again, never served from the old copy', () => {
  const dir = mkdtempSync(join(tmpdir(), 'yt-mat-'));
  const source = join(dir, 'page.jpg');
  writeFileSync(source, 'old picture');
  const first = materialise(source, join(dir, 'pictures', 'page.jpg'));
  writeFileSync(source, 'new picture');
  const second = materialise(source, join(dir, 'pictures', 'page.jpg'));
  assert.notEqual(first, second);
  assert.equal(readFileSync(second, 'utf8'), 'new picture');
  assert.equal(materialise(source, join(dir, 'pictures', 'page.jpg')), second);
});

test('voice lines: one grown-ups line per language and one title line per book and language', () => {
  const stories = [{ stem: 'lion-and-mouse', title: 'The Lion and the Mouse', titles: {} }];
  const lines = voiceLines(stories, ['en']);
  assert.deepEqual(lines.map((l) => l.key), ['grown-ups', 'title-lion-and-mouse']);
  assert.equal(lines[0].text, STRINGS.en.grownUps);
  assert.equal(lines[1].text, 'The Lion and the Mouse');
  const warnings = [];
  const faLines = voiceLines(stories, ['fa'], (message) => warnings.push(message));
  assert.deepEqual(faLines.map((l) => l.key), ['grown-ups']);
  assert.match(warnings[0], /title-lion-and-mouse in fa.*no title in fa/);
  assert.throws(() => parseVoiceArgs(['--x', '1']), /Unknown argument/);
  assert.throws(() => checkedList('', ['a'], 'book'), /lists nothing/);
  assert.throws(() => checkedList(',', LANGUAGES, 'lang'), /lists nothing/);
  assert.throws(() => checkedList('en,zz', LANGUAGES, 'lang'), /unknown code\(s\): zz/);
  assert.deepEqual(checkedList('all', ['a', 'b'], 'book'), ['a', 'b']);
});

test('an apostrophe in an ffconcat path is escaped the ffmpeg way', () => {
  assert.equal(concatFileLine("C:\\out\\Tom's book\\a.png"), "file 'C:/out/Tom'\\''s book/a.png'");
  assert.equal(concatFileLine('/plain/a.png'), "file '/plain/a.png'");
});
