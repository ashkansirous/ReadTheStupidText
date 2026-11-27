// Slice 435: renders the free books as read-aloud videos. No agent per video: one run, one pass/fail line per video, one contact sheet per book.
//
//   node render.mjs --out <folder> [--book <stem,...>|all] [--lang <code,...>|all] [--no-qr] [--max-pages N]
//
// The free list is read from the live store at run time. Exit code 1 when any video fails its check. A failing video is retried once, then reported.

import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { valueOf } from './lib/args.mjs';
import { renderVideo } from './lib/render-video.mjs';
import { LANGUAGES } from './lib/strings.mjs';
import { fetchFreeStories, stemOfId } from './lib/story.mjs';
import { concatFileLine, ffmpeg } from './lib/tools.mjs';

const CONTENT_ROOT = fileURLToPath(new URL('../../../../content/dino/', import.meta.url));
const SHEET_COLUMNS = 5;
const SHEET_TILE_WIDTH = 384;

export function parseArgs(argv) {
  const options = { book: 'all', lang: 'all', noQr: false, maxPages: Infinity, out: null };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--no-qr') options.noQr = true;
    else if (['--book', '--lang', '--out', '--max-pages'].includes(arg)) {
      const value = valueOf(argv, i, arg);
      i += 1;
      if (arg === '--max-pages') options.maxPages = Number(value);
      else options[arg.slice(2)] = value;
    }
    else throw new Error(`Unknown argument ${arg}`);
  }
  if (options.maxPages !== Infinity && !(Number.isInteger(options.maxPages) && options.maxPages >= 1)) throw new Error('--max-pages must be a whole number of 1 or more.');
  if (!options.out) throw new Error('--out <folder> is required (use tmp/ inside the worktree).');
  return options;
}

const listOf = (value, all) => (value === 'all' ? all : value.split(',').map((v) => v.trim()).filter(Boolean));

async function renderOnce(job) {
  try {
    return await renderVideo(job);
  } catch (error) {
    return { pass: false, line: `FAIL ${job.story.stem} ${job.language} :: ${error.message.split('\n')[0]}`, problems: [error.message], error: true };
  }
}

async function renderWithOneRetry(job) {
  const first = await renderOnce(job);
  if (first.pass || !first.error) return first;
  return renderOnce(job);
}

function contactSheet(outRoot, stem, results) {
  const thumbs = results.filter((r) => r.dir).map((r) => join(r.dir, 'thumbnail.jpg'));
  if (thumbs.length === 0) return null;
  const list = join(outRoot, stem, 'contact-list.ffconcat');
  writeFileSync(list, ['ffconcat version 1.0', ...thumbs.flatMap((t) => [concatFileLine(t), 'duration 1'])].join('\n'));
  const sheet = join(outRoot, stem, 'contact-sheet.jpg');
  const rows = Math.ceil(thumbs.length / SHEET_COLUMNS);
  ffmpeg(['-f', 'concat', '-safe', '0', '-i', list, '-vf', `fps=1,scale=${SHEET_TILE_WIDTH}:-1,tile=${SHEET_COLUMNS}x${rows}`, '-frames:v', '1', '-q:v', '3', sheet]);
  return sheet;
}

export async function main(argv) {
  const options = parseArgs(argv);
  const outRoot = resolve(options.out);
  const free = (await fetchFreeStories()).map((s) => ({ ...s, stem: stemOfId(CONTENT_ROOT, s.id) }));
  const stories = listOf(options.book, free.map((s) => s.stem)).map((stem) => free.find((s) => s.stem === stem) ?? (() => { throw new Error(`${stem} is not a free book now; free: ${free.map((s) => s.stem).join(', ')}`); })());
  let failed = 0;
  for (const story of stories) {
    mkdirSync(join(outRoot, story.stem), { recursive: true });
    const results = [];
    for (const language of listOf(options.lang, LANGUAGES)) {
      const result = await renderWithOneRetry({ story, language, outRoot, contentRoot: CONTENT_ROOT, noQr: options.noQr, maxPages: options.maxPages });
      console.log(result.line);
      if (!result.pass) failed += 1;
      results.push(result);
    }
    const sheet = contactSheet(outRoot, story.stem, results);
    if (sheet) console.log(`contact sheet ${story.stem}: ${sheet}`);
  }
  return failed === 0 ? 0 : 1;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).then((code) => process.exit(code), (error) => { console.error(error.message); process.exit(1); });
}
