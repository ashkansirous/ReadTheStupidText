import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { tmpdir } from 'node:os';
import { assertCutFits, cutArgs, cutStart, encodeArgs, loadConfig, markName, readMark, recordingName } from './store-media.mjs';

test('the cut starts beforeMark seconds before the mark, never below 0', () => {
    assert.ok(Math.abs(cutStart(6.2, 3) - 3.2) < 1e-9);
    assert.equal(cutStart(0.4, 1), 0);
    assert.equal(cutStart(2, 0), 2);
    assert.throws(() => cutStart(-1, 0));
    assert.throws(() => cutStart(2, -0.5));
    assert.throws(() => cutStart(NaN, 1));
});

test('the mark file sits beside its recording, and a missing or empty one is refused', () => {
    assert.equal(markName('store-dino-iphone-6.9-video-1a-reading.mp4'), 'store-dino-iphone-6.9-video-1a-reading.mark');
    const dir = mkdtempSync(join(tmpdir(), 'store-media-'));
    writeFileSync(join(dir, 'a.mark'), '2.345\n');
    writeFileSync(join(dir, 'b.mark'), '\n');
    assert.equal(readMark(join(dir, 'a.mark')), 2.345);
    assert.throws(() => readMark(join(dir, 'b.mark')), /holds no mark/);
    assert.throws(() => readMark(join(dir, 'c.mark')), /missing mark file/);
    rmSync(dir, { recursive: true, force: true });
});

test('a recording shorter than start + seconds, or a clip shorter than asked, is refused', () => {
    const cut = { start: 3, seconds: 1 };
    assert.throws(() => assertCutFits('3-language-en', 2.5, cut), /shorter than the cut start 3 s \+ 1 s/);
    assert.throws(() => assertCutFits('3-language-en', 3.5, cut), /shorter than the cut start/);
    assert.doesNotThrow(() => assertCutFits('3-language-en', 4, cut));
    assert.doesNotThrow(() => assertCutFits('3-language-en', 3.95, cut));
    assert.throws(() => assertCutFits('3-language-en', Infinity, cut, 0.4), /shorter than the 1 s/);
    assert.doesNotThrow(() => assertCutFits('3-language-en', Infinity, cut, 0.98));
});

test('a trim skips to the cut start, keeps the planned length and drops the sound', () => {
    const args = cutArgs('in.mp4', 'out.mp4', { start: 3, seconds: 6 });
    assert.deepEqual(args.slice(0, 5), ['-y', '-ss', '3', '-i', 'in.mp4']);
    assert.ok(!args.includes('-sseof'), 'no cutting from the end of the recording');
    assert.ok(args.includes('-an'));
    assert.equal(args[args.indexOf('-t') + 1], '6');
    assert.equal(args.at(-1), 'out.mp4');
});

test('a trim needs a start of zero or more and a positive length', () => {
    assert.doesNotThrow(() => cutArgs('in.mp4', 'out.mp4', { start: 0, seconds: 1 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { start: -1, seconds: 1 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { start: 2, seconds: 0 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { seconds: 1 }));
});

test('the encode is Apple spec: exact size, 30 fps, h264 about 11 Mbps, stereo AAC 256 kbps at 48 kHz', () => {
    const args = encodeArgs('r.mp4', 'o.mp4', { width: 886, height: 1920 });
    const value = (flag) => args[args.indexOf(flag) + 1];
    assert.equal(value('-vf'), 'scale=886:1920:flags=lanczos,fps=30');
    assert.equal(value('-c:v'), 'libx264');
    assert.equal(value('-b:v'), '11M');
    assert.equal(value('-maxrate'), '12M');
    assert.equal(value('-c:a'), 'aac');
    assert.equal(value('-b:a'), '256k');
    assert.equal(value('-ac'), '2');
    assert.equal(value('-ar'), '48000');
});

test('the recording of a device label and clip', () => {
    assert.equal(recordingName('iphone-6.9', '3-language-es'), 'store-dino-iphone-6.9-video-3-language-es.mp4');
});

test('every clip has a recording flow and a setup flow, a beforeMark and its seconds on one line the shell script can read', () => {
    const flows = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '.maestro', 'dino');
    const config = loadConfig();
    assert.equal(config.leadIn, undefined, 'leadIn is replaced by a per-clip beforeMark');
    const lines = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'footage-cuts.json'), 'utf8').split('\n');
    for (const [clip, cut] of Object.entries(config.clips)) {
        assert.deepEqual(Object.keys(cut), ['beforeMark', 'seconds'], clip);
        assert.ok(cut.beforeMark >= 0, `${clip}: beforeMark`);
        assert.ok(lines.some((line) => new RegExp(`^ *"${clip}": *\\{ *"beforeMark": *[0-9.]+, *"seconds": *[0-9.]+ *\\}`).test(line)), `${clip}: one line for sed`);
        assert.ok(existsSync(join(flows, `store-shot-ios-video-${clip}.yaml`)), `${clip}: the recorded flow`);
        assert.ok(existsSync(join(flows, `store-shot-ios-videoprep-${clip}.yaml`)), `${clip}: the unrecorded setup flow`);
    }
});

test('the cuts file holds the note timeline: 6 + 4 + three 1 s language clips + 3 s of shelf = 16 s before the 4 s end frame', () => {
    const config = loadConfig();
    const total = Object.values(config.clips).reduce((sum, cut) => sum + cut.seconds, 0);
    assert.equal(total, 6 + 4 + 3 + 3);
    for (const cut of Object.values(config.clips)) assert.ok(cut.seconds <= 4, 'no shot longer than about 4 s (the note)');
    assert.deepEqual(Object.keys(config.devices).sort(), ['ipad', 'iphone']);
    assert.equal(config.devices.iphone.width + 'x' + config.devices.iphone.height, '886x1920');
    assert.equal(config.devices.ipad.width + 'x' + config.devices.ipad.height, '1600x1200');
});
