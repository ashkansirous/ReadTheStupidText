"""Re-renders the Persian review pages with the approved marks applied (Slice 154), exactly as the book will speak them.

The marks come from the list build_marks_list.py writes (base marking everywhere, an ezafe only at the one sentence and word
position the user approved it for, the user's spoken replacements), applied by apply_marks.py, the same code that builds the
book's "how to read" table.

    python regen_review_audio.py --marks <x.fa.marks.json> --pages <dir of page-<n>.json> --out <dir> --voice-root <piper-fa folder> [--all] [--probe]

--voice-root may instead come from the environment variable RTST_PIPER_FA_ROOT. Each page-<n>.json holds {"text": the page's Farsi}.
Without --all only the pages whose marked text differs from the plain text are rendered. Needs numpy, ffmpeg and the engines in this folder.
"""
import argparse
import glob
import json
import os
import re
import subprocess
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import engines
from apply_marks import apply

VOICE_ROOT_ENV = "RTST_PIPER_FA_ROOT"
VOICE = "mana"
PROBE_PHRASES = ["آخ جون", "آخ جوون", "آخ‌جون", "آخ، جون!"]
SILENCE_RMS = 0.01


def to_wav(path, samples, rate):
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes((np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes())


def render_webm(tts, text, dest):
    samples, rate = engines.speak(tts, text, 0)
    wav = dest[:-5] + ".wav"
    to_wav(wav, samples, rate)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libopus", "-b:a", "32k", dest], check=True)
    os.remove(wav)


def page_number(path):
    return int(re.findall(r"\d+", os.path.basename(path))[0])


def regen(tts, args):
    data = json.load(open(args.marks, encoding="utf8"))
    os.makedirs(args.out, exist_ok=True)
    manifest = []
    for path in sorted(glob.glob(os.path.join(args.pages, "page-*.json")), key=page_number):
        page = page_number(path)
        plain = json.load(open(path, encoding="utf8"))["text"]
        marked = apply(plain, data)
        if marked == plain and not args.all:
            continue
        name = f"page-{page}.webm"
        render_webm(tts, marked, os.path.join(args.out, name))
        manifest.append({"page": page, "file": name, "markedText": marked})
        print("page", page, "rendered")
    json.dump(manifest, open(os.path.join(args.out, "manifest.json"), "w", encoding="utf8"), ensure_ascii=False, indent=1)


def probe(tts):
    for phrase in PROBE_PHRASES:
        samples, rate = engines.speak(tts, phrase, 0)
        win = int(rate * 0.05)
        rms = [float(np.sqrt(np.mean(samples[i:i + win] ** 2))) for i in range(0, max(len(samples) - win, 1), win)]
        loud = sum(r > SILENCE_RMS for r in rms) * 0.05
        print(f"probe {phrase!r}: dur={len(samples) / rate:.2f}s loud={loud:.2f}s peakRMS={max(rms, default=0):.3f}")


def parse(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--marks", required=True, help="the approved marks list (x.fa.marks.json)")
    parser.add_argument("--pages", required=True, help="folder of page-<n>.json files")
    parser.add_argument("--out", required=True, help="folder to write the clips and manifest.json to")
    parser.add_argument("--voice-root", default=os.environ.get(VOICE_ROOT_ENV), help=f"the piper-fa pack folder (or set {VOICE_ROOT_ENV})")
    parser.add_argument("--all", action="store_true", help="render every page, not only the ones the marks change")
    parser.add_argument("--probe", action="store_true", help="also report how loud a few phrasings of 'akh joon' are")
    args = parser.parse_args(argv)
    if not args.voice_root:
        parser.error(f"--voice-root or {VOICE_ROOT_ENV} is required")
    return args


def main(argv):
    args = parse(argv)
    tts = engines.build_piper(args.voice_root, VOICE)
    regen(tts, args)
    if args.probe:
        probe(tts)


if __name__ == "__main__":
    main(sys.argv[1:])
