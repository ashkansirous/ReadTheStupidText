// Slice 433 item 6: the two ffmpeg steps around the HyperFrames render of the App Store previews.
//   node store-media.mjs cut <iphone|ipad> <folder of the downloaded recordings>
//       trims each clip of footage-cuts.json (its own recording store-dino-<device label>-video-<clip>.mp4: one recording per clip) into assets/footage/<device>/<clip>.mp4 (silent, h264): from max(0, mark - beforeMark), the mark being the number in the recording's own store-dino-<device label>-video-<clip>.mark file, for the planned length
//   node store-media.mjs encode <iphone|ipad> <rendered mp4> <output mp4>
//       encodes the render to Apple's app preview spec: exact size, 30 fps, H.264 high profile at about 11 Mbps (10-12), stereo AAC 256 kbps at 48 kHz, faststart
// Then check the result with tools/emulator/store-size-check.ps1 -Target preview-iphone|preview-ipad -Path <output>.
import { spawnSync } from 'node:child_process';
import { readFileSync, mkdirSync, existsSync } from 'node:fs';
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

// Pure: the ffmpeg arguments that trim a clip's own recording: skip `start` seconds, keep `seconds`, no audio.
export function cutArgs(input, output, { start, seconds }) {
    if (!(start >= 0) || !(seconds > 0)) throw new Error(`a trim needs start >= 0 and seconds > 0 (got start ${start}, seconds ${seconds})`);
    return ['-y', '-ss', String(start), '-i', input, '-t', String(seconds), '-an', '-c:v', 'libx264', '-crf', '12', '-pix_fmt', 'yuv420p', output];
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

// The mark of a recording, read from its .mark file (one number, seconds); throws when the file is missing or holds no number.
export function readMark(file) {
    if (!existsSync(file)) throw new Error(`missing mark file ${file}: a recording without a mark is retaken`);
    const text = readFileSync(file, 'utf8').trim();
    const mark = /^[0-9]+(\.[0-9]+)?$/.test(text) ? Number(text) : NaN;
    if (!(mark >= 0)) throw new Error(`${file} holds no mark (a number of seconds, 0 or more)`);
    return mark;
}

// Pure: the mark file that belongs to a recording file: ...video-<clip>.mp4 gives ...video-<clip>.mark.
export function markName(recording) {
    return recording.replace(/\.mp4$/, '.mark');
}

// Pure: throws when a clip cannot be what footage-cuts.json says: its own recording is shorter than start + seconds (start = mark - beforeMark; simctl recordVideo writes frames only when the screen changes, so a recording of a mostly static page can be shorter than the flow's wall-clock time), or the trimmed clip came out shorter than `seconds`.
export function assertCutFits(clip, recordingSeconds, { start, seconds }, clipSeconds = null) {
    if (!(recordingSeconds >= start + seconds - CUT_TOLERANCE_SECONDS)) throw new Error(`${clip}: the recording is ${recordingSeconds} s, shorter than the cut start ${start} s + ${seconds} s: the clip would be short or empty. Tune beforeMark in footage-cuts.json on the real recording, or record the clip again.`);
    if (clipSeconds !== null && !(clipSeconds >= seconds - CUT_TOLERANCE_SECONDS)) throw new Error(`${clip}: the clip is ${clipSeconds} s, shorter than the ${seconds} s asked for.`);
}

// The duration in seconds of a media file, from ffprobe.
function probeSeconds(file) {
    const result = spawnSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=nw=1:nk=1', file], { encoding: 'utf8' });
    if (result.error) throw new Error(`ffprobe did not start: ${result.error.message}`);
    const seconds = Number.parseFloat(result.stdout);
    if (result.status !== 0 || !Number.isFinite(seconds)) throw new Error(`ffprobe could not read the duration of ${file}`);
    return seconds;
}

function cutOne(clip, cut, input, output) {
    assertCutFits(clip, probeSeconds(input), cut);
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
            const start = cutStart(readMark(markName(input)), cut.beforeMark);
            const status = cutOne(clip, { start, seconds: cut.seconds }, input, join(outDir, `${clip}.mp4`));
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
    console.error('usage: store-media.mjs cut <iphone|ipad> <recordings folder> | encode <iphone|ipad> <render.mp4> <out.mp4>');
    return 2;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
    try { process.exitCode = main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
