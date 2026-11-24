import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertCutFits, cutArgs, encodeArgs, loadConfig, recordingName } from './store-media.mjs';

test('a recording shorter than leadIn + seconds, or a clip shorter than asked, is refused', () => {
    const cut = { leadIn: 3, seconds: 1 };
    assert.throws(() => assertCutFits('3-language-en', 2.5, cut), /shorter than leadIn 3 s \+ 1 s/);
    assert.throws(() => assertCutFits('3-language-en', 3.5, cut), /shorter than leadIn/);
    assert.doesNotThrow(() => assertCutFits('3-language-en', 4, cut));
    assert.doesNotThrow(() => assertCutFits('3-language-en', 3.95, cut));
    assert.throws(() => assertCutFits('3-language-en', Infinity, cut, 0.4), /shorter than the 1 s/);
    assert.doesNotThrow(() => assertCutFits('3-language-en', Infinity, cut, 0.98));
});

test('a trim skips the fixed lead-in, keeps the planned length and drops the sound', () => {
    const args = cutArgs('in.mp4', 'out.mp4', { leadIn: 3, seconds: 6 });
    assert.deepEqual(args.slice(0, 5), ['-y', '-ss', '3', '-i', 'in.mp4']);
    assert.ok(!args.includes('-sseof'), 'no cutting from the end of the recording');
    assert.ok(args.includes('-an'));
    assert.equal(args[args.indexOf('-t') + 1], '6');
    assert.equal(args.at(-1), 'out.mp4');
});

test('a trim needs a lead-in of zero or more and a positive length', () => {
    assert.doesNotThrow(() => cutArgs('in.mp4', 'out.mp4', { leadIn: 0, seconds: 1 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { leadIn: -1, seconds: 1 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { leadIn: 2, seconds: 0 }));
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

test('every clip has a recording flow and a setup flow, one shared leadIn, and a clip holds only its seconds', () => {
    const flows = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '.maestro', 'dino');
    const config = loadConfig();
    assert.ok(config.leadIn > 0 && config.leadIn <= 1, 'leadIn covers only the recorder start-up');
    for (const [clip, cut] of Object.entries(config.clips)) {
        assert.deepEqual(Object.keys(cut), ['seconds'], clip);
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
