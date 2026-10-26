"""Copies the WAVs worth a human listen (first take of each clip) into <dest>: per language and sound the baseline, every
spelling the skill recommends, and up to three more that no take dropped and whose exact-token ASR hit rate is within 20 points
of the best, longest first. Also the recommended spellings inside a sentence (kind lead).

Usage: shortlist.py <dest> <run folder with results.json and wav/> [<run folder> ...]
"""
import json
import os
import shutil
import sys
from collections import OrderedDict
from spellings import RECOMMENDED

MAX_EXTRA = 3
HIT_MARGIN = 0.2


def hit_rate(rows):
    takes = [t for r in rows for t in r["takes"]]
    return sum(bool(t["survived"]) for t in takes) / len(takes)


def never_dropped(rows):
    return not any(t["skipped"] for r in rows for t in r["takes"])


def mean_ms(rows):
    return sum(t["active_ms"] for r in rows for t in r["takes"]) / sum(len(r["takes"]) for r in rows)


def pick(rows):
    """Rows (all voices) of the spellings to listen to, for one language and sound."""
    by_text = OrderedDict()
    for r in rows:
        by_text.setdefault(r["text"], []).append(r)
    texts = list(by_text)
    lang = rows[0]["lang"]
    best = max(hit_rate(v) for v in by_text.values())
    # The Chinese exact-token check is homophone noise (it accepts any root character), so it never excludes a Chinese spelling.
    extra = [t for t in texts[1:] if t not in RECOMMENDED[lang] and never_dropped(by_text[t])
             and (lang == "zh" or hit_rate(by_text[t]) >= best - HIT_MARGIN)]
    extra.sort(key=lambda t: -mean_ms(by_text[t]))
    chosen = [texts[0]] + [t for t in texts[1:] if t in RECOMMENDED[rows[0]["lang"]]] + extra[:MAX_EXTRA]
    return [r for t in chosen for r in by_text[t]]


def copy_clip(out, dest, row):
    src = os.path.join(out, row["wav"])
    if row["wav"] and os.path.exists(src):
        target = os.path.join(dest, os.path.relpath(src, os.path.join(out, "wav")))
        os.makedirs(os.path.dirname(target), exist_ok=True)
        shutil.copy(src, target)


def main(dest, outs):
    for out in outs:
        rows = json.load(open(os.path.join(out, "results.json"), encoding="utf-8"))
        alone = [r for r in rows if r["kind"] == "alone"]
        for lang, sound in OrderedDict.fromkeys((r["lang"], r["sound"]) for r in alone):
            for r in pick([r for r in alone if r["lang"] == lang and r["sound"] == sound]):
                copy_clip(out, dest, r)
        carriers = {r["lang"]: r["text"] for r in rows if r["kind"] == "carrier"}
        for r in rows:
            if r["kind"] == "lead" and r["text"].removesuffix(" " + carriers[r["lang"]]) in RECOMMENDED[r["lang"]]:
                copy_clip(out, dest, r)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
