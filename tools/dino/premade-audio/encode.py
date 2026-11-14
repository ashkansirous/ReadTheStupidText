"""WAV to AAC `.m4a` with ffmpeg, leading and trailing silence trimmed (Slice 246 A4).

The encoder is ffmpeg's native `aac` (in every ffmpeg build; found on PATH, or named by $FFMPEG). Speech at 24 kHz mono 48 kb/s is about 6 KB a second.
Silence is cut at both ends (the second pass runs on the reversed audio) but a short pad is kept, so clips join without a gap and without a clipped consonant.
"""
import os
import re
import shutil
import subprocess

FFMPEG_ENV = "FFMPEG"
CODEC = "aac"
BITRATE = "48k"
CHANNELS = "1"
EXTENSION = "m4a"
THRESHOLD_DB = "-50dB"
PAD_SECONDS = "0.05"
TRIM_FILTER = (
    f"silenceremove=start_periods=1:start_threshold={THRESHOLD_DB}:start_silence={PAD_SECONDS},areverse,"
    f"silenceremove=start_periods=1:start_threshold={THRESHOLD_DB}:start_silence={PAD_SECONDS},areverse"
)
TIMEOUT_SECONDS = 120
MIN_CLIP_SECONDS = 0.1  # a shorter result is an empty or silent clip, never a spoken sentence
MIN_SOUND_CLIP_SECONDS = 0.03  # an animal sound word ("Oink!") can be this short and still be real; only an empty result is silent
DURATION = re.compile(r"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)")


class EncodeError(RuntimeError):
    pass


class SilentClipError(EncodeError):
    """The vendor returned audio with no speech in it: retrying the sentence is right (unlike a broken ffmpeg)."""


def find_ffmpeg(environ=None):
    """The ffmpeg to use ($FFMPEG, else PATH), or None."""
    environ = os.environ if environ is None else environ
    return environ.get(FFMPEG_ENV) or shutil.which("ffmpeg")


def command(ffmpeg, wav_path, out_path):
    return [ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", wav_path, "-af", TRIM_FILTER,
            "-c:a", CODEC, "-b:a", BITRATE, "-ac", CHANNELS, "-movflags", "+faststart", out_path]


def duration_seconds(ffmpeg, path):
    """The length of an audio file read from ffmpeg's banner; 0.0 when it has none (an empty file reports N/A)."""
    probe = subprocess.run([ffmpeg, "-hide_banner", "-i", path], capture_output=True, text=True, timeout=TIMEOUT_SECONDS, check=False)
    found = DURATION.search(probe.stderr)
    return int(found.group(1)) * 3600 + int(found.group(2)) * 60 + float(found.group(3)) if found else 0.0


def to_m4a(ffmpeg, wav_path, out_path, min_seconds=MIN_CLIP_SECONDS):
    """Encodes `wav_path` to `out_path` (.m4a); raises EncodeError with ffmpeg's message (SilentClipError when less than `min_seconds` is left). Writes a temp name first so a failure leaves no half clip."""
    temp = out_path + ".part." + EXTENSION
    try:
        run_encode(ffmpeg, wav_path, temp)
        if duration_seconds(ffmpeg, temp) < min_seconds:
            raise SilentClipError("the clip is silent or empty after trimming")
    except subprocess.TimeoutExpired as error:
        remove_quietly(temp)
        raise EncodeError(f"ffmpeg did not finish within {TIMEOUT_SECONDS} s") from error
    except EncodeError:
        remove_quietly(temp)
        raise
    os.replace(temp, out_path)


def remove_quietly(path):
    if os.path.isfile(path):
        os.remove(path)


def run_encode(ffmpeg, wav_path, temp):
    done = subprocess.run(command(ffmpeg, wav_path, temp), capture_output=True, text=True, timeout=TIMEOUT_SECONDS, check=False)
    if done.returncode != 0 or not os.path.isfile(temp) or os.path.getsize(temp) == 0:
        raise EncodeError(f"ffmpeg failed ({done.returncode}): {done.stderr.strip()[:300]}")
