"""Per-sentence clip manifest (Slice 246 A2), version 3.

`manifest.json` of one book and language = {"version": 3, "book", "language", "entries": [{"index", "sentence", "hash", "voice", "direction", "variant", "clip", "text"}]},
one entry per spoken sentence index (clip N is highlight N). `hash` is the SHA-256 of the NFC sentence text. `variant` is "<service>-<settings tag>", the tag being the hash
of the request built for a probe text with this voice and delivery (Adapter.settings_tag), so another model, voice, style or direction never reuses a clip.
The clip file name is derived from hash + voice + variant, so a cache hit is "that file exists": an edited sentence has a new hash and renders one clip; an unchanged
one (or the same words spoken again in the same voice and delivery) keeps its file. Entries of indexes past the end of the book are dropped on save.
"""
import hashlib
import json
import os
import re
import sys
import unicodedata

MANIFEST_VERSION = 3
MANIFEST_NAME = "manifest.json"
UNSAFE_FILE_CHARS = re.compile(r"[^A-Za-z0-9_.-]")
HASH_CHARS_IN_NAME = 16


def text_hash(text):
    return hashlib.sha256(unicodedata.normalize("NFC", text).encode("utf8")).hexdigest()


def file_safe(value):
    """A voice or service name as a file-name part: anything but letters, digits, '_', '.' and '-' becomes '_'."""
    return UNSAFE_FILE_CHARS.sub("_", value)


def clip_stem(text, voice, variant):
    return f"{text_hash(text)[:HASH_CHARS_IN_NAME]}-{file_safe(voice)}-{file_safe(variant)}"


class Manifest:
    def __init__(self, folder, book, language, readonly=False):
        """`readonly` (a dry run, which holds no folder lock) never renames an unreadable manifest: it warns and starts empty."""
        self.folder = folder
        self.book = book
        self.language = language
        self.readonly = readonly
        self.entries = {}
        path = os.path.join(folder, MANIFEST_NAME)
        if os.path.isfile(path):
            self.load(path)

    def load(self, path):
        """Reads the manifest; an unreadable or other-version file is an empty list, kept as manifest.json.bak and warned about, never silently overwritten."""
        try:
            with open(path, encoding="utf8") as f:
                data = json.load(f)
            if data["version"] != MANIFEST_VERSION:
                raise ValueError(f"version {data['version']}, expected {MANIFEST_VERSION}")
            loaded = {e["index"]: e for e in data["entries"] if e["clip"]}
        except (OSError, ValueError, KeyError, TypeError) as error:
            backup = path + ".bak"
            if self.readonly:
                print(f"WARNING: {path} is unreadable or old ({type(error).__name__}: {error}); counting from empty, the file is left as it is", file=sys.stderr)
                return
            os.replace(path, backup)
            print(f"WARNING: {path} is unreadable or old ({type(error).__name__}: {error}); starting empty, the old file is kept as {backup}", file=sys.stderr)
            return
        self.entries = loaded

    def has_clip(self, clip):
        return os.path.isfile(os.path.join(self.folder, clip))

    def set(self, sentence, delivery, variant, clip):
        self.entries[sentence["index"]] = {
            "index": sentence["index"], "sentence": sentence.get("sentence"), "hash": text_hash(sentence["text"]), "voice": delivery.voice,
            "direction": delivery.direction, "variant": variant, "clip": clip, "text": sentence["text"]}
        if delivery.say:
            self.entries[sentence["index"]]["say"] = delivery.say  # the text Gemini read instead of `text` (Slice 246 A6); the clip name is keyed by it

    def drop_stale(self, current):
        """Forgets every entry that does not match the current dump: `current` maps a sentence index to its (delivery, variant, clip stem). An entry whose text, voice or
        variant changed (or whose index left the book) must never reach the packager under the new words, even when the run ends before reaching that sentence."""
        dropped = 0
        for index in list(self.entries):
            entry = self.entries[index]
            if index not in current or not self.matches(entry, current[index]):
                del self.entries[index]
                dropped += 1
        return dropped

    def matches(self, entry, identity):
        """The entry is for this sentence's voice, variant and text AND its clip file is still on disk (a deleted clip must never reach the packager)."""
        delivery, variant, stem = identity
        clip = entry.get("clip", "")
        return entry.get("voice") == delivery.voice and entry.get("variant") == variant and clip.startswith(stem + ".") and self.has_clip(clip)

    def save(self, keep_indexes):
        os.makedirs(self.folder, exist_ok=True)
        entries = [self.entries[i] for i in sorted(self.entries) if i in keep_indexes]
        data = {"version": MANIFEST_VERSION, "book": self.book, "language": self.language, "entries": entries}
        path = os.path.join(self.folder, MANIFEST_NAME)
        temp = path + ".tmp"
        with open(temp, "w", encoding="utf8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)
        os.replace(temp, path)  # atomic: a kill mid-save leaves the old manifest, never a truncated one
