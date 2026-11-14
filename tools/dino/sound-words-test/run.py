"""Slice 145: synthesise sound-word spellings with the apps' own engines and judge them objectively.

Every clip is synthesised TAKES times (the engines start from noise, so one clip is one sample; review 145 item 1) and a
difference only counts when it is larger than the spread of the takes. Four kinds of row:
  alone      the spelling spoken by itself (what a sound in its own quote or sentence gets)
  lead/trail the spelling glued to a short carrier sentence the way SentenceUnits does it (textunits.py)
  carrier    the carrier sentence alone (the baseline the context rows subtract)
  pace:<x>   Farsi only: the spelling at a character flavour's engine pace

Usage: run.py --packs <dir with extracted packs> --asr <dir with whisper model> --out <dir> [--only en] [--no-asr] [--takes 5]
Writes WAVs of the first take to <out>/wav and every take's numbers to <out>/results.json. Needs no network.
"""
import argparse
import hashlib
import json
import os
import wave
import numpy as np
import analysis
import engines
import textunits
from spellings import SPELLINGS, ROOTS, CARRIERS, RECOMMENDED, FLAVOUR_PACES, FLAVOUR_VOICES

WHISPER_SIZE = "small"
DEFAULT_TAKES = 5


def write_wav(path, samples, rate):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = (np.clip(np.asarray(samples), -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


def slug(text):
    """A readable stem plus a short hash of the full text, so spellings that differ only in punctuation never share a file."""
    stem = "".join(c if c.isalnum() else "_" for c in text)[:30]
    return f"{stem}_{hashlib.sha1(text.encode('utf-8')).hexdigest()[:6]}"


def take_numbers(samples, rate, asr, lang, roots):
    m = analysis.measure(samples, rate)
    heard = analysis.transcribe(asr, lang, samples, rate) if asr and rate > 0 and len(samples) else ""
    return dict(active_ms=round(m["active"]), total_ms=round(m["total"]), gap_ms=round(m["gap"]), peak=round(m["peak"], 2),
                clipped=m["clipped"], skipped=bool(m["active"] < analysis.MIN_ACTIVE_MS), heard=heard,
                survived=(bool(roots) and analysis.word_survived(heard, roots, lang)) if asr else None,
                spelled_out=analysis.letter_by_letter(heard, lang) if asr else None)


def synth_row(ctx, kind, sound, text, pace=1.0, roots=None):
    """Synthesises one text `ctx.takes` times; the first take's WAV is kept for listening."""
    takes, first_wav = [], ""
    for n in range(ctx.takes):
        samples, rate = engines.speak(ctx.tts, text, ctx.sid, ctx.sup_lang, pace)
        takes.append(take_numbers(samples, rate, ctx.asr, ctx.lang, roots))
        if n == 0 and rate > 0 and len(samples):
            wav = os.path.join(ctx.out, "wav", ctx.lang, slug(ctx.label), kind.replace(":", "_"), f"{sound}_{slug(text)}.wav")
            write_wav(wav, samples, rate)
            first_wav = os.path.relpath(wav, ctx.out)
    return dict(kind=kind, lang=ctx.lang, voice=ctx.label, sound=sound, text=text, pace=pace, wav=first_wav, takes=takes)


class Voice:
    """Everything one voice needs to synthesise and judge a row."""

    def __init__(self, args, asr, lang, label, tts, sid, sup_lang):
        self.out, self.takes, self.asr, self.lang, self.label = args.out, args.takes, asr, lang, label
        self.tts, self.sid, self.sup_lang = tts, sid, sup_lang


def is_glued(text):
    """True for a sound the app joins to the next sentence (shorter than a unit), so its context can differ."""
    return len(text) < textunits.MIN_UNIT_LENGTH


def alone_rows(ctx):
    rows = []
    for sound, texts in SPELLINGS[ctx.lang].items():
        rows += [synth_row(ctx, "alone", sound, t, roots=ROOTS[ctx.lang][sound]) for t in texts]
    return rows


def context_rows(ctx):
    carrier = CARRIERS[ctx.lang]
    rows = [synth_row(ctx, "carrier", "carrier", carrier)]
    for sound, texts in SPELLINGS[ctx.lang].items():
        for text in (t for t in texts if is_glued(t) or t in RECOMMENDED[ctx.lang]):
            rows.append(synth_row(ctx, "lead", sound, f"{text} {carrier}", roots=ROOTS[ctx.lang][sound]))
            rows.append(synth_row(ctx, "trail", sound, f"{carrier} {text}", roots=ROOTS[ctx.lang][sound]))
    return rows


def flavour_rows(ctx):
    """Farsi roster flavours: the baseline and the recommended spellings at each flavour pace."""
    rows = []
    for sound, texts in SPELLINGS[ctx.lang].items():
        for text in [texts[0]] + [t for t in texts[1:] if t in RECOMMENDED[ctx.lang]]:
            rows += [synth_row(ctx, f"pace:{p}", sound, text, p, ROOTS[ctx.lang][sound]) for p in FLAVOUR_PACES]
    return rows


def voice_rows(ctx):
    rows = alone_rows(ctx) + context_rows(ctx)
    if ctx.lang == "fa" and ctx.label.split()[-1] in FLAVOUR_VOICES:
        rows += flavour_rows(ctx)
    return rows


def build_all_asr(asr_dir, only=""):
    model_dir = os.path.join(asr_dir, f"sherpa-onnx-whisper-{WHISPER_SIZE}")
    return {lang: analysis.build_asr(model_dir, WHISPER_SIZE, analysis.WHISPER_LANG[lang]) for lang in SPELLINGS
            if not only or lang == only}


def save(args, rows):
    with open(os.path.join(args.out, "results.json"), "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1, default=float)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--packs", required=True)
    ap.add_argument("--asr", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", default="")
    ap.add_argument("--voices", default="", help="comma list: only voices whose label contains one of these words")
    ap.add_argument("--no-asr", action="store_true")
    ap.add_argument("--takes", type=int, default=DEFAULT_TAKES)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    asr = None if args.no_asr else build_all_asr(args.asr, args.only)
    cache, rows = {}, []
    for lang, label, key, build, sid, sup_lang in engines.voice_plan(args.packs):
        if (args.only and lang != args.only) or (args.voices and not any(w in label.split() for w in args.voices.split(","))):
            continue
        if key not in cache and len(cache) > 1:
            cache.clear()
        if key not in cache:
            cache[key] = build()
        rows += voice_rows(Voice(args, asr, lang, label, cache[key], sid, sup_lang))
        save(args, rows)
        print(lang, label, "done,", len(rows), "rows", flush=True)


if __name__ == "__main__":
    main()
