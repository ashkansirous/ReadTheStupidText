"""The cast of a book's audio (Slice 246 A2): `content/dino/<book>/audio-cast.json`.

{
  "version": 1,
  "narrator": {"voice": "Hali", "style": "<delivery for narration>"},
  "characters": {"<speaker name as in the sentence dump>": {"voice": "Cleo", "style": "<delivery>", "direction": "<optional, applies to all their lines>"}},
  "directions": {"<sha-256 text hash>": "<direction>"},        // optional, follows the spoken text
  "lines": [                                                    // optional, one entry per line
    {"speaker": "<speaker as in the dump, null for the narrator>", "source": "<the line's English source text>",
     "occurrence": 2,                                           // 1-based; only when that speaker says that source more than once in the book
     "direction": "<direction, applies in every language>",
     "say": "<text Gemini reads instead>", "lang": "fa"}       // say is language-specific, so it needs lang
  ]
}

A line entry finds its line by the dump piece's `speaker` and English `source`, which are the same in every language, never by position (each language's dump orders its
pieces differently: Farsi puts tags first and moves animal sounds). `check` fails the run before any request when an entry matches no piece, matches several without an
`occurrence`, names an occurrence past the last, or sets one field of a piece twice. A `direction` entry applies to every language; a `say` entry only to the language in
its `lang` (other languages still match it against the dump, so a stale entry fails, but do not apply it). An entry may not target an animal-sound piece (`"sound": true` in the dump):
its source is localized and the count differs by language, so only spoken lines and narration can be keyed. A direction for one line beats the character's own. A hash key follows the spoken text when sentences move.
The delivery sent to the voice is `<style>; <direction>` (or just the style).

A `say` (Slice 246 A6) is the text Gemini reads for that line instead of the book text (a pronunciation fix, e.g. one word with a vowel mark). The book text, the manifest
`hash` and the on-device voice are untouched; the clip is keyed by the spoken text.
"""
import json
import re
import unicodedata

from clip_manifest import text_hash

CAST_VERSION = 1
HASH_KEY = re.compile(r"^[0-9a-f]{64}$")
STYLE_JOINER = "; "
LINE_TEXT_FIELDS = ("direction", "say")  # optional, at least one
LANGUAGE_CODE = re.compile(r"^[a-z]{2,3}$")  # the dump's language, as BookBuilder writes it (fa, nb, en); "FA" or "fa-IR" would silently skip a say


class CastError(ValueError):
    """The cast file is invalid, or a speaker in the book has no voice."""


class Delivery:
    """What one sentence is spoken with."""

    def __init__(self, voice, style, direction, say=""):
        self.voice = voice
        self.style = style
        self.direction = direction
        self.say = say  # the text to read instead of the book text; "" = the book text

    @property
    def prompt(self):
        return f"{self.style}{STYLE_JOINER}{self.direction}" if self.direction else self.style


def load(path):
    with open(path, encoding="utf8") as f:
        return AudioCast(json.load(f), path)


def voice_entry(entry, label, path):
    if not isinstance(entry, dict) or not entry.get("voice") or not entry.get("style"):
        raise CastError(f"{path}: {label} needs a non-empty voice and style")
    return entry


def normalize_say(text):
    """The text a `say` is validated, sent and hashed as: NFC, trimmed."""
    return unicodedata.normalize("NFC", text).strip()


class AudioCast:
    def __init__(self, data, path="audio-cast.json"):
        self.path = path
        if data.get("version") != CAST_VERSION:
            raise CastError(f"{path}: version must be {CAST_VERSION}")
        self.narrator = voice_entry(data.get("narrator"), "narrator", path)
        self.characters = {name: voice_entry(e, f"character {name!r}", path) for name, e in (data.get("characters") or {}).items()}
        self.directions = data.get("directions") or {}
        for key, value in self.directions.items():
            self.check_hash_direction(key, value)
        self.lines = data.get("lines") or []
        for position, entry in enumerate(self.lines):
            self.check_line_entry(position, entry)
        self.bound = None  # dump piece index -> {"direction": ..., "say": ...}; None until check() binds the entries to a dump

    def check_hash_direction(self, key, value):
        if not HASH_KEY.match(key):
            raise CastError(f"{self.path}: direction key {key!r} must be a SHA-256 text hash (per-line entries go in `lines`, keyed by speaker and source, not by index)")
        if not isinstance(value, str) or not value:
            raise CastError(f"{self.path}: direction for hash {key} must be a non-empty string")

    def check_line_entry(self, position, entry):
        problem = (f"{self.path}: lines[{position}] must be an object with a non-empty source, a speaker (null for the narrator), an optional occurrence (1 or more), "
                   "a non-empty direction and/or say, and a lang when it has a say")
        if not isinstance(entry, dict) or not isinstance(entry.get("source"), str) or not entry["source"] or "speaker" not in entry:
            raise CastError(problem)
        if entry["speaker"] is not None and (not isinstance(entry["speaker"], str) or not entry["speaker"]):
            raise CastError(problem)
        occurrence = entry.get("occurrence")
        if "occurrence" in entry and (not isinstance(occurrence, int) or isinstance(occurrence, bool) or occurrence < 1):
            raise CastError(problem)
        texts = [normalize_say(entry[f]) if f == "say" and isinstance(entry[f], str) else entry.get(f) for f in LINE_TEXT_FIELDS if f in entry]
        if not texts or any(not isinstance(t, str) or not t.strip() for t in texts):
            raise CastError(problem)
        if "say" in entry and (not isinstance(entry.get("lang"), str) or not LANGUAGE_CODE.match(entry["lang"])):
            raise CastError(problem + " (lang is a lower-case code such as fa)")

    def direction_for(self, sentence):
        by_hash = self.directions.get(text_hash(sentence["text"]))
        if by_hash:
            return by_hash
        return self.bound_slot(sentence).get("direction", "")

    def say_for(self, sentence):
        return normalize_say(self.bound_slot(sentence).get("say", ""))  # NFC and trimmed, so the same spelling typed two ways sends and hashes alike

    def bound_slot(self, sentence):
        if self.bound is None:
            if self.lines:
                raise CastError(f"{self.path}: check(sentences, language) must run before delivery(), or the line entries are silently dropped")
            return {}
        return self.bound.get(sentence["index"], {})

    def delivery(self, sentence):
        """The Delivery for one dumped sentence ({"index", "text", "speaker", "narrator"}); raises CastError for a speaker with no entry."""
        entry = self.narrator if sentence["narrator"] or not sentence.get("speaker") else self.characters.get(sentence["speaker"])
        if entry is None:
            raise CastError(f"{self.path}: no character entry for speaker {sentence['speaker']!r} (sentence {sentence['index']})")
        direction = self.direction_for(sentence) or entry.get("direction") or ""
        return Delivery(entry["voice"], entry["style"], direction, self.say_for(sentence))

    def check(self, sentences, language):
        """Fails before any request when a speaker has no voice, or a line entry matches no piece, is ambiguous, targets a sound or sets one field of a piece twice. Binds the entries to this dump."""
        if not isinstance(language, str) or not LANGUAGE_CODE.match(language):
            raise CastError(f"{self.path}: language {language!r} must be a lower-case code such as fa")
        self.bound = {}
        for sentence in sentences:
            self.delivery(sentence)
        for position, entry in enumerate(self.lines):
            fields = [f for f in LINE_TEXT_FIELDS if f in entry and (f != "say" or entry["lang"] == language)]  # a say belongs to one language, a direction to all
            self.bind(position, entry, self.find_piece(position, entry, sentences), fields)

    def find_piece(self, position, entry, sentences):
        source = unicodedata.normalize("NFC", entry["source"])
        matches = [s for s in sentences if s.get("speaker") == entry["speaker"] and unicodedata.normalize("NFC", s.get("source") or "") == source]
        found = [s for s in matches if not s.get("sound")]  # sound pieces never count: a spoken line sharing speaker and source with one stays selectable
        label = f"{self.path}: lines[{position}] ({entry['speaker']!r} saying {entry['source']!r})"
        if not matches:
            raise CastError(f"{label} matches no line of the book; the speaker or the English source changed")
        if not found:
            raise CastError(f"{label} targets an animal sound; sounds cannot be keyed by a line entry (their source differs by language)")
        occurrence = entry.get("occurrence")
        if occurrence is None and len(found) > 1:
            raise CastError(f"{label} matches {len(found)} lines; add an occurrence from 1 to {len(found)}")
        if occurrence is not None and occurrence > len(found):
            raise CastError(f"{label} names occurrence {occurrence} but the book has only {len(found)}")
        return found[(occurrence or 1) - 1]

    def bind(self, position, entry, piece, fields):
        slot = self.bound.setdefault(piece["index"], {})
        for field in fields:
            if field == "direction" and text_hash(piece["text"]) in self.directions:
                raise CastError(f"{self.path}: lines[{position}] sets a direction for a line that already has a hash-keyed direction; keep only one of them")
            if field in slot:
                raise CastError(f"{self.path}: lines[{position}] sets {field} for a line that another entry already set")
            slot[field] = entry[field]
