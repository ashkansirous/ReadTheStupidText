// Slice 447: the true-size line-up script and the accepted-page save guard, run for real (python -I with Pillow; pwsh).
// Run: node --test ".claude/skills/create-dino-story/*.test.mjs"
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const LINEUP = path.join(HERE, 'true-size-lineup.py');
const TAKE = path.join(HERE, 'gemini-take-download.ps1');

const has = (cmd, args) => spawnSync(cmd, args, { encoding: 'utf8' }).status === 0;
const PIL = has('python', ['-I', '-c', 'import PIL']);
const PWSH = has('pwsh', ['-NoProfile', '-Command', '$PSVersionTable.PSVersion.Major']);
const tmp = prefix => fs.mkdtempSync(path.join(os.tmpdir(), prefix));
const py = (...args) => spawnSync('python', ['-I', ...args], { encoding: 'utf8' });

// Three same-height figures (the first has an ear detached by a 6 px gap) on a white row, then the script rescales them.
const MAKE_ROW = `
import sys
from PIL import Image, ImageDraw
im = Image.new("RGB", (1200, 400), "white")
d = ImageDraw.Draw(im)
d.rectangle((50, 60, 250, 350), fill=(200, 80, 20))
d.rectangle((130, 40, 170, 54), fill=(200, 80, 20))
d.rectangle((400, 50, 500, 350), fill=(200, 80, 20))
d.rectangle((700, 50, 900, 350), fill=(20, 80, 200))
im.save(sys.argv[1])
`;

test('line-up: three figures found, each rescaled to its height in Carrot units', { skip: !PIL && 'Pillow not installed' }, () => {
  const dir = tmp('lineup-');
  const row = path.join(dir, 'row.png');
  const out = path.join(dir, 'out.png');
  assert.equal(py('-c', MAKE_ROW, row).status, 0);
  const run = py(LINEUP, row, out, '--names', 'Carrot,Mouse,Owl', '--heights', '1,0.5,0.25', '--carrot-px', '400');
  assert.equal(run.status, 0, run.stderr);
  const measure = py('-c', `
from PIL import Image, ImageChops
im = Image.open(${JSON.stringify(out)}).convert("RGB")
import sys
cols = [x for x in range(im.width) if any(im.getpixel((x, y)) != (255, 255, 255) for y in range(0, im.height, 4))]
runs, start = [], cols[0]
for a, b in zip(cols, cols[1:] + [None]):
    if b is None or b - a > 3:
        runs.append((start, a)); start = b
heights = []
for l, r in runs:
    ys = [y for y in range(im.height) if im.getpixel(((l + r) // 2, y)) != (255, 255, 255)]
    heights.append(max(ys) - min(ys) + 1)
print(heights)
`);
  assert.equal(measure.stdout.trim(), '[400, 200, 100]', measure.stderr);
});

test('line-up: a wrong figure count writes nothing and exits 2', { skip: !PIL && 'Pillow not installed' }, () => {
  const dir = tmp('lineup-');
  const row = path.join(dir, 'row.png');
  const out = path.join(dir, 'out.png');
  assert.equal(py('-c', MAKE_ROW, row).status, 0);
  const run = py(LINEUP, row, out, '--names', 'A,B', '--heights', '1,1');
  assert.equal(run.status, 2);
  assert.match(run.stderr, /found 3 figures, expected 2/);
  assert.equal(fs.existsSync(out), false);
});

function book() {
  const dir = tmp('book-');
  fs.mkdirSync(path.join(dir, 'high-res'));
  fs.writeFileSync(path.join(dir, 'art.md'),
    '# Art\n\n## Accepted by the user\n\n| Page | File | Bytes |\n| --- | --- | --- |\n| 3 | high-res/3.jpg | 5 |\n\n## Next\n\n| 4 | high-res/4.jpg | 5 |\n');
  fs.writeFileSync(path.join(dir, 'high-res', '3.jpg'), 'ACCEPTED');
  return dir;
}

// Always an empty temp Downloads folder (and backup folder), never the user's: a real Gemini download must not be touched.
const take = (target, ...flags) => {
  const downloads = tmp('downloads-');
  return spawnSync('pwsh', ['-NoProfile', '-File', TAKE, target, '-WaitSeconds', '1', '-DownloadsFolder', downloads, '-BackupFolder', tmp('backups-'), ...flags], { encoding: 'utf8' });
};

test('save guard: an accepted page is refused without -UserAskedRedo and the file is unchanged', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = book();
  const target = path.join(dir, 'high-res', '3.jpg');
  const run = take(target);
  assert.notEqual(run.status, 0);
  assert.match(run.stderr + run.stdout, /REFUSED/);
  assert.equal(fs.readFileSync(target, 'utf8'), 'ACCEPTED');
  assert.equal(fs.existsSync(path.join(dir, 'high-res', 'previous')), false);
});

test('save guard: the same page under another extension is refused too', { skip: !PWSH && 'pwsh not installed' }, () => {
  const run = take(path.join(book(), 'high-res', '3.png'), '-Untouched');
  assert.notEqual(run.status, 0);
  assert.match(run.stderr + run.stdout, /REFUSED/);
});

test('save guard: a page below the accepted list is not refused (it only fails for want of a download)', { skip: !PWSH && 'pwsh not installed' }, () => {
  const run = take(path.join(book(), 'high-res', '4.jpg'));
  assert.notEqual(run.status, 0);
  assert.doesNotMatch(run.stderr + run.stdout, /REFUSED/);
  assert.match(run.stderr + run.stdout, /No Gemini download/);
});

test('save guard: -UserAskedRedo passes the guard', { skip: !PWSH && 'pwsh not installed' }, () => {
  const run = take(path.join(book(), 'high-res', '3.jpg'), '-UserAskedRedo');
  assert.doesNotMatch(run.stderr + run.stdout, /REFUSED/);
});

function bookWith(artText) {
  const dir = book();
  fs.writeFileSync(path.join(dir, 'art.md'), artText);
  return dir;
}

test('save guard: a dated heading and a bullet list still count as the accepted list', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = bookWith('# Art\n\n## Accepted by the user (2026-11-20)\n\n- page 3: high-res/3.jpg, 3196433 bytes\n');
  const run = take(path.join(dir, 'high-res', '3.jpg'));
  assert.match(run.stderr + run.stdout, /REFUSED/);
});

test('save guard: an accepted section that cannot be parsed refuses the save (fails closed)', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = bookWith('# Art\n\n## Accepted by the user\n\nPage three is fine, page five too.\n');
  const target = path.join(dir, 'high-res', '3.jpg');
  const run = take(target);
  assert.notEqual(run.status, 0);
  assert.match(run.stderr + run.stdout, /REFUSED[^]*picture\s+file\s+name/);
  assert.equal(fs.readFileSync(target, 'utf8'), 'ACCEPTED');
});

test('save guard: an empty or "none yet" accepted section refuses nothing', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = bookWith('# Art\n\n## Accepted by the user\n\nNone yet.\n');
  const run = take(path.join(dir, 'high-res', '3.jpg'));
  assert.doesNotMatch(run.stderr + run.stdout, /REFUSED/);
});

test('failed -Untouched redo: the page keeps its picture and the download goes back to Downloads', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = book();
  const target = path.join(dir, 'high-res', '3.jpg');
  const downloads = tmp('downloads-');
  const backups = tmp('backups-');
  fs.writeFileSync(path.join(downloads, 'Gemini_Generated_Image_x.png'), 'not a picture');
  const run = spawnSync('pwsh', ['-NoProfile', '-File', TAKE, target, '-WaitSeconds', '2', '-Untouched', '-UserAskedRedo', '-DownloadsFolder', downloads, '-BackupFolder', backups], { encoding: 'utf8' });
  assert.notEqual(run.status, 0);
  assert.equal(fs.readFileSync(target, 'utf8'), 'ACCEPTED');
  assert.equal(fs.existsSync(path.join(downloads, 'Gemini_Generated_Image_x.png')), true);
  assert.equal(fs.readFileSync(path.join(backups, '3.jpg'), 'utf8'), 'ACCEPTED');
});

test('-Untouched redo: the new picture replaces the page, the old extension goes only afterwards, no temp file is left', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = book();
  const target = path.join(dir, 'high-res', '3.jpg');
  const downloads = tmp('downloads-');
  const backups = tmp('backups-');
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==', 'base64');
  fs.writeFileSync(path.join(downloads, 'Gemini_Generated_Image_x.png'), png);
  spawnSync('pwsh', ['-NoProfile', '-File', TAKE, target, '-WaitSeconds', '3', '-Untouched', '-UserAskedRedo', '-DownloadsFolder', downloads, '-BackupFolder', backups], { encoding: 'utf8' });
  assert.equal(fs.existsSync(path.join(dir, 'high-res', '3.png')), true);
  assert.equal(fs.existsSync(target), false);
  assert.deepEqual(fs.readdirSync(path.join(dir, 'high-res')).filter((f) => f.includes('.new-')), []);
  assert.equal(fs.readFileSync(path.join(backups, '3.jpg'), 'utf8'), 'ACCEPTED');
});

test('backup: a save into book content without -BackupFolder is refused before anything is touched', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = book();
  const target = path.join(dir, 'high-res', '3.jpg');
  const downloads = tmp('downloads-');
  fs.writeFileSync(path.join(downloads, 'Gemini_Generated_Image_x.png'), 'x');
  const run = spawnSync('pwsh', ['-NoProfile', '-File', TAKE, target, '-WaitSeconds', '1', '-UserAskedRedo', '-DownloadsFolder', downloads], { encoding: 'utf8' });
  assert.notEqual(run.status, 0);
  assert.match(run.stderr + run.stdout, /REFUSED[^]*BackupFolder/);
  assert.equal(fs.readFileSync(target, 'utf8'), 'ACCEPTED');
  assert.equal(fs.existsSync(path.join(dir, 'high-res', 'previous')), false);
  assert.equal(fs.existsSync(path.join(downloads, 'Gemini_Generated_Image_x.png')), true);
});

test('backup: two replacements in the same second keep both copies', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = tmp('backup-');
  const target = path.join(dir, 'cand-1.jpg');
  fs.writeFileSync(target, 'ONE');
  const script = `. '${path.join(HERE, 'accepted-pages.ps1')}'; 1..3 | ForEach-Object { Backup-ReplacedFiles '${target}' | Out-Null }`;
  assert.equal(spawnSync('pwsh', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).status, 0);
  assert.equal(fs.readdirSync(path.join(dir, 'previous')).length, 3);
});

test('line-up: an even --gap is rounded up, not a traceback', { skip: !PIL && 'Pillow not installed' }, () => {
  const dir = tmp('lineup-');
  const row = path.join(dir, 'row.png');
  assert.equal(py('-c', MAKE_ROW, row).status, 0);
  const run = py(LINEUP, row, path.join(dir, 'out.png'), '--names', 'A,B,C', '--heights', '1,1,1', '--gap', '10');
  assert.equal(run.status, 0, run.stderr);
  assert.match(run.stderr, /even; using 11/);
});

test('backup: a replaced file is copied to previous/ and an earlier copy is never overwritten', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = tmp('backup-');
  const target = path.join(dir, 'cand-1.jpg');
  fs.writeFileSync(target, 'ONE');
  const script = `. '${path.join(HERE, 'accepted-pages.ps1')}'; Backup-ReplacedFiles '${target}' | Out-Null; Set-Content '${target}' TWO; Backup-ReplacedFiles '${target}' | Out-Null`;
  assert.equal(spawnSync('pwsh', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).status, 0);
  const kept = fs.readdirSync(path.join(dir, 'previous')).sort();
  assert.equal(kept.length, 2);
  assert.equal(fs.readFileSync(path.join(dir, 'previous', 'cand-1.jpg'), 'utf8'), 'ONE');
});

test('save guard: a bare file name or a repo-relative path in the list still matches the page', { skip: !PWSH && 'pwsh not installed' }, () => {
  for (const entry of ['- page 3: 3.jpg, 3196433 bytes', '| 3 | content/dino/foo/high-res/3.jpg | 5 |']) {
    const dir = bookWith(`# Art\n\n## Accepted by the user\n\n${entry}\n`);
    const target = path.join(dir, 'high-res', '3.jpg');
    const run = take(target);
    assert.match(run.stderr + run.stdout, /REFUSED/, entry);
    assert.equal(fs.readFileSync(target, 'utf8'), 'ACCEPTED');
  }
});

test('save guard: prose that merely contains "nothing" is not an empty list', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = bookWith('# Art\n\n## Accepted by the user\n\nPage three accepted, nothing to redo.\n');
  const run = take(path.join(dir, 'high-res', '3.jpg'));
  assert.match(run.stderr + run.stdout, /REFUSED/);
});

test('save guard: a relative -BackupFolder follows the PowerShell location', { skip: !PWSH && 'pwsh not installed' }, () => {
  const dir = tmp('backup-');
  const target = path.join(dir, 'cand-1.jpg');
  fs.writeFileSync(target, 'ONE');
  const script = `. '${path.join(HERE, 'accepted-pages.ps1')}'; Set-Location '${dir}'; Backup-ReplacedFiles '${target}' 'kept' | Out-Null`;
  assert.equal(spawnSync('pwsh', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).status, 0);
  assert.equal(fs.readdirSync(path.join(dir, 'kept')).length, 1);
});

test('line-up: bad --heights exit 2 with a message, not a traceback', { skip: !PIL && 'Pillow not installed' }, () => {
  const dir = tmp('lineup-');
  const row = path.join(dir, 'row.png');
  assert.equal(py('-c', MAKE_ROW, row).status, 0);
  for (const heights of ['1,0.3,', '1,0,0.6', '1,-1,2', '1,inf,2', '1,nan,2', '1,1e9,2']) {
    const run = py(LINEUP, row, path.join(dir, 'out.png'), '--names', 'A,B,C', '--heights', heights);
    assert.equal(run.status, 2, heights);
    assert.doesNotMatch(run.stderr, /Traceback/);
  }
});
