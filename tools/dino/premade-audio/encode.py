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
QUICK_BITRATE = "24k"  # Slice 463: the quick package's clips (AAC-LC mono, about 3 KB a second); the owner's listening pick (item 2) may change it before release
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


def quick_command(ffmpeg, clip_path, out_path):
    """The re-encode of an already trimmed clip at the quick bitrate (no trim filter: the standard clip's silence is already cut)."""
    return [ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", clip_path,
            "-c:a", CODEC, "-b:a", QUICK_BITRATE, "-ac", CHANNELS, "-movflags", "+faststart", out_path]


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


def to_quick_m4a(ffmpeg, clip_path, out_path):
    """Slice 463: re-encodes a standard clip (`.m4a`, already trimmed) at the quick bitrate into `out_path`; raises EncodeError with ffmpeg's message. Writes a temp name first so a failure leaves no half clip."""
    temp = out_path + ".part." + EXTENSION
    try:
        done = subprocess.run(quick_command(ffmpeg, clip_path, temp), capture_output=True, text=True, timeout=TIMEOUT_SECONDS, check=False)
        if done.returncode != 0 or not os.path.isfile(temp) or os.path.getsize(temp) == 0:
            raise EncodeError(f"ffmpeg failed ({done.returncode}): {done.stderr.strip()[:300]}")
        if duration_seconds(ffmpeg, temp) < MIN_SOUND_CLIP_SECONDS:
            raise SilentClipError("the quick clip is empty after the re-encode")
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


QUICK_FOLDER = "quick"  # Slice 463: inside a pack's clips folder; BookBuilder zips it as audio-<lang>-v<n>.quick.zip


def quick_folder(ffmpeg, clips_folder):
    """Re-encodes every `.m4a` clip of `clips_folder` at the quick bitrate into `<clips_folder>/quick/` under the same name; returns how many. A clip that fails stops the run (no half folder is trusted: BookBuilder needs every clip)."""
    target = os.path.join(clips_folder, QUICK_FOLDER)
    os.makedirs(target, exist_ok=True)
    names = sorted(name for name in os.listdir(clips_folder) if name.endswith("." + EXTENSION) and os.path.isfile(os.path.join(clips_folder, name)))
    for name in names:
        to_quick_m4a(ffmpeg, os.path.join(clips_folder, name), os.path.join(target, name))
    return len(names)


def main(argv):
    """`python encode.py quick <clips folder>`: makes the quick clips of one pack (Slice 463)."""
    ffmpeg = find_ffmpeg()
    if len(argv) != 3 or argv[1] != "quick" or not ffmpeg or not os.path.isdir(argv[2]):
        print("usage: python encode.py quick <clips folder>   (needs ffmpeg on PATH or $FFMPEG)")
        return 2
    print(f"{quick_folder(ffmpeg, argv[2])} quick clip(s) in {os.path.join(argv[2], QUICK_FOLDER)}")
    return 0


def run_encode(ffmpeg, wav_path, temp):
    done = subprocess.run(command(ffmpeg, wav_path, temp), capture_output=True, text=True, timeout=TIMEOUT_SECONDS, check=False)
    if done.returncode != 0 or not os.path.isfile(temp) or os.path.getsize(temp) == 0:
        raise EncodeError(f"ffmpeg failed ({done.returncode}): {done.stderr.strip()[:300]}")


if __name__ == "__main__":
    import sys
    sys.exit(main(sys.argv))

