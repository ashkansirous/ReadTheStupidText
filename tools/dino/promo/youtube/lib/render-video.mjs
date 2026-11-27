// Renders one video: book + language -> <out>/<stem>/<lang>/video.mp4 (3840 x 2160, 30 fps, H.264 + AAC), thumbnail.jpg, meta.json.
// Steps: pieces and clips -> timeline -> one Chrome still per state -> one mp4 per scene (stills held for their frames, page
// turns fade through the paper colour) -> concat the scenes, mux the one audio track -> the automatic check.

import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { assembleAudio } from './audio.mjs';
import { checkVideo } from './check.mjs';
import { closingStill, openingStill, pageStill, thumbnailStill, PAGE_HEIGHT, PAGE_WIDTH } from './frame.mjs';
import { videoMetadata } from './metadata.mjs';
import { qrSvg } from './qr.mjs';
import { creditsIn, loadPieces, titleIn } from './story.mjs';
import { stringsFor } from './strings.mjs';
import { FADE_FRAMES, FPS, buildTimeline, sceneFrames } from './timeline.mjs';
import { concatFileLine, durationSeconds, ffmpeg, ffprobe, materialise, screenshot } from './tools.mjs';

export const SCALE = 2; // 1920 x 1080 CSS pixels -> 3840 x 2160
export const CHECK_FILE = 'check.json'; // written last by renderVideo; upload.mjs refuses a video without a passing, non-sample one
export const VIDEO_WIDTH = 3840;
export const VIDEO_HEIGHT = 2160;
const THUMB_WIDTH = 1280;
const THUMB_HEIGHT = 720;
const PAPER = '0xf6efe0';
const VOICE_FOLDER = new URL('../voice/', import.meta.url);

const sha = (text) => createHash('sha1').update(text).digest('hex').slice(0, 16);

function clipsOf(bookDir, workDir, language, groups) {
  const files = new Map();
  for (const piece of groups.flatMap((g) => g.pieces)) {
    const path = materialise(join(bookDir, 'audio', language, piece.clip), join(workDir, 'clips', piece.clip));
    files.set(piece.clip, { path, sec: durationSeconds(path) });
  }
  return files;
}

/** An optional spoken card line (the title, the grown-ups line) from voice/<lang>/<key>.m4a; absent = the card is silent and the run says so. */
export function voicePath(language, key) {
  return fileURLToPath(new URL(`${language}/${key}.m4a`, VOICE_FOLDER));
}

function voiceClip(language, key, workDir) {
  const path = voicePath(language, key);
  if (!existsSync(path)) return null;
  const real = materialise(path, join(workDir, 'voice', `${language}-${key}.m4a`));
  return { file: `voice:${key}`, path: real, sec: durationSeconds(real) };
}

function stillPng(html, workDir, { cssWidth, cssHeight, scale }) {
  const name = sha(`${cssWidth}x${cssHeight}@${scale}${html}`);
  const png = join(workDir, 'stills', `${name}.png`);
  if (existsSync(png)) return png;
  const htmlPath = join(workDir, 'stills', `${name}.html`);
  mkdirSync(join(workDir, 'stills'), { recursive: true });
  writeFileSync(htmlPath, html);
  screenshot(htmlPath, png, { cssWidth, cssHeight, scale, profileDir: join(workDir, 'chrome-profile') });
  rmSync(htmlPath);
  return png;
}

const sizeOf = { cssWidth: PAGE_WIDTH, cssHeight: PAGE_HEIGHT, scale: SCALE };

function encodeScene(scene, pngs, workDir) {
  const frames = sceneFrames(scene);
  const list = ['ffconcat version 1.0'];
  scene.stills.forEach((still) => list.push(concatFileLine(pngs.get(still.key)), `duration ${(still.frames / FPS).toFixed(6)}`));
  list.push(concatFileLine(pngs.get(scene.stills.at(-1).key)));
  const listPath = join(workDir, `${scene.id}.ffconcat`);
  const out = join(workDir, `${scene.id}.mp4`);
  writeFileSync(listPath, list.join('\n'));
  const fade = Math.min(FADE_FRAMES, Math.floor(frames / 2));
  const filter = `fps=${FPS},fade=t=in:s=0:n=${fade}:color=${PAPER},fade=t=out:s=${frames - fade}:n=${fade}:color=${PAPER},format=yuv420p`;
  ffmpeg(['-f', 'concat', '-safe', '0', '-i', listPath, '-vf', filter, '-frames:v', String(frames), '-c:v', 'libx264', '-preset', 'medium', '-crf', '20', '-tune', 'stillimage', '-g', String(FPS * 2), out]);
  return out;
}

function muxVideo(scenePaths, wavPath, workDir, target) {
  const list = ['ffconcat version 1.0', ...scenePaths.map(concatFileLine)];
  const listPath = join(workDir, 'scenes.ffconcat');
  writeFileSync(listPath, list.join('\n'));
  ffmpeg(['-f', 'concat', '-safe', '0', '-i', listPath, '-i', wavPath, '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-map', '0:v', '-map', '1:a', '-movflags', '+faststart', target]);
}

function probeVideo(target) {
  const out = ffprobe(['-count_packets', '-show_entries', 'stream=codec_type,width,height,nb_read_packets', '-show_entries', 'format=duration', '-of', 'json', target]);
  const info = JSON.parse(out);
  const video = info.streams.find((s) => s.codec_type === 'video');
  return { width: video.width, height: video.height, frames: Number(video.nb_read_packets), seconds: Number(info.format.duration) };
}

/** options: { story (a free store book + stem), language, outRoot, contentRoot, noQr, maxPages } -> { pass, line, problems } */
export async function renderVideo({ story, language, outRoot, contentRoot, noQr = false, maxPages = Infinity }) {
  const bookDir = resolve(contentRoot, story.stem);
  const workDir = resolve(outRoot, story.stem, language);
  mkdirSync(workDir, { recursive: true });
  rmSync(join(workDir, CHECK_FILE), { force: true }); // a stale pass never survives a new render
  const { groups: allGroups } = loadPieces(bookDir, language);
  const groups = allGroups.slice(0, maxPages);
  const clipFiles = clipsOf(bookDir, workDir, language, groups);
  const openingVoice = voiceClip(language, `title-${story.stem}`, workDir);
  const closingVoice = voiceClip(language, 'grown-ups', workDir);
  const timeline = buildTimeline({
    openingVoice, closingVoice,
    pages: groups.map((g) => ({ page: g.page, pieces: g.pieces.map((p) => ({ index: p.index, clip: p.clip, sec: clipFiles.get(p.clip).sec })) })),
  });

  const art = await artFor({ story, language, bookDir, workDir, noQr });
  const { pngs, checkStills } = captureStills({ story, language, groups, timeline, workDir, art });
  const audioFiles = new Map([...clipFiles, ...[openingVoice, closingVoice].filter(Boolean).map((v) => [v.file, v])]);
  const target = encodeAndMux({ timeline, pngs, audioFiles, workDir });
  writeSidecars({ story, language, workDir, art });

  const result = verdict({ story, language, groups, checkStills, timeline, target, hasVoice: Boolean(openingVoice && closingVoice), maxPages });
  writeFileSync(join(workDir, CHECK_FILE), JSON.stringify({ pass: result.pass, sample: Number.isFinite(maxPages), maxPages: Number.isFinite(maxPages) ? maxPages : null, book: story.stem, language }, null, 2));
  return result;
}

/** The card inputs; `url(name)` is the picture's file URL, copied once per render under a name that carries a hash of its bytes. */
async function artFor({ story, language, bookDir, workDir, noQr }) {
  const urls = new Map();
  const url = (name) => {
    if (!urls.has(name)) urls.set(name, pathToFileURL(materialise(join(bookDir, 'high-res', name), join(workDir, 'pictures', name))).href);
    return urls.get(name);
  };
  return { url, strings: stringsFor(language), qr: await qrSvg({ noQr }), title: titleIn(story, language) };
}

/** One Chrome still per state: the opening and closing cards, then one page still per highlighted sentence. */
function captureStills({ story, language, groups, timeline, workDir, art }) {
  const pngs = new Map();
  const checkStills = [];
  pngs.set('opening', stillPng(openingStill({ language, coverUrl: art.url('cover.jpg'), title: art.title, author: story.author, qrSvg: art.qr }), workDir, sizeOf));
  pngs.set('closing', stillPng(closingStill({ language, backCoverUrl: art.url('back-cover.jpg'), credits: creditsIn(story, language), aiLine: art.strings.ai, grownUps: art.strings.grownUps, qrSvg: art.qr }), workDir, sizeOf));
  for (const scene of timeline.scenes.filter((s) => s.kind === 'page')) {
    const group = groups.find((g) => g.page === scene.page);
    for (const still of scene.stills) {
      const html = pageStill({ language, pictureUrl: art.url(`${group.page + 1}.jpg`), pieces: group.pieces, activeIndex: still.pieceIndex });
      pngs.set(still.key, stillPng(html, workDir, sizeOf));
      checkStills.push({ pieceIndex: still.pieceIndex, html, pageIndexes: group.pieces.map((p) => p.index) });
    }
  }
  return { pngs, checkStills };
}

function encodeAndMux({ timeline, pngs, audioFiles, workDir }) {
  const scenePaths = timeline.scenes.map((scene) => encodeScene(scene, pngs, workDir));
  const wav = join(workDir, 'audio.wav');
  writeFileSync(wav, assembleAudio(timeline.audio, audioFiles));
  const target = join(workDir, 'video.mp4');
  muxVideo(scenePaths, wav, workDir, target);
  return target;
}

function writeSidecars({ story, language, workDir, art }) {
  const thumb = stillPng(thumbnailStill({ language, coverUrl: art.url('cover.jpg'), title: art.title, tag: art.strings.kids }), workDir, { cssWidth: THUMB_WIDTH, cssHeight: THUMB_HEIGHT, scale: 1 });
  ffmpeg(['-i', thumb, '-q:v', '3', join(workDir, 'thumbnail.jpg')]);
  writeFileSync(join(workDir, 'meta.json'), JSON.stringify(videoMetadata(story, language), null, 2));
}

function verdict({ story, language, groups, checkStills, timeline, target, hasVoice, maxPages }) {
  const entries = groups.flatMap((g) => g.pieces);
  const problems = checkVideo({ entries, stills: checkStills, audio: timeline.audio });
  const video = probeVideo(target);
  if (video.width !== VIDEO_WIDTH || video.height !== VIDEO_HEIGHT) problems.push(`size ${video.width}x${video.height}, expected ${VIDEO_WIDTH}x${VIDEO_HEIGHT}`);
  if (video.frames !== timeline.totalFrames) problems.push(`${video.frames} video frames, the timeline has ${timeline.totalFrames}`);
  if (Math.abs(video.seconds - timeline.totalSec) > 0.25) problems.push(`length ${video.seconds.toFixed(2)} s, the audio plan is ${timeline.totalSec.toFixed(2)} s`);
  const notes = [hasVoice ? '' : 'no spoken card lines', Number.isFinite(maxPages) ? `SAMPLE first ${maxPages} pages` : ''].filter(Boolean).join('; ');
  const line = `${problems.length ? 'FAIL' : 'PASS'} ${story.stem} ${language} ${video.width}x${video.height} ${video.seconds.toFixed(1)}s ${video.frames}f ${entries.length} sentences${notes ? ` (${notes})` : ''}${problems.length ? ` :: ${problems.slice(0, 3).join(' | ')}` : ` [checked: the output matches the render inputs (page HTML and audio plan against the manifest, file size, frame count, length); the picture pixels are not compared]`}`;
  return { pass: problems.length === 0, line, problems, dir: join(target, '..') };
}
