"""Per-sentence Gemini clip generator for a Dino book (Slice 246 A2-A4). Runs on the user's PC; one request per spoken sentence.

    dotnet run --project tools/DinoReader.BookBuilder -- dump-sentences three-little-pigs --lang fa --out tmp/pigs-fa.json
    python tools/premade-audio/generate.py --sentences tmp/pigs-fa.json --out artifacts/premade-audio [--free] [--max-requests N] [--dry-run]

Clips land in <out>/<book>/<language>/ with manifest.json (version 3); a rerun renders only sentences without a clip (a changed text, voice, style or direction
makes a new one), saves after every clip, and ends cleanly at the request budget, the daily limit, a 402/403 or a second 429. The key is GEMINI_API_KEY.
"""
import argparse
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import audio_cast  # noqa: E402
import encode  # noqa: E402
import pacing  # noqa: E402
from adapter_base import HTTP_RATE_LIMITED, STATUS_OK, STATUS_SKIPPED, is_fatal  # noqa: E402
from adapter_gemini import Gemini  # noqa: E402
from clip_manifest import Manifest, clip_stem  # noqa: E402

REPO_ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
DEFAULT_OUT = os.path.join(REPO_ROOT, "artifacts", "premade-audio")
CAST_NAME = "audio-cast.json"
FORMAT_M4A = "m4a"
FORMAT_WAV = "wav"
MAX_CONSECUTIVE_ERRORS = 3
OUTCOME_SILENT = "silent-clip"  # a clip that failed the silence check: recorded, the run goes on until the silent caps below
MAX_CONSECUTIVE_SILENT = 3
MAX_SILENT_PER_RUN = 5
MAX_RETRY_WAIT_SECONDS = 300.0
DEFAULT_RETRY_WAIT_SECONDS = 60.0
EXIT_OK = 0
EXIT_ERRORS = 1  # sentences failed and nothing was rendered, or three errors in a row
EXIT_NO_FFMPEG = 2
EXIT_NO_KEY = 3
EXIT_AUTH = 4  # 401 / 402 / 403: a bad key, no balance or no access
EXIT_ENCODE = 5  # ffmpeg failed or hung (the vendor WAV is kept and encoded on the next run, with no new request)
EXIT_RATE_LIMITED = 6  # a second 429, or one with a wait that is too long
EXIT_LOCKED = 7  # another run is using the output folder


class Summary:
    def __init__(self):
        self.rendered = 0
        self.cached = 0
        self.stopped = ""
        self.exit_code = EXIT_OK
        self.errors = []

    def stop(self, reason, exit_code):
        self.stopped = reason
        self.exit_code = exit_code

    def line(self, total):
        text = f"{self.rendered} rendered, {self.cached} already had a clip, {total - self.rendered - self.cached} left"
        return text + (f"; stopped: {self.stopped}" if self.stopped else "")


class Generator:
    def __init__(self, adapter, cast, folder, pacer, book, language, fmt=FORMAT_M4A, ffmpeg=None, log=print, sleep=None, readonly=False):
        self.adapter = adapter
        self.cast = cast
        self.folder = folder
        self.pacer = pacer
        self.fmt = fmt
        self.ffmpeg = ffmpeg
        self.log = log
        self.sleep = sleep or pacer.sleep
        self.manifest = Manifest(folder, book, language, readonly)
        self.waited_for_429 = False
        self.errors_in_a_row = 0
        self.silent_in_a_row = 0
        self.silent_total = 0

    def run(self, sentences):
        os.makedirs(self.folder, exist_ok=True)
        summary = Summary()
        keep = {s["index"] for s in sentences}
        if self.manifest.drop_stale({s["index"]: self.identity(s) for s in sentences}):
            self.manifest.save(keep)  # a run killed before its first clip must not leave the stale entries on disk
        for sentence in sentences:
            if not self.step(sentence, keep, summary):
                break
        self.manifest.save(keep)
        return summary

    def identity(self, sentence):
        """(delivery, variant, clip stem) of one sentence: everything that decides which clip file it uses."""
        delivery = self.cast.delivery(sentence)
        variant = f"{self.adapter.name}-{self.adapter.settings_tag(delivery.voice, delivery.prompt)}"
        return delivery, variant, clip_stem(sentence["text"], delivery.voice, variant)

    def step(self, sentence, keep, summary):
        """Handles one sentence; False when the run must end (summary.stopped says why)."""
        delivery, variant, stem = self.identity(sentence)
        clip = f"{stem}.{self.fmt}"
        if self.has_clip(stem):
            self.manifest.set(sentence, delivery, variant, clip)
            summary.cached += 1
            return True
        outcome = self.adopt_wav(sentence, stem, summary) if self.has_wav(stem) else self.render_one(sentence, delivery, stem, summary)
        if outcome == OUTCOME_SILENT:
            return self.after_silent_clip(summary)
        if outcome == STATUS_OK:
            self.errors_in_a_row = 0
            self.silent_in_a_row = 0
            self.manifest.set(sentence, delivery, variant, clip)
            self.manifest.save(keep)
            summary.rendered += 1
            return True
        if outcome:
            return False
        self.errors_in_a_row += 1
        if self.errors_in_a_row >= MAX_CONSECUTIVE_ERRORS:
            summary.stop(f"{MAX_CONSECUTIVE_ERRORS} errors in a row; last: {summary.errors[-1]}", EXIT_ERRORS)
        return self.errors_in_a_row < MAX_CONSECUTIVE_ERRORS

    def after_silent_clip(self, summary):
        """A silent clip is recorded in summary.errors and the run goes on, but a wrong voice or style that returns silence every time must not burn the whole budget:
        the run ends after MAX_CONSECUTIVE_SILENT in a row or MAX_SILENT_PER_RUN in total (counted apart from HTTP errors)."""
        self.silent_in_a_row += 1
        self.silent_total += 1
        if self.silent_in_a_row >= MAX_CONSECUTIVE_SILENT or self.silent_total >= MAX_SILENT_PER_RUN:
            summary.stop(f"{self.silent_in_a_row} silent clips in a row, {self.silent_total} in this run; last: {summary.errors[-1]}", EXIT_ERRORS)
            return False
        return True

    def has_clip(self, stem):
        return os.path.isfile(os.path.join(self.folder, f"{stem}.{self.fmt}"))

    def has_wav(self, stem):
        """A paid vendor WAV from an earlier run whose encode did not finish: it is encoded, never requested again."""
        path = os.path.join(self.folder, f"{stem}.{FORMAT_WAV}")
        return self.fmt != FORMAT_WAV and os.path.isfile(path) and os.path.getsize(path) > 0

    def adopt_wav(self, sentence, stem, summary):
        self.log(f"encoding the WAV left by an earlier run ({stem}), no request")
        return self.finish_clip(os.path.join(self.folder, f"{stem}.{FORMAT_WAV}"), sentence, stem, summary)

    def render_one(self, sentence, delivery, stem, summary):
        """STATUS_OK when the clip is saved, '' for an error to count (kept in summary.errors), else the reason the run must end."""
        reason = self.pacer.stop_reason()
        if reason:
            summary.stopped = reason
            return reason
        self.pacer.wait_turn()
        result = self.adapter.render(sentence["text"], delivery.voice, os.path.join(self.folder, stem), delivery.prompt)
        if result.status != STATUS_SKIPPED:
            self.pacer.sent_one()
        if result.code == HTTP_RATE_LIMITED:
            return self.after_rate_limit(result, sentence, delivery, stem, summary)
        if result.status == STATUS_OK:
            return self.finish_clip(result.path, sentence, stem, summary)
        if is_fatal(result):
            summary.stop(result.detail, EXIT_NO_KEY if result.status == STATUS_SKIPPED else EXIT_AUTH)
            return result.detail
        summary.errors.append(f"sentence {sentence['index']}: {result.detail}")
        self.log(f"error on sentence {sentence['index']}: {result.detail}")
        return ""

    def after_rate_limit(self, result, sentence, delivery, stem, summary):
        """A per-day 429 ends the run at once (no wait, no retry). The first per-minute 429 of the run waits the vendor's retry-after (capped) and tries the sentence once more; a later one, or a wait that is too long, ends the run."""
        wait = result.retry_after if result.retry_after is not None else DEFAULT_RETRY_WAIT_SECONDS
        if result.daily_limit or self.waited_for_429 or wait > MAX_RETRY_WAIT_SECONDS:
            reason = f"rate limited (429), not retrying: {result.detail}"
            summary.stop(reason, EXIT_RATE_LIMITED)
            return reason
        budget_reason = self.pacer.stop_reason()
        if budget_reason:  # the 429 used up the budget: waiting would be for a retry that cannot be sent
            summary.stopped = budget_reason
            return budget_reason
        self.waited_for_429 = True
        self.log(f"429 on sentence {sentence['index']}: waiting {wait:.0f}s once, then retrying")
        self.sleep(wait)
        return self.render_one(sentence, delivery, stem, summary)

    def finish_clip(self, wav_path, sentence, stem, summary):
        """STATUS_OK, OUTCOME_SILENT for a silent clip (the WAV is dropped so the sentence is requested again; it is recorded and does not count towards the errors in a row),
        or the reason the run ends (encode failure). A sound-word sentence ("Oink!") passes at a shorter length than speech."""
        if self.fmt == FORMAT_WAV:
            return STATUS_OK
        out_path = os.path.join(self.folder, f"{stem}.{encode.EXTENSION}")
        try:
            encode.to_m4a(self.ffmpeg, wav_path, out_path, encode.MIN_SOUND_CLIP_SECONDS if sentence.get("sound") else encode.MIN_CLIP_SECONDS)
        except encode.SilentClipError as error:
            summary.errors.append(f"sentence {sentence['index']} ({stem}): {error}")
            self.log(f"silent clip for sentence {sentence['index']} ({stem}): {error}; it will be requested again on a rerun")
            self.drop_wav(wav_path)
            return OUTCOME_SILENT
        except (encode.EncodeError, OSError, subprocess.SubprocessError) as error:
            reason = f"encoding failed: {error}"
            summary.stop(reason, EXIT_ENCODE)
            return reason
        os.remove(wav_path)
        return STATUS_OK

    def drop_wav(self, wav_path):
        """Removes a silent clip's WAV; a locked file (Windows) is logged, never a crash before the manifest is saved."""
        try:
            os.remove(wav_path)
        except OSError as error:
            self.log(f"could not remove {wav_path}: {error}")


def load_sentences(path):
    with open(path, encoding="utf8") as f:
        data = json.load(f)
    return data["book"], data["language"], data["sentences"]


def parse_args(argv):
    p = argparse.ArgumentParser(description="Render one Gemini clip per spoken sentence of a Dino book.")
    p.add_argument("--sentences", required=True, help="JSON from BookBuilder dump-sentences")
    p.add_argument("--cast", help=f"audio cast file (default content/dino/<book>/{CAST_NAME})")
    p.add_argument("--out", default=DEFAULT_OUT, help="output root (default artifacts/premade-audio)")
    p.add_argument("--free", action="store_true", help=f"free tier preset: {pacing.FREE_RPM} requests a minute, {pacing.FREE_RPD} a day")
    p.add_argument("--rpm", type=int, help=f"requests per minute (0 = no pacing; default {pacing.DEFAULT_RPM}, a placeholder for the paid tier)")
    p.add_argument("--rpd", type=int, help="requests per rolling 24 hours (0 = no limit; default none)")
    p.add_argument("--max-requests", type=int, default=pacing.DEFAULT_MAX_REQUESTS,
                   help=f"most requests to send in this run (default {pacing.DEFAULT_MAX_REQUESTS}; 0 = no limit)")
    p.add_argument("--format", choices=(FORMAT_M4A, FORMAT_WAV), default=FORMAT_M4A, help="m4a needs ffmpeg; wav keeps the raw vendor clips")
    p.add_argument("--dry-run", action="store_true", help="check the cast and list what would be rendered; sends nothing")
    return p.parse_args(argv)


def limits(args):
    rpm = args.rpm if args.rpm is not None else (pacing.FREE_RPM if args.free else pacing.DEFAULT_RPM)
    rpd = args.rpd if args.rpd is not None else (pacing.FREE_RPD if args.free else pacing.NO_LIMIT)
    return rpm, rpd


def pending_count(generator, sentences):
    """Sentences that need a request: no clip, and no kept vendor WAV waiting to be encoded (that one costs nothing)."""
    stems = [generator.identity(s)[2] for s in sentences]
    return sum(0 if generator.has_clip(stem) or generator.has_wav(stem) else 1 for stem in set(stems))  # sentences sharing a stem share one clip file


def main(argv=None):
    args = parse_args(argv)
    book, language, sentences = load_sentences(args.sentences)
    cast = audio_cast.load(args.cast or os.path.join(REPO_ROOT, "content", "dino", book, CAST_NAME))
    cast.check(sentences)
    ffmpeg = encode.find_ffmpeg() if args.format == FORMAT_M4A else None
    if args.dry_run:
        return run_locked(args, book, language, sentences, cast, ffmpeg)  # sends nothing, so it needs neither ffmpeg nor the folder lock
    if args.format == FORMAT_M4A and not ffmpeg:
        print("ffmpeg was not found on PATH (set $FFMPEG to its path); rerun with --format wav to keep raw WAV clips", file=sys.stderr)
        return EXIT_NO_FFMPEG
    try:
        with pacing.RunLock(args.out):
            return run_locked(args, book, language, sentences, cast, ffmpeg)
    except pacing.RunLocked as error:
        print(str(error), file=sys.stderr)
        return EXIT_LOCKED


def run_locked(args, book, language, sentences, cast, ffmpeg):
    rpm, rpd = limits(args)
    log = pacing.UsageLog(pacing.usage_path(args.out))
    pacer = pacing.Pacer(log, rpm, rpd, args.max_requests)
    generator = Generator(Gemini(), cast, os.path.join(args.out, book, language), pacer, book, language, args.format, ffmpeg,
                          readonly=args.dry_run)
    if args.dry_run:
        print(f"{book} [{language}]: {len(sentences)} sentences, {pending_count(generator, sentences)} need a request (limits: {rpm}/min, {rpd or 'no'}/day, "
              f"{args.max_requests or 'no'} this run, {log.count()} sent in the last 24 h)")
        return EXIT_OK
    summary = generator.run(sentences)
    print(f"{book} [{language}]: {summary.line(len(sentences))}")
    return summary.exit_code or (EXIT_ERRORS if summary.errors and not summary.rendered else EXIT_OK)


if __name__ == "__main__":
    sys.exit(main())
