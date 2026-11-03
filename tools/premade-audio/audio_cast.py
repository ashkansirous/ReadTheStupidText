"""The cast of a book's audio (Slice 246 A2): `content/dino/<book>/audio-cast.json`.

{
  "version": 1,
  "narrator": {"voice": "Hali", "style": "<delivery for narration>"},
  "characters": {"<speaker name as in the sentence dump>": {"voice": "Cleo", "style": "<delivery>", "direction": "<optional, applies to all their lines>"}},
  "directions": {
    "<sentence index>": {"direction": "<direction>", "say": "<text Gemini reads instead>", "speaker": "<speaker as in the dump, null for the narrator>", "source": "<the line's English source text>"},
    "<sha-256 text hash>": "<direction>"
  }   // optional, one line only
}

A direction for one line beats the character's own. A hash key follows the spoken text when sentences move. An index key follows the position, so it also names the
speaker and the English source text of its line (the same in every language) and `check` fails the run before any request when the dump's line at that index differs.
The delivery sent to the voice is `<style>; <direction>` (or just the style).

An index entry may carry `say` (Slice 246 A6): the text Gemini reads for that line instead of the book text (a pronunciation fix, e.g. one word with a vowel mark). It needs
`direction` or `say` or both, and is guarded like a direction. The book text, the manifest `hash` and the on-device voice are untouched; the clip is keyed by the spoken text.
"""
import json
import re
import unicodedata

from clip_manifest import text_hash

CAST_VERSION = 1
HASH_KEY = re.compile(r"^[0-9a-f]{64}$")
STYLE_JOINER = "; "
INDEX_TEXT_FIELDS = ("direction", "say")  # optional, at least one


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
            self.check_direction_entry(key, value)

    def check_direction_entry(self, key, value):
        if HASH_KEY.match(key):
            if not isinstance(value, str) or not value:
                raise CastError(f"{self.path}: direction for hash {key} must be a non-empty string")
        elif key.isdigit():
            self.check_index_entry(key, value)
        else:
            raise CastError(f"{self.path}: direction key {key!r} is neither a sentence index nor a SHA-256 text hash")

    def check_index_entry(self, key, value):
        problem = f"{self.path}: entry for sentence {key} must be an object with a non-empty source, a speaker (null for the narrator) and a non-empty direction and/or say"
        if not isinstance(value, dict) or not isinstance(value.get("source"), str) or not value["source"] or "speaker" not in value:
            raise CastError(problem)
        if value["speaker"] is not None and (not isinstance(value["speaker"], str) or not value["speaker"]):
            raise CastError(problem)
        texts = [normalize_say(value[f]) if f == "say" and isinstance(value[f], str) else value.get(f) for f in INDEX_TEXT_FIELDS if f in value]
        if not texts or any(not isinstance(t, str) or not t.strip() for t in texts):
            raise CastError(problem)

    def direction_for(self, sentence):
        by_hash = self.directions.get(text_hash(sentence["text"]))
        if by_hash:
            return by_hash
        by_index = self.directions.get(str(sentence["index"]))
        return by_index.get("direction", "") if by_index else ""

    def say_for(self, sentence):
        by_index = self.directions.get(str(sentence["index"]))
        return normalize_say(by_index.get("say", "")) if isinstance(by_index, dict) else ""  # NFC and trimmed, so the same spelling typed two ways sends and hashes alike

    def delivery(self, sentence):
        """The Delivery for one dumped sentence ({"index", "text", "speaker", "narrator"}); raises CastError for a speaker with no entry."""
        entry = self.narrator if sentence["narrator"] or not sentence.get("speaker") else self.characters.get(sentence["speaker"])
        if entry is None:
            raise CastError(f"{self.path}: no character entry for speaker {sentence['speaker']!r} (sentence {sentence['index']})")
        direction = self.direction_for(sentence) or entry.get("direction") or ""
        return Delivery(entry["voice"], entry["style"], direction, self.say_for(sentence))

    def check(self, sentences):
        """Fails before any request when a speaker has no voice, or when an index direction no longer matches the dump (its line's speaker or English source moved)."""
        for sentence in sentences:
            self.delivery(sentence)
        by_index = {str(s["index"]): s for s in sentences}
        for key, value in self.directions.items():
            if key.isdigit():
                self.check_line(key, value, by_index.get(key))

    def check_line(self, key, value, sentence):
        if sentence is None:
            raise CastError(f"{self.path}: entry for sentence {key} but the book has no sentence {key}; update its index")
        if sentence.get("speaker") != value["speaker"] or unicodedata.normalize("NFC", sentence.get("source") or "") != unicodedata.normalize("NFC", value["source"]):
            raise CastError(f"{self.path}: entry for sentence {key} expects {value['speaker']!r} saying {value['source']!r} but the book has "
                            f"{sentence.get('speaker')!r} saying {sentence.get('source')!r}; the lines moved, update its index")
