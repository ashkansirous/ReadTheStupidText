"""Per-sentence Gemini clip generator for a Dino book (Slice 246 A2-A4). Runs on the user's PC; one request per spoken sentence.

    dotnet run --project tools/dino/DinoReader.BookBuilder -- dump-sentences three-little-pigs --lang fa --out tmp/pigs-fa.json
    python tools/dino/premade-audio/generate.py --sentences tmp/pigs-fa.json --out artifacts/premade-audio [--free] [--max-requests N] [--dry-run]
python tools/dino/premade-audio/generate.py --sentences tmp/pigs-fa.json --out artifacts/premade-audio --variants 17 --say "<spelling 1>" --say "<spelling 2>"   (A6 listening test)

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
import batch  # noqa: E402
import encode  # noqa: E402
import pacing  # noqa: E402
from adapter_base import HTTP_RATE_LIMITED, STATUS_OK, STATUS_SKIPPED, is_fatal  # noqa: E402
from adapter_gemini import Gemini  # noqa: E402
from clip_manifest import Manifest, clip_stem  # noqa: E402
from run_common import (EXIT_AUTH, EXIT_ENCODE, EXIT_ERRORS, EXIT_LOCKED, EXIT_NO_FFMPEG, EXIT_NO_KEY, EXIT_OK, EXIT_RATE_LIMITED, FORMAT_WAV,  # noqa: E402,F401
                        MAX_CONSECUTIVE_SILENT, MAX_SILENT_PER_RUN, OUTCOME_SILENT, Summary)

REPO_ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
DEFAULT_OUT = os.path.join(REPO_ROOT, "artifacts", "premade-audio")
CONTENT_ROOT = os.path.join(REPO_ROOT, "content", "dino")
AUDIO_FOLDER = "audio"  # content/dino/<book>/audio/<language>/: the clip cache, tracked in Git LFS (Slice 265)
LFS_POINTER_PREFIX = b"version https://git-lfs"
CAST_NAME = "audio-cast.json"
FORMAT_M4A = "m4a"
VARIANTS_FOLDER = "variants"
VARIANT_TEMP_PREFIX = "new-"  # a candidate is rendered under this name and moved over <n> only on success
MAX_CONSECUTIVE_ERRORS = 3
MAX_RETRY_WAIT_SECONDS = 300.0
DEFAULT_RETRY_WAIT_SECONDS = 60.0


def spoken_text(sentence, delivery):
    """What Gemini is asked to read: the cast's `say` for this line, else the book text."""
    return delivery.say or sentence["text"]


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
        return delivery, variant, clip_stem(spoken_text(sentence, delivery), delivery.voice, variant)

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
        result = self.adapter.render(spoken_text(sentence, delivery), delivery.voice, os.path.join(self.folder, stem), delivery.prompt)
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


def render_variants(generator, sentence, says):
    """Listening test (A6): renders each candidate `say` for one line to <generator.folder>/<n>.m4a (n from 1) in the line's voice and delivery. The manifest is never
    read or written, existing files are replaced, and every request counts against the same budget as a normal run. Returns a Summary (rendered = files written)."""
    os.makedirs(generator.folder, exist_ok=True)
    summary = Summary()
    line = generator.cast.delivery(sentence)
    delivery = audio_cast.Delivery(line.voice, line.style, line.direction)  # without the line's own `say`: the candidate is what is read
    for n, say in enumerate(says, start=1):
        outcome = generator.render_one(dict(sentence, text=say), delivery, f"{VARIANT_TEMP_PREFIX}{n}", summary)
        if outcome == STATUS_OK:
            keep_variant(generator, n)
            summary.rendered += 1
            generator.log(f"variant {n}: {os.path.join(generator.folder, str(n) + '.' + generator.fmt)}")
        elif outcome not in ("", OUTCOME_SILENT):
            break  # the run must end (budget, daily limit, fatal): summary.stopped says why
        else:
            generator.log(f"variant {n} not saved: {summary.errors[-1] if summary.errors else 'no detail'}")
    if summary.rendered == len(says):
        remove_stale_variants(generator, len(says))
    return summary


def keep_variant(generator, n):
    """Replaces <n>.<fmt> with the clip just rendered under its temporary name, so a failed request never costs the old file."""
    temp = os.path.join(generator.folder, f"{VARIANT_TEMP_PREFIX}{n}.{generator.fmt}")
    os.replace(temp, os.path.join(generator.folder, f"{n}.{generator.fmt}"))


def remove_stale_variants(generator, keep_count):
    """After a complete run, deletes numbered files beyond the candidates just rendered, so the folder holds only the latest run."""
    for name in os.listdir(generator.folder):
        stem, _, extension = name.rpartition(".")
        if stem.isdigit() and int(stem) > keep_count and extension in (generator.fmt, FORMAT_WAV):
            os.remove(os.path.join(generator.folder, name))


def load_sentences(path):
    with open(path, encoding="utf8") as f:
        data = json.load(f)
    # Slice 255: an animal sound is a bundled real recording in the app, never a rendered clip. The dump no longer lists them; an older dump still might.
    return data["book"], data["language"], [s for s in data["sentences"] if not s.get("sound")]


def parse_args(argv):
    p = argparse.ArgumentParser(description="Render one Gemini clip per spoken sentence of a Dino book.")
    p.add_argument("--sentences", required=True, help="JSON from BookBuilder dump-sentences")
    p.add_argument("--cast", help=f"audio cast file (default content/dino/<book>/{CAST_NAME})")
    p.add_argument("--out", help="scratch output root, <out>/<book>/<language>/ (default: the clips go to content/dino/<book>/audio/<language>/, Git LFS, Slice 265)")
    p.add_argument("--free", action="store_true", help=f"free tier preset: {pacing.FREE_RPM} requests a minute, {pacing.FREE_RPD} a day")
    p.add_argument("--rpm", type=int, help=f"requests per minute (0 = no pacing; default {pacing.DEFAULT_RPM}, a placeholder for the paid tier)")
    p.add_argument("--rpd", type=int, help="requests per rolling 24 hours (0 = no limit; default none)")
    p.add_argument("--max-requests", type=int, default=pacing.DEFAULT_MAX_REQUESTS,
                   help=f"most requests to send in this run (default {pacing.DEFAULT_MAX_REQUESTS}; 0 = no limit)")
    p.add_argument("--format", choices=(FORMAT_M4A, FORMAT_WAV), default=FORMAT_M4A, help="m4a needs ffmpeg; wav keeps the raw vendor clips")
    p.add_argument("--dry-run", action="store_true", help="check the cast and list what would be rendered; sends nothing")
    p.add_argument("--batch", action="store_true", help="render the missing clips as Gemini Batch API jobs (not counted against the 100/day cap); resumable, see README")
    p.add_argument("--batch-chunk", type=int, default=batch.DEFAULT_CHUNK_REQUESTS, metavar="N", help=f"most requests per batch job (default {batch.DEFAULT_CHUNK_REQUESTS})")
    p.add_argument("--poll-seconds", type=float, default=batch.DEFAULT_POLL_SECONDS, help=f"seconds between batch polls (default {batch.DEFAULT_POLL_SECONDS:g})")
    p.add_argument("--wait-minutes", type=float, default=batch.DEFAULT_WAIT_MINUTES, help=f"most minutes to wait for batches (default {batch.DEFAULT_WAIT_MINUTES:g}); then exit {batch.EXIT_BATCH_PENDING}, state kept")
    p.add_argument("--variants", type=int, metavar="INDEX", help="listening test: render the --say candidates for this sentence index to <out>/<book>/<language>/variants/<INDEX>/<n>.m4a; "
                   "the manifest is not touched")
    p.add_argument("--say", action="append", default=[], metavar="TEXT", help="a candidate spoken text for --variants (repeat for each)")
    args = p.parse_args(argv)
    args.say = [audio_cast.normalize_say(s) for s in args.say]
    if not all(args.say):
        p.error("--say TEXT must not be empty or whitespace only")
    if (args.variants is None) != (not args.say):
        p.error("--variants INDEX and at least one --say TEXT go together")
    if args.batch and args.variants is not None:
        p.error("--batch and --variants do not go together")
    return args


def limits(args):
    rpm = args.rpm if args.rpm is not None else (pacing.FREE_RPM if args.free else pacing.DEFAULT_RPM)
    rpd = args.rpd if args.rpd is not None else (pacing.FREE_RPD if args.free else pacing.NO_LIMIT)
    return rpm, rpd


def pending_count(generator, sentences):
    """Sentences that need a request: no clip, and no kept vendor WAV waiting to be encoded (that one costs nothing)."""
    stems = [generator.identity(s)[2] for s in sentences]
    return sum(0 if generator.has_clip(stem) or generator.has_wav(stem) else 1 for stem in set(stems))  # sentences sharing a stem share one clip file


def batch_dry_run_line(args, generator, sentences, max_bytes=batch.MAX_CHUNK_BYTES):
    """What the real --batch run would submit: the misses not already in a pending batch (batch-state.json), within --max-requests, in the jobs the runner would cut (by count and by size)."""
    chunk = max(1, args.batch_chunk)
    runner = batch.BatchRunner(generator, os.path.join(generator.folder, batch.STATE_NAME), chunk, args.max_requests, max_bytes=max_bytes)
    try:
        state = runner.read_state()
    except batch.BatchStopped as stop:
        return f"stopped: {stop}"
    covered = {key for pending in state["batches"] for key in pending["keys"]} | set((state["intent"] or {}).get("keys", []))
    stems = {generator.identity(s)[2] for s in sentences}
    misses = sorted(stem for stem in stems if not generator.has_clip(stem) and not generator.has_wav(stem))
    fresh = runner.fresh_stems(misses, covered, state["silent"], state["failures"])
    need = len(fresh)
    jobs = len(batch.chunk_entries(runner.entries_for(fresh, runner.groups(sentences)), chunk, max_bytes))
    in_flight = len(covered & set(misses))
    return (f"{len(sentences)} sentences, {need} need a batch request in {jobs} job(s) of up to {chunk}"
            + (f"; {in_flight} already in pending batches" if in_flight else ""))


def main(argv=None):
    args = parse_args(argv)
    book, language, sentences = load_sentences(args.sentences)
    cast = audio_cast.load(args.cast or os.path.join(REPO_ROOT, "content", "dino", book, CAST_NAME))
    cast.check(sentences, language)
    pointer = lfs_pointer_in(clips_folder(args.out, book, language))
    if pointer:
        print(f"{pointer} is a Git LFS pointer, not the real file (.lfsconfig keeps the clips out of a normal checkout); run: git lfs pull --include \"content/dino/{book}/audio/{language}/**\" --exclude \"\"", file=sys.stderr)
        return EXIT_ERRORS
    ffmpeg = encode.find_ffmpeg() if args.format == FORMAT_M4A else None
    if args.dry_run:
        return run_locked(args, book, language, sentences, cast, ffmpeg)  # sends nothing, so it needs neither ffmpeg nor the folder lock
    if args.format == FORMAT_M4A and not ffmpeg:
        print("ffmpeg was not found on PATH (set $FFMPEG to its path); rerun with --format wav to keep raw WAV clips", file=sys.stderr)
        return EXIT_NO_FFMPEG
    try:
        with pacing.RunLock(args.out or DEFAULT_OUT):
            return run_locked(args, book, language, sentences, cast, ffmpeg)
    except pacing.RunLocked as error:
        print(str(error), file=sys.stderr)
        return EXIT_LOCKED


def run_variants(args, generator, sentences):
    sentence = next((s for s in sentences if s["index"] == args.variants), None)
    if sentence is None:
        print(f"the book has no sentence {args.variants}", file=sys.stderr)
        return EXIT_ERRORS
    generator.folder = os.path.join(generator.folder, VARIANTS_FOLDER, str(args.variants))
    summary = render_variants(generator, sentence, args.say)
    print(f"{generator.manifest.book} [{generator.manifest.language}] sentence {args.variants}: {summary.rendered} of {len(args.say)} variants in {generator.folder}"
          + (f"; stopped: {summary.stopped}" if summary.stopped else ""))
    return summary.exit_code or (EXIT_OK if summary.rendered == len(args.say) else EXIT_ERRORS)


def run_batch(args, generator, sentences):
    """--batch: the cache misses go to the Batch API (batch.py); no pacing or usage log, batch jobs are outside the per-minute and per-day caps."""
    if generator.adapter.missing():
        print("skipped: no key (" + ", ".join(generator.adapter.missing()) + ")", file=sys.stderr)
        summary = Summary()
        summary.stop("no key", EXIT_NO_KEY)
        return summary
    state_path = os.path.join(generator.folder, batch.STATE_NAME)
    runner = batch.BatchRunner(generator, state_path, args.batch_chunk, args.max_requests, args.poll_seconds, args.wait_minutes)
    return runner.run(sentences)


def clips_folder(out, book, language):
    """Where one book-language's clips and manifest live: the book's own LFS folder, or <out>/<book>/<language> when --out names a scratch root."""
    if out:
        return os.path.join(out, book, language)
    return os.path.join(CONTENT_ROOT, book, AUDIO_FOLDER, language)


def lfs_pointer_in(folder):
    """The first file in the clip folder that is a Git LFS pointer (a text stub, not the clip), else None: a pointer manifest would be parsed as empty and every sentence re-requested."""
    if not os.path.isdir(folder):
        return None
    for name in sorted(os.listdir(folder)):
        path = os.path.join(folder, name)
        if os.path.isfile(path):
            with open(path, "rb") as handle:
                if handle.read(len(LFS_POINTER_PREFIX)) == LFS_POINTER_PREFIX:
                    return path
    return None


def run_locked(args, book, language, sentences, cast, ffmpeg):
    rpm, rpd = limits(args)
    log = pacing.UsageLog(pacing.usage_path(args.out or DEFAULT_OUT))
    pacer = pacing.Pacer(log, rpm, rpd, args.max_requests)
    generator = Generator(Gemini(), cast, clips_folder(args.out, book, language), pacer, book, language, args.format, ffmpeg,
                          readonly=args.dry_run or args.variants is not None)
    if args.dry_run and args.batch:
        print(f"{book} [{language}]: {batch_dry_run_line(args, generator, sentences)}")
        return EXIT_OK
    if args.dry_run:
        print(f"{book} [{language}]: {len(sentences)} sentences, {pending_count(generator, sentences)} need a request (limits: {rpm}/min, {rpd or 'no'}/day, "
              f"{args.max_requests or 'no'} this run, {log.count()} sent in the last 24 h)")
        return EXIT_OK
    if args.variants is not None:
        return run_variants(args, generator, sentences)
    summary = run_batch(args, generator, sentences) if args.batch else generator.run(sentences)
    print(f"{book} [{language}]: {summary.line(len(sentences))}")
    for note in summary.notes:
        print(f"note: {note}")
    return exit_code_for(generator, sentences, summary)


def missing_clips(generator, sentences, summary):
    """The stems of the book that have no clip and are not on the held-back list (clips an earlier run held back: silent, or failed in several batch results)."""
    stems = {generator.identity(s)[2] for s in sentences}
    return sorted(stem for stem in stems if not generator.has_clip(stem) and not generator.has_wav(stem) and stem not in summary.held)


def exit_code_for(generator, sentences, summary):
    """One rule: a stop's own code wins; otherwise 0 only when the run had no errors and every expected clip exists or is held back, else 1."""
    if summary.exit_code:
        return summary.exit_code
    return EXIT_ERRORS if summary.errors or missing_clips(generator, sentences, summary) else EXIT_OK


if __name__ == "__main__":
    sys.exit(main())
