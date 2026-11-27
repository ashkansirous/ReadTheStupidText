// The outside programs the renderer runs: ffmpeg, ffprobe, headless Chrome, git lfs. Each call is one process, waits for it,
// and fails loudly with the program's own stderr. ffmpeg/ffprobe/Chrome are found through $FFMPEG, $FFPROBE, $CHROME or the usual places.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { basename, dirname, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const LFS_POINTER_PREFIX = 'version https://git-lfs.github.com/spec/v1';
const CHROME_CANDIDATES = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
];
const MAX_BUFFER = 1024 * 1024 * 1024;
const PROCESS_TIMEOUT_MS = 30 * 60 * 1000;

export function run(program, args, options = {}) {
  const result = spawnSync(program, args, { encoding: options.binary ? 'buffer' : 'utf8', maxBuffer: MAX_BUFFER, timeout: PROCESS_TIMEOUT_MS, windowsHide: true, ...options.spawn });
  if (result.error) throw new Error(`${program} could not run: ${result.error.message}`);
  if (result.status !== 0) throw new Error(`${program} exited ${result.status}: ${String(result.stderr).slice(-1500)}`);
  return result.stdout;
}

export const ffmpeg = (args, options) => run(process.env.FFMPEG || 'ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', ...args], options);
export const ffprobe = (args) => run(process.env.FFPROBE || 'ffprobe', ['-v', 'error', ...args]);

export function chromePath() {
  const found = process.env.CHROME || CHROME_CANDIDATES.find((p) => existsSync(p));
  if (!found) throw new Error('No Chrome or Edge found; set $CHROME to its executable.');
  return found;
}

/** A file's bytes, with a Git LFS pointer replaced by the real object (`git lfs smudge`, as tools/dino/promo/linkedin does). */
export function readLfs(path) {
  const bytes = readFileSync(path);
  if (!bytes.subarray(0, LFS_POINTER_PREFIX.length).toString('latin1').startsWith(LFS_POINTER_PREFIX)) return bytes;
  const real = run('git', ['lfs', 'smudge'], { binary: true, spawn: { input: bytes, cwd: dirname(path) } });
  if (real.subarray(0, LFS_POINTER_PREFIX.length).toString('latin1').startsWith(LFS_POINTER_PREFIX)) {
    throw new Error(`${path} is still an LFS pointer after smudge; run \`git lfs pull\` for it.`);
  }
  return real;
}

/**
 * Writes an LFS-aware copy of `source` next to `target`'s name, prefixed with a hash of the source file's own bytes (an LFS pointer
 * holds the object's sha256, so it changes with the content), once, and returns that path. A re-illustrated picture or a
 * regenerated clip therefore never reuses the old copy in a reused --out folder.
 */
export function materialise(source, target) {
  const key = createHash('sha1').update(readFileSync(source)).digest('hex').slice(0, 12);
  target = join(dirname(target), `${key}-${basename(target)}`);
  if (!existsSync(target)) {
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, readLfs(source));
  }
  return target;
}

/** One `file '<path>'` line of an ffconcat list: forward slashes, and an apostrophe escaped the ffmpeg way ('path'\''s'). */
export function concatFileLine(path) {
  return `file '${path.replace(/\\/g, '/').replace(/'/g, "'\\''")}'`;
}

export function durationSeconds(file) {
  const seconds = Number(ffprobe(['-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', file]).trim());
  if (!Number.isFinite(seconds) || seconds <= 0) throw new Error(`ffprobe gave no duration for ${file}`);
  return seconds;
}

/** One Chrome capture: `htmlPath` to a PNG of `width` x `height` pixels (the page is laid out at width/scale CSS pixels). */
export function screenshot(htmlPath, pngPath, { cssWidth, cssHeight, scale, profileDir }) {
  mkdirSync(dirname(pngPath), { recursive: true });
  run(chromePath(), [
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--no-default-browser-check', `--user-data-dir=${profileDir}`,
    `--force-device-scale-factor=${scale}`, `--window-size=${cssWidth},${cssHeight}`, '--virtual-time-budget=3000',
    `--screenshot=${pngPath}`, pathToFileURL(htmlPath).href,
  ]);
  if (!existsSync(pngPath)) throw new Error(`Chrome wrote no screenshot for ${htmlPath}`);
}
