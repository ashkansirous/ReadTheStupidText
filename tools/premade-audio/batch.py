"""Gemini Batch API mode of the clip generator (Slice 246): the cache misses of a book-language go to the vendor in a few batch jobs instead of one request per sentence.

The paid tier caps gemini-3.8-flash-tts at 100 requests a day and 10 a minute (AI Studio rate-limit page, 2026-11-03); batch jobs are not counted against that
(proven 2026-11-03: a batch submitted after the day's 100 succeeded). Turnaround was about 2 minutes for 20 requests; the target is 24 h and a job expires after 48 h.

Shapes proven live 2026-11-03: POST models/<model>:batchGenerateContent with {"batch": {"display_name", "input_config": {"requests": {"requests": [{"request": <generateContent body>,
"metadata": {"key"}}]}}}}; GET <batch name> until the state is final; results in response.inlinedResponses.inlinedResponses[], each with metadata.key and either
response.candidates[0].content.parts[0].inlineData.data (base64 WAV) or error. A clip is keyed by its file stem (hash + voice + variant), so the clip names, cache and manifest
are the unary path's and a batch clip and a unary clip of the same piece are interchangeable.

The submitted batch names and the keys they cover are kept in <folder>/batch-state.json before polling starts; a rerun polls them instead of submitting again.
"""
import base64
import json
import os
import time
import urllib.parse
import uuid

from adapter_base import STATUS_OK, Audio, HttpError, Request, audio_problem
from adapter_gemini import BATCH_BASE, MODEL, wav_bytes
from run_common import EXIT_ENCODE, FORMAT_WAV, OUTCOME_SILENT, Summary

STATE_NAME = "batch-state.json"
DEFAULT_CHUNK_REQUESTS = 200
MAX_CHUNK_BYTES = 15 * 1024 * 1024  # the vendor allows 20 MB of inline requests; stay well under
DEFAULT_POLL_SECONDS = 20.0
DEFAULT_WAIT_MINUTES = 60.0
SECONDS_PER_MINUTE = 60
FINAL_STATES = ("SUCCEEDED", "FAILED", "CANCELLED", "EXPIRED")  # the vendor prefixes them BATCH_STATE_ or JOB_STATE_
STATE_SUCCEEDED = "SUCCEEDED"
HTTP_BAD_REQUEST = 400
HTTP_CODES_NO_RETRY = (HTTP_BAD_REQUEST, 401, 403)
HTTP_CODES_JOB_GONE = (404, 410)  # an expired or deleted job: it will never finish
LIST_PAGE_SIZE = 100
LIST_MAX_PAGES = 5
DISPLAY_ID_LENGTH = 8
EXIT_BATCH_FAILED = 1
EXIT_BATCH_AUTH = 4
EXIT_BATCH_PENDING = 8  # the wait ran out with a batch still running: its state is kept, rerun to resume
EXIT_BATCH_REFUSED = 9  # HTTP 400 on submit: the request itself was refused (not the key); nothing was created
MAX_KEY_ATTEMPTS = 2  # a key that failed inside a succeeded job this many times is not requested (and paid for) again
LISTING_KEY = "operations"
LISTING_META_KEY = "metadata"
LISTING_NAME_KEY = "displayName"
LISTING_TOKEN_KEY = "nextPageToken"
RESUME_HINT = "rerun the same command to resume: it polls the pending batch instead of submitting again"


def batch_request(text, voice, style=""):
    """One generateContent body of a batch entry (the same voice, style and text the unary request carries)."""
    part = {"text": text}
    if style:
        part["speech_metadata"] = {"style": style}
    return {"contents": [{"role": "user", "parts": [part]}],
            "generationConfig": {"responseModalities": ["AUDIO"], "speechConfig": {"voiceConfig": {"prebuiltVoiceConfig": {"voiceName": voice}}}}}


def batch_body(entries, display_name):
    """entries = [(key, generateContent body)]."""
    items = [{"request": body, "metadata": {"key": key}} for key, body in entries]
    return {"batch": {"display_name": display_name, "input_config": {"requests": {"requests": items}}}}


def entry_bytes(entry):
    return len(json.dumps(entry[1], ensure_ascii=False).encode("utf8"))


def chunk_entries(entries, max_requests, max_bytes=MAX_CHUNK_BYTES):
    """Splits entries into batches of at most `max_requests` requests and `max_bytes` of request JSON (a single oversize entry still gets its own batch)."""
    chunks, current, size = [], [], 0
    for entry in entries:
        weight = entry_bytes(entry)
        if current and (len(current) >= max_requests or size + weight > max_bytes):
            chunks.append(current)
            current, size = [], 0
        current.append(entry)
        size += weight
    if current:
        chunks.append(current)
    return chunks


def state_name(job):
    """The job state without its BATCH_STATE_ / JOB_STATE_ prefix; '' when the job reports none."""
    raw = (job.get("metadata") or {}).get("state") or job.get("state") or ""
    return raw.rsplit("_", 1)[-1] if raw.rsplit("_", 1)[-1] in FINAL_STATES else raw


def job_entries(job):
    holder = job.get("response") or job.get("dest") or {}
    inner = holder.get("inlinedResponses", [])
    return inner.get("inlinedResponses", []) if isinstance(inner, dict) else inner


def is_final(job):
    return state_name(job) in FINAL_STATES or bool(job.get("done"))


def job_succeeded(job):
    name = state_name(job)
    return name == STATE_SUCCEEDED or (name not in FINAL_STATES and not job.get("error"))


def entry_audio(entry):
    """The WAV bytes of one result entry, or raises ValueError with why there is none."""
    if "error" in entry:
        raise ValueError(json.dumps(entry["error"], ensure_ascii=False)[:300])
    try:
        part = entry["response"]["candidates"][0]["content"]["parts"][0]["inlineData"]
        data = wav_bytes(base64.b64decode(part["data"]))
    except (KeyError, IndexError, TypeError) as error:
        raise ValueError(f"no audio in the result ({type(error).__name__}: {error})") from error
    problem = audio_problem(Audio(data))
    if problem:
        raise ValueError(problem)
    return data


def listing_items(listing):
    """The operations of one page of GET batches (shape read live 2026-11-03: operations[] with name and metadata.displayName, plus nextPageToken).
    Anything else raises ValueError: a lookup that cannot read the list must never be taken for 'not found'."""
    if not isinstance(listing, dict):
        raise ValueError("the batches listing is not an object")
    if LISTING_KEY not in listing:
        if set(listing) - {LISTING_TOKEN_KEY}:
            raise ValueError(f"the batches listing has an unexpected shape (keys {sorted(listing)})")
        return []  # an account with no batches answers an empty object
    items = listing[LISTING_KEY]
    if not isinstance(items, list):
        raise ValueError(f"the batches listing's {LISTING_KEY} is not a list")
    for item in items:
        if not isinstance(item, dict) or not isinstance(item.get("name"), str) or not isinstance(item.get(LISTING_META_KEY), dict):
            raise ValueError("a batches listing entry has an unexpected shape (no name or metadata)")
    return items


def listed_display_name(item):
    return item[LISTING_META_KEY].get(LISTING_NAME_KEY) or ""


class BatchStopped(Exception):
    """The run must end now: `exit_code` and the message say why."""

    def __init__(self, message, exit_code):
        super().__init__(message)
        self.exit_code = exit_code


class BatchRunner:
    def __init__(self, generator, state_path, chunk_requests=DEFAULT_CHUNK_REQUESTS, max_requests=0, poll_seconds=DEFAULT_POLL_SECONDS,
                 wait_minutes=DEFAULT_WAIT_MINUTES, clock=time.monotonic, sleep=time.sleep, max_bytes=MAX_CHUNK_BYTES):
        self.generator = generator
        self.state_path = state_path
        self.chunk_requests = max(1, chunk_requests)
        self.max_requests = max_requests
        self.poll_seconds = poll_seconds
        self.wait_seconds = wait_minutes * SECONDS_PER_MINUTE
        self.clock = clock
        self.sleep = sleep
        self.max_bytes = max_bytes
        self.log = generator.log
        self.intent = None  # {"display_name", "keys"}: a submit that was started and whose answer is not known yet
        self.silent = []  # stems whose clip came back silent: not requested again by a rerun (each costs a paid request)
        self.failures = {}  # stem -> how many succeeded jobs returned an error or nothing for it

    # ---- transport -------------------------------------------------------------------------------------------------------------------

    def call(self, method, url, body=None):
        adapter = self.generator.adapter
        data = json.dumps(body, ensure_ascii=False).encode("utf8") if body is not None else None
        headers = {"x-goog-api-key": adapter.key(), "Content-Type": "application/json"}
        raw, _ = adapter.transport(Request(url, headers, data, method))
        return json.loads(raw)

    def display_name(self, number):
        """Unique per submit (book, language, a number and a short random id), so a lost response can be matched in the vendor's listing."""
        manifest = self.generator.manifest
        return f"rtst-{manifest.book}-{manifest.language}-{number}-{uuid.uuid4().hex[:DISPLAY_ID_LENGTH]}"

    def submit(self, entries, display_name):
        body = batch_body(entries, display_name)
        job = self.call("POST", f"{BATCH_BASE}/models/{MODEL}:batchGenerateContent", body)
        return job["name"]

    def fetch(self, name):
        return self.call("GET", f"{BATCH_BASE}/{urllib.parse.quote(name, safe='/')}")

    def find_by_display_name(self, display_name):
        """The name of the account's batch with this display name (newest pages first as the vendor lists them), or ''."""
        token = ""
        for _ in range(LIST_MAX_PAGES):
            query = f"pageSize={LIST_PAGE_SIZE}" + (f"&pageToken={urllib.parse.quote(token)}" if token else "")
            listing = self.call("GET", f"{BATCH_BASE}/batches?{query}")
            for item in listing_items(listing):
                if listed_display_name(item) == display_name:
                    return item["name"]
            token = listing.get(LISTING_TOKEN_KEY) or ""
            if not token:
                return ""
        raise ValueError(f"the batches listing has more than {LIST_MAX_PAGES} pages; {display_name} was not found in the ones read")

    # ---- state file ------------------------------------------------------------------------------------------------------------------

    def read_state(self):
        """The state file as {"batches": [...], "intent": dict or None, "silent": [...], "failures": {stem: count}}.
        Only a missing file is empty; one that exists but cannot be read stops the run (its pending batches are already paid for)."""
        empty = {"batches": [], "intent": None, "silent": [], "failures": {}}
        if not os.path.exists(self.state_path):
            return empty
        try:
            with open(self.state_path, encoding="utf8") as f:
                data = json.load(f)
            failures = {str(k): int(v) for k, v in (data.get("failures") or {}).items()}
            return {"batches": list(data["batches"]), "intent": data.get("intent"), "silent": list(data.get("silent") or []), "failures": failures}
        except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
            raise BatchStopped(f"{self.state_path} exists but cannot be read ({type(error).__name__}: {error}); it may hold batches already paid for, "
                               f"so nothing is submitted. Fix or move the file, then rerun", EXIT_BATCH_FAILED) from error

    def load_state(self):
        return self.read_state()["batches"]

    def save_state(self, batches):
        """Writes the batches with the runner's open intent and silent stems; the file goes away when all three are empty."""
        if not (batches or self.intent or self.silent or self.failures):
            if os.path.isfile(self.state_path):
                os.remove(self.state_path)
            return
        data = {"batches": batches}
        if self.intent:
            data["intent"] = self.intent
        if self.silent:
            data["silent"] = sorted(set(self.silent))
        if self.failures:
            data["failures"] = dict(sorted(self.failures.items()))
        temp = self.state_path + ".tmp"
        with open(temp, "w", encoding="utf8") as f:
            json.dump(data, f, indent=1)
        os.replace(temp, self.state_path)

    # ---- the run ---------------------------------------------------------------------------------------------------------------------

    def run(self, sentences):
        """Returns a run_common.Summary (rendered, cached, errors, stopped, exit_code)."""
        generator, summary = self.generator, Summary()
        os.makedirs(generator.folder, exist_ok=True)
        keep = {s["index"] for s in sentences}
        if generator.manifest.drop_stale({s["index"]: generator.identity(s) for s in sentences}):
            generator.manifest.save(keep)
        groups = self.groups(sentences)
        try:
            misses = self.settle_cached(groups, summary)
            self.drain(misses, groups, keep, summary)
        except BatchStopped as stop:
            summary.stop(str(stop), stop.exit_code)
        generator.manifest.save(keep)
        if summary.errors and not summary.exit_code:
            summary.exit_code = EXIT_BATCH_FAILED
        return summary

    def groups(self, sentences):
        """stem -> [(sentence, delivery, variant)]: sentences sharing a clip file share one batch entry."""
        groups = {}
        for s in sentences:
            delivery, variant, stem = self.generator.identity(s)
            groups.setdefault(stem, []).append((s, delivery, variant))
        return groups

    def settle_cached(self, groups, summary):
        """Records clips that already exist (and encodes WAVs left by an earlier run); returns the stems that still need a batch entry."""
        generator, misses = self.generator, []
        for stem, members in groups.items():
            if generator.has_clip(stem):
                self.record(stem, members)
                summary.cached += len(members)
            elif generator.has_wav(stem):
                self.adopt(stem, members, summary)
            else:
                misses.append(stem)
        return misses

    def adopt(self, stem, members, summary):
        outcome = self.generator.adopt_wav(members[0][0], stem, summary)
        if outcome == STATUS_OK:
            self.record(stem, members)
            summary.rendered += 1
            summary.cached += len(members) - 1  # the unary path counts the other sentences of a shared clip as already having it
        elif outcome != OUTCOME_SILENT:
            raise BatchStopped(outcome, EXIT_ENCODE)

    def record(self, stem, members):
        clip = f"{stem}.{self.generator.fmt}"
        for sentence, delivery, variant in members:
            self.generator.manifest.set(sentence, delivery, variant, clip)

    def exhausted(self, failures):
        return {stem for stem, count in (failures or {}).items() if count >= MAX_KEY_ATTEMPTS}

    def fresh_stems(self, misses, covered, silent=(), failures=None):
        """The misses that still need a new batch entry: not in a pending batch, not known to come back silent or to keep failing, and within --max-requests."""
        skip = set(silent) | self.exhausted(failures)
        fresh = [stem for stem in misses if stem not in covered and stem not in skip]
        if self.max_requests:
            fresh = fresh[:max(0, self.max_requests - len(covered))]
        return fresh

    def resolve_intent(self, pending):
        """A submit that was started but whose answer was lost: look for it by display name in the account's batches before anything is sent again."""
        intent = self.intent or {}
        name_wanted = intent.get("display_name")
        if name_wanted:
            try:
                name = self.find_by_display_name(name_wanted)
            except (HttpError, OSError, ValueError) as error:
                raise BatchStopped(f"an earlier submit ({name_wanted}) may have gone through and the account's batch list could not be read ({error}); "
                                   f"nothing is submitted again. {RESUME_HINT}", EXIT_BATCH_FAILED) from error
            if name:
                self.log(f"the earlier submit {name_wanted} was created as {name}; polling it")
                pending.append({"name": name, "keys": list(intent.get("keys", []))})
            else:
                self.log(f"the earlier submit {name_wanted} is not in the account's batches; submitting its clips again")
        self.intent = None
        self.save_state(pending)

    def drain(self, misses, groups, keep, summary):
        state = self.read_state()
        pending, self.intent = state["batches"], state["intent"]
        wanted = set(misses)
        self.silent = [stem for stem in state["silent"] if stem in wanted]
        self.failures = {stem: count for stem, count in state["failures"].items() if stem in wanted}
        self.resolve_intent(pending)
        covered = {key for batch in pending for key in batch["keys"]}
        fresh = self.fresh_stems(misses, covered, self.silent, self.failures)
        held = [stem for stem in misses if stem in self.silent and stem not in covered]
        if held:
            summary.errors.append(f"{len(held)} clip(s) came back silent in an earlier run and are not requested again (delete {STATE_NAME} to retry them)")
        gave_up = sorted(stem for stem in self.exhausted(self.failures) if stem in wanted and stem not in covered)
        if gave_up:
            summary.errors.append(f"{len(gave_up)} clip(s) failed in {MAX_KEY_ATTEMPTS} batch results and are not requested again (delete {STATE_NAME} to retry): {', '.join(gave_up)}")
        if pending:
            self.log(f"{len(pending)} batch(es) from an earlier run are pending; polling them")
        pending += self.submit_all(fresh, groups, pending)
        self.poll_all(pending, groups, keep, summary)

    def entries_for(self, stems, groups):
        entries = []
        for stem in stems:
            sentence, delivery, _ = groups[stem][0]
            entries.append((stem, batch_request(delivery.say or sentence["text"], delivery.voice, delivery.prompt)))
        return entries

    def submit_all(self, stems, groups, pending):
        """Submits the stems in chunks; each batch name is written to the state file before the next step. Returns the new state entries."""
        added = []
        for number, chunk in enumerate(chunk_entries(self.entries_for(stems, groups), self.chunk_requests, self.max_bytes), start=len(pending) + 1):
            display_name, keys = self.display_name(number), [key for key, _ in chunk]
            self.intent = {"display_name": display_name, "keys": keys}  # written before the POST: a lost response is found again by this name
            self.save_state(pending + added)
            try:
                name = self.submit(chunk, display_name)
            except HttpError as error:
                refused = error.code in HTTP_CODES_NO_RETRY
                self.intent = None if refused else self.intent  # a 400/401/403 created nothing; any other error may have
                self.save_state(pending + added)
                code = EXIT_BATCH_FAILED if not refused else EXIT_BATCH_REFUSED if error.code == HTTP_BAD_REQUEST else EXIT_BATCH_AUTH
                raise BatchStopped(f"batch submit refused, not retrying: {error}", code) from error
            except (OSError, ValueError, KeyError) as error:
                raise BatchStopped(f"batch submit {display_name} got no usable answer ({type(error).__name__}: {error}); it may have been created, "
                                   f"so the next run looks for it in the account's batches before submitting again", EXIT_BATCH_FAILED) from error
            self.intent = None
            added.append({"name": name, "keys": keys})
            self.save_state(pending + added)
            self.log(f"submitted {name}: {len(chunk)} requests")
        return added

    def poll_all(self, pending, groups, keep, summary):
        deadline = self.clock() + self.wait_seconds
        while pending:
            for batch in list(pending):
                if self.poll_one(batch, groups, keep, summary):
                    pending.remove(batch)
                    self.save_state(pending)
            if not pending:
                return
            if self.clock() + self.poll_seconds > deadline:
                names = ", ".join(b["name"] for b in pending)
                raise BatchStopped(f"still running after {self.wait_seconds / SECONDS_PER_MINUTE:g} min ({names}); {RESUME_HINT}", EXIT_BATCH_PENDING)
            self.sleep(self.poll_seconds)

    def poll_one(self, batch, groups, keep, summary):
        """True when the batch is final and handled (its state entry can go)."""
        try:
            job = self.fetch(batch["name"])
        except HttpError as error:
            if error.code in HTTP_CODES_NO_RETRY:
                raise BatchStopped(f"polling {batch['name']} refused, not retrying: {error}", EXIT_BATCH_AUTH) from error
            if error.code in HTTP_CODES_JOB_GONE:
                summary.errors.append(f"{batch['name']}: job gone ({error})")
                self.log(f"{batch['name']} no longer exists ({error}); its {len(batch['keys'])} requests stay misses")
                return True
            self.log(f"poll of {batch['name']} failed ({error}); trying again")
            return False
        except (OSError, ValueError) as error:
            self.log(f"poll of {batch['name']} failed ({type(error).__name__}: {error}); trying again")
            return False
        if not is_final(job):
            self.log(f"{batch['name']}: {state_name(job) or 'running'}")
            return False
        if not job_succeeded(job):
            summary.errors.append(f"{batch['name']}: job {state_name(job)}")
            self.log(f"{batch['name']} ended {state_name(job)}; its {len(batch['keys'])} requests stay misses")
            return True
        self.save_results(batch, job, groups, keep, summary)
        return True

    def save_results(self, batch, job, groups, keep, summary):
        """Writes every good WAV first (kept on disk if encoding then fails), then encodes and records each; failed keys stay misses."""
        wanted, wavs = set(batch["keys"]), {}
        for entry in job_entries(job):
            key = (entry.get("metadata") or {}).get("key", "")
            if key not in wanted:
                self.log(f"result for an unknown key {key!r} ignored")
                continue
            wanted.discard(key)
            try:
                wavs[key] = entry_audio(entry)
            except ValueError as error:
                self.note_failure(key)
                summary.errors.append(f"{key}: {error}")
                self.log(f"failed key {key}: {error}; it stays a miss (attempt {self.failures[key]} of {MAX_KEY_ATTEMPTS})")
        for key in sorted(wanted):
            self.note_failure(key)
            summary.errors.append(f"{key}: no result returned")
            self.log(f"failed key {key}: no result returned; it stays a miss (attempt {self.failures[key]} of {MAX_KEY_ATTEMPTS})")
        for key in wavs:
            self.failures.pop(key, None)
        self.encode_all(wavs, groups, keep, summary)

    def note_failure(self, key):
        self.failures[key] = self.failures.get(key, 0) + 1

    def encode_all(self, wavs, groups, keep, summary):
        generator = self.generator
        for stem in [stem for stem in wavs if stem not in groups]:  # a book edited after the submit: its result no longer belongs to any sentence
            self.log(f"result for {stem} is no longer in the sentence set; dropped")
            del wavs[stem]
        for stem, data in wavs.items():
            path = os.path.join(generator.folder, f"{stem}.{FORMAT_WAV}")
            with open(path, "wb") as f:
                f.write(data)
        for stem in wavs:
            members = groups[stem]
            path = os.path.join(generator.folder, f"{stem}.{FORMAT_WAV}")
            outcome = generator.finish_clip(path, members[0][0], stem, summary)
            if outcome == STATUS_OK:
                self.record(stem, members)
                summary.rendered += 1
                summary.cached += len(members) - 1  # the unary path counts the other sentences of a shared clip as already having it
            elif outcome == OUTCOME_SILENT:
                self.silent.append(stem)
            else:
                raise BatchStopped(outcome, EXIT_ENCODE)
        generator.manifest.save(keep)
