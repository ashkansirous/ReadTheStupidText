// Slice 433 item 6: the ffmpeg steps around the HyperFrames render of the App Store previews, and the mark and check of each recording on CI.
//   node store-media.mjs cut <iphone|ipad> <folder of the downloaded recordings>
//       trims each clip of footage-cuts.json (its own recording store-dino-<device label>-video-<clip>.mp4, recorded inside its Maestro flow with startRecording) into assets/footage/<device>/<clip>.mp4 (silent, h264, 30 fps): from max(0, mark - beforeMark) for `seconds`; mark (line 1) and span (line 2) come from the recording's store-dino-<device label>-video-<clip>.mark
//   node store-media.mjs mark <commands.json> <clip> <out.mark>
//       writes that mark file from the clip flow's Maestro commands.json: mark = end of the clip's markStep-th recorded step, span = how long the recorder ran, both counted from the end of startRecording (run by ios-sim-store-shots.sh)
//   node store-media.mjs check <recording.mp4> <clip>
//       exits 0 only when ffprobe (required, never skipped) reads the video to the end and the span covers the cut (ios-sim-store-shots.sh, before a recording is kept)
//   node store-media.mjs encode <iphone|ipad> <rendered mp4> <output mp4>
//       encodes the render to Apple's app preview spec: exact size, 30 fps, H.264 high profile at about 11 Mbps (10-12), stereo AAC 256 kbps at 48 kHz, faststart
// Then check the result with tools/emulator/store-size-check.ps1 -Target preview-iphone|preview-ipad -Path <output>.
import { spawnSync } from 'node:child_process';
import { readFileSync, mkdirSync, existsSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const CUTS_FILE = join(HERE, 'footage-cuts.json');
const CUT_TOLERANCE_SECONDS = 0.1;

// Pure: where a clip's cut starts in its recording: `beforeMark` seconds before the mark (the flow's first step-end line), never below 0.
export function cutStart(mark, beforeMark) {
    if (!(mark >= 0) || !(beforeMark >= 0)) throw new Error(`a cut point needs mark >= 0 and beforeMark >= 0 (got mark ${mark}, beforeMark ${beforeMark})`);
    return Math.max(0, mark - beforeMark);
}

// Pure: the ffmpeg arguments that trim a clip's own recording: keep `seconds` from `start`, no audio.
// simctl recordVideo writes a frame only when the screen changes and ends the file at the LAST change (iPad run 45582715957: 3-language-en has
// frames at 0, 27.6 and 29.4 s while its page was held to 66 s). So the recording is first made constant-rate (fps=30 repeats each frame until
// the next change), its still tail is held for start + seconds more (tpad clone), and only then trimmed by timestamp: a seek (-ss) would drop
// the frame that is on screen at `start` when it was written earlier.
export function cutArgs(input, output, { start, seconds }) {
    if (!(start >= 0) || !(seconds > 0)) throw new Error(`a trim needs start >= 0 and seconds > 0 (got start ${start}, seconds ${seconds})`);
    const hold = Math.ceil(start + seconds) + 1;
    const filter = `fps=30,tpad=stop_mode=clone:stop_duration=${hold},trim=start=${start}:duration=${seconds},setpts=PTS-STARTPTS`;
    return ['-y', '-i', input, '-an', '-vf', filter, '-c:v', 'libx264', '-crf', '12', '-pix_fmt', 'yuv420p', output];
}

// The Maestro step results that let a recorded clip count (an optional wait that timed out is WARNED; a `when` that did not apply is SKIPPED).
const STEP_OK = new Set(['COMPLETED', 'WARNED', 'SKIPPED']);

function commandKind(entry) {
    return Object.keys(entry?.command ?? {})[0] ?? '?';
}

function stepEnd(entry) {
    const { timestamp, duration } = entry.metadata ?? {};
    if (!Number.isFinite(timestamp)) throw new Error(`step ${commandKind(entry)} has no timestamp in commands.json`);
    return timestamp + (Number.isFinite(duration) ? duration : 0);
}

// Pure: the mark and span of a clip recorded inside its flow, from Maestro's commands.json (an array of { command: { <kind>Command: ... }, metadata: { status, timestamp (epoch ms at the step's start), duration (ms), depth } }; Maestro 2.12.0).
// Only top-level steps (depth 0) count. t = 0 of the recording is the end of the startRecording step; the mark is the end (markAt 'end') or the start (markAt 'start')
// of the `markStep`-th step after it (1 = the first recorded step); the span is how long the recorder ran (to the start of stopRecording). Throws, naming why, when the
// flow did not reach stopRecording or a step before it failed, so the clip is retaken.
// iPad run 45597845229: an openLink hook step took about 3 s and the app acted about 0.9 s after the step STARTED (the page turn at 3.91 s, step 2.98-6.15 s),
// so an action that a hook starts is marked from its step's start.
export function clipTiming(entries, markStep, markAt = 'end') {
    if (markAt !== 'end' && markAt !== 'start') throw new Error(`markAt must be 'start' or 'end', not '${markAt}'`);
    if (!Array.isArray(entries)) throw new Error('commands.json is not an array of steps');
    const top = entries.filter((entry) => (entry?.metadata?.depth ?? 0) === 0);
    const startAt = top.findIndex((entry) => commandKind(entry).startsWith('startRecording'));
    if (startAt < 0) throw new Error('no startRecording step in commands.json');
    const stopAt = top.findIndex((entry, i) => i > startAt && commandKind(entry).startsWith('stopRecording'));
    if (stopAt < 0) throw new Error('no stopRecording step after startRecording: the flow stopped before the end of the clip');
    const bad = top.slice(0, stopAt + 1).find((entry) => !STEP_OK.has(entry.metadata?.status));
    if (bad) throw new Error(`step ${commandKind(bad)} ended ${bad.metadata?.status ?? 'without a status'}${bad.metadata?.error ? `: ${JSON.stringify(bad.metadata.error).slice(0, 200)}` : ''}`);
    const steps = top.slice(startAt + 1, stopAt);
    if (!(Number.isInteger(markStep) && markStep >= 1 && markStep <= steps.length)) throw new Error(`markStep ${markStep} is not one of the ${steps.length} recorded steps`);
    const zero = stepEnd(top[startAt]);
    return {
        mark: ((markAt === 'start' ? steps[markStep - 1].metadata.timestamp : stepEnd(steps[markStep - 1])) - zero) / 1000,
        span: (top[stopAt].metadata.timestamp - zero) / 1000,
        steps: steps.map((entry) => ({ kind: commandKind(entry), status: entry.metadata.status, at: (entry.metadata.timestamp - zero) / 1000, end: (stepEnd(entry) - zero) / 1000 })),
    };
}

// Pure: the ffmpeg arguments that encode a render to Apple's app preview spec at the exact size.
export function encodeArgs(input, output, { width, height }) {
    return [
        '-y', '-i', input,
        '-vf', `scale=${width}:${height}:flags=lanczos,fps=30`,
        '-c:v', 'libx264', '-profile:v', 'high', '-pix_fmt', 'yuv420p', '-b:v', '11M', '-maxrate', '12M', '-bufsize', '22M',
        '-c:a', 'aac', '-b:a', '256k', '-ar', '48000', '-ac', '2',
        '-movflags', '+faststart', output,
    ];
}

// Pure: the recording file of a clip for a device label, e.g. store-dino-iphone-6.9-video-3-language-es.mp4.
export function recordingName(label, clip) {
    return `store-dino-${label}-video-${clip}.mp4`;
}

export function loadConfig(file = CUTS_FILE) {
    return JSON.parse(readFileSync(file, 'utf8'));
}

function run(args) {
    const result = spawnSync('ffmpeg', args, { stdio: 'inherit' });
    if (result.error) throw new Error(`ffmpeg did not start: ${result.error.message}`);
    return result.status ?? 1;
}

// The mark and span of a recording, read from its .mark file (two lines: the mark, then the span, seconds); throws when the file is missing or a line is not a number
// (a one-line mark file of the old wall-clock design is refused, so that recording is retaken).
export function readMark(file) {
    if (!existsSync(file)) throw new Error(`missing mark file ${file}: a recording without a mark is retaken`);
    const [mark, span] = readFileSync(file, 'utf8').trim().split(/\r?\n/).map((line) => (/^[0-9]+(\.[0-9]+)?$/.test(line.trim()) ? Number(line.trim()) : NaN));
    if (!(mark >= 0)) throw new Error(`${file} holds no mark (a number of seconds, 0 or more)`);
    if (!(span >= 0)) throw new Error(`${file} holds no span (its second line: how long the recorder ran, seconds)`);
    return { mark, span };
}

// Pure: the two-line text of a mark file.
export function markText({ mark, span }) {
    return `${mark.toFixed(3)}\n${span.toFixed(3)}\n`;
}

// Pure: the mark file that belongs to a recording file: ...video-<clip>.mp4 gives ...video-<clip>.mark.
export function markName(recording) {
    return recording.replace(/\.mp4$/, '.mark');
}

// Pure: throws when a clip cannot be what footage-cuts.json says: the recorder ran for less than start + seconds (the span; the FILE can be shorter, since
// simctl ends it at the last screen change, and that still tail is held by the cut), or the trimmed clip came out shorter than `seconds`.
export function assertCutFits(clip, spanSeconds, { start, seconds }, clipSeconds = null) {
    if (!(spanSeconds >= start + seconds - CUT_TOLERANCE_SECONDS)) throw new Error(`${clip}: the recorder ran ${spanSeconds} s, less than the cut start ${start} s + ${seconds} s: the clip would end after the recording. Tune markStep/beforeMark in footage-cuts.json, or lengthen the flow's last hold.`);
    if (clipSeconds !== null && !(clipSeconds >= seconds - CUT_TOLERANCE_SECONDS)) throw new Error(`${clip}: the clip is ${clipSeconds} s, shorter than the ${seconds} s asked for.`);
}

// Runs ffprobe; throws when it is not installed (a missing ffprobe never lets a recording pass unchecked: iPad run 45582715957).
function ffprobe(args) {
    const result = spawnSync('ffprobe', ['-v', 'error', ...args], { encoding: 'utf8' });
    if (result.error) throw new Error(`ffprobe is missing or did not start (${result.error.message}): no recording is checked without it`);
    if (result.status !== 0) throw new Error(`ffprobe failed: ${result.stderr.trim().slice(0, 300)}`);
    return result.stdout.trim();
}

// The duration in seconds of a media file, from ffprobe.
function probeSeconds(file) {
    const seconds = Number.parseFloat(ffprobe(['-show_entries', 'format=duration', '-of', 'default=nw=1:nk=1', file]));
    if (!Number.isFinite(seconds)) throw new Error(`ffprobe could not read the duration of ${file}`);
    return seconds;
}

// How many video packets ffprobe can read to the end of the file (0 or a throw for a file whose moov atom was never written).
function probePackets(file) {
    return Number.parseInt(ffprobe(['-count_packets', '-select_streams', 'v:0', '-show_entries', 'stream=nb_read_packets', '-of', 'csv=p=0', file]), 10) || 0;
}

// The cut of a clip from footage-cuts.json and its recording's mark file.
function clipCut(config, clip, recording) {
    const cut = config.clips[clip];
    if (!cut) throw new Error(`unknown clip '${clip}' (footage-cuts.json: ${Object.keys(config.clips).join(', ')})`);
    const { mark, span } = readMark(markName(recording));
    return { start: cutStart(mark, cut.beforeMark), seconds: cut.seconds, mark, span };
}

// A recording counts when ffprobe reads its video to the end and the recorder ran long enough for its cut. Prints one line; returns 0, or throws naming why.
function checkRecording(recording, clip) {
    const cut = clipCut(loadConfig(), clip, recording);
    const packets = probePackets(recording);
    if (!(packets > 0)) throw new Error(`${recording}: ffprobe read no video packet`);
    assertCutFits(clip, cut.span, cut);
    console.log(`${clip}: ok, ${packets} frames, file ${probeSeconds(recording)} s, recorder ran ${cut.span} s, mark ${cut.mark} s, cut ${cut.start} s + ${cut.seconds} s`);
    return 0;
}

// Writes a clip's mark file from Maestro's commands.json of its flow, printing every recorded step's times. Returns 0, or throws naming why.
function writeMark(commandsFile, clip, markFile) {
    const cut = loadConfig().clips[clip];
    if (!cut) throw new Error(`unknown clip '${clip}'`);
    const timing = clipTiming(JSON.parse(readFileSync(commandsFile, 'utf8')), cut.markStep, cut.markAt);
    for (const [i, step] of timing.steps.entries()) console.log(`  ${clip} step ${i + 1} ${step.kind} ${step.status} ${step.at.toFixed(2)}-${step.end.toFixed(2)} s${i + 1 === cut.markStep ? '  <- mark' : ''}`);
    writeFileSync(markFile, markText(timing));
    console.log(`${clip}: mark ${timing.mark.toFixed(3)} s (${cut.markAt} of step ${cut.markStep}), recorder ran ${timing.span.toFixed(3)} s`);
    return 0;
}

function cutOne(clip, cut, input, output) {
    assertCutFits(clip, cut.span, cut);
    const status = run(cutArgs(input, output, cut));
    if (status !== 0) return status;
    assertCutFits(clip, Infinity, cut, probeSeconds(output));
    return 0;
}

function cutAll(deviceKey, folder) {
    const config = loadConfig();
    const device = config.devices[deviceKey];
    if (!device) throw new Error(`unknown device '${deviceKey}' (${Object.keys(config.devices).join(', ')})`);
    const outDir = join(HERE, 'assets', 'footage', deviceKey);
    mkdirSync(outDir, { recursive: true });
    let failed = 0;
    for (const [clip, cut] of Object.entries(config.clips)) {
        const input = join(resolve(folder), recordingName(device.label, clip));
        if (!existsSync(input)) { console.error(`missing recording ${input}`); failed++; continue; }
        try {
            const status = cutOne(clip, clipCut(config, clip, input), input, join(outDir, `${clip}.mp4`));
            if (status !== 0) { console.error(`ffmpeg exited ${status} for ${clip}`); failed++; }
        } catch (error) { console.error(error.message); failed++; }
    }
    return failed === 0 ? 0 : 1;
}

function encode(deviceKey, input, output) {
    const device = loadConfig().devices[deviceKey];
    if (!device) throw new Error(`unknown device '${deviceKey}'`);
    return run(encodeArgs(input, output, device));
}

function main(argv) {
    const [command, deviceKey, a, b] = argv;
    if (command === 'cut' && deviceKey && a) return cutAll(deviceKey, a);
    if (command === 'encode' && deviceKey && a && b) return encode(deviceKey, a, b);
    if (command === 'mark' && deviceKey && a && b) return writeMark(deviceKey, a, b);
    if (command === 'check' && deviceKey && a) return checkRecording(deviceKey, a);
    console.error('usage: store-media.mjs cut <iphone|ipad> <recordings folder> | encode <iphone|ipad> <render.mp4> <out.mp4> | mark <commands.json> <clip> <out.mark> | check <recording.mp4> <clip>');
    return 2;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
    try { process.exitCode = main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
