// The video's one audio track: every clip decoded to 48 kHz mono 16-bit PCM and cut or padded to exactly its timeline
// length, silences as zero samples, written as one WAV. One format throughout, so nothing can play fast or slow.

import { ffmpeg } from './tools.mjs';

export const SAMPLE_RATE = 48000;
const BYTES_PER_SAMPLE = 2;

export const samplesOf = (seconds) => Math.round(seconds * SAMPLE_RATE);

export function silence(seconds) {
  return Buffer.alloc(samplesOf(seconds) * BYTES_PER_SAMPLE);
}

/** The clip as PCM, exactly `seconds` long (the tail of a longer decode is cut, a shorter one is padded with silence). */
export function decodeClip(file, seconds) {
  const decoded = ffmpeg(['-i', file, '-f', 's16le', '-ar', String(SAMPLE_RATE), '-ac', '1', '-'], { binary: true });
  const wanted = samplesOf(seconds) * BYTES_PER_SAMPLE;
  if (decoded.length >= wanted) return decoded.subarray(0, wanted);
  return Buffer.concat([decoded, Buffer.alloc(wanted - decoded.length)]);
}

export function wavOf(pcm) {
  const header = Buffer.alloc(44);
  header.write('RIFF', 0);
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write('WAVEfmt ', 8);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(1, 22);
  header.writeUInt32LE(SAMPLE_RATE, 24);
  header.writeUInt32LE(SAMPLE_RATE * BYTES_PER_SAMPLE, 28);
  header.writeUInt16LE(BYTES_PER_SAMPLE, 32);
  header.writeUInt16LE(16, 34);
  header.write('data', 36);
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
}

/** audio: timeline.audio parts; `files` maps a part's file name to { path, sec }. Returns the WAV bytes. */
export function assembleAudio(audio, files) {
  const parts = audio.map((part) => {
    if ('silence' in part) return silence(part.silence);
    const clip = files.get(part.file);
    if (!clip) throw new Error(`No clip for ${part.file}`);
    return decodeClip(clip.path, clip.sec);
  });
  return wavOf(Buffer.concat(parts));
}
