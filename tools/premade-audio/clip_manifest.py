"""Per-sentence clip manifest with a text-hash cache (Slice 246 step 3: generator core).

manifest.json = {"version": 2, "entries": [{"hash", "voice", "variant", "clip", "text"}]}; an entry is the clip rendered for one sentence in one voice by one
service with one set of settings. The hash is the SHA-256 of the NFC sentence text (hex), so a small edit changes only that sentence's hash and re-renders only
that clip; `variant` is "<service>-<settings tag>" (Adapter.settings_tag), so another service, model or style never reuses or overwrites a clip.
"""
import hashlib
import json
import os
import re
import sys
import unicodedata

from adapter_base import STATUS_OK, STATUS_UNSUPPORTED, is_fatal

MANIFEST_VERSION = 2
MANIFEST_NAME = "manifest.json"
UNSAFE_FILE_CHARS = re.compile(r"[^A-Za-z0-9_.-]")
MAX_CONSECUTIVE_ERRORS = 3


def text_hash(text):
    return hashlib.sha256(unicodedata.normalize("NFC", text).encode("utf8")).hexdigest()


class Manifest:
    def __init__(self, folder):
        self.folder = folder
        self.entries = {}
        path = os.path.join(folder, MANIFEST_NAME)
        if os.path.isfile(path):
            self.load(path)

    def load(self, path):
        """Reads the manifest; an unreadable or old-shape file is an empty cache, kept as manifest.json.bak and warned about, never silently overwritten."""
        try:
            with open(path, encoding="utf8") as f:
                data = json.load(f)
            loaded = {(e["hash"], e["voice"], e["variant"]): e for e in data["entries"]}
        except (OSError, ValueError, KeyError, TypeError) as error:
            backup = path + ".bak"
            os.replace(path, backup)
            print(f"WARNING: {path} is unreadable or old ({type(error).__name__}: {error}); starting with an empty cache, the old file is kept as {backup}", file=sys.stderr)
            return
        self.entries = loaded

    def cached(self, text, voice, variant):
        """The entry for this sentence, voice and variant when its clip file still exists."""
        e = self.entries.get((text_hash(text), voice, variant))
        return e if e and os.path.isfile(os.path.join(self.folder, e["clip"])) else None

    def add(self, text, voice, variant, clip):
        digest = text_hash(text)
        self.entries[(digest, voice, variant)] = {"hash": digest, "voice": voice, "variant": variant, "clip": clip, "text": text}

    def save(self):
        os.makedirs(self.folder, exist_ok=True)
        data = {"version": MANIFEST_VERSION, "entries": list(self.entries.values())}
        path = os.path.join(self.folder, MANIFEST_NAME)
        temp = path + ".tmp"
        with open(temp, "w", encoding="utf8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)
        os.replace(temp, path)  # atomic: a kill mid-save leaves the old manifest, never a truncated one


def file_safe(value):
    """A voice or service name as a file-name part: anything but letters, digits, '_', '.' and '-' becomes '_'."""
    return UNSAFE_FILE_CHARS.sub("_", value)


def variant_of(adapter, voice):
    return f"{adapter.name}-{adapter.settings_tag(voice)}"


def render_sentences(sentences, voice, adapter, folder):
    """Renders each sentence not already cached; returns (results of the sentences rendered now, count served from the cache). Saves after each clip so a run resumes.
    Stops at the first skipped result or fatal answer (no key, 401/402/403/429) and after MAX_CONSECUTIVE_ERRORS errors in a row, so a bad key makes one call, not hundreds."""
    manifest = Manifest(folder)
    os.makedirs(folder, exist_ok=True)
    variant = variant_of(adapter, voice)
    results, hits, errors = [], 0, 0
    for text in sentences:
        if manifest.cached(text, voice, variant):
            hits += 1
            continue
        stem = f"{text_hash(text)[:16]}-{file_safe(voice)}-{file_safe(variant)}"
        result = adapter.render(text, voice, os.path.join(folder, stem))
        results.append(result)
        if result.status == STATUS_OK:
            errors = 0
            manifest.add(text, voice, variant, os.path.basename(result.path))
            manifest.save()
            continue
        if result.status == STATUS_UNSUPPORTED:  # this service cannot take this sentence: skip it, it is not a failing service
            continue
        errors += 1
        if is_fatal(result) or errors >= MAX_CONSECUTIVE_ERRORS:
            break
    return results, hits
