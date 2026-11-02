"""Builds the Farsi listening paragraph of a Dino book the way the app speaks it (Slice 246).

Mirrors src/DinoReader.Core/BundledTranslations.cs: `SpeakKey(language)` is "speak:" + the language key (line 126), and `LookupSpoken` (line ~130)
takes the piece from the "how to read" table when it has it, else from the displayed translation table, else nothing (the source text).
In the repo the tables are translations/fa.speak.json (the speak:fa table) and translations/fa.json; pieces.json lists the pieces (sentences) in reading order.
"""
import json
import os
import re

SPEAK_PREFIX = "speak:"
# AppLanguages.Codes (src/ReadTheStupidText.Application/Reading/AppLanguage.cs:26-41): the fifteen supported primary codes.
SUPPORTED_LANGUAGES = frozenset(("en", "fa", "fr", "es", "pt", "zh", "ar", "ru", "ja", "da", "fi", "sv", "nb", "nl", "de"))
LEGACY_NORWEGIAN = frozenset(("no", "nn"))
DEFAULT_LANGUAGE = "en"
PAGE_SEPARATOR = re.compile(r"^---\s*$", re.MULTILINE)
PARAGRAPH_SEPARATOR = re.compile(r"\n\s*\n")
QUOTES = "\"“”"


def primary_code(language):
    """RuleSets.PrimaryCode (src/DinoReader.Core/RuleSets.cs:49-53): "en" when blank, else the part before '-' or '_', trimmed and lower-cased."""
    if not language or not language.strip():
        return DEFAULT_LANGUAGE
    return re.split("[-_]", language)[0].strip().lower()


def language_key(language):
    """BundledTranslations.Key (src/DinoReader.Core/BundledTranslations.cs:20-21): a supported language ("fa-IR", "FA") is its primary code
    (legacy "no"/"nn" are "nb", AppLanguages.Canonical, AppLanguage.cs:67-70); any other code is kept trimmed and lower-cased."""
    primary = primary_code(language)
    primary = "nb" if primary in LEGACY_NORWEGIAN else primary
    return primary if primary in SUPPORTED_LANGUAGES else (language or "").strip().lower()


def speak_key(language):
    """BundledTranslations.SpeakKey (BundledTranslations.cs:124): "speak:" + Key(language)."""
    return SPEAK_PREFIX + language_key(language)


def load_json(path):
    with open(path, encoding="utf8") as f:
        return json.load(f)


def load_tables(book_dir, language):
    """{ language key: table } for the displayed table and the speak table; a missing file is an empty table."""
    folder = os.path.join(book_dir, "translations")
    code = language_key(language)
    tables = {}
    for key, name in ((code, f"{code}.json"), (speak_key(language), f"{code}.speak.json")):
        path = os.path.join(folder, name)
        tables[key] = load_json(path) if os.path.isfile(path) else {}
    return tables


def lookup_spoken(tables, language, piece):
    """BundledTranslations.LookupSpoken (BundledTranslations.cs:129-130): a piece present in the speak table is returned as-is (even empty),
    else the displayed table's, else None."""
    speak = tables.get(speak_key(language), {})
    if piece in speak:
        return speak[piece]
    return tables.get(language_key(language), {}).get(piece)


def paragraphs(story_md):
    """Story paragraphs in reading order (pages are split by '---' lines, paragraphs by blank lines)."""
    result = []
    for page in PAGE_SEPARATOR.split(story_md):
        result += [p.strip() for p in PARAGRAPH_SEPARATOR.split(page.strip()) if p.strip()]
    return result


def pieces_in(paragraph, pieces):
    """The pieces (from pieces.json) that make up `paragraph`, in order. Quote marks are not part of a piece.
    Matches greedily from the start: at each position the longest piece that is a prefix of the rest; raises ValueError when the paragraph is not fully covered."""
    plain = paragraph.translate({ord(c): " " for c in QUOTES})  # a quote is a word break, so `chin!"said` never glues into `chin!said`
    by_length = sorted(pieces, key=len, reverse=True)
    found, cursor = [], 0
    while True:
        cursor = skip_spaces(plain, cursor)
        if cursor >= len(plain):
            return found
        piece = next((p for p in by_length if p and plain.startswith(p, cursor)), None)
        if piece is None:
            raise ValueError(f"no piece matches at: {plain[cursor:cursor + 40]!r} (paragraph {plain[:40]!r})")
        found.append(piece)
        cursor += len(piece)


def skip_spaces(text, cursor):
    while cursor < len(text) and text[cursor].isspace():
        cursor += 1
    return cursor


def listening_paragraph(book_dir, language="fa", index=0):
    """(source pieces, spoken text) for paragraph `index` of the book."""
    with open(os.path.join(book_dir, "story.md"), encoding="utf8") as f:
        all_paragraphs = paragraphs(f.read())
    pieces = pieces_in(all_paragraphs[index], load_json(os.path.join(book_dir, "translations", "pieces.json")))
    tables = load_tables(book_dir, language)
    spoken = [p if (s := lookup_spoken(tables, language, p)) is None else s for p in pieces]
    return pieces, " ".join(s for s in spoken if s.strip())  # the app speaks each piece alone; an empty entry says nothing, so it leaves no double space
