"""Builds a Farsi starter's "how to read" table (Slice 154) from its plain translation and the approved marks list:
whole-word replace, ZWNJ compounds are one word. The plain translation is not changed.

A word gets its base marking everywhere; the ezafe forms in the list's "ezafe" apply only at their sentence and word
position, because the user approved them for that one occurrence.

    python apply_marks.py <translations/x.fa.json> <translations/x.fa.marks.json> <translations/x.fa.speak.json>
"""
import json
import re
import sys

LF = chr(10)
WORD = re.compile("[ء-غف-يپچژکگیً-ْ‌]+")


def ezafe_positions(text, ezafe):
    """{offset in text -> marked word} for every ezafe occurrence whose sentence is in the text."""
    positions = {}
    for entry in ezafe:
        words = list(WORD.finditer(entry["sentence"]))
        if entry["word"] >= len(words) or words[entry["word"]].group(0) != entry["plain"]:
            raise ValueError(f"ezafe entry does not match its sentence: {entry['plain']!r}")
        start = text.find(entry["sentence"])
        while start >= 0:
            positions[start + words[entry["word"]].start()] = entry["marked"]
            start = text.find(entry["sentence"], start + 1)
    return positions


def spoken_segments(text, spoken):
    """Splits the text at the user's spoken replacements (for the sentence they were chosen on): [(chunk, offset or None)]."""
    cuts = []
    for entry in spoken:
        start = text.find(entry["sentence"])
        at = text.find(entry["replaces"], start) if start >= 0 else -1
        if at >= 0:
            cuts.append((at, at + len(entry["replaces"]), entry["text"]))
    segments, cursor = [], 0
    for begin, end, replacement in sorted(cuts):
        segments += [(text[cursor:begin], cursor), (replacement, None)]
        cursor = end
    return segments + [(text[cursor:], cursor)]


def apply_chunk(chunk, offset, marks, ezafe_at):
    def mark(match):
        if offset is not None and offset + match.start() in ezafe_at:
            return ezafe_at[offset + match.start()]
        return marks.get(match.group(0), match.group(0))
    return WORD.sub(mark, chunk)


def apply(text, data):
    """The spoken form of a text (a sentence or a page of sentences): replacements first, then the marks."""
    ezafe_at = ezafe_positions(text, data.get("ezafe", []))
    return "".join(apply_chunk(chunk, offset, data["marks"], ezafe_at) for chunk, offset in spoken_segments(text, data["spoken"]))


def main(argv):
    if len(argv) != 4:
        print(__doc__)
        return 2
    texts = json.load(open(argv[1], encoding="utf8"))
    data = json.load(open(argv[2], encoding="utf8"))
    out = {k: apply(v, data) for k, v in texts.items()}
    with open(argv[3], "w", encoding="utf8", newline=LF) as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
