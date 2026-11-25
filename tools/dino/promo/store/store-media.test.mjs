import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { tmpdir } from 'node:os';
import { assertCutFits, clipTiming, cutArgs, cutStart, encodeArgs, loadConfig, markName, markText, readMark, recordingName } from './store-media.mjs';

test('the cut starts beforeMark seconds before the mark, never below 0', () => {
    assert.ok(Math.abs(cutStart(6.2, 3) - 3.2) < 1e-9);
    assert.equal(cutStart(0.4, 1), 0);
    assert.equal(cutStart(2, 0), 2);
    assert.throws(() => cutStart(-1, 0));
    assert.throws(() => cutStart(2, -0.5));
    assert.throws(() => cutStart(NaN, 1));
});

test('the mark file sits beside its recording: mark then span; a missing, empty or one-line (old design) one is refused', () => {
    assert.equal(markName('store-dino-iphone-6.9-video-1a-reading.mp4'), 'store-dino-iphone-6.9-video-1a-reading.mark');
    const dir = mkdtempSync(join(tmpdir(), 'store-media-'));
    writeFileSync(join(dir, 'a.mark'), markText({ mark: 2.3451, span: 7.5 }));
    writeFileSync(join(dir, 'b.mark'), '\n');
    writeFileSync(join(dir, 'old.mark'), '66.350\n');
    assert.deepEqual(readMark(join(dir, 'a.mark')), { mark: 2.345, span: 7.5 });
    assert.throws(() => readMark(join(dir, 'b.mark')), /holds no mark/);
    assert.throws(() => readMark(join(dir, 'old.mark')), /holds no span/);
    assert.throws(() => readMark(join(dir, 'c.mark')), /missing mark file/);
    rmSync(dir, { recursive: true, force: true });
});

test('a recorder that ran less than start + seconds, or a clip shorter than asked, is refused', () => {
    const cut = { start: 3, seconds: 1 };
    assert.throws(() => assertCutFits('3-language-en', 2.5, cut), /less than the cut start 3 s \+ 1 s/);
    assert.throws(() => assertCutFits('3-language-en', 3.5, cut), /less than the cut start/);
    assert.doesNotThrow(() => assertCutFits('3-language-en', 4, cut));
    assert.doesNotThrow(() => assertCutFits('3-language-en', 3.95, cut));
    assert.throws(() => assertCutFits('3-language-en', Infinity, cut, 0.4), /shorter than the 1 s/);
    assert.doesNotThrow(() => assertCutFits('3-language-en', Infinity, cut, 0.98));
});

test('a trim makes the recording constant-rate, holds its still tail, then keeps the planned length from the cut start, no sound', () => {
    const args = cutArgs('in.mp4', 'out.mp4', { start: 3, seconds: 6 });
    assert.deepEqual(args.slice(0, 3), ['-y', '-i', 'in.mp4']);
    assert.ok(!args.includes('-ss') && !args.includes('-sseof'), 'no seek: it would drop the frame on screen at the cut start');
    assert.ok(args.includes('-an'));
    assert.equal(args[args.indexOf('-vf') + 1], 'fps=30,tpad=stop_mode=clone:stop_duration=10,trim=start=3:duration=6,setpts=PTS-STARTPTS');
    assert.equal(args.at(-1), 'out.mp4');
});

test('a trim needs a start of zero or more and a positive length', () => {
    assert.doesNotThrow(() => cutArgs('in.mp4', 'out.mp4', { start: 0, seconds: 1 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { start: -1, seconds: 1 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { start: 2, seconds: 0 }));
    assert.throws(() => cutArgs('in.mp4', 'out.mp4', { seconds: 1 }));
});

// A commands.json as Maestro 2.12.0 writes it (times in ms): the setup runFlow (depth 0) with its own steps (depth 1), startRecording, the clip's steps, stopRecording.
function commands(statuses = {}) {
    const step = (kind, timestamp, duration, depth = 0) => ({ command: { [kind]: {} }, metadata: { status: statuses[kind] ?? 'COMPLETED', timestamp, duration, depth } });
    return [
        step('runFlowCommand', 1000, 50000),
        step('launchAppCommand', 1100, 9000, 1),
        step('startRecordingCommand', 51000, 400),
        step('extendedWaitUntilCommand', 51400, 1500),
        step('openLinkCommand', 52900, 700),
        step('tapOnElementCommand', 53600, 1200),
        step('stopRecordingCommand', 58000, 300),
    ];
}

test('the mark is the end of the markStep-th recorded step and the span the recorder time, both from the end of startRecording', () => {
    const one = clipTiming(commands(), 1);
    assert.ok(Math.abs(one.mark - 1.5) < 1e-9);
    assert.ok(Math.abs(one.span - 6.6) < 1e-9);
    assert.deepEqual(one.steps.map((step) => step.kind), ['extendedWaitUntilCommand', 'openLinkCommand', 'tapOnElementCommand']);
    assert.ok(Math.abs(clipTiming(commands(), 3).mark - 3.4) < 1e-9);
    assert.doesNotThrow(() => clipTiming(commands({ extendedWaitUntilCommand: 'WARNED' }), 1), 'an optional hold that timed out still counts');
});

test('a clip flow that failed, never stopped recording, or names a step it does not have is refused', () => {
    assert.throws(() => clipTiming(commands({ tapOnElementCommand: 'FAILED' }), 1), /tapOnElementCommand ended FAILED/);
    assert.throws(() => clipTiming(commands({ runFlowCommand: 'FAILED' }), 1), /runFlowCommand ended FAILED/);
    assert.throws(() => clipTiming(commands().slice(0, -1), 1), /no stopRecording/);
    assert.throws(() => clipTiming(commands().filter((entry) => !('startRecordingCommand' in entry.command)), 1), /no startRecording/);
    assert.throws(() => clipTiming(commands(), 4), /markStep 4 is not one of the 3/);
    assert.throws(() => clipTiming(commands(), 0), /markStep 0/);
    assert.throws(() => clipTiming({}, 1), /not an array/);
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

test('every clip has a recording flow with at least markStep steps, a setup flow, a markStep, a beforeMark and its seconds', () => {
    const flows = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', '.maestro', 'dino');
    const config = loadConfig();
    assert.equal(config.leadIn, undefined, 'leadIn is replaced by a per-clip beforeMark');
    for (const [clip, cut] of Object.entries(config.clips)) {
        assert.deepEqual(Object.keys(cut), ['markStep', 'beforeMark', 'seconds'], clip);
        assert.ok(cut.beforeMark >= 0, `${clip}: beforeMark`);
        const flow = join(flows, `store-shot-ios-video-${clip}.yaml`);
        assert.ok(existsSync(flow), `${clip}: the recorded flow`);
        const body = readFileSync(flow, 'utf8').split(/\r?\n---\r?\n/)[1] ?? '';
        const steps = body.split(/\r?\n/).filter((line) => /^- /.test(line)).length;
        assert.ok(Number.isInteger(cut.markStep) && cut.markStep >= 1 && cut.markStep <= steps, `${clip}: markStep ${cut.markStep} of ${steps} top-level steps`);
        assert.ok(!/startRecording|stopRecording/.test(body), `${clip}: the script adds the recording steps`);
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
