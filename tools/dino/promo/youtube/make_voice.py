"""Slice 435 item 5: speaks the card lines of the YouTube videos with the book's narrator voice (the same Gemini TTS voice and style as its narration clips).

    node voice-lines.mjs --book lion-and-mouse --lang en > ../../../../tmp/lines.json
    python make_voice.py --lines ../../../../tmp/lines.json [--dry-run]

Reads each line's text, takes the narrator voice and style from content/dino/<book>/audio-cast.json (a grown-ups line, which belongs to no book, always uses
GROWN_UPS_BOOK's narrator, the Lion and the Mouse's), adds the language's accent line as premade-audio does, renders it through premade-audio's Gemini adapter (key from the
environment or D:/secrets, `skipped: no key` and nothing sent when absent) and writes voice/<lang>/<key>.m4a beside this script, which render.mjs reads.
An existing file is kept (never a second paid request); delete it to remake. Stdlib only, ffmpeg for AAC.
"""
import argparse
import json
import os
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO_ROOT, "tools", "dino", "premade-audio"))

import audio_cast  # noqa: E402
import encode  # noqa: E402
from adapter_base import STATUS_ERROR, STATUS_OK, STATUS_SKIPPED, is_fatal  # noqa: E402
from adapter_gemini import Gemini  # noqa: E402

VOICE_FOLDER = os.path.join(HERE, "voice")
CAST_NAME = "audio-cast.json"
EXIT_OK = 0
EXIT_FAILED = 1
EXIT_NO_KEY = 3  # as premade-audio's run_common.EXIT_NO_KEY
EXIT_NO_FFMPEG = 2
EXIT_QUOTA = 4  # a 401/402/403/429 answer: retrying the other lines is pointless
GROWN_UPS_BOOK = "lion-and-mouse"  # the narrator of every grown-ups line, so the voice never depends on which books a run lists
SILENT_ATTEMPTS = 2
REQUIRED_FIELDS = ("language", "key", "text")
HTTP_TOO_MANY = 429
RATE_WAITS = 3  # per-minute 429 answers waited out per request before the run stops as for a quota
MAX_WAIT_SECONDS = 120.0
DELIVERY_ERRORS = (OSError, KeyError, ValueError)  # no audio-cast.json, no narrator in it, or invalid JSON


def narrator_delivery(book, language, content_root=None):
    """(voice, style) of the book's narrator for the language: the cast style plus that language's accent line."""
    root = content_root or os.path.join(REPO_ROOT, "content", "dino")
    with open(os.path.join(root, book, CAST_NAME), encoding="utf8") as f:
        narrator = json.load(f)["narrator"]
    accent = audio_cast.ACCENTS.get(language)
    style = f"{narrator['style']}{audio_cast.ACCENT_JOINER}{accent}" if accent else narrator["style"]
    return narrator["voice"], style


def target_path(line, voice_folder=None):
    return os.path.join(voice_folder or VOICE_FOLDER, line["language"], f"{line['key']}.m4a")


def book_for(line):
    """The book whose narrator speaks the line: its own; a grown-ups line (no book) is always spoken by GROWN_UPS_BOOK's narrator, whatever books the run lists."""
    return line.get("book") or GROWN_UPS_BOOK


def check_lines(lines):
    """Raises ValueError naming the first line that lacks a language, key or text, before any request is sent."""
    if not isinstance(lines, list):
        raise ValueError("the lines file must hold a JSON list")
    for index, line in enumerate(lines):
        missing = [field for field in REQUIRED_FIELDS if not isinstance(line, dict) or not line.get(field)]
        if missing:
            raise ValueError(f"line {index} lacks {', '.join(missing)}")


def request_clip(adapter, line, voice, out_path, style, log, sleep):
    """One adapter request; a per-minute 429 with a retry_after is waited out (capped, at most RATE_WAITS times), a daily-quota 429 or any other answer is returned as is."""
    waits = 0
    while True:
        result = adapter.render(line["text"], voice, out_path, style)
        waitable = result.status == STATUS_ERROR and result.code == HTTP_TOO_MANY and not getattr(result, "daily_limit", False) and getattr(result, "retry_after", None) is not None
        if not waitable or waits >= RATE_WAITS:
            return result
        waits += 1
        seconds = min(max(float(result.retry_after), 0.0), MAX_WAIT_SECONDS)
        log(f"rate limited on {line['language']}/{line['key']}: waiting {seconds:g}s ({waits}/{RATE_WAITS})")
        sleep(seconds)


def encode_clip(line, result, ffmpeg, out, log):
    """Encodes the rendered WAV to `out`; a silent clip is retried once by the caller, any other encode problem is a FAIL line. Returns True when written."""
    try:
        encode.to_m4a(ffmpeg, result.path, out)
        return True
    except encode.SilentClipError:
        raise
    except (encode.EncodeError, OSError, subprocess.SubprocessError) as error:
        log(f"FAIL {line['language']}/{line['key']}: encoding failed: {error}")
        return False


def render_once(line, voice, style, adapter, ffmpeg, out, log, sleep=time.sleep):
    """One request and encode. Returns an exit code (EXIT_OK, EXIT_FAILED, EXIT_NO_KEY, EXIT_QUOTA) or None for a silent clip (try again)."""
    with tempfile.TemporaryDirectory() as scratch:
        result = request_clip(adapter, line, voice, os.path.join(scratch, "clip"), style, log, sleep)
        if result.status == STATUS_SKIPPED:
            log(result.detail)
            return EXIT_NO_KEY
        if result.status != STATUS_OK:
            log(f"FAIL {line['language']}/{line['key']}: {result.detail}")
            return EXIT_QUOTA if is_fatal(result) else EXIT_FAILED
        try:
            return EXIT_OK if encode_clip(line, result, ffmpeg, out, log) else EXIT_FAILED
        except encode.SilentClipError as error:
            log(f"silent clip for {line['language']}/{line['key']}: {error}")
            return None


def render_line(line, adapter, ffmpeg, log=print, voice_folder=None, sleep=time.sleep):
    out = target_path(line, voice_folder)
    if os.path.isfile(out):
        log(f"keep {line['language']}/{line['key']}: already made")
        return EXIT_OK
    try:
        voice, style = narrator_delivery(book_for(line), line["language"])
    except DELIVERY_ERRORS as error:
        log(f"FAIL {line['language']}/{line['key']}: no narrator for {book_for(line)}: {error!r}")
        return EXIT_FAILED
    os.makedirs(os.path.dirname(out), exist_ok=True)
    code = None
    for _ in range(SILENT_ATTEMPTS):
        code = render_once(line, voice, style, adapter, ffmpeg, out, log, sleep)
        if code is not None:
            break
    if code is None:
        log(f"FAIL {line['language']}/{line['key']}: silent clip {SILENT_ATTEMPTS} times")
        return EXIT_FAILED
    if code == EXIT_OK:
        log(f"made {line['language']}/{line['key']} with {voice}")
    return code


def run(lines, adapter, ffmpeg, dry_run=False, log=print, voice_folder=None, sleep=time.sleep):
    """Renders every line; stops at once on no key, a daily quota or an auth error (and says how many lines are left); a per-minute 429 is waited out first. The worst exit code wins."""
    code = EXIT_OK
    for index, line in enumerate(lines):
        if dry_run:
            try:
                voice, _ = narrator_delivery(book_for(line), line["language"])
            except DELIVERY_ERRORS as error:
                log(f"FAIL {line['language']}/{line['key']}: no narrator for {book_for(line)}: {error!r}")
                code = EXIT_FAILED
                continue
            log(f"would make {line['language']}/{line['key']} with {voice}" + (" (exists)" if os.path.isfile(target_path(line, voice_folder)) else ""))
            continue
        code = max(code, render_line(line, adapter, ffmpeg, log, voice_folder, sleep))
        if code in (EXIT_NO_KEY, EXIT_QUOTA):
            log(f"stopped: {len(lines) - index - 1} lines left (not tried); rerun later, made clips are kept")
            break
    return code


def main(argv=None):
    parser = argparse.ArgumentParser(description="Speak the YouTube card lines with each book's narrator voice.")
    parser.add_argument("--lines", required=True, help="JSON from voice-lines.mjs")
    parser.add_argument("--dry-run", action="store_true", help="list what would be made; sends nothing")
    args = parser.parse_args(argv)
    with open(args.lines, encoding="utf8") as f:
        lines = json.load(f)
    try:
        check_lines(lines)
    except ValueError as error:
        print(f"bad lines file {args.lines}: {error}")
        return EXIT_FAILED
    ffmpeg = encode.find_ffmpeg()
    if not ffmpeg and not args.dry_run:
        print("ffmpeg not found (PATH or $FFMPEG)")
        return EXIT_NO_FFMPEG
    return run(lines, Gemini(), ffmpeg, args.dry_run)


if __name__ == "__main__":
    sys.exit(main())
