"""Turns one or more results.json (run.py) into the markdown tables of the note.

Cell = mean +- sd of the spoken length in ms over the takes (silence trimmed), `skip n` = takes under 120 ms (the word was dropped),
`gap n` = takes with a silent hole over 250 ms that the spelling does not write, `spelled n` = takes read letter by letter, and
`k/N` = takes whose ASR transcript contained the word as an exact token (a WEAK signal: Whisper is not trained on onomatopoeia).
A difference is only called real when the ranges (min..max over the takes) of the two spellings do not overlap in that voice.

Usage: report.py <results.json> [<results.json> ...]
"""
import json
import sys
from collections import OrderedDict
import analysis
from spellings import RECOMMENDED

PAUSE_MARKS = ("...", "…", "——", "……", "!", "！", ",", "，", "،")


def load(paths):
    rows = []
    for path in paths:
        rows += json.load(open(path, encoding="utf-8"))
    return rows


def stats(row):
    return analysis.spread([t["active_ms"] for t in row["takes"]])


def has_written_pause(text):
    return any(m in text.strip("!！") for m in PAUSE_MARKS)


def cell(row):
    s, takes = stats(row), row["takes"]
    parts = [f'{s["mean"]:.0f}±{s["sd"]:.0f}']
    for label, n in (("skip", sum(bool(t["skipped"]) for t in takes)), ("spelled", sum(bool(t["spelled_out"]) for t in takes)),
                     ("gap", 0 if has_written_pause(row["text"]) else sum(bool(t["gap_ms"] > analysis.GAP_MS) for t in takes))):
        parts += [f"{label} {n}"] if n else []
    if takes[0]["survived"] is not None:
        parts.append(f'{sum(bool(t["survived"]) for t in takes)}/{len(takes)}')
    return " ".join(parts)


def versus(row, base):
    """(ratio of means, +1 longer / -1 shorter / 0 overlapping beyond the spread) of a row against the baseline row, same voice."""
    s, b = stats(row), stats(base)
    # A dropped baseline is floored at the drop threshold, so a ratio against it is a lower bound (understated).
    ratio = s["mean"] / max(b["mean"], analysis.MIN_ACTIVE_MS)
    return ratio, (1 if s["lo"] > b["hi"] else -1 if s["hi"] < b["lo"] else 0)


def ratio_cell(rows_for_text, base_by_voice):
    pairs = [versus(r, base_by_voice[r["voice"]]) for r in rows_for_text if r["voice"] in base_by_voice]
    if not pairs:
        return "-"
    mean = sum(p[0] for p in pairs) / len(pairs)
    longer, shorter = sum(p[1] > 0 for p in pairs), sum(p[1] < 0 for p in pairs)
    return f"x{mean:.2f} (longer in {longer}/{len(pairs)}, shorter in {shorter})"


def group(rows, key):
    out = OrderedDict()
    for r in rows:
        out.setdefault(key(r), []).append(r)
    return out


def alone_table(rows, voices):
    base_text = rows[0]["text"]
    base_by_voice = {r["voice"]: r for r in rows if r["text"] == base_text}
    out = ["| Spelling | " + " | ".join(voices) + " | vs `" + base_text + "` |", "|---|" + "---|" * (len(voices) + 1)]
    for text, per_text in group(rows, lambda r: r["text"]).items():
        by_voice = {r["voice"]: r for r in per_text}
        cells = [cell(by_voice[v]) if v in by_voice else "-" for v in voices]
        out.append(f"| `{text}` | " + " | ".join(cells) + " | " + ("baseline" if text == base_text else ratio_cell(per_text, base_by_voice)) + " |")
    return out


def context_cell(alone, lead, trail, carrier):
    base = stats(carrier)["mean"]
    deltas = [stats(r)["mean"] - base for r in (lead, trail)]
    heard = sum(bool(t["survived"]) for r in (lead, trail) for t in r["takes"])
    total = sum(len(r["takes"]) for r in (lead, trail))
    return f'{stats(alone)["mean"]:.0f} alone, {deltas[0]:+.0f} lead, {deltas[1]:+.0f} trail, heard {heard}/{total}'


def context_table(rows, lang, sound, voices):
    carrier_text = next((r["text"] for r in rows if r["lang"] == lang and r["kind"] == "carrier"), None)
    if carrier_text is None:
        return []
    carriers = {r["voice"]: r for r in rows if r["lang"] == lang and r["kind"] == "carrier"}
    index = {(r["kind"], r["voice"], r["text"]): r for r in rows if r["lang"] == lang and r["sound"] == sound}
    out = ["| Spelling | " + " | ".join(voices) + " |", "|---|" + "---|" * len(voices)]
    for text in OrderedDict.fromkeys(k[2] for k in index if k[0] == "alone"):
        cells = [_context_for(index, carriers, v, text, carrier_text) for v in voices]
        if any(c != "-" for c in cells):
            out.append(f"| `{text}` | " + " | ".join(cells) + " |")
    return out


def _context_for(index, carriers, voice, text, carrier_text):
    lead, trail = index.get(("lead", voice, f"{text} {carrier_text}")), index.get(("trail", voice, f"{carrier_text} {text}"))
    return context_cell(index[("alone", voice, text)], lead, trail, carriers[voice]) if lead and trail else "-"


def pace_table(rows, sound):
    sel = [r for r in rows if r["lang"] == "fa" and r["sound"] == sound and r["kind"].startswith("pace:")]
    voices = list(OrderedDict.fromkeys(r["voice"] for r in sel))
    out = ["| Spelling @ pace | " + " | ".join(voices) + " |", "|---|" + "---|" * len(voices)]
    for (text, pace), per in group(sel, lambda r: (r["text"], r["pace"])).items():
        by_voice = {r["voice"]: r for r in per}
        out.append(f"| `{text}` @ {pace} | " + " | ".join(cell(by_voice[v]) if v in by_voice else "-" for v in voices) + " |")
    return out


def _is_recommended(r):
    return r["kind"] == "alone" and r["text"] in RECOMMENDED[r["lang"]]


_DROP_PICKS = (lambda r: r["kind"] == "alone", _is_recommended, lambda r: r["kind"] in ("lead", "trail"),
               lambda r: r["kind"].startswith("pace:"))


def _drop(mine, carrier, pick):
    takes = [(r, t) for r in mine if pick(r) for t in r["takes"]]
    if not takes:
        return "-"
    dropped = sum(t["active_ms"] - (carrier if r["kind"] in ("lead", "trail") else 0) < analysis.MIN_ACTIVE_MS for r, t in takes)
    return f"{dropped}/{len(takes)} ({100 * dropped // len(takes)} %)"


def drop_rates(rows):
    """Per Farsi voice: the share of takes that dropped the sound (under 120 ms; in context: under 120 ms added to the carrier)."""
    out = ["| Voice | alone (all) | alone (recommended) | in context (lead/trail) | at flavour paces |", "|---|---|---|---|---|"]
    fa = [r for r in rows if r["lang"] == "fa"]
    carriers = {r["voice"]: stats(r)["mean"] for r in fa if r["kind"] == "carrier"}
    for voice in OrderedDict.fromkeys(r["voice"] for r in fa):
        mine = [r for r in fa if r["voice"] == voice]
        out.append(f"| {voice} | " + " | ".join(_drop(mine, carriers[voice], pick) for pick in _DROP_PICKS) + " |")
    return out


def main(paths):
    rows = load(paths)
    alone = [r for r in rows if r["kind"] == "alone"]
    for lang in OrderedDict.fromkeys(r["lang"] for r in alone):
        voices = list(OrderedDict.fromkeys(r["voice"] for r in alone if r["lang"] == lang))
        print(f"\n### {lang}\n")
        for sound in OrderedDict.fromkeys(r["sound"] for r in alone if r["lang"] == lang):
            print(f"**{sound}** alone (ms, mean±sd over the takes)\n")
            print("\n".join(alone_table([r for r in alone if r["lang"] == lang and r["sound"] == sound], voices)), "\n")
            print(f"**{sound}** in a sentence (ms the sound adds to the carrier; lead = before it, trail = after it)\n")
            print("\n".join(context_table(rows, lang, sound, voices)), "\n")
    print("\n### fa flavour paces\n")
    for sound in OrderedDict.fromkeys(r["sound"] for r in rows if r["lang"] == "fa" and r["kind"] != "carrier"):
        print(f"**{sound}**\n\n" + "\n".join(pace_table(rows, sound)) + "\n")
    print("\n### fa: share of takes that dropped the sound\n\n" + "\n".join(drop_rates(rows)))


if __name__ == "__main__":
    main(sys.argv[1:])
